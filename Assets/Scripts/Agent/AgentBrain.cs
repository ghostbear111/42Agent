/// <summary>
/// Agent大脑 - 三层决策调度器
/// 高层(LLM) → 中层(Utility AI) → 底层(状态机)
/// 负责协调三层决策系统，根据当前状态选择最优行动
/// </summary>
using System;
using System.Collections.Generic;
using System.Linq;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.World.Base;
using UnityEngine;

namespace GalaxyAgent
{
    public class AgentBrain
    {
        // Agent控制器引用
        private AgentController _controller;
        // 当前高层目标（由LLM或默认策略设定）
        private string _highLevelGoal = "survive";
        // 高层目标对应的行为倾向
        private AgentState _highLevelPreferredState = AgentState.Exploring;
        // 上一次决策的动作
        private AgentState _lastDecision = AgentState.Idle;

        /// <summary>
        /// 构造函数
        /// </summary>
        public AgentBrain(AgentController controller)
        {
            _controller = controller;
        }

        // ==================== 高层决策（LLM） ====================

        /// <summary>
        /// 请求高层决策
        /// 当前使用规则模拟，后续替换为LLM调用
        /// </summary>
        public void RequestHighLevelDecision()
        {
            var data = _controller.AgentData;

            // 紧急情况优先处理
            if (data.Health < 30f)
            {
                SetHighLevelGoal("survive", AgentState.ReturningToBase);
                return;
            }
            if (data.Hunger < 20f || data.Energy < 20f)
            {
                SetHighLevelGoal("resupply", AgentState.ReturningToBase);
                return;
            }

            // 根据Agent类型设定默认策略
            switch (data.AgentType)
            {
                case AgentType.Scout:
                    SetHighLevelGoal("explore", AgentState.Exploring);
                    break;
                case AgentType.Worker:
                    // 如果携带资源已满，回基地
                    if (data.CarryingAmount >= data.MaxCarry * 0.8f)
                        SetHighLevelGoal("deliver", AgentState.ReturningToBase);
                    else
                        SetHighLevelGoal("gather", AgentState.Gathering);
                    break;
                case AgentType.Guard:
                    // 守卫优先巡逻
                    SetHighLevelGoal("patrol", AgentState.Exploring);
                    break;
            }

            // TODO: 后续接入LLM
            // LLMClient.Instance.RequestDecisionAsync(data, nearbyInfo, memories, response => {
            //     SetHighLevelGoal(response.action, response.preferredState);
            // });
        }

        /// <summary>
        /// 设置高层目标
        /// </summary>
        private void SetHighLevelGoal(string goal, AgentState preferredState)
        {
            if (_highLevelGoal != goal)
            {
                _highLevelGoal = goal;
                _highLevelPreferredState = preferredState;
            }
        }

        // ==================== 中层决策（Utility AI） ====================

        /// <summary>
        /// 中层决策：Utility AI评估所有可能动作，选择分数最高的
        /// 每3秒调用一次
        /// </summary>
        public void EvaluateMidLevel()
        {
            var data = _controller.AgentData;
            var nearbyResources = _controller.GetNearbyResources();
            var nearbyThreats = _controller.GetNearbyThreats();

            // 评估所有候选动作的分数
            var actions = new Dictionary<AgentState, float>();

            // === 探索 ===
            float exploreScore = 50f;
            if (data.AgentType == AgentType.Scout) exploreScore += 30f;
            if (_highLevelGoal == "explore") exploreScore += 20f;
            if (data.Energy < 30f) exploreScore -= 30f;
            actions[AgentState.Exploring] = exploreScore;

            // === 采集 ===
            float gatherScore = 30f;
            if (data.AgentType == AgentType.Worker) gatherScore += 30f;
            if (_highLevelGoal == "gather") gatherScore += 20f;
            if (nearbyResources.Count > 0) gatherScore += 25f;
            if (data.CarryingAmount >= data.MaxCarry * 0.8f) gatherScore -= 50f;
            if (data.Energy < 20f) gatherScore -= 30f;
            actions[AgentState.Gathering] = gatherScore;

            // === 返回基地 ===
            float returnScore = 20f;
            if (_highLevelGoal == "resupply" || _highLevelGoal == "survive" || _highLevelGoal == "deliver")
                returnScore += 40f;
            if (data.Health < 40f) returnScore += 35f;
            if (data.Hunger < 25f) returnScore += 25f;
            if (data.Energy < 25f) returnScore += 25f;
            if (data.CarryingAmount >= data.MaxCarry * 0.8f) returnScore += 30f;
            actions[AgentState.ReturningToBase] = returnScore;

            // === 逃跑 ===
            float fleeScore = 10f;
            if (nearbyThreats.Count > 0)
            {
                fleeScore += 60f;
                if (data.Health < 50f) fleeScore += 30f;
                if (data.AgentType == AgentType.Guard) fleeScore -= 30f; // 守卫不太容易逃跑
            }
            actions[AgentState.Fleeing] = fleeScore;

            // === 战斗（仅守卫） ===
            float combatScore = 0f;
            if (data.AgentType == AgentType.Guard && nearbyThreats.Count > 0)
            {
                combatScore = 40f;
                if (data.Health > 70f) combatScore += 20f;
                if (_highLevelGoal == "patrol") combatScore += 10f;
            }
            actions[AgentState.InCombat] = combatScore;

            // === 休息 ===
            float restScore = 5f;
            if (data.Energy < 40f) restScore += 20f;
            if (data.Hunger < 40f) restScore += 15f;
            if (data.Health < 60f) restScore += 15f;
            actions[AgentState.Resting] = restScore;

            // 找到最高分的动作
            AgentState bestAction = AgentState.Idle;
            float bestScore = float.MinValue;
            foreach (var kvp in actions)
            {
                if (kvp.Value > bestScore)
                {
                    bestScore = kvp.Value;
                    bestAction = kvp.Key;
                }
            }

            // 执行最佳动作
            ExecuteAction(bestAction);
        }

        // ==================== 底层执行 ====================

        /// <summary>
        /// 根据决策结果执行对应动作
        /// </summary>
        private void ExecuteAction(AgentState action)
        {
            if (_lastDecision == action && _controller.AgentData.CurrentState == action)
                return; // 同样的决策，不重复执行

            _lastDecision = action;
            var data = _controller.AgentData;

            switch (action)
            {
                case AgentState.Exploring:
                    _controller.SetState(AgentState.Exploring);
                    // 选择一个随机可通行目标
                    MoveToRandomTarget();
                    break;

                case AgentState.Gathering:
                    var resources = _controller.GetNearbyResources();
                    if (resources.Count > 0)
                    {
                        _controller.SetState(AgentState.Gathering);
                        // 移动到最近的资源
                        var nearest = resources.OrderBy(r =>
                            Vector2.Distance(data.Position,
                                new Vector2(r.Position.x, r.Position.y))).First();
                        _controller.MoveTo(nearest.Position);

                        // 简化采集逻辑
                        if (nearest.Amount > 0)
                        {
                            float gathered = nearest.Harvest(Mathf.Min(10f, data.MaxCarry - data.CarryingAmount));
                            data.CarryingType = nearest.ResourceType;
                            data.CarryingAmount += gathered;
                            EventBus.Publish(new AgentGatheredResourceEvent
                            {
                                AgentId = data.AgentId,
                                ResourceType = nearest.ResourceType,
                                Amount = gathered
                            });
                        }
                    }
                    else
                    {
                        // 没有资源，改为探索
                        _controller.SetState(AgentState.Exploring);
                        MoveToRandomTarget();
                    }
                    break;

                case AgentState.ReturningToBase:
                    _controller.SetState(AgentState.ReturningToBase);
                    // 获取基地位置
                    var basePos = new Vector2Int(
                        Mathf.RoundToInt(data.Position.x), // 简化：基地在地图中心
                        Mathf.RoundToInt(data.Position.y));
                    // 实际中应该获取真实基地位置
                    break;

                case AgentState.Fleeing:
                    _controller.SetState(AgentState.Fleeing);
                    // 远离最近的威胁
                    var threats = _controller.GetNearbyThreats();
                    if (threats.Count > 0)
                    {
                        var closestThreat = threats[0];
                        Vector2 fleeDir = (data.Position -
                            new Vector2(closestThreat.Position.x, closestThreat.Position.y)).normalized;
                        var fleeTarget = new Vector2Int(
                            Mathf.RoundToInt(data.Position.x + fleeDir.x * 10),
                            Mathf.RoundToInt(data.Position.y + fleeDir.y * 10));
                        _controller.MoveTo(fleeTarget);
                    }
                    break;

                case AgentState.InCombat:
                    _controller.SetState(AgentState.InCombat);
                    break;

                case AgentState.Resting:
                    _controller.SetState(AgentState.Resting);
                    break;

                default:
                    _controller.SetState(AgentState.Idle);
                    break;
            }
        }

        /// <summary>
        /// 移动到随机目标点
        /// </summary>
        private void MoveToRandomTarget()
        {
            var data = _controller.AgentData;
            // 在当前位置周围选择一个随机目标
            int range = 15;
            int targetX = Mathf.RoundToInt(data.Position.x) + UnityEngine.Random.Range(-range, range);
            int targetY = Mathf.RoundToInt(data.Position.y) + UnityEngine.Random.Range(-range, range);
            _controller.MoveTo(new Vector2Int(targetX, targetY));
        }
    }
}
