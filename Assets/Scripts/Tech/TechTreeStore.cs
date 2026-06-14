/// <summary>
/// 科技树配置存储（静态，JSON 读写）
/// 与 GameConfigStore 同构：persistentDataPath + JsonUtility + 三层兜底。
/// 运行时 TechTreeManager 与编辑器窗口共用同一份 tech_tree.json。
///
/// 文件路径：{Application.persistentDataPath}/tech_tree.json
/// 不存在/解析失败/异常 → 使用 CreateDefault() 内置 7 科技节点。
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Tech
{
    public static class TechTreeStore
    {
        /// <summary>科技树配置文件名</summary>
        public const string FILE_NAME = "tech_tree.json";

        /// <summary>科技树文件完整路径</summary>
        public static string GetPath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, FILE_NAME);
        }

        /// <summary>
        /// 创建默认科技树（内置 7 科技，数值对齐旧 TechConfig.cs）
        /// energy_efficiency 用 EnergyDrainMul=0.8 正向语义（不再 1-pct 取反）
        /// </summary>
        public static TechTreeData CreateDefault()
        {
            var data = new TechTreeData { Version = 1 };
            data.Nodes.Add(Node("attack_boost", "攻击强化", "攻击力提升20%",
                Cost(ResourceType.Mineral, 50f, ResourceType.Crystal, 20f),
                Eff(EffectType.AttackMul, 1.2f)));
            data.Nodes.Add(Node("defense_boost", "防御强化", "防御力提升20%",
                Cost(ResourceType.Mineral, 40f, ResourceType.Organic, 20f),
                Eff(EffectType.DefenseMul, 1.2f)));
            data.Nodes.Add(Node("speed_boost", "移动优化", "移动速度提升15%",
                Cost(ResourceType.Organic, 30f, ResourceType.Crystal, 15f),
                Eff(EffectType.SpeedMul, 1.15f)));
            data.Nodes.Add(Node("carry_boost", "扩展背包", "携带上限提升30%",
                Cost(ResourceType.Mineral, 30f, ResourceType.Organic, 30f),
                Eff(EffectType.CarryMul, 1.3f)));
            data.Nodes.Add(Node("gather_boost", "采集增效", "采集效率提升25%",
                Cost(ResourceType.Crystal, 25f, ResourceType.Water, 25f),
                Eff(EffectType.GatherMul, 1.25f)));
            data.Nodes.Add(Node("perception_boost", "感知扩展", "感知半径提升50%",
                Cost(ResourceType.Crystal, 30f, ResourceType.RuinData, 10f),
                Eff(EffectType.PerceptionMul, 1.5f)));
            data.Nodes.Add(Node("energy_efficiency", "节能训练", "能量消耗降低20%",
                Cost(ResourceType.Water, 30f, ResourceType.Organic, 20f),
                Eff(EffectType.EnergyDrainMul, 0.8f)));
            return data;
        }

        /// <summary>
        /// 加载科技树：三层兜底（文件不存在 / 解析为 null / 异常 → CreateDefault）
        /// </summary>
        public static TechTreeData Load()
        {
            string path = GetPath();
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    Debug.Log($"[TechTreeStore] 科技树文件不存在，使用默认配置: {path}");
                    return CreateDefault();
                }
                string json = System.IO.File.ReadAllText(path);
                var data = JsonUtility.FromJson<TechTreeData>(json);
                if (data == null || data.Nodes == null)
                {
                    Debug.LogWarning("[TechTreeStore] 科技树解析失败，使用默认配置");
                    return CreateDefault();
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TechTreeStore] 加载科技树异常: {e.Message}，使用默认配置");
                return CreateDefault();
            }
        }

        /// <summary>把科技树写入磁盘（prettyPrint，便于人工编辑）</summary>
        public static void Save(TechTreeData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[TechTreeStore] 保存失败：data 为空");
                return;
            }
            try
            {
                string path = GetPath();
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                string json = JsonUtility.ToJson(data, true);
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[TechTreeStore] 科技树已保存: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TechTreeStore] 保存科技树异常: {e.Message}");
            }
        }

        // ==================== 构建辅助（仅 CreateDefault 用，减少样板） ====================

        private static TechNode Node(string id, string name, string desc,
            List<CostEntry> cost, List<TechEffect> effects)
        {
            return new TechNode
            {
                Id = id,
                DisplayName = name,
                Description = desc,
                Category = TechCategory.Tech,
                CivLevel = CivLevel.Outpost,
                Cost = cost,
                Effects = effects
            };
        }

        private static List<CostEntry> Cost(ResourceType r1, float a1, ResourceType r2, float a2)
            => new List<CostEntry>
            {
                new CostEntry { Resource = r1, Amount = a1 },
                new CostEntry { Resource = r2, Amount = a2 }
            };

        private static List<TechEffect> Eff(EffectType type, float value)
            => new List<TechEffect>
            {
                new TechEffect { Type = type, Target = EffectTarget.AllAgents, Value = value }
            };
    }
}
