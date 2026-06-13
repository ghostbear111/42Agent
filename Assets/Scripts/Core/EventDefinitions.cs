/// <summary>
/// 全局事件定义
/// 所有跨系统通信的事件类型都在此定义
/// 使用EventBus发布/订阅模式
/// </summary>
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.Core
{
    // ==================== 场景事件 ====================

    /// <summary>场景加载完成事件</summary>
    public class SceneLoadedEvent : IEvent
    {
        public string SceneName;
    }

    // ==================== 游戏状态事件 ====================

    /// <summary>游戏初始化完成事件</summary>
    public class GameInitializedEvent : IEvent { }

    /// <summary>新游戏开始事件</summary>
    public class NewGameStartedEvent : IEvent
    {
        /// <summary>地图配置</summary>
        public MapConfig MapConfig;
        /// <summary>生成种子</summary>
        public int Seed;
    }

    /// <summary>游戏加载完成事件</summary>
    public class GameLoadedEvent : IEvent
    {
        /// <summary>加载的存档ID</summary>
        public string SaveId;
    }

    /// <summary>游戏保存完成事件</summary>
    public class GameSavedEvent : IEvent
    {
        /// <summary>存档ID</summary>
        public string SaveId;
    }

    // ==================== 时间事件 ====================

    /// <summary>新的一天开始事件</summary>
    public class NewDayEvent : IEvent
    {
        /// <summary>当前游戏天数</summary>
        public int Day;
    }

    /// <summary>时段变化事件（白天/夜晚切换）</summary>
    public class TimeOfDayChangedEvent : IEvent
    {
        /// <summary>新时段</summary>
        public TimeOfDay NewTimeOfDay;
    }

    /// <summary>时间速度变更事件</summary>
    public class TimeSpeedChangedEvent : IEvent
    {
        /// <summary>新的时间倍率</summary>
        public float SpeedMultiplier;
    }

    // ==================== Agent事件 ====================

    /// <summary>Agent状态变更事件</summary>
    public class AgentStateChangedEvent : IEvent
    {
        /// <summary>Agent ID</summary>
        public string AgentId;
        /// <summary>旧状态</summary>
        public AgentState OldState;
        /// <summary>新状态</summary>
        public AgentState NewState;
    }

    /// <summary>Agent受到伤害事件</summary>
    public class AgentDamagedEvent : IEvent
    {
        /// <summary>Agent ID</summary>
        public string AgentId;
        /// <summary>伤害值</summary>
        public float Damage;
        /// <summary>伤害来源</summary>
        public string Source;
    }

    /// <summary>Agent发现资源事件</summary>
    public class AgentDiscoveredResourceEvent : IEvent
    {
        /// <summary>Agent ID</summary>
        public string AgentId;
        /// <summary>资源数据</summary>
        public ResourceNodeData Resource;
    }

    /// <summary>Agent发现威胁事件</summary>
    public class AgentDiscoveredThreatEvent : IEvent
    {
        /// <summary>Agent ID</summary>
        public string AgentId;
        /// <summary>威胁数据</summary>
        public ThreatData Threat;
    }

    /// <summary>Agent采集资源事件</summary>
    public class AgentGatheredResourceEvent : IEvent
    {
        /// <summary>Agent ID</summary>
        public string AgentId;
        /// <summary>资源类型</summary>
        public ResourceType ResourceType;
        /// <summary>采集数量</summary>
        public float Amount;
    }

    /// <summary>Agent返回基地事件</summary>
    public class AgentReturnedToBaseEvent : IEvent
    {
        /// <summary>Agent ID</summary>
        public string AgentId;
        /// <summary>携带的资源类型</summary>
        public ResourceType? CarryingType;
        /// <summary>携带数量</summary>
        public float CarryingAmount;
    }

    // ==================== 世界事件 ====================

    /// <summary>天气变化事件</summary>
    public class WeatherChangedEvent : IEvent
    {
        /// <summary>新天气</summary>
        public WeatherType NewWeather;
    }

    /// <summary>区域被探索事件</summary>
    public class ZoneExploredEvent : IEvent
    {
        /// <summary>区域ID</summary>
        public string ZoneId;
        /// <summary>探索者Agent ID</summary>
        public string AgentId;
    }

    /// <summary>基地被点击事件</summary>
    public class BaseClickedEvent : IEvent { }

    /// <summary>Agent被点击事件</summary>
    public class AgentClickedEvent : IEvent
    {
        /// <summary>被点击的Agent ID</summary>
        public string AgentId;
    }

    /// <summary>地图被点击事件</summary>
    public class MapClickedEvent : IEvent
    {
        /// <summary>点击的格子坐标</summary>
        public Vector2Int TilePosition;
    }

    // ==================== 战斗事件 ====================

    /// <summary>战斗事件（Agent攻击威胁或受到反击）</summary>
    public class CombatEvent : IEvent
    {
        /// <summary>发起攻击的Agent ID</summary>
        public string AgentId;
        /// <summary>被攻击的威胁ID</summary>
        public int ThreatId;
        /// <summary>Agent对威胁造成的伤害</summary>
        public float DamageDealt;
        /// <summary>威胁对Agent造成的反击伤害</summary>
        public float DamageReceived;
    }

    /// <summary>威胁被击杀事件</summary>
    public class ThreatKilledEvent : IEvent
    {
        /// <summary>被击杀的威胁ID</summary>
        public int ThreatId;
        /// <summary>击杀者的Agent ID</summary>
        public string AgentId;
    }

    // ==================== 发现/调查事件 ====================

    /// <summary>Agent发现可调查目标事件</summary>
    public class DiscoveryFoundEvent : IEvent
    {
        /// <summary>发现者Agent ID</summary>
        public string AgentId;
        /// <summary>发现的ID</summary>
        public int DiscoveryId;
    }

    /// <summary>调查完成事件（获得奖励）</summary>
    public class DiscoveryInvestigatedEvent : IEvent
    {
        /// <summary>调查者Agent ID</summary>
        public string AgentId;
        /// <summary>调查的发现ID</summary>
        public int DiscoveryId;
    }

    // ==================== 科技/升级事件 ====================

    /// <summary>科技解锁事件</summary>
    public class TechUnlockedEvent : IEvent
    {
        /// <summary>解锁科技的Agent ID</summary>
        public string AgentId;
        /// <summary>解锁的科技类型</summary>
        public TechType TechType;
    }

    /// <summary>Agent升级事件</summary>
    public class AgentLevelUpEvent : IEvent
    {
        /// <summary>升级的Agent ID</summary>
        public string AgentId;
        /// <summary>新等级</summary>
        public int NewLevel;
    }
}
