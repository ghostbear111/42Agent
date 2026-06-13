/// <summary>
/// 全局常量定义
/// 集中管理游戏中使用的所有常量值，方便统一修改
/// </summary>
using UnityEngine;

namespace GalaxyAgent.Core
{
    public static class Constants
    {
        // ==================== 场景名称 ====================
        /// <summary>主菜单场景名</summary>
        public const string SCENE_MAIN_MENU = "MainMenu";
        /// <summary>地图生成场景名</summary>
        public const string SCENE_MAP_GENERATION = "MapGeneration";
        /// <summary>游戏主场景名</summary>
        public const string SCENE_GAME = "GameScene";

        // ==================== 地图相关 ====================
        /// <summary>地图块大小（每块的格子数）</summary>
        public const int CHUNK_SIZE = 64;
        /// <summary>视口外额外加载的块数（防止边缘闪烁）</summary>
        public const int CHUNK_LOAD_MARGIN = 2;
        /// <summary>每帧最大加载/卸载块数</summary>
        public const int CHUNK_BUDGET_PER_FRAME = 4;

        /// <summary>小型地图尺寸（格数）</summary>
        public const int MAP_SIZE_SMALL = 1024;
        /// <summary>中型地图尺寸（格数）</summary>
        public const int MAP_SIZE_MEDIUM = 3072;
        /// <summary>大型地图尺寸（格数）</summary>
        public const int MAP_SIZE_LARGE = 5120;

        /// <summary>小格尺寸（像素）</summary>
        public const int TILE_SIZE_SMALL = 32;
        /// <summary>大格尺寸（像素）</summary>
        public const int TILE_SIZE_LARGE = 64;

        // ==================== Agent相关 ====================
        /// <summary>Agent感知半径（格子数）</summary>
        public const int AGENT_PERCEPTION_RADIUS = 10;
        /// <summary>Agent移动速度（格/秒）</summary>
        public const float AGENT_MOVE_SPEED = 3f;
        /// <summary>Agent最大生命值</summary>
        public const float AGENT_MAX_HEALTH = 100f;
        /// <summary>Agent最大饥饿值</summary>
        public const float AGENT_MAX_HUNGER = 100f;
        /// <summary>Agent最大能量值</summary>
        public const float AGENT_MAX_ENERGY = 100f;
        /// <summary>Agent最大携带量</summary>
        public const float AGENT_MAX_CARRY = 50f;
        /// <summary>Agent每秒饥饿消耗</summary>
        public const float AGENT_HUNGER_DRAIN = 0.05f;
        /// <summary>Agent每秒能量消耗</summary>
        public const float AGENT_ENERGY_DRAIN = 0.03f;
        /// <summary>中层决策评估间隔（秒）</summary>
        public const float MID_LEVEL_DECISION_INTERVAL = 3f;
        /// <summary>高层LLM决策最小间隔（秒）</summary>
        public const float HIGH_LEVEL_DECISION_MIN_INTERVAL = 30f;
        /// <summary>高层LLM决策最大间隔（秒）</summary>
        public const float HIGH_LEVEL_DECISION_MAX_INTERVAL = 60f;

        // ==================== 时间系统 ====================
        /// <summary>默认时间比例：5分钟现实时间 = 1个游戏日</summary>
        public const float DEFAULT_TIME_RATIO = 288f; // 86400秒/天 ÷ 300秒现实 = 288
        /// <summary>一天的总小时数</summary>
        public const int HOURS_PER_DAY = 24;
        /// <summary>游戏内白天开始小时</summary>
        public const int DAY_START_HOUR = 6;
        /// <summary>游戏内夜晚开始小时</summary>
        public const int NIGHT_START_HOUR = 20;

        // ==================== 颜色定义 ====================
        /// <summary>矿物颜色（棕色）</summary>
        public static readonly Color COLOR_MINERAL = new Color(0.6f, 0.4f, 0.2f);
        /// <summary>能源晶体颜色（黄色）</summary>
        public static readonly Color COLOR_CRYSTAL = new Color(1f, 0.9f, 0.2f);
        /// <summary>水颜色（蓝色）</summary>
        public static readonly Color COLOR_WATER = new Color(0.2f, 0.5f, 0.9f);
        /// <summary>有机物颜色（绿色）</summary>
        public static readonly Color COLOR_ORGANIC = new Color(0.3f, 0.8f, 0.3f);
        /// <summary>遗迹数据颜色（紫色）</summary>
        public static readonly Color COLOR_RUIN = new Color(0.7f, 0.3f, 0.9f);

        /// <summary>探索者Agent颜色（青色）</summary>
        public static readonly Color COLOR_AGENT_SCOUT = new Color(0f, 0.9f, 0.9f);
        /// <summary>采集者Agent颜色（绿色）</summary>
        public static readonly Color COLOR_AGENT_WORKER = new Color(0.2f, 0.8f, 0.2f);
        /// <summary>守卫Agent颜色（红色）</summary>
        public static readonly Color COLOR_AGENT_GUARD = new Color(0.9f, 0.2f, 0.2f);
        /// <summary>基地颜色（白色）</summary>
        public static readonly Color COLOR_BASE = Color.white;

        // ==================== 地图记忆颜色 ====================
        /// <summary>安全区域（蓝色）</summary>
        public static readonly Color COLOR_ZONE_SAFE = new Color(0.2f, 0.4f, 0.8f);
        /// <summary>资源区域（黄色）</summary>
        public static readonly Color COLOR_ZONE_RESOURCE = new Color(0.9f, 0.85f, 0.2f);
        /// <summary>危险区域（红色）</summary>
        public static readonly Color COLOR_ZONE_DANGER = new Color(0.8f, 0.2f, 0.2f);
        /// <summary>异常区域（紫色）</summary>
        public static readonly Color COLOR_ZONE_ANOMALY = new Color(0.6f, 0.2f, 0.8f);
        /// <summary>未探索区域（灰色）</summary>
        public static readonly Color COLOR_ZONE_UNEXPLORED = new Color(0.5f, 0.5f, 0.5f);

        // ==================== 地形颜色 ====================
        /// <summary>平原颜色</summary>
        public static readonly Color COLOR_TERRAIN_PLAIN = new Color(0.76f, 0.78f, 0.55f);
        /// <summary>山地颜色</summary>
        public static readonly Color COLOR_TERRAIN_MOUNTAIN = new Color(0.55f, 0.45f, 0.35f);
        /// <summary>峡谷颜色</summary>
        public static readonly Color COLOR_TERRAIN_CANYON = new Color(0.45f, 0.35f, 0.25f);
        /// <summary>湖泊颜色</summary>
        public static readonly Color COLOR_TERRAIN_LAKE = new Color(0.25f, 0.55f, 0.75f);
        /// <summary>火山颜色</summary>
        public static readonly Color COLOR_TERRAIN_VOLCANO = new Color(0.65f, 0.25f, 0.15f);
        /// <summary>废墟颜色</summary>
        public static readonly Color COLOR_TERRAIN_RUINS = new Color(0.4f, 0.4f, 0.42f);
        /// <summary>水晶沙漠颜色</summary>
        public static readonly Color COLOR_TERRAIN_CRYSTAL_DESERT = new Color(0.85f, 0.78f, 0.65f);

        // ==================== LLM相关 ====================
        /// <summary>默认Ollama API地址</summary>
        public const string OLLAMA_DEFAULT_URL = "http://localhost:11434";
        /// <summary>默认Ollama模型</summary>
        public const string OLLAMA_DEFAULT_MODEL = "qwen3:8b";
        /// <summary>LLM请求超时时间（秒）</summary>
        public const float LLM_REQUEST_TIMEOUT = 10f;
        /// <summary>LLM最大输出Token数</summary>
        public const int LLM_MAX_TOKENS = 512;

        // ==================== 数据库相关 ====================
        /// <summary>存档数据库文件名</summary>
        public const string DATABASE_FILE_NAME = "galaxy_agent_saves.db";
        /// <summary>短期记忆最大条数</summary>
        public const int SHORT_TERM_MEMORY_CAPACITY = 20;
    }
}
