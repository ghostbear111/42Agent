/// <summary>
/// 游戏场景控制器
/// 整合所有子系统的入口点
/// 负责：初始化地图、创建Agent、设置基地、启动时间系统、连接UI
/// 当场景加载时（新游戏/加载游戏）自动执行初始化流程
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Config;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
using GalaxyAgent.LLM;
using GalaxyAgent.Map;
using GalaxyAgent.Memory;
using GalaxyAgent.Tech;
using GalaxyAgent.UI;
using GalaxyAgent.World;
using GalaxyAgent.World.Base;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GalaxyAgent
{
    public class GameSceneController : MonoBehaviour
    {
        // ==================== 场景对象引用 ====================

        [Header("场景组件")]
        [Tooltip("地形Tilemap")]
        public Tilemap terrainTilemap;
        [Tooltip("HUD控制器")]
        public GameHUD gameHUD;
        [Tooltip("Agent父节点")]
        public Transform agentsParent;

        // ==================== 运行时子系统 ====================

        private MapGenerator _mapGenerator;
        private ChunkManager _chunkManager;
        private MapClickHandler _mapClickHandler;
        private TimeSystem _timeSystem;
        private WeatherSystem _weatherSystem;
        private DatabaseManager _dbManager;
        private SaveLoadManager _saveManager;
        private MemoryManager _memoryManager;
        private MapConfig _mapConfig;
        private int _seed;

        // 实体
        private BaseController _baseController;
        private Dictionary<string, AgentController> _agents = new Dictionary<string, AgentController>();
        private bool _isInitialized;

        private void Start()
        {
            Debug.Log("[GameScene] 游戏场景启动");

            // 确保必要的场景对象存在
            EnsureSceneSetup();

            // 初始化数据库
            _dbManager = new DatabaseManager();
            _dbManager.Initialize();
            _saveManager = new SaveLoadManager(_dbManager);

            // 初始化LLM管理器（全局单例：内部创建唯一LLM客户端并异步检查可用性，
            // Agent高层决策与"查看对话"窗口都通过它访问LLM，不再各自持有客户端）
            _ = LLMManager.Instance;

            // 订阅场景事件
            EventBus.Subscribe<NewGameStartedEvent>(OnNewGameStarted);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);

            // 判断是新游戏还是加载
            if (!string.IsNullOrEmpty(GameManager.Instance.CurrentSaveId))
            {
                // 加载已有存档
                LoadGame(GameManager.Instance.CurrentSaveId);
            }
            else if (GameManager.Instance.CurrentMapConfig != null)
            {
                // 新游戏
                InitializeNewGame(GameManager.Instance.CurrentMapConfig, GameManager.Instance.CurrentSeed);
            }
            else
            {
                Debug.LogWarning("[GameScene] 未找到当前存档或地图配置，场景保持待机状态");
            }
        }

        private void Update()
        {
            // 更新时间系统
            if (_timeSystem != null)
                _timeSystem.Tick(Time.deltaTime);

            // 更新天气系统（用游戏时间）
            if (_weatherSystem != null && _timeSystem != null)
                _weatherSystem.Tick(Time.deltaTime * GameManager.Instance.TimeMultiplier);
        }

        // ==================== 场景初始化 ====================

        /// <summary>
        /// 确保场景中有必要的对象
        /// 优先查找场景中已有的对象，找不到才创建新的
        /// </summary>
        private void EnsureSceneSetup()
        {
            // ---------- 解析已有场景对象引用 ----------

            // 解析GameHUD（与GameSceneController在同一Canvas GameObject上）
            if (gameHUD == null)
                gameHUD = GetComponent<GameHUD>();

            // 查找场景中已有的Terrain Tilemap（在Grid子对象下）
            if (terrainTilemap == null)
            {
                var existingTerrain = GameObject.Find("Terrain");
                if (existingTerrain != null)
                    terrainTilemap = existingTerrain.GetComponent<Tilemap>();
            }

            // 查找场景中已有的Agents父节点
            if (agentsParent == null)
            {
                var existingAgents = GameObject.Find("Agents");
                if (existingAgents != null)
                    agentsParent = existingAgents.transform;
            }

            // ---------- 创建缺失的场景对象 ----------

            // 确保有Grid和Tilemap（如果场景中没有）
            if (terrainTilemap == null)
            {
                var gridObj = new GameObject("Grid");
                gridObj.AddComponent<Grid>();
                var tilemapObj = new GameObject("Terrain");
                tilemapObj.transform.SetParent(gridObj.transform);
                terrainTilemap = tilemapObj.AddComponent<Tilemap>();
                tilemapObj.AddComponent<TilemapRenderer>();
            }

            // 确保有摄像机
            var cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 20f;
                cam.transform.position = new Vector3(0, 0, -10);
            }

            // 给摄像机添加拖拽/缩放控制器
            if (cam.GetComponent<CameraController>() == null)
                cam.gameObject.AddComponent<CameraController>();

            // 确保有分块管理器，避免场景重复初始化时生成多个运行时对象
            if (_chunkManager == null)
                _chunkManager = FindFirstObjectByType<ChunkManager>();
            if (_chunkManager == null)
            {
                var chunkMgrObj = new GameObject("[ChunkManager]");
                _chunkManager = chunkMgrObj.AddComponent<ChunkManager>();
            }

            // 确保有地图点击处理器，复用已有对象以避免重复发布点击事件
            if (_mapClickHandler == null)
                _mapClickHandler = FindFirstObjectByType<MapClickHandler>();
            if (_mapClickHandler == null)
            {
                var clickObj = new GameObject("[MapClickHandler]");
                _mapClickHandler = clickObj.AddComponent<MapClickHandler>();
            }
            _mapClickHandler.Initialize(terrainTilemap);

            // 确保Agent父节点存在（如果场景中没有则创建）
            if (agentsParent == null)
            {
                var parentObj = new GameObject("Agents");
                agentsParent = parentObj.transform;
            }
        }

        // ==================== 新游戏初始化 ====================

        /// <summary>
        /// 初始化新游戏
        /// </summary>
        private void InitializeNewGame(MapConfig config, int seed)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[GameScene] 初始化请求被忽略：游戏场景已经初始化");
                return;
            }
            _isInitialized = true;

            _mapConfig = config;
            _seed = seed;

            Debug.Log($"[GameScene] 初始化新游戏 - {config.PlanetName}, 种子:{seed}");

            // 生成地图
            _mapGenerator = new MapGenerator(config, seed);
            _mapGenerator.Generate();

            // 初始化分块系统
            _chunkManager.Initialize(_mapGenerator, terrainTilemap, config);

            // 基地位置（地图中心）
            Vector2 basePos = new Vector2(config.MapWidth / 2f, config.MapWidth / 2f);

            // 创建基地
            CreateBase(basePos);

            // 触发科技树单例初始化（Awake 加载 tech_tree.json，新游戏解锁集合默认空）
            _ = TechTreeManager.Instance;

            // 初始化时间系统
            _timeSystem = new TimeSystem();
            _timeSystem.Initialize(config.DayNight, GameConfigManager.Instance.Config.World.TimeRatio);

            // 初始化天气系统
            _weatherSystem = new WeatherSystem();
            _weatherSystem.Initialize(config.Weather, seed);

            // 初始化记忆系统
            _memoryManager = new MemoryManager();
            _memoryManager.Initialize(_dbManager, GameManager.Instance.CurrentSaveId);

            // 创建Agent
            CreateAgents(basePos);

            // 加载初始块
            _chunkManager.LoadInitialChunks(basePos);

            // 设置摄像机到基地位置
            Camera.main.transform.position = new Vector3(basePos.x, basePos.y, -10);

            // 设置摄像机地图边界
            var camCtrl = Camera.main.GetComponent<CameraController>();
            if (camCtrl != null) camCtrl.SetMapBounds(config.MapWidth);

            // 初始化HUD
            if (gameHUD != null)
                gameHUD.Initialize(_timeSystem, _baseController, _agents,
                    _dbManager, _saveManager, _mapGenerator, _mapConfig);

            Debug.Log("[GameScene] 新游戏初始化完成！");
        }

        // ==================== 加载游戏 ====================

        /// <summary>
        /// 加载已有存档
        /// </summary>
        private void LoadGame(string saveId)
        {
            if (_isInitialized)
            {
                Debug.LogWarning("[GameScene] 加载请求被忽略：游戏场景已经初始化");
                return;
            }

            Debug.Log($"[GameScene] 加载存档: {saveId}");

            var saveData = _saveManager.GetSave(saveId);
            if (saveData == null)
            {
                Debug.LogError($"[GameScene] 存档 {saveId} 不存在！");
                return;
            }
            _isInitialized = true;

            // 从存档恢复配置
            _mapConfig = ScriptableObject.CreateInstance<MapConfig>();
            _mapConfig.PlanetName = saveData.PlanetName;
            _mapConfig.MapSize = (MapSize)saveData.MapSize;
            _mapConfig.TileSize = (TilePixelSize)saveData.TileSize;
            _mapConfig.Terrain = saveData.TerrainType;
            _mapConfig.Resources = saveData.ResourceLevel;
            _mapConfig.Risk = saveData.RiskLevel;
            _mapConfig.Weather = saveData.WeatherType;
            _mapConfig.DayNight = saveData.DayNightMode;
            _mapConfig.Seed = saveData.Seed;
            _seed = saveData.Seed;

            // 重新生成地图（确定性）
            _mapGenerator = new MapGenerator(_mapConfig, _seed);
            _mapGenerator.Generate();

            // 初始化分块系统
            _chunkManager.Initialize(_mapGenerator, terrainTilemap, _mapConfig);

            // 加载基地
            var basePos = _saveManager.LoadBasePosition(saveId);
            if (basePos.HasValue)
                CreateBase(basePos.Value);
            else
                CreateBase(new Vector2(_mapConfig.MapWidth / 2f, _mapConfig.MapWidth / 2f));

            // 恢复基地仓库
            var storage = _saveManager.LoadBaseStorage(saveId);
            if (_baseController != null)
                _baseController.Storage = storage;

            // 恢复已解锁科技集合
            TechTreeManager.Instance.RestoreUnlocked(_saveManager.LoadUnlockedTechs(saveId));

            // 加载Agent
            var agentStates = _saveManager.LoadAgentStates(saveId);
            foreach (var agentData in agentStates)
            {
                CreateAgent(agentData);
            }

            // 初始化时间系统（从存档恢复游戏内时间，保持昼夜时刻正确）
            _timeSystem = new TimeSystem();
            _timeSystem.Initialize(saveData.DayNightMode, GameConfigManager.Instance.Config.World.TimeRatio);
            _timeSystem.LoadFromSave(saveData.GameDay, saveData.GameTimeSeconds, saveData.PlayTimeSeconds);

            Debug.Log($"[GameScene] 时间恢复: 第{_timeSystem.GameDay}天 {_timeSystem.GetTimeString()}, gameTimeSec={saveData.GameTimeSeconds:F1}");

            // 初始化天气系统
            _weatherSystem = new WeatherSystem();
            _weatherSystem.Initialize(saveData.WeatherType, _seed);

            // 初始化记忆系统
            _memoryManager = new MemoryManager();
            _memoryManager.Initialize(_dbManager, saveId);

            // 恢复存档保存时的LLM配置（服务地址+模型），空串则保持默认配置不变
            if (!string.IsNullOrEmpty(saveData.LlmUrl) || !string.IsNullOrEmpty(saveData.LlmModel))
            {
                LLMManager.Instance?.Configure(saveData.LlmUrl, saveData.LlmModel);
                Debug.Log($"[GameScene] 已恢复LLM配置: url={saveData.LlmUrl}, model={saveData.LlmModel}");
            }

            // 设置摄像机
            if (_baseController != null)
            {
                Camera.main.transform.position =
                    new Vector3(_baseController.transform.position.x,
                                _baseController.transform.position.y, -10);
                _chunkManager.LoadInitialChunks(_baseController.transform.position);
            }

            // 设置摄像机地图边界
            var camCtrl = Camera.main.GetComponent<CameraController>();
            if (camCtrl != null) camCtrl.SetMapBounds(_mapConfig.MapWidth);

            // 初始化HUD
            if (gameHUD != null)
                gameHUD.Initialize(_timeSystem, _baseController, _agents,
                    _dbManager, _saveManager, _mapGenerator, _mapConfig);

            Debug.Log($"[GameScene] 存档加载完成！第{saveData.GameDay}天, {saveData.GetFormattedPlayTime()}");
        }

        // ==================== 实体创建 ====================

        /// <summary>
        /// 创建基地
        /// </summary>
        private void CreateBase(Vector2 position)
        {
            var baseObj = new GameObject("Base");
            _baseController = baseObj.AddComponent<BaseController>();
            _baseController.Initialize(position);
        }

        /// <summary>
        /// 创建所有初始Agent
        /// </summary>
        private void CreateAgents(Vector2 spawnPosition)
        {
            // 默认3个Agent
            var agentTypes = new[] { AgentType.Scout, AgentType.Worker, AgentType.Guard };
            var agentIds = new[] { "scout_01", "worker_01", "guard_01" };

            for (int i = 0; i < agentTypes.Length; i++)
            {
                var data = AgentData.CreateDefault(agentIds[i], agentTypes[i], spawnPosition);
                CreateAgent(data);
            }
        }

        /// <summary>
        /// 创建单个Agent
        /// </summary>
        private void CreateAgent(AgentData data)
        {
            var agentObj = new GameObject($"Agent_{data.AgentId}");
            if (agentsParent != null)
                agentObj.transform.SetParent(agentsParent);

            var controller = agentObj.AddComponent<AgentController>();
            controller.Initialize(data, _mapGenerator, _chunkManager,
                _baseController, _weatherSystem, _mapConfig.MapWidth);

            _agents[data.AgentId] = controller;
        }

        // ==================== 事件处理 ====================

        private void OnNewGameStarted(NewGameStartedEvent e)
        {
            InitializeNewGame(e.MapConfig, e.Seed);
        }

        private void OnGameLoaded(GameLoadedEvent e)
        {
            LoadGame(e.SaveId);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<NewGameStartedEvent>(OnNewGameStarted);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
            _dbManager?.Close();
        }
    }
}
