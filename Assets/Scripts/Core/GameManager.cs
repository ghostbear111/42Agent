/// <summary>
/// 游戏管理器（全局单例）
/// 负责初始化和管理所有游戏子系统，跨场景持久化
/// 是整个游戏的中枢神经系统
/// </summary>
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.Core
{
    public class GameManager : Singleton<GameManager>
    {
        // ==================== 运行时状态 ====================

        /// <summary>当前是否在游戏中</summary>
        public bool IsInGame { get; private set; }
        /// <summary>当前存档ID（空表示新游戏未存档）</summary>
        public string CurrentSaveId { get; set; }
        /// <summary>当前地图配置</summary>
        public MapConfig CurrentMapConfig { get; set; }
        /// <summary>当前地图种子</summary>
        public int CurrentSeed { get; set; }
        /// <summary>是否暂停</summary>
        public bool IsPaused { get; set; }
        /// <summary>时间倍率</summary>
        public float TimeMultiplier { get; set; } = 1f;

        // ==================== 子系统引用 ====================

        // 这些在后续Phase中实现，这里先预留引用
        // public TimeSystem TimeSystem { get; private set; }
        // public MapGenerator MapGenerator { get; private set; }
        // public ChunkManager ChunkManager { get; private set; }
        // public SaveLoadManager SaveLoadManager { get; private set; }
        // public AgentController[] Agents { get; private set; }
        // public BaseController BaseController { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[GameManager] 初始化完成");
        }

        /// <summary>
        /// 开始新游戏（从地图生成场景调用）
        /// </summary>
        public void StartNewGame(MapConfig config, int seed)
        {
            CurrentMapConfig = config;
            CurrentSeed = seed;
            CurrentSaveId = ""; // 新游戏尚未存档
            IsInGame = true;
            IsPaused = false;
            TimeMultiplier = 1f;

            Debug.Log($"[GameManager] 开始新游戏 - 星球: {config.PlanetName}, 种子: {seed}, " +
                      $"地图: {config.MapWidth}×{config.MapWidth}");

            SceneLoader.LoadScene(Constants.SCENE_GAME, () =>
            {
                EventBus.Publish(new NewGameStartedEvent
                {
                    MapConfig = config,
                    Seed = seed
                });
            });
        }

        /// <summary>
        /// 加载已有存档（从主菜单调用）
        /// </summary>
        public void LoadGame(string saveId)
        {
            CurrentSaveId = saveId;
            IsInGame = true;
            IsPaused = false;
            TimeMultiplier = 1f;

            Debug.Log($"[GameManager] 加载存档: {saveId}");

            SceneLoader.LoadScene(Constants.SCENE_GAME, () =>
            {
                EventBus.Publish(new GameLoadedEvent { SaveId = saveId });
            });
        }

        /// <summary>
        /// 保存当前游戏
        /// </summary>
        public void SaveGame()
        {
            if (!IsInGame) return;

            Debug.Log($"[GameManager] 保存游戏, 存档ID: {CurrentSaveId}");
            // 具体保存逻辑在SaveLoadManager中实现
            EventBus.Publish(new GameSavedEvent { SaveId = CurrentSaveId });
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        public void ReturnToMainMenu()
        {
            IsInGame = false;
            IsPaused = false;
            TimeMultiplier = 1f;
            EventBus.Clear();
            SceneLoader.LoadScene(Constants.SCENE_MAIN_MENU);
        }

        /// <summary>
        /// 暂停/恢复游戏
        /// </summary>
        public void TogglePause()
        {
            IsPaused = !IsPaused;
            TimeMultiplier = IsPaused ? 0f : 1f;
            Debug.Log($"[GameManager] {(IsPaused ? "暂停" : "恢复")}游戏");
        }

        /// <summary>
        /// 设置时间倍率
        /// </summary>
        public void SetTimeSpeed(float multiplier)
        {
            TimeMultiplier = multiplier;
            IsPaused = multiplier <= 0f;
            EventBus.Publish(new TimeSpeedChangedEvent { SpeedMultiplier = multiplier });
            Debug.Log($"[GameManager] 时间倍率设置为 {multiplier}x");
        }

        private void Update()
        {
            // ESC键暂停/恢复
            if (Input.GetKeyDown(KeyCode.Escape) && IsInGame)
            {
                TogglePause();
            }
        }
    }
}
