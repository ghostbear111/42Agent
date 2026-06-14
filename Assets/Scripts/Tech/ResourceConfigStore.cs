/// <summary>
/// 资源配置存储（静态，JSON 读写）
/// 与 GameConfigStore/TechTreeStore 同构：persistentDataPath + JsonUtility + 兜底。
/// 运行时与编辑器共用同一份 resource_config.json。
///
/// 文件路径：{Application.persistentDataPath}/resource_config.json
/// 不存在/解析失败/异常 → CreateDefault（5 资源，颜色取自 Constants）。
///
/// 采集科技条件由 AgentController 运行时检查（Get 返回 RequiredTech，由调用方查 TechTreeManager），
/// 本类不直接依赖 TechTreeManager（避免 EditMode Singleton 限制）。
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Tech
{
    public static class ResourceConfigStore
    {
        /// <summary>资源配置文件名</summary>
        public const string FILE_NAME = "resource_config.json";

        /// <summary>运行时缓存（首次 Load 后常驻，编辑器改配置后 InvalidateCache）</summary>
        private static ResourceConfigData _cached;
        private static bool _loaded;

        /// <summary>资源配置文件完整路径</summary>
        public static string GetPath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, FILE_NAME);
        }

        /// <summary>创建默认资源配置（5 资源，颜色取自 Constants，RequiredTech 默认空）</summary>
        public static ResourceConfigData CreateDefault()
        {
            var data = new ResourceConfigData { Version = 1 };
            data.Resources.Add(Make(ResourceType.Mineral, "矿物", "基础金属矿物，用于建造与装备制造", Constants.COLOR_MINERAL));
            data.Resources.Add(Make(ResourceType.Crystal, "晶体", "能源晶体，驱动高级科技与设施", Constants.COLOR_CRYSTAL));
            data.Resources.Add(Make(ResourceType.Water, "水", "维持 Agent 生命运转的基础资源", Constants.COLOR_WATER));
            data.Resources.Add(Make(ResourceType.Organic, "有机物", "有机材料，用于生物相关科技与补给", Constants.COLOR_ORGANIC));
            data.Resources.Add(Make(ResourceType.RuinData, "遗迹数据", "远古文明的数据碎片，稀有高价值", Constants.COLOR_RUIN));
            return data;
        }

        private static ResourceTypeConfig Make(ResourceType type, string name, string desc, Color color)
            => new ResourceTypeConfig { Type = type, DisplayName = name, Description = desc, Color = color };

        /// <summary>加载资源配置（带缓存，三层兜底）</summary>
        public static ResourceConfigData Load()
        {
            if (_loaded) return _cached;
            string path = GetPath();
            try
            {
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var data = JsonUtility.FromJson<ResourceConfigData>(json);
                    if (data != null && data.Resources != null)
                    {
                        _cached = data;
                        _loaded = true;
                        return _cached;
                    }
                    Debug.LogWarning("[ResourceConfigStore] 解析失败，使用默认配置");
                }
                else
                {
                    Debug.Log($"[ResourceConfigStore] 配置文件不存在，使用默认: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceConfigStore] 加载异常: {e.Message}，使用默认配置");
            }
            _cached = CreateDefault();
            _loaded = true;
            return _cached;
        }

        /// <summary>把资源配置写入磁盘（prettyPrint）</summary>
        public static void Save(ResourceConfigData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[ResourceConfigStore] 保存失败：data 为空");
                return;
            }
            try
            {
                string path = GetPath();
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(path, JsonUtility.ToJson(data, true));
                _cached = data;
                _loaded = true;
                Debug.Log($"[ResourceConfigStore] 资源配置已保存: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceConfigStore] 保存异常: {e.Message}");
            }
        }

        /// <summary>查询指定资源的配置（无配置返回默认可采集的占位）</summary>
        public static ResourceTypeConfig Get(ResourceType type)
        {
            var data = Load();
            if (data?.Resources != null)
            {
                foreach (var c in data.Resources)
                    if (c.Type == type) return c;
            }
            return new ResourceTypeConfig { Type = type, Gatherable = true };
        }

        /// <summary>查询指定资源的显示名（UI 用，无配置回退枚举名）</summary>
        public static string GetDisplayName(ResourceType type)
        {
            var cfg = Get(type);
            return !string.IsNullOrEmpty(cfg.DisplayName) ? cfg.DisplayName : type.ToString();
        }

        /// <summary>使缓存失效（编辑器改配置后调用，下次 Load 重新读盘）</summary>
        public static void InvalidateCache()
        {
            _cached = null;
            _loaded = false;
        }
    }
}
