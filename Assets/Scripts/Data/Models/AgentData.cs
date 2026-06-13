/// <summary>
/// Agent序列化数据模型
/// 用于存档和Agent状态传输
/// </summary>
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class AgentData
    {
        /// <summary>Agent唯一标识（如 "scout_01"）</summary>
        public string AgentId;
        /// <summary>Agent类型</summary>
        public AgentType AgentType;
        /// <summary>显示名称</summary>
        public string DisplayName;
        /// <summary>世界坐标位置</summary>
        public Vector2 Position;
        /// <summary>当前生命值</summary>
        public float Health;
        /// <summary>最大生命值</summary>
        public float MaxHealth;
        /// <summary>当前饥饿值（0-100，0=饿死）</summary>
        public float Hunger;
        /// <summary>当前能量值（0-100，0=力竭）</summary>
        public float Energy;
        /// <summary>携带的资源类型（null表示空手）</summary>
        public ResourceType? CarryingType;
        /// <summary>携带的资源数量</summary>
        public float CarryingAmount;
        /// <summary>最大携带量</summary>
        public float MaxCarry;
        /// <summary>当前状态</summary>
        public AgentState CurrentState;
        /// <summary>当前任务描述</summary>
        public string CurrentTask;
        /// <summary>当前目标位置（用于移动）</summary>
        public Vector2Int? TargetPosition;
        /// <summary>Agent等级/经验（预留）</summary>
        public int Level = 1;
        /// <summary>攻击力（守卫更高）</summary>
        public float AttackPower = 10f;
        /// <summary>防御力</summary>
        public float Defense = 5f;
        /// <summary>探索速度倍率（探索者更高）</summary>
        public float ExploreSpeed = 1f;
        /// <summary>采集效率倍率（采集者更高）</summary>
        public float GatherEfficiency = 1f;

        /// <summary>
        /// 根据Agent类型创建默认数据
        /// </summary>
        public static AgentData CreateDefault(string id, AgentType type, Vector2 spawnPosition)
        {
            var data = new AgentData
            {
                AgentId = id,
                AgentType = type,
                Position = spawnPosition,
                MaxHealth = 100f,
                Health = 100f,
                Hunger = 100f,
                Energy = 100f,
                MaxCarry = 50f,
                CurrentState = AgentState.Idle,
                CurrentTask = "待命中"
            };

            // 根据类型设置不同属性
            switch (type)
            {
                case AgentType.Scout:
                    data.DisplayName = "探索者";
                    data.ExploreSpeed = 1.5f;
                    data.AttackPower = 8f;
                    data.MaxCarry = 30f;
                    break;
                case AgentType.Worker:
                    data.DisplayName = "采集者";
                    data.GatherEfficiency = 1.5f;
                    data.AttackPower = 5f;
                    data.MaxCarry = 80f;
                    break;
                case AgentType.Guard:
                    data.DisplayName = "守卫";
                    data.AttackPower = 20f;
                    data.Defense = 15f;
                    data.MaxHealth = 150f;
                    data.Health = 150f;
                    data.MaxCarry = 20f;
                    break;
            }

            return data;
        }
    }
}
