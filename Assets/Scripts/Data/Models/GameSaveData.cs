 /// <summary>
/// 游戏存档数据模型
/// 保存一局游戏的所有元信息，用于存档列表展示和加载
/// </summary>
using GalaxyAgent.Data.Enums;
using System;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class GameSaveData
    {
        /// <summary>存档唯一ID（GUID）</summary>
        public string SaveId;
        /// <summary>星球名称</summary>
        public string PlanetName;
        /// <summary>地图生成种子</summary>
        public int Seed;
        /// <summary>地图边长（格数）</summary>
        public int MapSize;
        /// <summary>单格像素大小</summary>
        public int TileSize;
        /// <summary>地形复杂度</summary>
        public TerrainComplexity TerrainType;
        /// <summary>资源丰富度</summary>
        public ResourceAbundance ResourceLevel;
        /// <summary>风险等级</summary>
        public RiskLevel RiskLevel;
        /// <summary>天气模式</summary>
        public WeatherPattern WeatherType;
        /// <summary>昼夜模式</summary>
        public DayNightMode DayNightMode;
        /// <summary>存档创建时间</summary>
        public string CreatedAt;
        /// <summary>游戏内游玩总时长（秒）</summary>
        public float PlayTimeSeconds;
        /// <summary>游戏内天数</summary>
        public int GameDay;
        /// <summary>游戏内累计秒数（决定小时/昼夜）</summary>
        public float GameTimeSeconds;
        /// <summary>该存档保存时的LLM服务地址（加载后恢复，空串表示用默认）</summary>
        public string LlmUrl = "";
        /// <summary>该存档保存时的LLM模型名（加载后恢复，空串表示用默认）</summary>
        public string LlmModel = "";

        /// <summary>星球介绍/档案（LLM 创建星球时生成，游戏内顶栏点击星球名可查看）</summary>
        public string PlanetDescription = "";

        /// <summary>
        /// 获取地图参数的中文描述
        /// </summary>
        public string GetParamDescription()
        {
            return $"地形: {TerrainType} | 资源: {ResourceLevel} | 风险: {RiskLevel} | " +
                   $"天气: {WeatherType} | 昼夜: {DayNightMode}";
        }

        /// <summary>
        /// 获取地图大小的中文描述
        /// </summary>
        public string GetMapSizeDescription()
        {
            if (MapSize <= 1024) return "小型";
            if (MapSize <= 3072) return "中型";
            return "大型";
        }

        /// <summary>
        /// 格式化游玩时间
        /// </summary>
        public string GetFormattedPlayTime()
        {
            var ts = TimeSpan.FromSeconds(PlayTimeSeconds);
            return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }
}
