/// <summary>
/// 地图区域数据模型
/// 表示地图中的一个逻辑区域（由多个格子组成）
/// 用于Agent的地图记忆系统
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class ZoneData
    {
        /// <summary>区域唯一标识</summary>
        public string ZoneId;
        /// <summary>区域左上角X坐标（格子）</summary>
        public int ZoneX;
        /// <summary>区域左上角Y坐标（格子）</summary>
        public int ZoneY;
        /// <summary>区域宽度（格子数）</summary>
        public int Width;
        /// <summary>区域高度（格子数）</summary>
        public int Height;
        /// <summary>主要生物群系</summary>
        public BiomeType Biome;
        /// <summary>平均温度</summary>
        public float Temperature = 20f;
        /// <summary>平均辐射值</summary>
        public float Radiation = 0f;
        /// <summary>可见度（0-1）</summary>
        public float Visibility = 1f;
        /// <summary>是否已被探索</summary>
        public bool Visited = false;
        /// <summary>记忆颜色标记</summary>
        public ZoneMemoryColor MemoryColor = ZoneMemoryColor.Grey;
        /// <summary>已知资源类型列表</summary>
        public List<ResourceType> KnownResources = new List<ResourceType>();
        /// <summary>已知威胁列表</summary>
        public List<string> KnownThreats = new List<string>();
        /// <summary>风险等级评分（0-1）</summary>
        public float RiskScore = 0f;
        /// <summary>资源价值评分（0-1）</summary>
        public float ResourceValue = 0f;
        /// <summary>最后探索时间</summary>
        public string LastVisited = "";
        /// <summary>最后被哪个Agent探索</summary>
        public List<string> VisitedBy = new List<string>();

        /// <summary>
        /// 检查指定坐标是否在此区域内
        /// </summary>
        public bool Contains(int tileX, int tileY)
        {
            return tileX >= ZoneX && tileX < ZoneX + Width &&
                   tileY >= ZoneY && tileY < ZoneY + Height;
        }

        /// <summary>
        /// 获取区域中心坐标
        /// </summary>
        public Vector2Int Center => new Vector2Int(ZoneX + Width / 2, ZoneY + Height / 2);
    }
}
