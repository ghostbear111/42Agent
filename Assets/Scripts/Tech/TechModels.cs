/// <summary>
/// 科技树可序列化数据模型
/// 统一表达"科技"与"文明里程碑"两类节点，支持前置依赖（DAG）、资源消耗、多效果。
///
/// 设计要点：
/// - 全部 [Serializable] + 公共字段，兼容 Unity JsonUtility 序列化（不使用 Dictionary，规避 JsonUtility 限制）
/// - Cost 用 List&lt;CostEntry&gt;、Effects 用 List&lt;TechEffect&gt;
/// - TechNode.Id 为跨存档/CSV 的稳定字符串标识（非枚举），便于配置工具增删节点无需改代码
/// - TechTreeData 含 Version 字段，便于未来 schema 迁移
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;

namespace GalaxyAgent.Tech
{
    /// <summary>单条资源消耗（一种资源 + 数量）</summary>
    [Serializable]
    public class CostEntry
    {
        /// <summary>资源类型</summary>
        public ResourceType Resource;
        /// <summary>消耗数量</summary>
        public float Amount;
    }

    /// <summary>单条科技效果（类型 + 目标 + 数值）</summary>
    [Serializable]
    public class TechEffect
    {
        /// <summary>效果类型（如 AttackMul）</summary>
        public EffectType Type;
        /// <summary>作用目标（如 AllAgents / Guard）</summary>
        public EffectTarget Target;
        /// <summary>数值（Mul 类为目标倍率，1.2=+20%；EnergyDrainMul 用 0.8=降20%）</summary>
        public float Value = 1f;
    }

    /// <summary>
    /// 科技树节点。统一表达普通科技与文明里程碑。
    /// 多前置（Prerequisites）实现 DAG 汇聚；多 Effects 实现复合加成。
    /// </summary>
    [Serializable]
    public class TechNode
    {
        /// <summary>稳定字符串标识（如 "attack_boost"），跨存档/CSV/JSON 一致</summary>
        public string Id;
        /// <summary>节点类别</summary>
        public TechCategory Category = TechCategory.Tech;
        /// <summary>显示名称</summary>
        public string DisplayName;
        /// <summary>描述说明</summary>
        public string Description;
        /// <summary>需求文明等级（P1 全部 Outpost）</summary>
        public CivLevel CivLevel = CivLevel.Outpost;
        /// <summary>前置节点 Id 列表（全部解锁后才能解锁本节点）</summary>
        public List<string> Prerequisites = new List<string>();
        /// <summary>解锁消耗（多种资源）</summary>
        public List<CostEntry> Cost = new List<CostEntry>();
        /// <summary>解锁后生效的效果列表</summary>
        public List<TechEffect> Effects = new List<TechEffect>();
    }

    /// <summary>科技树根数据（对应一份 tech_tree.json）</summary>
    [Serializable]
    public class TechTreeData
    {
        /// <summary>全部节点</summary>
        public List<TechNode> Nodes = new List<TechNode>();
        /// <summary>数据 schema 版本，便于未来迁移</summary>
        public int Version = 1;
    }
}
