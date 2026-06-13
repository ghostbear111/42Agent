/// <summary>
/// Agent主控制器
/// 挂载在每个Agent游戏对象上的MonoBehaviour
/// 协调感知、决策、移动、战斗、采集、调查等子系统
///
/// 状态执行说明：
/// - Idle: 闲置，等待决策
/// - Exploring/ReturningToBase/Fleeing: 跟随A*路径移动
/// - Gathering: 采集计时，时间到后收获资源存入背包
/// - InCombat: 攻击冷却，范围内攻击威胁，受到反击
/// - Investigating: 调查计时，时间到后获得发现奖励
/// - Resting: 在基地恢复饥饿和能量
/// </summary>
using System.Collections.Generic;
using System.Linq;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Map;
using GalaxyAgent.Pathfinding;
using UnityEngine;

namespace GalaxyAgent
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class AgentController : MonoBehaviour
    {
        // ==================== 核心数据 ====================

        /// <summary>Agent运行时数据（可序列化）</summary>
        public AgentData AgentData { get; private set; }

        // ==================== 子系统 ====================

        private AgentBrain _brain;
        private List<Vector2Int> _currentPath;
        private int _pathIndex;
        private AgentState _currentState = AgentState.Idle;
        private float _decisionTimer;
        private float _highLevelTimer;

        // 感知列表
        private List<ResourceNodeData> _nearbyResources = new List<ResourceNodeData>();
        private List<ThreatData> _nearbyThreats = new List<ThreatData>();
        private List<TileData> _nearbyTiles = new List<TileData>();
        private List<DiscoveryData> _nearbyDiscoveries = new List<DiscoveryData>();

        // 采集状态
        private ResourceNodeData _gatheringTarget;
        private float _gatherTimer;
        private float _gatherDuration;

        // 战斗状态
        private ThreatData _combatTarget;
        private float _attackCooldown;

        // 调查状态
        private DiscoveryData _investigateTarget;
        private float _investigateTimer;

        // 重大事件检测（用于触发高层LLM决策，见DetectMajorEvents）
        private int _lastThreatCount;
        private float _lastHealth;
        private bool _eventTrackingInitialized;

        // 外部引用
        private MapGenerator _mapGenerator;
        private ChunkManager _chunkManager;
        private World.Base.BaseController _baseController;
        private World.WeatherSystem _weatherSystem;
        private int _mapWidth;
        private SpriteRenderer _renderer;
        private BoxCollider2D _collider;

        // ==================== 初始化 ====================

        /// <summary>
        /// 初始化Agent控制器
        /// </summary>
        public void Initialize(AgentData data, MapGenerator mapGen, ChunkManager chunkMgr,
            World.Base.BaseController baseCtrl, World.WeatherSystem weatherSys, int mapWidth)
        {
            AgentData = data;
            _mapGenerator = mapGen;
            _chunkManager = chunkMgr;
            _baseController = baseCtrl;
            _weatherSystem = weatherSys;
            _mapWidth = mapWidth;

            // 存档只保存快照，不保存路径和当前目标；恢复时让Agent重新评估任务，避免卡在旧状态。
            if (data.CurrentState != AgentState.Idle)
            {
                data.CurrentState = AgentState.Idle;
                data.CurrentTask = "从存档恢复，重新评估任务";
                data.TargetPosition = null;
            }
            _currentState = data.CurrentState;

            // 设置视觉
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = CreateColorSprite(GetAgentColor(data.AgentType));
            _renderer.color = GetAgentColor(data.AgentType);
            _renderer.sortingOrder = 10;

            // 明确设置点击碰撞体尺寸，避免运行时Sprite赋值后碰撞体保持极小尺寸。
            _collider = GetComponent<BoxCollider2D>();
            if (_collider == null)
                _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.size = Vector2.one;
            _collider.offset = Vector2.zero;
            _collider.isTrigger = false;

            // 设置位置
            transform.position = data.Position;
            name = $"Agent_{data.AgentId}";

            // 初始化大脑
            _brain = new AgentBrain(this);

            Debug.Log($"[Agent] {data.DisplayName}({data.AgentId}) 初始化 HP:{data.Health} ATK:{data.AttackPower} DEF:{data.Defense}");
        }

        // ==================== 每帧更新 ====================

        private void Update()
        {
            if (AgentData == null || GameManager.Instance == null || GameManager.Instance.IsPaused)
                return;

            float dt = Time.deltaTime * GameManager.Instance.TimeMultiplier;

            // 消耗属性
            UpdateStats(dt);

            // 感知环境
            Perceive();

            // 检测重大事件（遭遇威胁/受重创），必要时立即触发高层LLM决策
            DetectMajorEvents();

            // 决策
            _decisionTimer += dt;
            _highLevelTimer += dt;

            // 中层决策（每3秒）
            if (_decisionTimer >= Constants.MID_LEVEL_DECISION_INTERVAL)
            {
                _decisionTimer = 0f;
                _brain.EvaluateMidLevel();
            }

            // 高层决策（30-60秒）
            if (_highLevelTimer >= Constants.HIGH_LEVEL_DECISION_MIN_INTERVAL)
            {
                _highLevelTimer = 0f;
                _brain.RequestHighLevelDecision();
            }

            // 执行当前状态
            ExecuteCurrentState(dt);

            // 更新Data中的位置
            AgentData.Position = transform.position;
        }

        // ==================== 属性更新 ====================

        /// <summary>
        /// 更新饥饿、能量等消耗
        /// </summary>
        private void UpdateStats(float dt)
        {
            // 饥饿消耗
            AgentData.Hunger -= Constants.AGENT_HUNGER_DRAIN * dt;
            if (AgentData.Hunger < 0) AgentData.Hunger = 0;

            // 能量消耗（科技可降低消耗）
            float energyDrain = Constants.AGENT_ENERGY_DRAIN;
            if (AgentData.TechUnlocked.Contains(TechType.EnergyEfficiency))
                energyDrain *= (1f - TechConfig.Get(TechType.EnergyEfficiency).BonusPercent);
            // 天气影响能量消耗
            if (_weatherSystem != null)
            {
                var (_, _, energyMult) = _weatherSystem.GetWeatherEffects();
                energyDrain *= energyMult;
            }
            AgentData.Energy -= energyDrain * dt;
            if (AgentData.Energy < 0) AgentData.Energy = 0;

            // 饥饿或能量为0时损失生命
            if (AgentData.Hunger <= 0 || AgentData.Energy <= 0)
            {
                AgentData.Health -= 0.5f * dt;
                if (AgentData.Health < 0) AgentData.Health = 0;
            }
        }

        // ==================== 感知 ====================

        /// <summary>
        /// 感知周围环境：瓦片、资源、威胁、发现
        /// </summary>
        private void Perceive()
        {
            _nearbyResources.Clear();
            _nearbyThreats.Clear();
            _nearbyTiles.Clear();
            _nearbyDiscoveries.Clear();

            int cx = Mathf.RoundToInt(transform.position.x);
            int cy = Mathf.RoundToInt(transform.position.y);
            // 感知半径（科技可扩展）
            int radius = Constants.AGENT_PERCEPTION_RADIUS;
            if (AgentData.TechUnlocked.Contains(TechType.PerceptionBoost))
                radius = Mathf.RoundToInt(radius * (1f + TechConfig.Get(TechType.PerceptionBoost).BonusPercent));

            // 扫描周围的瓦片
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int wx = cx + dx;
                    int wy = cy + dy;
                    var tile = _mapGenerator?.GetTileAt(wx, wy);
                    if (tile != null)
                    {
                        _nearbyTiles.Add(tile);

                        // 检查瓦片上的资源
                        if (tile.ResourceNodeId >= 0)
                        {
                            var res = _mapGenerator.Resources.Find(r => r.ResourceId == tile.ResourceNodeId);
                            if (res != null && !res.IsDepleted)
                                _nearbyResources.Add(res);
                        }

                        // 检查瓦片上的发现
                        if (tile.DiscoveryId >= 0)
                        {
                            var disc = _mapGenerator.Discoveries?.Find(d => d.DiscoveryId == tile.DiscoveryId);
                            if (disc != null && !disc.IsInvestigated)
                                _nearbyDiscoveries.Add(disc);
                        }
                    }
                }
            }

            // 检测附近的威胁
            foreach (var threat in _mapGenerator?.Threats ?? new List<ThreatData>())
            {
                if (!threat.IsAlive) continue;
                float dist = Vector2.Distance(transform.position,
                    new Vector2(threat.Position.x, threat.Position.y));
                if (dist <= radius)
                {
                    _nearbyThreats.Add(threat);
                }
            }
        }

        /// <summary>
        /// 检测重大事件并触发高层LLM决策。
        /// 触发条件：从无威胁到发现威胁（遭遇威胁）、生命值骤降（受到重创）。
        /// 事件触发受LLM事件冷却约束（见AgentBrain），不会高频打扰LLM。
        /// </summary>
        private void DetectMajorEvents()
        {
            if (AgentData == null || _brain == null) return;

            // 首次初始化基线，避免启动即触发误报
            if (!_eventTrackingInitialized)
            {
                _lastThreatCount = _nearbyThreats.Count;
                _lastHealth = AgentData.Health;
                _eventTrackingInitialized = true;
                return;
            }

            // 事件1：遭遇新威胁（上一刻无威胁，此刻出现威胁）
            if (_lastThreatCount == 0 && _nearbyThreats.Count > 0)
            {
                _brain.TriggerHighLevelForEvent($"遭遇威胁x{_nearbyThreats.Count}");
            }
            _lastThreatCount = _nearbyThreats.Count;

            // 事件2：生命值骤降（相比上一刻下降超过阈值，通常由战斗造成）
            float healthDelta = _lastHealth - AgentData.Health;
            if (healthDelta >= 20f)
            {
                _brain.TriggerHighLevelForEvent($"受到重创-{healthDelta:F0}");
            }
            _lastHealth = AgentData.Health;
        }

        // ==================== 状态执行 ====================

        /// <summary>
        /// 执行当前状态逻辑
        /// </summary>
        private void ExecuteCurrentState(float dt)
        {
            switch (_currentState)
            {
                case AgentState.Idle:
                    break;

                case AgentState.Exploring:
                case AgentState.ReturningToBase:
                case AgentState.Fleeing:
                    MoveAlongPath(dt);
                    break;

                case AgentState.Gathering:
                    ExecuteGathering(dt);
                    break;

                case AgentState.InCombat:
                    ExecuteCombat(dt);
                    break;

                case AgentState.Investigating:
                    ExecuteInvestigating(dt);
                    break;

                case AgentState.Resting:
                    // 在基地休息恢复
                    AgentData.Hunger = Mathf.Min(AgentData.Hunger + 0.2f * dt, 100f);
                    AgentData.Energy = Mathf.Min(AgentData.Energy + 0.3f * dt, 100f);
                    if (AgentData.Energy >= 80f && AgentData.Hunger >= 80f)
                        SetState(AgentState.Idle);
                    break;
            }
        }

        // ==================== 采集执行 ====================

        /// <summary>
        /// 采集资源：计时完成后收获，考虑硬度和采集效率
        /// </summary>
        private void ExecuteGathering(float dt)
        {
            if (_gatheringTarget == null || _gatheringTarget.IsDepleted)
            {
                // 目标消失（已被采完），回到空闲
                SetState(AgentState.Idle);
                return;
            }

            // 累计采集时间
            _gatherTimer += dt;
            AgentData.CurrentTask = $"采集{_gatheringTarget.Name} {_gatherTimer:F1}/{_gatherDuration:F1}s";

            if (_gatherTimer >= _gatherDuration)
            {
                // 采集完成：计算收获量（考虑采集效率科技加成）
                float efficiency = AgentData.GatherEfficiency;
                if (AgentData.TechUnlocked.Contains(TechType.GatherBoost))
                    efficiency *= (1f + TechConfig.Get(TechType.GatherBoost).BonusPercent);

                float requested = Mathf.Min(15f * efficiency, AgentData.InventoryRemaining);
                float gathered = _gatheringTarget.Harvest(requested);

                if (gathered > 0)
                {
                    AgentData.AddToInventory(_gatheringTarget.ResourceType, gathered);

                    // 经验
                    bool leveled = AgentData.AddExperience(Constants.XP_GATHER_RESOURCE);

                    EventBus.Publish(new AgentGatheredResourceEvent
                    {
                        AgentId = AgentData.AgentId,
                        ResourceType = _gatheringTarget.ResourceType,
                        Amount = gathered
                    });

                    Debug.Log($"[Agent] {AgentData.DisplayName} 采集 {_gatheringTarget.ResourceType}×{gathered:F0}");
                }

                _gatheringTarget = null;
                _gatherTimer = 0f;

                // 背包满了则返回基地，否则继续
                if (AgentData.IsInventoryFull)
                    SetState(AgentState.Idle); // 让决策系统决定返回
                else
                    SetState(AgentState.Idle);
            }
        }

        // ==================== 战斗执行 ====================

        /// <summary>
        /// 战斗逻辑：攻击冷却 → 造成伤害 → 受到反击
        /// </summary>
        private void ExecuteCombat(float dt)
        {
            // 检查目标是否有效
            if (_combatTarget == null || !_combatTarget.IsAlive)
            {
                _combatTarget = null;
                SetState(AgentState.Idle);
                return;
            }

            // 检查距离
            float dist = Vector2.Distance(transform.position,
                new Vector2(_combatTarget.Position.x, _combatTarget.Position.y));

            // 不在攻击范围则走近
            float atkRange = Constants.THREAT_ATTACK_RANGE;
            if (dist > atkRange)
            {
                // 向目标移动
                Vector2 dir = (new Vector2(_combatTarget.Position.x, _combatTarget.Position.y)
                    - (Vector2)transform.position).normalized;
                float speed = Constants.AGENT_MOVE_SPEED * AgentData.ExploreSpeed * dt;
                transform.position += (Vector3)(dir * speed);
                AgentData.CurrentTask = $"接近敌人 {_combatTarget.Name}";
                return;
            }

            // 攻击冷却
            _attackCooldown -= dt;
            if (_attackCooldown > 0)
            {
                AgentData.CurrentTask = $"攻击冷却 {_attackCooldown:F1}s";
                return;
            }

            // === 发起攻击 ===
            // 攻击力（含科技加成）
            float atkPower = AgentData.AttackPower;
            if (AgentData.TechUnlocked.Contains(TechType.AttackBoost))
                atkPower = TechConfig.Get(TechType.AttackBoost).Apply(atkPower);

            // 伤害 = 攻击力 - 目标防御（保底1）
            float damageDealt = Mathf.Max(atkPower * 0.5f, atkPower - 2f);

            bool killed = _combatTarget.TakeDamage(damageDealt);
            _attackCooldown = Constants.ATTACK_COOLDOWN;

            AgentData.CurrentTask = $"攻击{_combatTarget.Name} -{damageDealt:F0}伤害";

            EventBus.Publish(new CombatEvent
            {
                AgentId = AgentData.AgentId,
                ThreatId = _combatTarget.ThreatId,
                DamageDealt = damageDealt,
                DamageReceived = 0f
            });

            // === 受到反击 ===
            if (_combatTarget.IsAlive && dist <= _combatTarget.AttackRange)
            {
                float dmgReceived = Mathf.Max(1f, _combatTarget.Damage - AgentData.Defense * 0.3f);
                AgentData.Health -= dmgReceived;

                EventBus.Publish(new AgentDamagedEvent
                {
                    AgentId = AgentData.AgentId,
                    Damage = dmgReceived,
                    Source = _combatTarget.Name
                });
            }

            // === 威胁被击杀 ===
            if (killed)
            {
                Debug.Log($"[Agent] {AgentData.DisplayName} 击杀 {_combatTarget.Name}!");
                bool leveled = AgentData.AddExperience(Constants.XP_KILL_THREAT);

                EventBus.Publish(new ThreatKilledEvent
                {
                    ThreatId = _combatTarget.ThreatId,
                    AgentId = AgentData.AgentId
                });

                _combatTarget = null;
                SetState(AgentState.Idle);
            }
        }

        // ==================== 调查执行 ====================

        /// <summary>
        /// 调查发现：计时完成后获得资源奖励和经验
        /// </summary>
        private void ExecuteInvestigating(float dt)
        {
            if (_investigateTarget == null || _investigateTarget.IsInvestigated)
            {
                _investigateTarget = null;
                SetState(AgentState.Idle);
                return;
            }

            _investigateTimer += dt;
            AgentData.CurrentTask = $"调查{_investigateTarget.Name} {_investigateTimer:F1}/{_investigateTarget.RequiredTime:F1}s";

            if (_investigateTimer >= _investigateTarget.RequiredTime)
            {
                // 调查完成：获得奖励
                _investigateTarget.IsInvestigated = true;

                // 资源奖励存入背包
                foreach (var reward in _investigateTarget.Rewards)
                {
                    AgentData.AddToInventory(reward.Key, reward.Value);
                }

                // 经验
                bool leveled = AgentData.AddExperience(_investigateTarget.ExperienceReward);

                Debug.Log($"[Agent] {AgentData.DisplayName} 完成调查: {_investigateTarget.Name}");

                EventBus.Publish(new DiscoveryInvestigatedEvent
                {
                    AgentId = AgentData.AgentId,
                    DiscoveryId = _investigateTarget.DiscoveryId
                });

                _investigateTarget = null;
                _investigateTimer = 0f;
                SetState(AgentState.Idle);
            }
        }

        // ==================== 移动 ====================

        /// <summary>
        /// 沿路径移动，到达终点时处理到达逻辑
        /// </summary>
        private void MoveAlongPath(float dt)
        {
            if (_currentPath == null || _pathIndex >= _currentPath.Count)
            {
                // 路径完成
                if (_currentState == AgentState.ReturningToBase)
                {
                    OnArriveAtBase();
                }
                else
                {
                    SetState(AgentState.Idle);
                }
                return;
            }

            // 移向下一个路径点
            Vector2 target = new Vector2(_currentPath[_pathIndex].x, _currentPath[_pathIndex].y);
            float speed = Constants.AGENT_MOVE_SPEED * AgentData.ExploreSpeed * dt;
            // 科技加成移动速度
            if (AgentData.TechUnlocked.Contains(TechType.SpeedBoost))
                speed = TechConfig.Get(TechType.SpeedBoost).Apply(speed);
            // 天气影响移动
            if (_weatherSystem != null)
            {
                var (_, moveMult, _) = _weatherSystem.GetWeatherEffects();
                speed *= moveMult;
            }

            Vector2 pos = transform.position;
            Vector2 direction = (target - pos).normalized;
            float distance = Vector2.Distance(pos, target);

            if (distance <= speed)
            {
                transform.position = target;
                _pathIndex++;
            }
            else
            {
                transform.position = pos + direction * speed;
            }
        }

        /// <summary>
        /// 到达基地：存入背包中所有资源，清空背包，进入休息
        /// </summary>
        private void OnArriveAtBase()
        {
            // 将背包中所有资源存入基地仓库
            if (AgentData.Inventory.Count == 0 && AgentData.CarryingType.HasValue && AgentData.CarryingAmount > 0f)
            {
                // 兼容旧存档：旧版本只保存单一携带资源，不保存多槽背包。
                AgentData.AddToInventory(AgentData.CarryingType.Value, AgentData.CarryingAmount);
            }

            if (AgentData.Inventory.Count > 0 && _baseController != null)
            {
                foreach (var kvp in AgentData.Inventory)
                {
                    _baseController.DepositResource(kvp.Key, kvp.Value);
                }
                Debug.Log($"[Agent] {AgentData.DisplayName} 存入 {AgentData.Inventory.Count} 种资源到基地");
            }

            EventBus.Publish(new AgentReturnedToBaseEvent
            {
                AgentId = AgentData.AgentId,
                CarryingType = AgentData.CarryingType,
                CarryingAmount = AgentData.CarryingAmount
            });

            AgentData.ClearInventory();
            SetState(AgentState.Resting);
        }

        // ==================== 公共方法 ====================

        /// <summary>设置Agent状态</summary>
        public void SetState(AgentState newState)
        {
            if (_currentState == newState) return;

            var oldState = _currentState;
            _currentState = newState;
            AgentData.CurrentState = newState;

            EventBus.Publish(new AgentStateChangedEvent
            {
                AgentId = AgentData.AgentId,
                OldState = oldState,
                NewState = newState
            });

            // 状态重置
            _currentPath = null;
            _pathIndex = 0;

            // 只清理与新状态无关的运行时目标，避免StartGathering/StartCombat刚设置目标又被清空。
            if (newState != AgentState.Gathering)
            {
                _gatheringTarget = null;
                _gatherTimer = 0f;
            }
            if (newState != AgentState.InCombat)
            {
                _combatTarget = null;
                _attackCooldown = 0f;
            }
            if (newState != AgentState.Investigating)
            {
                _investigateTarget = null;
                _investigateTimer = 0f;
            }

            AgentData.CurrentTask = newState switch
            {
                AgentState.Idle => "待命中",
                AgentState.Exploring => "探索中",
                AgentState.Gathering => "采集资源中",
                AgentState.ReturningToBase => "返回基地",
                AgentState.InCombat => "战斗中",
                AgentState.Fleeing => "逃离危险",
                AgentState.Resting => "休息恢复中",
                AgentState.Investigating => "调查中",
                _ => newState.ToString()
            };
        }

        /// <summary>移动到指定目标位置（A*寻路）</summary>
        public bool MoveTo(Vector2Int target)
        {
            if (_mapGenerator == null || _mapWidth <= 0)
            {
                Debug.LogWarning($"[Agent] {AgentData.AgentId} 无法寻路：地图尚未初始化");
                SetState(AgentState.Idle);
                return false;
            }

            target.x = Mathf.Clamp(target.x, 0, _mapWidth - 1);
            target.y = Mathf.Clamp(target.y, 0, _mapWidth - 1);

            Vector2Int start = new Vector2Int(
                Mathf.RoundToInt(transform.position.x),
                Mathf.RoundToInt(transform.position.y));

            _currentPath = AStarPathfinder.FindPath(start, target, _mapWidth,
                (x, y) =>
                {
                    var tile = _mapGenerator.GetTileAt(x, y);
                    if (tile == null || !tile.IsWalkable) return -1;
                    return tile.MovementCost;
                });

            _pathIndex = _currentPath != null && _currentPath.Count > 0 ? 1 : 0;

            if (_currentPath == null || _currentPath.Count == 0)
            {
                Debug.LogWarning($"[Agent] {AgentData.AgentId} 无法找到路径到 ({target.x},{target.y})");
                SetState(AgentState.Idle);
                return false;
            }

            return true;
        }

        /// <summary>获取基地所在格子坐标，供决策层返回基地使用</summary>
        public Vector2Int GetBaseTilePosition()
        {
            if (_baseController != null)
            {
                Vector3 pos = _baseController.transform.position;
                return new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
            }

            int center = Mathf.Max(0, _mapWidth / 2);
            return new Vector2Int(center, center);
        }

        /// <summary>测试用：立即触发一次高层LLM决策（由GameHUD"测试决策"按钮调用，无需等30秒）</summary>
        public void TriggerHighLevelDecisionForTest()
        {
            _brain?.ForceHighLevelLLMRequest();
        }

        // ==================== 采集/战斗/调查的公共设置方法（由AgentBrain调用） ====================

        /// <summary>开始采集指定资源</summary>
        public void StartGathering(ResourceNodeData target)
        {
            _gatheringTarget = target;
            // 采集时间 = 基础时间 × 硬度 / 采集效率
            float efficiency = AgentData.GatherEfficiency;
            if (AgentData.TechUnlocked.Contains(TechType.GatherBoost))
                efficiency *= (1f + TechConfig.Get(TechType.GatherBoost).BonusPercent);
            _gatherDuration = Constants.BASE_GATHER_TIME * target.Hardness / efficiency;
            _gatherTimer = 0f;
            SetState(AgentState.Gathering);
        }

        /// <summary>开始与指定威胁战斗</summary>
        public void StartCombat(ThreatData target)
        {
            _combatTarget = target;
            _attackCooldown = 0f; // 立即可以攻击
            SetState(AgentState.InCombat);
        }

        /// <summary>开始调查指定发现</summary>
        public void StartInvestigating(DiscoveryData target)
        {
            _investigateTarget = target;
            _investigateTimer = 0f;
            SetState(AgentState.Investigating);
        }

        // ==================== 感知访问器 ====================

        public List<ResourceNodeData> GetNearbyResources() => _nearbyResources;
        public List<ThreatData> GetNearbyThreats() => _nearbyThreats;
        public List<DiscoveryData> GetNearbyDiscoveries() => _nearbyDiscoveries;

        // ==================== 点击 ====================

        private void OnMouseDown()
        {
            EventBus.Publish(new AgentClickedEvent { AgentId = AgentData.AgentId });
        }

        // ==================== 辅助 ====================

        private static Color GetAgentColor(AgentType type)
        {
            return type switch
            {
                AgentType.Scout => Constants.COLOR_AGENT_SCOUT,
                AgentType.Worker => Constants.COLOR_AGENT_WORKER,
                AgentType.Guard => Constants.COLOR_AGENT_GUARD,
                _ => Color.gray
            };
        }

        private static Sprite CreateColorSprite(Color color)
        {
            int size = 32;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
