/// <summary>
/// Agent主控制器
/// 挂载在每个Agent游戏对象上的MonoBehaviour
/// 协调感知、决策、移动、记忆等子系统
/// </summary>
using System.Collections.Generic;
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

        /// <summary>Agent大脑（三层决策）</summary>
        private AgentBrain _brain;
        /// <summary>当前路径</summary>
        private List<Vector2Int> _currentPath;
        /// <summary>当前路径索引</summary>
        private int _pathIndex;
        /// <summary>状态机当前状态</summary>
        private AgentState _currentState = AgentState.Idle;
        /// <summary>中层决策计时器</summary>
        private float _decisionTimer;
        /// <summary>高层决策计时器</summary>
        private float _highLevelTimer;
        /// <summary>感知范围内的资源</summary>
        private List<ResourceNodeData> _nearbyResources = new List<ResourceNodeData>();
        /// <summary>感知范围内的威胁</summary>
        private List<ThreatData> _nearbyThreats = new List<ThreatData>();
        /// <summary>感知范围内的瓦片</summary>
        private List<TileData> _nearbyTiles = new List<TileData>();

        // 外部引用
        private MapGenerator _mapGenerator;
        private ChunkManager _chunkManager;
        private World.Base.BaseController _baseController;
        private World.WeatherSystem _weatherSystem;
        private int _mapWidth;
        private SpriteRenderer _renderer;

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

            // 设置视觉
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sprite = CreateColorSprite(GetAgentColor(data.AgentType));
            _renderer.color = GetAgentColor(data.AgentType);
            _renderer.sortingOrder = 10;

            // 设置位置
            transform.position = data.Position;
            name = $"Agent_{data.AgentId}";

            // 初始化大脑
            _brain = new AgentBrain(this);

            Debug.Log($"[Agent] {data.DisplayName}({data.AgentId}) 初始化于 ({data.Position.x:F0}, {data.Position.y:F0})");
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

            // 能量消耗
            float energyDrain = Constants.AGENT_ENERGY_DRAIN;
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
        /// 感知周围环境
        /// </summary>
        private void Perceive()
        {
            _nearbyResources.Clear();
            _nearbyThreats.Clear();
            _nearbyTiles.Clear();

            int cx = Mathf.RoundToInt(transform.position.x);
            int cy = Mathf.RoundToInt(transform.position.y);
            int radius = Constants.AGENT_PERCEPTION_RADIUS;

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

        // ==================== 状态执行 ====================

        /// <summary>
        /// 执行当前状态
        /// </summary>
        private void ExecuteCurrentState(float dt)
        {
            switch (_currentState)
            {
                case AgentState.Idle:
                    // 闲置，等待决策
                    break;

                case AgentState.Exploring:
                case AgentState.ReturningToBase:
                case AgentState.Fleeing:
                    // 跟随路径移动
                    MoveAlongPath(dt);
                    break;

                case AgentState.Gathering:
                    // 采集资源（简化为计时器）
                    AgentData.CurrentTask = $"采集资源中...";
                    break;

                case AgentState.Resting:
                    // 在基地休息恢复
                    AgentData.Hunger = Mathf.Min(AgentData.Hunger + 0.2f * dt, Constants.AGENT_MAX_HUNGER);
                    AgentData.Energy = Mathf.Min(AgentData.Energy + 0.3f * dt, Constants.AGENT_MAX_ENERGY);
                    if (AgentData.Energy >= 80f && AgentData.Hunger >= 80f)
                    {
                        SetState(AgentState.Idle);
                    }
                    break;

                case AgentState.InCombat:
                    AgentData.CurrentTask = "战斗中";
                    break;
            }
        }

        // ==================== 移动 ====================

        /// <summary>
        /// 沿路径移动
        /// </summary>
        private void MoveAlongPath(float dt)
        {
            if (_currentPath == null || _pathIndex >= _currentPath.Count)
            {
                // 路径完成
                if (_currentState == AgentState.ReturningToBase)
                {
                    // 到达基地
                    EventBus.Publish(new AgentReturnedToBaseEvent
                    {
                        AgentId = AgentData.AgentId,
                        CarryingType = AgentData.CarryingType,
                        CarryingAmount = AgentData.CarryingAmount
                    });
                    // 存入资源
                    if (AgentData.CarryingType.HasValue && AgentData.CarryingAmount > 0)
                    {
                        _baseController.DepositResource(AgentData.CarryingType.Value, AgentData.CarryingAmount);
                        AgentData.CarryingType = null;
                        AgentData.CarryingAmount = 0;
                    }
                    SetState(AgentState.Resting);
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

        // ==================== 公共方法 ====================

        /// <summary>
        /// 设置Agent状态
        /// </summary>
        public void SetState(AgentState newState)
        {
            if (_currentState == newState) return;

            var oldState = _currentState;
            _currentState = newState;
            AgentData.CurrentState = newState;

            // 发布状态变更事件
            EventBus.Publish(new AgentStateChangedEvent
            {
                AgentId = AgentData.AgentId,
                OldState = oldState,
                NewState = newState
            });

            // 状态重置
            _currentPath = null;
            _pathIndex = 0;

            // 设置任务描述
            AgentData.CurrentTask = newState switch
            {
                AgentState.Idle => "待命中",
                AgentState.Exploring => "探索中",
                AgentState.Gathering => "采集资源中",
                AgentState.ReturningToBase => "返回基地",
                AgentState.InCombat => "战斗中",
                AgentState.Fleeing => "逃离危险",
                AgentState.Resting => "休息恢复中",
                _ => newState.ToString()
            };
        }

        /// <summary>
        /// 移动到指定目标位置（使用A*寻路）
        /// </summary>
        public void MoveTo(Vector2Int target)
        {
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

            if (_currentPath == null)
            {
                Debug.LogWarning($"[Agent] {AgentData.AgentId} 无法找到路径到 ({target.x},{target.y})");
            }
        }

        /// <summary>
        /// 获取感知范围内的资源
        /// </summary>
        public List<ResourceNodeData> GetNearbyResources() => _nearbyResources;
        /// <summary>获取感知范围内的威胁</summary>
        public List<ThreatData> GetNearbyThreats() => _nearbyThreats;

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
