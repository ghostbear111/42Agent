/// <summary>
/// 科技与文明系统的枚举定义
/// 集中存放科技类别、文明等级、效果类型、效果目标等枚举
/// 由 TechModels / TechTreeManager / TechCsvConverter 共用
/// </summary>
namespace GalaxyAgent.Tech
{
    /// <summary>科技节点类别：普通科技 / 文明里程碑（P2 文明系统用）</summary>
    public enum TechCategory
    {
        /// <summary>普通科技——消耗资源解锁，提升 Agent 能力</summary>
        Tech = 0,
        /// <summary>文明里程碑——达成条件晋升，解锁更高阶科技（P2）</summary>
        Civilization = 1
    }

    /// <summary>文明等级（科技节点的解锁闸门，P1 全部为 Outpost）</summary>
    public enum CivLevel
    {
        /// <summary>前哨（初始）</summary>
        Outpost = 0,
        /// <summary>聚落</summary>
        Settlement = 1,
        /// <summary>殖民（工业级）</summary>
        Colony = 2,
        /// <summary>星际</summary>
        Stellar = 3
    }

    /// <summary>
    /// 科技效果类型。统一表达各类加成，替代旧 TechConfig 的单一 BonusPercent。
    /// 约定：Mul 类效果 Value 为"目标倍率"（1.2=+20%）；EnergyDrainMul 用 0.8 表示"降低20%"（正向语义，调用方直接乘，无需取反）。
    /// </summary>
    public enum EffectType
    {
        /// <summary>攻击力倍率</summary>
        AttackMul = 0,
        /// <summary>防御力倍率</summary>
        DefenseMul = 1,
        /// <summary>移动速度倍率</summary>
        SpeedMul = 2,
        /// <summary>携带上限倍率</summary>
        CarryMul = 3,
        /// <summary>采集效率倍率</summary>
        GatherMul = 4,
        /// <summary>感知半径倍率</summary>
        PerceptionMul = 5,
        /// <summary>能量消耗倍率（0.8=降低20%）</summary>
        EnergyDrainMul = 6,
        /// <summary>暴露值变化（P2 暴露值系统用，正值增加暴露、负值降低）</summary>
        ExposureDelta = 7
    }

    /// <summary>效果作用目标（决定该加成对哪类 Agent 生效）</summary>
    public enum EffectTarget
    {
        /// <summary>所有 Agent</summary>
        AllAgents = 0,
        /// <summary>仅探索者</summary>
        Scout = 1,
        /// <summary>仅采集者</summary>
        Worker = 2,
        /// <summary>仅守卫</summary>
        Guard = 3,
        /// <summary>全局效果（非 Agent 属性，如暴露值/文明）</summary>
        Global = 4
    }
}
