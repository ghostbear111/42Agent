/// <summary>
/// Agent序列化数据模型
/// 用于存档和Agent状态传输
/// 包含基础属性、多槽背包、经验等级、已解锁科技
/// </summary>
using System.Collections.Generic;
using System.Linq;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class AgentData
    {
        // ==================== 基础属性 ====================

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
        /// <summary>最大携带量（所有资源合计上限）</summary>
        public float MaxCarry;
        /// <summary>当前状态</summary>
        public AgentState CurrentState;
        /// <summary>当前任务描述</summary>
        public string CurrentTask;
        /// <summary>当前目标位置（用于移动）</summary>
        public Vector2Int? TargetPosition;
        /// <summary>Agent等级</summary>
        public int Level = 1;
        /// <summary>攻击力（守卫更高）</summary>
        public float AttackPower = 10f;
        /// <summary>防御力</summary>
        public float Defense = 5f;
        /// <summary>探索速度倍率（探索者更高）</summary>
        public float ExploreSpeed = 1f;
        /// <summary>采集效率倍率（采集者更高）</summary>
        public float GatherEfficiency = 1f;

        // ==================== 背包系统 ====================

        /// <summary>
        /// 多槽背包：每种资源类型对应一个槽位
        /// 键=资源类型，值=数量。总重量不超过MaxCarry
        /// </summary>
        public Dictionary<ResourceType, float> Inventory = new Dictionary<ResourceType, float>();

        /// <summary>当前背包总重量</summary>
        public float InventoryWeight => Inventory.Values.Sum();

        /// <summary>背包剩余空间</summary>
        public float InventoryRemaining => MaxCarry - InventoryWeight;

        // ==================== 旧字段（兼容） ====================

        /// <summary>携带的资源类型（null表示空手）——兼容旧逻辑</summary>
        public ResourceType? CarryingType;
        /// <summary>携带的资源数量——兼容旧逻辑</summary>
        public float CarryingAmount;

        // ==================== 经验与科技 ====================

        /// <summary>经验值（采集+1，调查+5，击杀+10）</summary>
        public float Experience;
        /// <summary>升级所需经验值</summary>
        public float ExperienceToLevel => Level * 100f;
        /// <summary>已解锁的科技列表</summary>
        public List<TechType> TechUnlocked = new List<TechType>();

        // ==================== 背包操作 ====================

        /// <summary>
        /// 向背包中添加资源
        /// </summary>
        /// <param name="type">资源类型</param>
        /// <param name="amount">要添加的数量</param>
        /// <returns>实际添加的数量（受背包容量限制）</returns>
        public float AddToInventory(ResourceType type, float amount)
        {
            float canAdd = Mathf.Min(amount, InventoryRemaining);
            if (canAdd <= 0) return 0f;

            if (!Inventory.ContainsKey(type))
                Inventory[type] = 0f;
            Inventory[type] += canAdd;

            // 同步旧字段（取数量最多的资源类型）
            SyncLegacyCarrying();
            return canAdd;
        }

        /// <summary>
        /// 从背包中移除资源
        /// </summary>
        public void RemoveFromInventory(ResourceType type, float amount)
        {
            if (Inventory.ContainsKey(type))
            {
                Inventory[type] -= amount;
                if (Inventory[type] <= 0.01f)
                    Inventory.Remove(type);
            }
            SyncLegacyCarrying();
        }

        /// <summary>
        /// 清空背包
        /// </summary>
        public void ClearInventory()
        {
            Inventory.Clear();
            CarryingType = null;
            CarryingAmount = 0f;
        }

        /// <summary>
        /// 从存档恢复背包内容，并同步旧版携带字段
        /// </summary>
        public void SetInventory(Dictionary<ResourceType, float> inventory)
        {
            Inventory = inventory ?? new Dictionary<ResourceType, float>();
            SyncLegacyCarrying();
        }

        /// <summary>
        /// 背包是否已满（剩余空间不足1单位）
        /// </summary>
        public bool IsInventoryFull => InventoryRemaining < 1f;

        /// <summary>
        /// 同步旧字段 CarryingType/CarryingAmount 为背包中数量最多的资源
        /// </summary>
        private void SyncLegacyCarrying()
        {
            if (Inventory.Count == 0)
            {
                CarryingType = null;
                CarryingAmount = 0f;
                return;
            }
            var max = Inventory.Aggregate((a, b) => a.Value > b.Value ? a : b);
            CarryingType = max.Key;
            CarryingAmount = max.Value;
        }

        // ==================== 经验与升级 ====================

        /// <summary>
        /// 增加经验值，达到阈值时自动升级
        /// </summary>
        /// <param name="xp">获得的经验值</param>
        /// <returns>是否发生了升级</returns>
        public bool AddExperience(float xp)
        {
            Experience += xp;
            if (Experience >= ExperienceToLevel)
            {
                Experience -= ExperienceToLevel;
                Level++;
                // 升级奖励：恢复20%生命和能量
                Health = Mathf.Min(Health + MaxHealth * 0.2f, MaxHealth);
                Energy = Mathf.Min(Energy + 20f, 100f);
                return true;
            }
            return false;
        }

        // ==================== 工厂方法 ====================

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
