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
        /// <summary>Ollama可选模型预设（供配置界面选择，参数越小推理越快）</summary>
        public static readonly string[] OLLAMA_MODEL_OPTIONS =
        {
            "qwen3:8b",            // 默认，质量高但慢
            "qwen3-1.5b-turbo",    // 极快，适合高层决策
            "llama3.2:3b",         // 平衡
            "llama3.2:1b",         // 最快，质量一般
            "gemma2:2b"            // 轻量
        };
        /// <summary>LLM请求超时时间（秒）</summary>
        public const float LLM_REQUEST_TIMEOUT = 10f;
        /// <summary>LLM最大输出Token数</summary>
        public const int LLM_MAX_TOKENS = 1024;
        /// <summary>每个Agent保留的LLM对话记录条数上限（超出自动裁剪最早的）</summary>
        public const int LLM_CONVERSATION_LOG_MAX = 50;
        /// <summary>重大事件触发高层LLM决策的最小冷却时间（游戏秒）</summary>
        public const float LLM_EVENT_TRIGGER_COOLDOWN = 15f;
        /// <summary>全局同时进行的LLM请求数上限（本地Ollama建议保持1，避免压垮）</summary>
        public const int LLM_MAX_CONCURRENT_REQUESTS = 1;

        // ==================== 数据库相关 ====================
        /// <summary>存档数据库文件名</summary>
        public const string DATABASE_FILE_NAME = "galaxy_agent_saves.db";
        /// <summary>短期记忆最大条数</summary>
        public const int SHORT_TERM_MEMORY_CAPACITY = 20;

        // ==================== 存档 / 自动保存 ====================
        /// <summary>自动保存默认开启</summary>
        public const bool AUTOSAVE_DEFAULT_ENABLED = true;
        /// <summary>自动保存默认间隔（现实秒）</summary>
        public const float AUTOSAVE_DEFAULT_INTERVAL = 60f;

        // ==================== 战斗系统 ====================
        /// <summary>攻击冷却时间（秒）</summary>
        public const float ATTACK_COOLDOWN = 1.5f;
        /// <summary>最低伤害值（伤害公式保底）</summary>
        public const float MIN_DAMAGE = 1f;
        /// <summary>威胁攻击范围（格）</summary>
        public const float THREAT_ATTACK_RANGE = 1.5f;
        /// <summary>威胁检测范围倍率（基于DetectionRange）</summary>
        public const float THREAT_AGGRO_MULTIPLIER = 1.2f;
        /// <summary>击杀威胁获得经验值</summary>
        public const float XP_KILL_THREAT = 10f;

        // ==================== 采集系统 ====================
        /// <summary>基础采集时间（秒），实际 = 基础 × 硬度 / 采集效率</summary>
        public const float BASE_GATHER_TIME = 2f;
        /// <summary>采集资源获得经验值（每次）</summary>
        public const float XP_GATHER_RESOURCE = 1f;

        // ==================== 发现/事件 ====================
        /// <summary>发现物生成密度（0-1概率）</summary>
        public const float DISCOVERY_DENSITY = 0.002f;
        /// <summary>发现物采样间隔（每隔N格检查一次）</summary>
        public const int DISCOVERY_SAMPLE_INTERVAL = 15;
        /// <summary>调查发现获得经验值</summary>
        public const float XP_DISCOVERY = 5f;

        // ==================== 经验升级 ====================
        /// <summary>每级所需经验值倍数（Level × 此值）</summary>
        public const float XP_PER_LEVEL = 100f;
        /// <summary>升级时恢复生命百分比</summary>
        public const float LEVEL_UP_HEAL_PERCENT = 0.2f;
    }
}
