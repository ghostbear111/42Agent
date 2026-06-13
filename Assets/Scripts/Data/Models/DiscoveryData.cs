/// <summary>
/// 探索发现数据模型
/// 在RuinField和CrystalWaste区域中生成的可调查事件
/// Agent靠近后可花费时间调查，获得资源奖励和经验值
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class DiscoveryData
    {
        /// <summary>发现唯一ID</summary>
        public int DiscoveryId;
        /// <summary>发现类型（遗迹建筑/远古终端/能量异常/坠毁飞船/研究缓存）</summary>
        public DiscoveryType Type;
        /// <summary>发现名称</summary>
        public string Name;
        /// <summary>世界坐标位置</summary>
        public Vector2 Position;
        /// <summary>是否已被调查</summary>
        public bool IsInvestigated;
        /// <summary>调查所需时间（秒）</summary>
        public float RequiredTime = 3f;
        /// <summary>调查完成后获得的资源奖励</summary>
        public Dictionary<ResourceType, float> Rewards = new Dictionary<ResourceType, float>();
        /// <summary>调查完成后获得的经验值</summary>
        public float ExperienceReward = 5f;
        /// <summary>描述文本</summary>
        public string Description;

        /// <summary>
        /// 根据发现类型生成默认数据
        /// </summary>
        public static DiscoveryData Create(int id, DiscoveryType type, Vector2 position)
        {
            var data = new DiscoveryData
            {
                DiscoveryId = id,
                Type = type,
                Position = position,
                IsInvestigated = false
            };

            // 根据类型设定名称、描述、调查时间、奖励
            switch (type)
            {
                case DiscoveryType.RuinStructure:
                    data.Name = "远古遗迹建筑";
                    data.Description = "一座半掩埋的异星建筑，内部可能藏有数据";
                    data.RequiredTime = 5f;
                    data.ExperienceReward = 8f;
                    data.Rewards[ResourceType.RuinData] = Random.Range(20f, 50f);
                    data.Rewards[ResourceType.Mineral] = Random.Range(5f, 15f);
                    break;
                case DiscoveryType.AncientTerminal:
                    data.Name = "远古数据终端";
                    data.Description = "闪烁着微光的控制台，仍可读取部分数据";
                    data.RequiredTime = 3f;
                    data.ExperienceReward = 10f;
                    data.Rewards[ResourceType.RuinData] = Random.Range(30f, 60f);
                    data.Rewards[ResourceType.Crystal] = Random.Range(10f, 20f);
                    break;
                case DiscoveryType.Anomaly:
                    data.Name = "能量异常点";
                    data.Description = "不稳定的能量场，蕴含大量晶化能量";
                    data.RequiredTime = 4f;
                    data.ExperienceReward = 6f;
                    data.Rewards[ResourceType.Crystal] = Random.Range(25f, 50f);
                    break;
                case DiscoveryType.CrashedShip:
                    data.Name = "坠毁飞船残骸";
                    data.Description = "前人探险队的飞船残骸，物资散落一地";
                    data.RequiredTime = 6f;
                    data.ExperienceReward = 12f;
                    data.Rewards[ResourceType.Mineral] = Random.Range(15f, 30f);
                    data.Rewards[ResourceType.Water] = Random.Range(10f, 25f);
                    data.Rewards[ResourceType.Organic] = Random.Range(10f, 20f);
                    break;
                case DiscoveryType.ResearchCache:
                    data.Name = "研究物资箱";
                    data.Description = "被标记的密封箱，里面是珍贵的研究物资";
                    data.RequiredTime = 2f;
                    data.ExperienceReward = 5f;
                    data.Rewards[ResourceType.Organic] = Random.Range(15f, 30f);
                    data.Rewards[ResourceType.Water] = Random.Range(15f, 25f);
                    break;
            }

            return data;
        }
    }
}
