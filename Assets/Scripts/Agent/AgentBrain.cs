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
/// 高层决策预留LLM接口，当前用随机策略替代
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
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
        private readonly AgentController _controller;
        private readonly System.Random _rng = new System.Random();

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

        // ==================== 高层决策（预留LLM） ====================

        /// <summary>
        /// 高层决策：30-60秒调用一次
        /// 当前用简单启发式策略替代LLM
        /// TODO: 接入LLM API进行复杂推理
        /// </summary>
        public void RequestHighLevelDecision()
        {
            var data = _controller.AgentData;
            if (data == null) return;

            // 高层决策：根据全局状态调整优先级
            // 饥饿/能量极低时强制返回基地
            if (data.Hunger < 15f || data.Energy < 15f)
            {
                if (data.CurrentState == AgentState.Idle)
                {
                    ForceReturnToBase();
                }
                return;
            }

            // 背包快满时优先返回基地
            if (data.IsInventoryFull && data.CurrentState == AgentState.Idle)
            {
                ForceReturnToBase();
                return;
            }

            // 生命值低时避免战斗
            if (data.Health < data.MaxHealth * 0.3f)
            {
                // 如果正在战斗，撤退
                if (data.CurrentState == AgentState.InCombat)
                {
                    _controller.SetState(AgentState.Fleeing);
                }
            }
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
