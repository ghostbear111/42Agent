/// <summary>
/// 瓦片数据模型
/// 表示地图中单个格子的所有数据
/// </summary>
using GalaxyAgent.Data.Enums;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class TileData
    {
        /// <summary>X坐标（世界格子坐标）</summary>
        public int X;
        /// <summary>Y坐标（世界格子坐标）</summary>
        public int Y;
        /// <summary>地形类型</summary>
        public TileType TileType = TileType.Plain;
        /// <summary>所属生物群系</summary>
        public BiomeType Biome = BiomeType.Grassland;
        /// <summary>温度值</summary>
        public float Temperature = 20f;
        /// <summary>辐射值（0-1）</summary>
        public float Radiation = 0f;
        /// <summary>是否可通行</summary>
        public bool IsWalkable = true;
        /// <summary>移动代价（用于寻路，越大越难通行）</summary>
        public float MovementCost = 1f;
        /// <summary>该格上的资源节点ID（-1表示无资源）</summary>
        public int ResourceNodeId = -1;
        /// <summary>该格上的威胁ID（-1表示无威胁）</summary>
        public int ThreatId = -1;

        /// <summary>
        /// 便捷构造方法
        /// </summary>
        public TileData(int x, int y, TileType tileType = TileType.Plain)
        {
            X = x;
            Y = y;
            TileType = tileType;
            // 不可通行地形设置高移动代价
            IsWalkable = tileType != TileType.Impassable;
            MovementCost = tileType == TileType.Mountain ? 3f :
                           tileType == TileType.Canyon ? 2f :
                           tileType == TileType.Volcano ? 2.5f :
                           tileType == TileType.Lake ? 2f : 1f;
        }
    }
}
