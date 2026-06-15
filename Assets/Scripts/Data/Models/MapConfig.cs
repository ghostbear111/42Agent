/// <summary>
/// 地图生成配置 ScriptableObject
/// 存储地图生成的所有参数，可在Inspector中配置
/// </summary>
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [CreateAssetMenu(fileName = "MapConfig", menuName = "GalaxyAgent/地图配置")]
    public class MapConfig : ScriptableObject
    {
        [Header("基本参数")]
        [Tooltip("地图大小")]
        public MapSize MapSize = MapSize.Small;
        [Tooltip("单格像素大小")]
        public TilePixelSize TileSize = TilePixelSize.Size32;
        [Tooltip("地图生成种子（0=随机）")]
        public int Seed = 0;

        [Header("星球环境")]
        [Tooltip("地形复杂度")]
        public TerrainComplexity Terrain = TerrainComplexity.Rich;
        [Tooltip("资源丰富度")]
        public ResourceAbundance Resources = ResourceAbundance.Moderate;
        [Tooltip("风险等级")]
        public RiskLevel Risk = RiskLevel.Medium;
        [Tooltip("天气模式")]
        public WeatherPattern Weather = WeatherPattern.Variable;
        [Tooltip("昼夜模式")]
        public DayNightMode DayNight = DayNightMode.Alternating;

        [Header("星球信息")]
        [Tooltip("星球名称")]
        public string PlanetName = "";

        [Tooltip("星球介绍/档案（LLM 创建星球时生成，游戏内顶栏点击星球名可查看；手动创建时为空）")]
        [TextArea] public string PlanetDescription = "";

        /// <summary>
        /// 获取地图边长（格数）
        /// </summary>
        public int MapWidth => (int)MapSize;

        /// <summary>
        /// 获取单格像素大小
        /// </summary>
        public int PixelSize => (int)TileSize;

        /// <summary>
        /// 获取分块数量（每维）
        /// </summary>
        public int ChunkCount => Mathf.CeilToInt(MapWidth / 64f);

        /// <summary>
        /// 获取总格子数
        /// </summary>
        public int TotalTiles => MapWidth * MapWidth;
    }
}
