/// <summary>
/// Agent大脑 - 三层决策调度器
/// 高层(LLM) → 中层(Utility AI) → 底层(状态机)
/// 负责协调三层决策系统，根据当前状态选择最优行动
///
/// 中层Utility AI评估7种候选动作：
/// - 探索：随机移动探索未知区域
/// - 采集：走近资源并启动计时采集
/// - 返回基地：走回基地存放背包资源
/// - 逃跑：远离威胁
/// - 战斗：与范围内威胁交战
/// - 调查：走近未调查的发现点
/// - 休息：返回基地恢复饥饿和能量
///
/// 每个动作根据当前环境计算Utility分数，选择最高分的动作执行
///
/// 高层决策（RequestHighLevelDecision）三层处理：
/// 1. 紧急规则立即执行（饥饿/能量/生命危急、背包满）——生存底线，不依赖LLM
/// 2. 非紧急时异步请求LLM给出战略建议（通过LLMManager串行化，避免压垮本地服务）
/// 3. LLM不可用/超时/解析失败 → 静默回退，由中层Utility AI继续驱动
/// 所有LLM交互自动记录到LLMManager对话日志，可在"查看对话"窗口回看
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Config;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.LLM;
using UnityEngine;

namespace GalaxyAgent
{
    /// <summary>
    /// 三层决策大脑
    /// - 高层：长期目标规划（预留LLM，当前用启发式）
    /// - 中层：Utility AI评估候选动作，选最优执行
    /// - 底层：由AgentController的状态机驱动
    /// </summary>
    public class AgentBrain
    {
        // 运行时游戏配置访问（null安全回退）
        private static readonly GameConfig _fallbackConfig = new GameConfig();
        private static GameConfig Cfg => GameConfigManager.Instance != null
            ? GameConfigManager.Instance.Config : _fallbackConfig;

        private readonly AgentController _controller;
        private readonly System.Random _rng = new System.Random();

        // ===== 高层LLM决策状态 =====
        // 是否有一个LLM高层请求在途（防止在结果返回前重复发起，避免请求积压）
        private bool _highLevelLLMPending;
        // 上次因"重大事件"触发LLM决策的真实时间（用于事件冷却，避免短时间内反复打扰LLM）
        private float _lastEventTriggerTime = -999f;

        // 中层决策候选动作类型
        private enum ActionType
        {
            Explore,
            Gather,
            ReturnToBase,
            Flee,
            Fight,
            Investigate,
            Rest
        }

        public AgentBrain(AgentController controller)
        {
            _controller = controller;
        }

        // ==================== 中层决策（Utility AI） ====================

        /// <summary>
        /// 中层决策：每3秒调用一次
        /// 用Utility AI评估所有候选动作，选最高分执行
        /// </summary>
        public void EvaluateMidLevel()
        {
            var data = _controller.AgentData;
            if (data == null) return;

            // 只在空闲时做新决策
            if (data.CurrentState != AgentState.Idle)
                return;

            // 评估所有候选动作
            var scores = new List<(ActionType action, float score)>();

            scores.Add((ActionType.Explore, ScoreExplore()));
            scores.Add((ActionType.Gather, ScoreGather()));
            scores.Add((ActionType.ReturnToBase, ScoreReturnToBase()));
            scores.Add((ActionType.Flee, ScoreFlee()));
            scores.Add((ActionType.Fight, ScoreFight()));
            scores.Add((ActionType.Investigate, ScoreInvestigate()));
            scores.Add((ActionType.Rest, ScoreRest()));

            // 按分数降序排列，取最高
            scores.Sort((a, b) => b.score.CompareTo(a.score));

            float bestScore = scores[0].score;
            if (bestScore <= 0f) return; // 无可行动作

            ActionType best = scores[0].action;
            ExecuteAction(best);
        }

        // ==================== 高层决策（LLM战略层） ====================

        /// <summary>
        /// 高层决策：每30-60秒由AgentController定时调用，重大事件时也可主动触发（见TriggerHighLevelForEvent）。
        /// 处理顺序：
        /// 1) 紧急规则立即执行（生存底线，不等待LLM）
        /// 2) 非紧急时异步向LLM征询战略建议（不阻塞主线程）
        /// LLM结果在ApplyLLMSuggestion回调中处理，失败则静默回退到中层Utility AI。
        /// </summary>
        public void RequestHighLevelDecision()
        {
            var data = _controller.AgentData;
            if (data == null) return;

            // 紧急规则优先且确定性执行，不交给慢且不可靠的LLM
            if (HandleUrgentRules()) return;

            // 非紧急：向LLM征询战略建议（异步、串行化、带在途去重）
            RequestHighLevelFromLLM("高层决策");
        }

        /// <summary>
        /// 紧急规则处理。返回true表示已命中紧急情况，本轮不再请求LLM。
        /// 包含：饥饿/能量危急、背包满、生命危急撤退。这些是生存底线，必须即时、确定性执行。
        /// </summary>
        private bool HandleUrgentRules()
        {
            var data = _controller.AgentData;

            // 饥饿/能量极低：强制返回基地补给
            if (data.Hunger < 15f || data.Energy < 15f)
            {
                if (data.CurrentState == AgentState.Idle)
                    ForceReturnToBase();
                return true;
            }

            // 背包快满：优先返回基地卸货
            if (data.IsInventoryFull && data.CurrentState == AgentState.Idle)
            {
                ForceReturnToBase();
                return true;
            }

            // 生命值危急：若正在战斗则立即撤退
            if (data.Health < data.MaxHealth * 0.3f)
            {
                if (data.CurrentState == AgentState.InCombat)
                    _controller.SetState(AgentState.Fleeing);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 重大事件触发高层LLM决策（由AgentController在发现威胁/受重创等时刻调用）。
        /// 带事件冷却，不受30秒定时约束，但防止短时间内反复打扰LLM。
        /// </summary>
        /// <param name="reason">事件原因，写入对话日志标签便于追溯</param>
        public void TriggerHighLevelForEvent(string reason)
        {
            var data = _controller.AgentData;
            if (data == null) return;

            // 紧急规则优先（例如受重创后生命危急，直接走规则撤退，不等LLM）
            if (HandleUrgentRules()) return;

            // 事件冷却：距离上次事件触发需超过冷却时间
            if (Time.time - _lastEventTriggerTime < Cfg.Llm.EventTriggerCooldown) return;
            _lastEventTriggerTime = Time.time;

            RequestHighLevelFromLLM($"重大事件:{reason}");
        }

        /// <summary>
        /// 异步向LLM请求战略建议。
        /// 若已有在途请求则跳过本次（防止积压）；构建提示词后交由LLMManager串行执行。
        /// </summary>
        private void RequestHighLevelFromLLM(string tag)
        {
            // 已有在途LLM请求，跳过本次，等结果回来后再说
            if (_highLevelLLMPending)
            {
                Debug.Log($"[{_controller.AgentData?.AgentId}] 高层决策跳过：上次请求仍在途");
                return;
            }

            // LLMManager可能在场景切换/应用退出时为null
            var mgr = LLMManager.Instance;
            if (mgr == null) return;
            // LLM未连接时不发起请求（避免日志噪音），由紧急规则+中层Utility AI兜底
            if (!mgr.IsAvailable) return;

            var data = _controller.AgentData;

            // 构建提示词：当前状态 + 周围环境 + 决策要求
            string userPrompt = PromptBuilder.BuildAgentDecisionPrompt(
                data,
                _controller.GetNearbyResources() ?? new List<ResourceNodeData>(),
                _controller.GetNearbyThreats() ?? new List<ThreatData>(),
                "",   // 团队共享记忆暂未接入
                "");  // 个人近期记忆暂未接入

            var request = new LLMRequest
            {
                Model = "", // 留空，使用LLMManager当前配置的模型（见OllamaProvider._defaultModel）
                SystemPrompt = PromptBuilder.BuildSystemPrompt(),
                UserPrompt = userPrompt,
                Temperature = 0.7f,
                MaxTokens = Cfg.Llm.MaxTokens
            };

            _highLevelLLMPending = true;
            string capturedTag = tag;
            string agentId = data.AgentId;

            // 交由LLMManager串行执行，完成后回到主线程回调
            mgr.EnqueueRequest(agentId, request, capturedTag, response =>
            {
                _highLevelLLMPending = false;
                Debug.Log($"[{agentId}] 收到LLM回复: success={response?.Success} 耗时={response?.DurationMs:F0}ms 内容长度={response?.Content?.Length ?? 0}");
                ApplyLLMSuggestion(response);
            });
            Debug.Log($"[{agentId}] 已提交LLM高层请求(tag={capturedTag})");
        }

        /// <summary>
        /// 测试用：强制立即请求一次高层LLM决策。
        /// 跳过30秒定时与紧急规则，但仍受"在途去重"与LLM可用性约束。
        /// 供GameHUD"测试决策"按钮快速观察LLM反应。
        /// </summary>
        public void ForceHighLevelLLMRequest(string tag = "测试触发")
        {
            var mgr = LLMManager.Instance;
            string aid = _controller?.AgentData?.AgentId ?? "?";
            if (mgr == null) { Debug.Log($"[{aid}] 测试决策: LLMManager为null"); return; }
            Debug.Log($"[{aid}] 测试决策开始: IsAvailable={mgr.IsAvailable} 在途={_highLevelLLMPending}");
            if (!mgr.IsAvailable)
            {
                Debug.Log($"[{aid}] LLM未连接，测试决策已跳过（请确认Ollama已启动且模型已拉取）");
                return;
            }
            RequestHighLevelFromLLM(tag);
        }

        /// <summary>
        /// 应用LLM的战略建议。
        /// 解析JSON回复并映射为中层动作执行；仅在Agent空闲时生效，避免打断进行中的动作。
        /// 解析失败/LLM不可用时静默回退（中层Utility AI继续工作）。
        /// </summary>
        private void ApplyLLMSuggestion(LLMResponse response)
        {
            // 防御：LLM回调可能在Agent/场景已销毁后到达（异步），此时静默丢弃
            if (_controller == null) return;

            // LLM不可用或失败 → 静默回退，交由中层Utility AI
            if (response == null || !response.Success || string.IsNullOrEmpty(response.Content))
            {
                Debug.Log($"[{_controller.AgentData?.AgentId}] LLM高层建议不可用，维持本地决策。原因:{response?.Error}");
                return;
            }

            var data = _controller.AgentData;
            // 仅在空闲时应用建议，不打断采集/战斗/移动等进行中的动作
            if (data.CurrentState != AgentState.Idle) return;

            // 从回复中提取JSON对象与字段
            string json = ExtractJsonObject(response.Content);
            if (json == null)
            {
                Debug.Log($"[{data.AgentId}] LLM回复未包含JSON，维持本地决策");
                return;
            }

            string action = ExtractJsonField(json, "action")?.ToLowerInvariant();
            string direction = ExtractJsonField(json, "direction");
            string reasoning = ExtractJsonField(json, "reasoning");

            if (!string.IsNullOrEmpty(reasoning))
                data.CurrentTask = $"LLM建议:{Truncate(reasoning, 24)}";

            // 映射LLM的action到中层动作
            switch (action)
            {
                case "explore":
                    DoExploreToward(direction);
                    break;
                case "gather":
                    DoGather();
                    break;
                case "return":
                    DoReturnToBase();
                    break;
                case "flee":
                    DoFlee();
                    break;
                case "rest":
                    ForceReturnToBase();
                    break;
                // fight/investigate 不在LLM的选项中，默认不处理（中层Utility AI会自行评估）
                default:
                    Debug.Log($"[{data.AgentId}] LLM建议动作'{action}'未识别，维持本地决策");
                    break;
            }
        }

        /// <summary>探索：按LLM给出的方向前往；无方向则退回随机探索</summary>
        private void DoExploreToward(string direction)
        {
            var data = _controller.AgentData;
            Vector2Int start = new Vector2Int(
                Mathf.RoundToInt(data.Position.x),
                Mathf.RoundToInt(data.Position.y));

            Vector2 dir;
            switch ((direction ?? "").ToLowerInvariant())
            {
                case "north": dir = Vector2.up; break;
                case "south": dir = Vector2.down; break;
                case "east": dir = Vector2.right; break;
                case "west": dir = Vector2.left; break;
                default:
                    DoExplore(); // 无明确方向，退回随机探索
                    return;
            }

            int dist = _rng.Next(5, 16);
            Vector2Int target = new Vector2Int(
                start.x + Mathf.RoundToInt(dir.x * dist),
                start.y + Mathf.RoundToInt(dir.y * dist));

            _controller.SetState(AgentState.Exploring);
            _controller.MoveTo(target);
        }

        // ==================== JSON简易解析（不引入第三方库） ====================

        /// <summary>从LLM回复文本中截取第一个JSON对象 { ... }</summary>
        private static string ExtractJsonObject(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            return text.Substring(start, end - start + 1);
        }

        /// <summary>从JSON文本中提取字符串字段的值</summary>
        private static string ExtractJsonField(string json, string field)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return null;
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx);
            if (colon < 0) return null;
            int startQ = json.IndexOf('"', colon + 1);
            if (startQ < 0) return null;
            int endQ = json.IndexOf('"', startQ + 1);
            if (endQ < 0) return null;
            return json.Substring(startQ + 1, endQ - startQ - 1);
        }

        /// <summary>截断字符串到指定长度（超出加省略号）</summary>
        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
        }

        // ==================== 动作执行 ====================

        private void ExecuteAction(ActionType action)
        {
            switch (action)
            {
                case ActionType.Explore:
                    DoExplore();
                    break;
                case ActionType.Gather:
                    DoGather();
                    break;
                case ActionType.ReturnToBase:
                    DoReturnToBase();
                    break;
                case ActionType.Flee:
                    DoFlee();
                    break;
                case ActionType.Fight:
                    DoFight();
                    break;
                case ActionType.Investigate:
                    DoInvestigate();
                    break;
                case ActionType.Rest:
                    ForceReturnToBase();
                    break;
            }
        }

        // ==================== 动作实现 ====================

        /// <summary>探索：随机选择感知范围内一个位置前往</summary>
        private void DoExplore()
        {
            var data = _controller.AgentData;
            Vector2Int start = new Vector2Int(
                Mathf.RoundToInt(data.Position.x),
                Mathf.RoundToInt(data.Position.y));

            // 随机方向，距离5-15格
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            int dist = _rng.Next(5, 16);
            Vector2Int target = new Vector2Int(
                start.x + Mathf.RoundToInt(Mathf.Cos(angle) * dist),
                start.y + Mathf.RoundToInt(Mathf.Sin(angle) * dist));

            _controller.SetState(AgentState.Exploring);
            _controller.MoveTo(target);
        }

        /// <summary>采集：走向最近的资源并开始采集</summary>
        private void DoGather()
        {
            var resources = _controller.GetNearbyResources();
            if (resources == null || resources.Count == 0)
            {
                // 没有附近资源，改为探索
                DoExplore();
                return;
            }

            // 选最近的资源
            ResourceNodeData best = null;
            float bestDist = float.MaxValue;
            Vector2 pos = _controller.AgentData.Position;

            foreach (var res in resources)
            {
                float d = Vector2.Distance(pos, new Vector2(res.Position.x, res.Position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = res;
                }
            }

            if (best == null) return;

            // 先走向资源位置
            _controller.SetState(AgentState.Exploring);
            _controller.MoveTo(best.Position);

            // 足够近则直接开始采集
            if (bestDist <= 1.5f)
            {
                _controller.StartGathering(best);
            }
            else
            {
                // 记住目标，到达后由AgentController触发
                // 简化实现：先移动到位置，下次决策时再开始采集
                _controller.AgentData.TargetPosition = best.Position;
            }
        }

        /// <summary>返回基地：寻路回基地位置</summary>
        private void DoReturnToBase()
        {
            ForceReturnToBase();
        }

        /// <summary>逃跑：远离最近的威胁</summary>
        private void DoFlee()
        {
            var threats = _controller.GetNearbyThreats();
            if (threats == null || threats.Count == 0)
            {
                _controller.SetState(AgentState.Idle);
                return;
            }

            // 找最近的威胁
            Vector2 pos = _controller.AgentData.Position;
            Vector2 closestThreatPos = Vector2.zero;
            float closestDist = float.MaxValue;

            foreach (var t in threats)
            {
                float d = Vector2.Distance(pos, new Vector2(t.Position.x, t.Position.y));
                if (d < closestDist)
                {
                    closestDist = d;
                    closestThreatPos = new Vector2(t.Position.x, t.Position.y);
                }
            }

            // 反方向逃跑10格
            Vector2 fleeDir = (pos - closestThreatPos).normalized;
            Vector2Int fleeTarget = new Vector2Int(
                Mathf.RoundToInt(pos.x + fleeDir.x * 10),
                Mathf.RoundToInt(pos.y + fleeDir.y * 10));

            _controller.SetState(AgentState.Fleeing);
            _controller.MoveTo(fleeTarget);
        }

        /// <summary>战斗：与最近的威胁交战</summary>
        private void DoFight()
        {
            var threats = _controller.GetNearbyThreats();
            if (threats == null || threats.Count == 0) return;

            // 选最近的威胁
            ThreatData best = null;
            float bestDist = float.MaxValue;
            Vector2 pos = _controller.AgentData.Position;

            foreach (var t in threats)
            {
                float d = Vector2.Distance(pos, new Vector2(t.Position.x, t.Position.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }

            if (best == null) return;

            // 守卫直接开战，其他类型如果太强则逃跑
            if (_controller.AgentData.AgentType != AgentType.Guard &&
                best.ThreatLevel > 0.7f && _controller.AgentData.Health < _controller.AgentData.MaxHealth * 0.6f)
            {
                DoFlee();
                return;
            }

            _controller.StartCombat(best);
        }

        /// <summary>调查：走向最近的未调查发现点</summary>
        private void DoInvestigate()
        {
            var discoveries = _controller.GetNearbyDiscoveries();
            if (discoveries == null || discoveries.Count == 0)
            {
                DoExplore();
                return;
            }

            // 选最近的发现点
            DiscoveryData best = null;
            float bestDist = float.MaxValue;
            Vector2 pos = _controller.AgentData.Position;

            foreach (var d in discoveries)
            {
                float dist = Vector2.Distance(pos, d.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = d;
                }
            }

            if (best == null) return;

            if (bestDist <= 1.5f)
            {
                _controller.StartInvestigating(best);
            }
            else
            {
                _controller.SetState(AgentState.Exploring);
                Vector2Int target = new Vector2Int(
                    Mathf.RoundToInt(best.Position.x),
                    Mathf.RoundToInt(best.Position.y));
                _controller.MoveTo(target);
            }
        }

        /// <summary>强制返回基地</summary>
        private void ForceReturnToBase()
        {
            // 返回真实基地坐标；如果基地引用暂不可用，则由控制器回退到地图中心。
            Vector2Int basePos = _controller.GetBaseTilePosition();

            _controller.SetState(AgentState.ReturningToBase);
            _controller.MoveTo(basePos);
        }

        // ==================== Utility评分函数 ====================

        /// <summary>探索评分：背包空且没有紧急需求时高</summary>
        private float ScoreExplore()
        {
            var data = _controller.AgentData;
            float score = 30f; // 基础分

            // 背包越空越倾向探索
            score += data.InventoryRemaining / data.MaxCarry * 20f;

            // 有资源在附近降低探索优先级
            var resources = _controller.GetNearbyResources();
            if (resources != null && resources.Count > 0)
                score -= 15f;

            return Mathf.Max(score, 0f);
        }

        /// <summary>采集评分：附近有资源且背包未满时高</summary>
        private float ScoreGather()
        {
            var data = _controller.AgentData;
            if (data.IsInventoryFull) return 0f;

            var resources = _controller.GetNearbyResources();
            if (resources == null || resources.Count == 0) return 0f;

            float score = 50f + resources.Count * 5f;

            // 采集者类型加成
            if (data.AgentType == AgentType.Worker)
                score += 20f;

            return score;
        }

        /// <summary>返回基地评分：背包满、饥饿低、能量低时高</summary>
        private float ScoreReturnToBase()
        {
            var data = _controller.AgentData;
            float score = 0f;

            // 背包越满越倾向返回
            float fillRatio = data.InventoryWeight / data.MaxCarry;
            score += fillRatio * 40f;

            // 饥饿低时加急
            if (data.Hunger < 30f) score += 30f;
            if (data.Hunger < 15f) score += 30f;

            // 能量低时加急
            if (data.Energy < 30f) score += 30f;
            if (data.Energy < 15f) score += 30f;

            return score;
        }

        /// <summary>逃跑评分：附近有威胁且自身较弱时高</summary>
        private float ScoreFlee()
        {
            var threats = _controller.GetNearbyThreats();
            if (threats == null || threats.Count == 0) return 0f;

            var data = _controller.AgentData;
            float score = threats.Count * 15f;

            // 生命值越低越倾向逃跑（非守卫）
            if (data.AgentType != AgentType.Guard)
            {
                float healthRatio = data.Health / data.MaxHealth;
                score += (1f - healthRatio) * 30f;
            }
            else
            {
                // 守卫逃跑倾向很低
                score *= 0.3f;
            }

            return score;
        }

        /// <summary>战斗评分：附近有威胁且自身有战斗力时高</summary>
        private float ScoreFight()
        {
            var threats = _controller.GetNearbyThreats();
            if (threats == null || threats.Count == 0) return 0f;

            var data = _controller.AgentData;
            float score = 20f;

            // 守卫优先战斗
            if (data.AgentType == AgentType.Guard)
                score += 40f;

            // 攻击力高更倾向战斗
            score += data.AttackPower * 0.5f;

            // 生命值低降低战斗意愿
            float healthRatio = data.Health / data.MaxHealth;
            if (healthRatio < 0.5f) score -= 20f;

            return Mathf.Max(score, 0f);
        }

        /// <summary>调查评分：附近有未调查的发现时高</summary>
        private float ScoreInvestigate()
        {
            var discoveries = _controller.GetNearbyDiscoveries();
            if (discoveries == null || discoveries.Count == 0) return 0f;

            float score = 40f + discoveries.Count * 8f;

            // 探索者更倾向调查
            if (_controller.AgentData.AgentType == AgentType.Scout)
                score += 15f;

            return score;
        }

        /// <summary>休息评分：饥饿或能量低于阈值时高</summary>
        private float ScoreRest()
        {
            var data = _controller.AgentData;
            float score = 0f;

            if (data.Hunger < 40f) score += 20f;
            if (data.Energy < 40f) score += 20f;
            if (data.Hunger < 20f) score += 30f;
            if (data.Energy < 20f) score += 30f;

            return score;
        }
    }
}
