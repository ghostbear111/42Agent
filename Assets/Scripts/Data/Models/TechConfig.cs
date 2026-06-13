/// <summary>
/// 科技升级配置
/// 定义每种科技的解锁成本和效果加成
/// 消耗基地仓库中的资源来解锁，永久提升Agent能力
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;

namespace GalaxyAgent.Data.Models
{
    /// <summary>
    /// 单条科技配置
    /// </summary>
    public struct TechConfigEntry
    {
        /// <summary>科技类型</summary>
        public TechType Type;
        /// <summary>显示名称</summary>
        public string DisplayName;
        /// <summary>描述说明</summary>
        public string Description;
        /// <summary>解锁所需资源</summary>
        public Dictionary<ResourceType, float> Cost;
        /// <summary>加成百分比（如0.2 = +20%）</summary>
        public float BonusPercent;

        /// <summary>
        /// 将加成应用到基础值上
        /// </summary>
        public float Apply(float baseValue)
        {
            return baseValue * (1f + BonusPercent);
        }
    }

    /// <summary>
    /// 科技配置表（静态）
    /// </summary>
    public static class TechConfig
    {
        /// <summary>所有科技配置，按TechType索引</summary>
        public static readonly Dictionary<TechType, TechConfigEntry> All = new Dictionary<TechType, TechConfigEntry>
        {
            {
                TechType.AttackBoost, new TechConfigEntry
                {
                    Type = TechType.AttackBoost,
                    DisplayName = "攻击强化",
                    Description = "攻击力提升20%",
                    Cost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Mineral, 50f },
                        { ResourceType.Crystal, 20f }
                    },
                    BonusPercent = 0.2f
                }
            },
            {
                TechType.DefenseBoost, new TechConfigEntry
                {
                    Type = TechType.DefenseBoost,
                    DisplayName = "防御强化",
                    Description = "防御力提升20%",
                    Cost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Mineral, 40f },
                        { ResourceType.Organic, 20f }
                    },
                    BonusPercent = 0.2f
                }
            },
            {
                TechType.SpeedBoost, new TechConfigEntry
                {
                    Type = TechType.SpeedBoost,
                    DisplayName = "移动优化",
                    Description = "移动速度提升15%",
                    Cost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Organic, 30f },
                        { ResourceType.Crystal, 15f }
                    },
                    BonusPercent = 0.15f
                }
            },
            {
                TechType.CarryBoost, new TechConfigEntry
                {
                    Type = TechType.CarryBoost,
                    DisplayName = "扩展背包",
                    Description = "携带上限提升30%",
                    Cost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Mineral, 30f },
                        { ResourceType.Organic, 30f }
                    },
                    BonusPercent = 0.3f
                }
            },
            {
                TechType.GatherBoost, new TechConfigEntry
                {
                    Type = TechType.GatherBoost,
                    DisplayName = "采集增效",
                    Description = "采集效率提升25%",
                    Cost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Crystal, 25f },
                        { ResourceType.Water, 25f }
                    },
                    BonusPercent = 0.25f
                }
            },
            {
                TechType.PerceptionBoost, new TechConfigEntry
                {
                    Type = TechType.PerceptionBoost,
                    DisplayName = "感知扩展",
                    Description = "感知半径提升50%",
                    Cost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Crystal, 30f },
                        { ResourceType.RuinData, 10f }
                    },
                    BonusPercent = 0.5f
                }
            },
            {
                TechType.EnergyEfficiency, new TechConfigEntry
                {
                    Type = TechType.EnergyEfficiency,
                    DisplayName = "节能训练",
                    Description = "能量消耗降低20%",
                    Cost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Water, 30f },
                        { ResourceType.Organic, 20f }
                    },
                    BonusPercent = 0.2f // 用于乘以消耗率时取反效果
                }
            }
        };

        /// <summary>
        /// 获取指定科技的配置
        /// </summary>
        public static TechConfigEntry Get(TechType type)
        {
            return All.ContainsKey(type) ? All[type] : default;
        }
    }
}
