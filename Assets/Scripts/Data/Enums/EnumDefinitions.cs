/// <summary>
/// 所有枚举类型的集中定义
/// 包含游戏中使用的所有枚举：瓦片类型、生物群系、资源类型、天气、昼夜、Agent类型等
/// </summary>

namespace GalaxyAgent.Data.Enums
{
    // ==================== 地图与地形 ====================

    /// <summary>
    /// 瓦片/地形类型
    /// </summary>
    public enum TileType
    {
        /// <summary>平原</summary>
        Plain = 0,
        /// <summary>山地</summary>
        Mountain = 1,
        /// <summary>峡谷</summary>
        Canyon = 2,
        /// <summary>湖泊/水域</summary>
        Lake = 3,
        /// <summary>火山</summary>
        Volcano = 4,
        /// <summary>废墟</summary>
        Ruins = 5,
        /// <summary>水晶沙漠</summary>
        CrystalDesert = 6,
        /// <summary>不可通行（深水/悬崖）</summary>
        Impassable = 7
    }

    /// <summary>
    /// 生物群系类型
    /// </summary>
    public enum BiomeType
    {
        /// <summary>草原</summary>
        Grassland = 0,
        /// <summary>沙漠</summary>
        Desert = 1,
        /// <summary>冻原</summary>
        Tundra = 2,
        /// <summary>火山地</summary>
        Volcanic = 3,
        /// <summary>森林</summary>
        Forest = 4,
        /// <summary>沼泽</summary>
        Swamp = 5,
        /// <summary>水晶荒原</summary>
        CrystalWaste = 6,
        /// <summary>废墟区域</summary>
        RuinField = 7
    }

    // ==================== 资源 ====================

    /// <summary>
    /// 资源类型
    /// </summary>
    public enum ResourceType
    {
        /// <summary>矿物/铁矿石</summary>
        Mineral = 0,
        /// <summary>能源晶体</summary>
        Crystal = 1,
        /// <summary>水</summary>
        Water = 2,
        /// <summary>有机物</summary>
        Organic = 3,
        /// <summary>遗迹数据</summary>
        RuinData = 4
    }

    // ==================== 环境参数 ====================

    /// <summary>
    /// 地形复杂度
    /// </summary>
    public enum TerrainComplexity
    {
        /// <summary>平坦</summary>
        Flat = 0,
        /// <summary>丰富</summary>
        Rich = 1,
        /// <summary>凶险</summary>
        Dangerous = 2
    }

    /// <summary>
    /// 资源丰富度
    /// </summary>
    public enum ResourceAbundance
    {
        /// <summary>贫乏</summary>
        Scarce = 0,
        /// <summary>适中</summary>
        Moderate = 1,
        /// <summary>富饶</summary>
        Rich = 2
    }

    /// <summary>
    /// 风险等级
    /// </summary>
    public enum RiskLevel
    {
        /// <summary>低</summary>
        Low = 0,
        /// <summary>中</summary>
        Medium = 1,
        /// <summary>高</summary>
        High = 2
    }

    /// <summary>
    /// 天气模式
    /// </summary>
    public enum WeatherPattern
    {
        /// <summary>温和</summary>
        Mild = 0,
        /// <summary>多变</summary>
        Variable = 1,
        /// <summary>恶劣</summary>
        Harsh = 2
    }

    /// <summary>
    /// 昼夜模式
    /// </summary>
    public enum DayNightMode
    {
        /// <summary>永昼</summary>
        EternalDay = 0,
        /// <summary>交替</summary>
        Alternating = 1,
        /// <summary>永夜</summary>
        EternalNight = 2
    }

    /// <summary>
    /// 天气类型（运行时天气状态）
    /// </summary>
    public enum WeatherType
    {
        /// <summary>晴朗</summary>
        Clear = 0,
        /// <summary>沙尘暴</summary>
        Sandstorm = 1,
        /// <summary>酸雨</summary>
        AcidRain = 2,
        /// <summary>寒潮</summary>
        ColdWave = 3,
        /// <summary>磁暴</summary>
        MagneticStorm = 4,
        /// <summary>暴风雪</summary>
        Blizzard = 5
    }

    /// <summary>
    /// 时段（游戏内时间）
    /// </summary>
    public enum TimeOfDay
    {
        /// <summary>黎明</summary>
        Dawn = 0,
        /// <summary>白天</summary>
        Day = 1,
        /// <summary>黄昏</summary>
        Dusk = 2,
        /// <summary>夜晚</summary>
        Night = 3
    }

    // ==================== 地图大小 ====================

    /// <summary>
    /// 地图大小选项
    /// </summary>
    public enum MapSize
    {
        /// <summary>小型 1024×1024</summary>
        Small = 1024,
        /// <summary>中型 3072×3072</summary>
        Medium = 3072,
        /// <summary>大型 5120×5120</summary>
        Large = 5120
    }

    // ==================== Agent ====================

    /// <summary>
    /// Agent类型
    /// </summary>
    public enum AgentType
    {
        /// <summary>探索者</summary>
        Scout = 0,
        /// <summary>采集者</summary>
        Worker = 1,
        /// <summary>守卫</summary>
        Guard = 2,
        /// <summary>工程师（预留）</summary>
        Engineer = 3,
        /// <summary>记录者（预留）</summary>
        Archivist = 4
    }

    /// <summary>
    /// Agent行为状态
    /// </summary>
    public enum AgentState
    {
        /// <summary>闲置</summary>
        Idle = 0,
        /// <summary>探索中</summary>
        Exploring = 1,
        /// <summary>采集中</summary>
        Gathering = 2,
        /// <summary>返回基地</summary>
        ReturningToBase = 3,
        /// <summary>战斗中</summary>
        InCombat = 4,
        /// <summary>逃跑中</summary>
        Fleeing = 5,
        /// <summary>休息中</summary>
        Resting = 6,
        /// <summary>护卫中</summary>
        Guarding = 7,
        /// <summary>巡逻中</summary>
        Patrolling = 8,
        /// <summary>调查发现中</summary>
        Investigating = 9
    }

    // ==================== 记忆 ====================

    /// <summary>
    /// 记忆类型
    /// </summary>
    public enum MemoryType
    {
        /// <summary>短期记忆</summary>
        ShortTerm = 0,
        /// <summary>长期记忆</summary>
        LongTerm = 1,
        /// <summary>地图记忆</summary>
        Map = 2,
        /// <summary>过程记忆（学习规则）</summary>
        Procedural = 3,
        /// <summary>共享团队记忆</summary>
        Shared = 4
    }

    /// <summary>
    /// 记忆分类
    /// </summary>
    public enum MemoryCategory
    {
        /// <summary>危险</summary>
        Danger = 0,
        /// <summary>资源</summary>
        Resource = 1,
        /// <summary>路径</summary>
        Path = 2,
        /// <summary>社交</summary>
        Social = 3,
        /// <summary>事件</summary>
        Event = 4
    }

    /// <summary>
    /// 地图记忆区域颜色标记
    /// </summary>
    public enum ZoneMemoryColor
    {
        /// <summary>灰色 - 未探索</summary>
        Grey = 0,
        /// <summary>蓝色 - 安全</summary>
        Blue = 1,
        /// <summary>黄色 - 资源</summary>
        Yellow = 2,
        /// <summary>红色 - 危险</summary>
        Red = 3,
        /// <summary>紫色 - 异常</summary>
        Purple = 4
    }

    // ==================== 存档 ====================

    /// <summary>
    /// 瓦片像素大小选项
    /// </summary>
    public enum TilePixelSize
    {
        /// <summary>32×32像素</summary>
        Size32 = 32,
        /// <summary>64×64像素</summary>
        Size64 = 64
    }

    // ==================== 探索发现 ====================

    /// <summary>
    /// 地图发现/事件类型
    /// 在RuinField和CrystalWaste区域中随机生成
    /// </summary>
    public enum DiscoveryType
    {
        /// <summary>遗迹建筑 — 可调查获取遗迹数据</summary>
        RuinStructure = 0,
        /// <summary>远古终端 — 可调查获取科技数据</summary>
        AncientTerminal = 1,
        /// <summary>能量异常 — 高辐射区域，调查获取晶体</summary>
        Anomaly = 2,
        /// <summary>坠毁飞船 — 可搜寻稀有资源</summary>
        CrashedShip = 3,
        /// <summary>研究缓存 — 前人留下的物资箱</summary>
        ResearchCache = 4
    }

    // ==================== 科技升级 ====================

    /// <summary>
    /// 科技类型
    /// 在基地消耗资源解锁，永久提升Agent能力
    /// </summary>
    public enum TechType
    {
        /// <summary>攻击强化 — 提升攻击力20%</summary>
        AttackBoost = 0,
        /// <summary>防御强化 — 提升防御力20%</summary>
        DefenseBoost = 1,
        /// <summary>移动优化 — 提升移动速度15%</summary>
        SpeedBoost = 2,
        /// <summary>扩展背包 — 提升携带上限30%</summary>
        CarryBoost = 3,
        /// <summary>采集增效 — 提升采集效率25%</summary>
        GatherBoost = 4,
        /// <summary>感知扩展 — 提升感知半径50%</summary>
        PerceptionBoost = 5,
        /// <summary>节能训练 — 降低能量消耗20%</summary>
        EnergyEfficiency = 6
    }
}
