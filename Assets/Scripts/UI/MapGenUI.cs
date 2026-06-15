/// <summary>
/// 地图生成界面控制器
/// 在Start()中通过RuntimeUIBuilder动态构建完整UI
/// 点击发射后：生成地图 → 保存 → 进入游戏场景
/// </summary>
using System;
using System.Collections;
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
using GalaxyAgent.LLM;
using GalaxyAgent.Map;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class MapGenUI : MonoBehaviour
    {
        // 运行时UI引用
        private InputField _inputPlanetName;
        private Dropdown _dropdownMapSize;
        private Dropdown _dropdownTileSize;
        private Dropdown _dropdownTerrain;
        private Dropdown _dropdownResources;
        private Dropdown _dropdownRisk;
        private Dropdown _dropdownWeather;
        private Dropdown _dropdownDayNight;
        private InputField _inputSeed;

        // 随机名称素材
        private static readonly string[] Prefixes = { "阿尔法", "贝塔", "伽马", "德尔塔", "厄普西隆", "泽塔", "西格玛", "欧米茄" };
        private static readonly string[] Suffixes = { "-7b", "-12c", "-X", "-9d", "-Prime", "-Nova" };
        private static readonly string[] Names = { "蔚蓝", "赤焰", "翡翠", "黄金", "银霜", "暗影", "极光", "深渊", "黎明", "星尘" };

        private System.Random _rng;

        // LLM 创建星球相关
        private Button _btnLLMCreate;       // LLM 创建星球按钮（按连接状态启用/禁用）
        private Text _llmHintText;          // 未连接提示
        private LLMPlanetDialog _dialog;    // 对话窗口（按需创建）
        private float _statusTimer;

        // LLM 生成的星球介绍（暂存，OnLaunch 时写入 config.PlanetDescription，供游戏内顶栏查看）
        private string _pendingLLMDescription = "";

        private void Start()
        {
            _rng = new System.Random();

            // 触发 LLMManager 初始化（主菜单通常已创建并跨场景持久，此处复用同一实例）
            _ = LLMManager.Instance;

            // 始终构建UI（输入框引用为空说明尚未构建）
            if (_inputPlanetName == null)
            {
                BuildUI();
            }

            // 自动生成随机名称和种子
            OnRandomName();
            OnRandomSeed();

            Debug.Log("[MapGenUI] 初始化完成");
        }

        /// <summary>
        /// 动态构建地图生成UI
        /// </summary>
        private void BuildUI()
        {
            RuntimeUIBuilder.EnsureEventSystem();

            // 背景（贴场景背景图，覆盖默认面板底纹）
            var bg = RuntimeUIBuilder.CreatePanel("BG", transform, new Color(0.04f, 0.06f, 0.12f),
                0f, 0f, 1f, 1f);
            RuntimeUIBuilder.ApplySceneBackground(bg, "mapgen");

            // 标题
            RuntimeUIBuilder.CreateText("Title", transform, "星球环境配置", 36,
                new Color(0.9f, 0.85f, 0.4f), TextAnchor.MiddleCenter,
                0.2f, 0.88f, 0.8f, 0.96f);

            // 所有输入行
            _inputPlanetName = RuntimeUIBuilder.CreateInputField("PlanetName", transform,
                "星球名称", "留空随机生成", 0.80f);
            _dropdownMapSize = RuntimeUIBuilder.CreateDropdown("MapSize", transform,
                "地图大小", new[] { "微型 (128x128)", "小型 (256x256)", "中型 (512x512)", "大型 (1024x1024)", "巨型 (2048x2048)" }, 0.72f);
            _dropdownTileSize = RuntimeUIBuilder.CreateDropdown("TileSize", transform,
                "瓦片大小", new[] { "32x32", "64x64", "128x128" }, 0.64f);
            _dropdownTerrain = RuntimeUIBuilder.CreateDropdown("Terrain", transform,
                "地形复杂度", new[] { "平坦", "丰富", "凶险" }, 0.56f);
            _dropdownResources = RuntimeUIBuilder.CreateDropdown("Resources", transform,
                "资源丰富度", new[] { "贫乏", "适中", "富饶" }, 0.48f);
            _dropdownRisk = RuntimeUIBuilder.CreateDropdown("Risk", transform,
                "风险等级", new[] { "低", "中", "高" }, 0.40f);
            _dropdownWeather = RuntimeUIBuilder.CreateDropdown("Weather", transform,
                "天气模式", new[] { "温和", "多变", "恶劣" }, 0.32f);
            _dropdownDayNight = RuntimeUIBuilder.CreateDropdown("DayNight", transform,
                "昼夜模式", new[] { "永昼", "交替", "永夜" }, 0.24f);
            _inputSeed = RuntimeUIBuilder.CreateInputField("Seed", transform,
                "种子 (留空随机)", "", 0.16f);

            // LLM 创建星球按钮（最左，按连接状态启用/禁用）
            _btnLLMCreate = RuntimeUIBuilder.CreateButton("BtnLLMCreate", transform,
                "LLM创建星球", new Color(0.35f, 0.25f, 0.55f),
                0.04f, 0.06f, 0.30f, 0.12f);
            _btnLLMCreate.onClick.AddListener(OnLLMCreateClicked);

            // 未连接提示（LLM 按钮上方一行，连接后留空）
            _llmHintText = RuntimeUIBuilder.CreateText("LLMHint", transform,
                "", 13, new Color(0.85f, 0.65f, 0.4f), TextAnchor.MiddleCenter,
                0.04f, 0.125f, 0.30f, 0.155f);

            // 发射按钮（中）
            var btnLaunch = RuntimeUIBuilder.CreateButton("Launch", transform,
                "发射!", new Color(0.8f, 0.5f, 0.1f), 0.36f, 0.06f, 0.64f, 0.12f);
            btnLaunch.onClick.AddListener(OnLaunch);

            // 返回按钮（右）
            var btnBack = RuntimeUIBuilder.CreateButton("Back", transform,
                "返回", new Color(0.3f, 0.3f, 0.3f), 0.70f, 0.06f, 0.96f, 0.12f);
            btnBack.onClick.AddListener(OnBack);
        }

        /// <summary>生成随机星球名称</summary>
        private void OnRandomName()
        {
            string name = Names[_rng.Next(Names.Length)];
            string prefix = Prefixes[_rng.Next(Prefixes.Length)];
            string suffix = Suffixes[_rng.Next(Suffixes.Length)];
            if (_inputPlanetName != null)
                _inputPlanetName.text = $"{prefix}-{name}{suffix}";
        }

        /// <summary>生成随机种子</summary>
        private void OnRandomSeed()
        {
            if (_inputSeed != null)
                _inputSeed.text = _rng.Next(1, 999999).ToString();
        }

        private void Update()
        {
            // 轮询 LLM 连接状态，控制「LLM 创建星球」按钮可用性
            _statusTimer += Time.unscaledDeltaTime;
            if (_statusTimer >= 0.5f)
            {
                _statusTimer = 0f;
                UpdateLLMButtonState();
            }
        }

        /// <summary>按 LLM 连接状态启用/禁用「LLM 创建星球」按钮，并刷新未连接提示</summary>
        private void UpdateLLMButtonState()
        {
            var mgr = LLMManager.Instance;
            bool available = mgr != null && mgr.IsAvailable;
            if (_btnLLMCreate != null) _btnLLMCreate.interactable = available;
            if (_llmHintText != null)
                _llmHintText.text = available ? "" : "LLM未连接，请到主菜单设置";
        }

        /// <summary>点击「LLM 创建星球」：按需构建并打开对话窗口</summary>
        private void OnLLMCreateClicked()
        {
            if (_dialog == null)
            {
                _dialog = gameObject.AddComponent<LLMPlanetDialog>();
                _dialog.BuildUI(transform);
                // 订阅：LLM 生成成功后回填到本界面表单
                _dialog.OnPlanetCreated += ApplyCreationResult;
            }
            _dialog.Show();
        }

        /// <summary>
        /// 把 LLM 生成的星球参数回填到表单（名称/各下拉/随机种子），供用户确认或微调后再发射。
        /// 注意：MapSize/TileSize 枚举值为实际尺寸（1024/3072/5120、32/64），不能直接当下拉索引，
        /// 需单独映射；其余枚举值恰好与下拉选项顺序一致，可直接转 int。
        /// </summary>
        private void ApplyCreationResult(PlanetCreationResult result)
        {
            if (result == null) return;

            // 暂存星球介绍，供发射时写入存档（游戏内顶栏点击星球名可查看）
            _pendingLLMDescription = result.Description ?? "";

            // 星球名称
            if (_inputPlanetName != null && !string.IsNullOrWhiteSpace(result.PlanetName))
                _inputPlanetName.text = result.PlanetName;

            // 地图大小（枚举值=尺寸，需映射为下拉索引）
            if (_dropdownMapSize != null)
            {
                _dropdownMapSize.value = result.MapSize switch
                {
                    MapSize.Tiny => 0, MapSize.Small => 1, MapSize.Medium => 2,
                    MapSize.Large => 3, _ => 4
                };
            }
            // 瓦片大小（枚举值=像素，需映射为下拉索引）
            if (_dropdownTileSize != null)
                _dropdownTileSize.value = result.TileSize switch
                {
                    TilePixelSize.Size32 => 0, TilePixelSize.Size64 => 1, _ => 2
                };

            // 其余枚举值与下拉选项顺序一致，直接转 int
            if (_dropdownTerrain != null) _dropdownTerrain.value = (int)result.Terrain;
            if (_dropdownResources != null) _dropdownResources.value = (int)result.Resources;
            if (_dropdownRisk != null) _dropdownRisk.value = (int)result.Risk;
            if (_dropdownWeather != null) _dropdownWeather.value = (int)result.Weather;
            if (_dropdownDayNight != null) _dropdownDayNight.value = (int)result.DayNight;

            // 种子随机（地球基准：种子随机）
            if (_inputSeed != null)
                _inputSeed.text = _rng.Next(1, 999999).ToString();

            Debug.Log($"[MapGenUI] LLM 星球已回填: {result.PlanetName} | " +
                      $"大小:{result.MapSize} 地形:{result.Terrain} 资源:{result.Resources} " +
                      $"风险:{result.Risk} 天气:{result.Weather} 昼夜:{result.DayNight}");
        }

        /// <summary>点击发射按钮：启动发射协程（用 Loading 遮罩覆盖地图生成等同步耗时阶段）</summary>
        private void OnLaunch()
        {
            StartCoroutine(LaunchCoroutine());
        }

        /// <summary>
        /// 发射协程：生成地图 → 建存档 → 缓存地图 → 切换场景。
        /// 各阶段用 LoadingScreen 给出进度反馈；地图生成是同步阻塞，故在生成前先 Show 并 yield 一帧。
        /// 把已生成的地图缓存到 GameManager，供 GameScene 复用，省掉进场景时的重复生成。
        /// </summary>
        private IEnumerator LaunchCoroutine()
        {
            // 构建配置
            var config = ScriptableObject.CreateInstance<MapConfig>();
            config.PlanetName = _inputPlanetName != null ? _inputPlanetName.text : "未命名星球";
            config.MapSize = _dropdownMapSize.value switch
            {
                0 => MapSize.Tiny, 1 => MapSize.Small, 2 => MapSize.Medium,
                3 => MapSize.Large, _ => MapSize.Huge
            };
            config.TileSize = _dropdownTileSize.value switch
            {
                0 => TilePixelSize.Size32, 1 => TilePixelSize.Size64, _ => TilePixelSize.Size128
            };
            config.Terrain = (TerrainComplexity)_dropdownTerrain.value;
            config.Resources = (ResourceAbundance)_dropdownResources.value;
            config.Risk = (RiskLevel)_dropdownRisk.value;
            config.Weather = (WeatherPattern)_dropdownWeather.value;
            config.DayNight = (DayNightMode)_dropdownDayNight.value;

            // 写入 LLM 生成的星球介绍（若有），供游戏内顶栏点击星球名查看
            config.PlanetDescription = _pendingLLMDescription ?? "";

            int seed;
            if (_inputSeed == null || string.IsNullOrEmpty(_inputSeed.text) || !int.TryParse(_inputSeed.text, out seed))
                seed = UnityEngine.Random.Range(1, 999999);

            Debug.Log($"[MapGenUI] 发射！星球:{config.PlanetName}, 种子:{seed}");

            // 显示 Loading 遮罩，让出一帧使其渲染出来
            LoadingScreen.Show("正在生成星球…", 0f);
            yield return null;

            // 协程化生成地图：按地形行分帧推进进度，避免大地图生成时进度条卡死
            var mapGen = new MapGenerator(config, seed);
            yield return mapGen.GenerateCoroutine((tip, p) => LoadingScreen.Show(tip, p));

            // 导出地图图片(俯视PNG，供转 AI 生图)，在 Loading 遮罩下同步导出到 Assets/MapExports/
            LoadingScreen.Show("正在导出地图图片…", 0.90f);
            yield return null;
            string pngPath = MapImageExporter.Export(mapGen, config, seed);
            Debug.Log($"[MapGenUI] 地图图片已导出: {pngPath}");

            LoadingScreen.Show("正在创建存档…", 0.92f);
            yield return null;

            // 创建Agent
            Vector2 basePos = new Vector2(config.MapWidth / 2f, config.MapWidth / 2f);
            var agents = new AgentData[]
            {
                AgentData.CreateDefault("scout_01", AgentType.Scout, basePos),
                AgentData.CreateDefault("worker_01", AgentType.Worker, basePos),
                AgentData.CreateDefault("guard_01", AgentType.Guard, basePos)
            };

            // 保存到数据库
            var dbManager = new DatabaseManager();
            dbManager.Initialize();
            var saveManager = new SaveLoadManager(dbManager);
            string saveId = saveManager.CreateNewSave(mapGen, config, seed, agents, basePos);
            dbManager.Close();

            // 缓存已生成的地图，供 GameScene 直接复用，省掉一次重复生成（大地图收益明显）
            GameManager.Instance.PendingMapGenerator = mapGen;

            LoadingScreen.Show("准备进入星球…", 1f);
            yield return null;

            // 切换到游戏场景（遮罩是 DontDestroyOnLoad 会持续显示，由 GameScene 的 InitializeCoroutine 完成后收起）
            GameManager.Instance.StartNewGame(config, seed, saveId);
        }

        /// <summary>返回主菜单</summary>
        private void OnBack()
        {
            SceneLoader.LoadScene(Constants.SCENE_MAIN_MENU);
        }
    }
}
