/// <summary>
/// 游戏配置数据模型
/// 集中存放所有可在运行时/编辑器调整的游戏数值，替代散落在 Constants 中的可调常量。
/// 四个分组：Agent平衡 / 世界时间 / 战斗采集发现 / LLM。
///
/// 设计要点：
/// - 所有字段带默认值（取自 Constants），new GameConfig() 即得到与原版一致的配置
/// - [Serializable] + 公共字段，兼容 Unity JsonUtility 序列化
/// - 后续新增字段时，旧 JSON 缺失的字段会保留默认值（前向兼容）
/// - 非配置类常量（场景名、颜色、分块预算等）仍保留在 Constants 中
/// </summary>
using System;
using GalaxyAgent.Core;

namespace GalaxyAgent.Config
{
    /// <summary>游戏配置根（对应一份 game_config.json）</summary>
    [Serializable]
    public class GameConfig
    {
        /// <summary>Agent 平衡参数</summary>
        public AgentConfig Agent = new AgentConfig();
        /// <summary>世界/时间参数</summary>
        public WorldConfig World = new WorldConfig();
        /// <summary>战斗参数</summary>
        public CombatConfig Combat = new CombatConfig();
        /// <summary>采集参数</summary>
        public GatherConfig Gather = new GatherConfig();
        /// <summary>探索发现参数</summary>
        public DiscoveryConfig Discovery = new DiscoveryConfig();
        /// <summary>LLM 参数</summary>
        public LlmConfig Llm = new LlmConfig();
        /// <summary>存档 / 自动保存参数</summary>
        public SaveConfig Save = new SaveConfig();
        /// <summary>地图视觉风格参数</summary>
        public MapStyleConfig MapStyle = new MapStyleConfig();
    }

    /// <summary>地图视觉风格：风格Id(见 MapStyleProfilePalette.All，设置面板可切换)</summary>
    [Serializable]
    public class MapStyleConfig
    {
        [UnityEngine.Tooltip("地图视觉风格Id(starchart/infrared/hologram/thermal/radiation/matrix/cyberpunk/cyberhypsometric/dotmatrix)")]
        public string StyleId = "starchart";
    }

    /// <summary>Agent 平衡：属性上限/消耗、移动、感知、决策间隔</summary>
    [Serializable]
    public class AgentConfig
    {
        [UnityEngine.Tooltip("感知半径（格）")] public int PerceptionRadius = Constants.AGENT_PERCEPTION_RADIUS;
        [UnityEngine.Tooltip("移动速度（格/秒）")] public float MoveSpeed = Constants.AGENT_MOVE_SPEED;
        [UnityEngine.Tooltip("最大生命")] public float MaxHealth = Constants.AGENT_MAX_HEALTH;
        [UnityEngine.Tooltip("最大饥饿")] public float MaxHunger = Constants.AGENT_MAX_HUNGER;
        [UnityEngine.Tooltip("最大能量")] public float MaxEnergy = Constants.AGENT_MAX_ENERGY;
        [UnityEngine.Tooltip("最大携带量")] public float MaxCarry = Constants.AGENT_MAX_CARRY;
        [UnityEngine.Tooltip("每秒饥饿消耗")] public float HungerDrain = Constants.AGENT_HUNGER_DRAIN;
        [UnityEngine.Tooltip("每秒能量消耗")] public float EnergyDrain = Constants.AGENT_ENERGY_DRAIN;
        [UnityEngine.Tooltip("中层决策间隔（秒）")] public float MidLevelDecisionInterval = Constants.MID_LEVEL_DECISION_INTERVAL;
        [UnityEngine.Tooltip("高层LLM决策最小间隔（秒）")] public float HighLevelMinInterval = Constants.HIGH_LEVEL_DECISION_MIN_INTERVAL;
        [UnityEngine.Tooltip("高层LLM决策最大间隔（秒）")] public float HighLevelMaxInterval = Constants.HIGH_LEVEL_DECISION_MAX_INTERVAL;
    }

    /// <summary>世界/时间：时间流速、昼夜起止</summary>
    [Serializable]
    public class WorldConfig
    {
        [UnityEngine.Tooltip("时间比例（现实秒/游戏秒，288=现实5分钟=游戏1天）")] public float TimeRatio = Constants.DEFAULT_TIME_RATIO;
        [UnityEngine.Tooltip("一天小时数")] public int HoursPerDay = Constants.HOURS_PER_DAY;
        [UnityEngine.Tooltip("白天开始小时")] public int DayStartHour = Constants.DAY_START_HOUR;
        [UnityEngine.Tooltip("夜晚开始小时")] public float NightStartHour = Constants.NIGHT_START_HOUR;
    }

    /// <summary>战斗：冷却、伤害、范围、经验与升级</summary>
    [Serializable]
    public class CombatConfig
    {
        [UnityEngine.Tooltip("攻击冷却（秒）")] public float AttackCooldown = Constants.ATTACK_COOLDOWN;
        [UnityEngine.Tooltip("最低伤害")] public float MinDamage = Constants.MIN_DAMAGE;
        [UnityEngine.Tooltip("威胁攻击范围（格）")] public float ThreatAttackRange = Constants.THREAT_ATTACK_RANGE;
        [UnityEngine.Tooltip("击杀威胁经验")] public float KillThreatXP = Constants.XP_KILL_THREAT;
        [UnityEngine.Tooltip("每级所需经验倍数")] public float XpPerLevel = Constants.XP_PER_LEVEL;
        [UnityEngine.Tooltip("升级回血比例")] public float LevelUpHealPercent = Constants.LEVEL_UP_HEAL_PERCENT;
    }

    /// <summary>采集：基础时间与经验</summary>
    [Serializable]
    public class GatherConfig
    {
        [UnityEngine.Tooltip("基础采集时间（秒，实际=基础×硬度/效率）")] public float BaseGatherTime = Constants.BASE_GATHER_TIME;
        [UnityEngine.Tooltip("采集经验")] public float GatherResourceXP = Constants.XP_GATHER_RESOURCE;
    }

    /// <summary>探索发现：密度、采样间隔、经验</summary>
    [Serializable]
    public class DiscoveryConfig
    {
        [UnityEngine.Tooltip("发现物生成密度（0-1概率）")] public float Density = Constants.DISCOVERY_DENSITY;
        [UnityEngine.Tooltip("发现物采样间隔（格）")] public int SampleInterval = Constants.DISCOVERY_SAMPLE_INTERVAL;
        [UnityEngine.Tooltip("调查发现经验")] public float DiscoveryXP = Constants.XP_DISCOVERY;
    }

    /// <summary>LLM：服务地址、模型、超时、Token、决策冷却</summary>
    [Serializable]
    public class LlmConfig
    {
        [UnityEngine.Tooltip("Ollama服务地址")] public string Url = Constants.OLLAMA_DEFAULT_URL;
        [UnityEngine.Tooltip("模型名")] public string Model = Constants.OLLAMA_DEFAULT_MODEL;
        [UnityEngine.Tooltip("请求超时（秒）")] public float RequestTimeout = Constants.LLM_REQUEST_TIMEOUT;
        [UnityEngine.Tooltip("最大输出Token")] public int MaxTokens = Constants.LLM_MAX_TOKENS;
        [UnityEngine.Tooltip("每个Agent对话记录上限")] public int ConversationLogMax = Constants.LLM_CONVERSATION_LOG_MAX;
        [UnityEngine.Tooltip("重大事件触发LLM冷却（游戏秒）")] public float EventTriggerCooldown = Constants.LLM_EVENT_TRIGGER_COOLDOWN;
    }

    /// <summary>存档 / 自动保存：开关、间隔</summary>
    [Serializable]
    public class SaveConfig
    {
        [UnityEngine.Tooltip("是否启用自动保存")] public bool AutoSaveEnabled = Constants.AUTOSAVE_DEFAULT_ENABLED;
        [UnityEngine.Tooltip("自动保存间隔（现实秒，到点自动存档）")] public float AutoSaveInterval = Constants.AUTOSAVE_DEFAULT_INTERVAL;
    }
}
