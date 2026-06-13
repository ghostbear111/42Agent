/// <summary>
/// 地图生成界面控制器
/// 在Start()中通过RuntimeUIBuilder动态构建完整UI
/// 点击发射后：生成地图 → 保存 → 进入游戏场景
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
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

        private void Start()
        {
            _rng = new System.Random();

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

            // 背景
            RuntimeUIBuilder.CreatePanel("BG", transform, new Color(0.04f, 0.06f, 0.12f),
                0f, 0f, 1f, 1f);

            // 标题
            RuntimeUIBuilder.CreateText("Title", transform, "星球环境配置", 36,
                new Color(0.9f, 0.85f, 0.4f), TextAnchor.MiddleCenter,
                0.2f, 0.88f, 0.8f, 0.96f);

            // 所有输入行
            _inputPlanetName = RuntimeUIBuilder.CreateInputField("PlanetName", transform,
                "星球名称", "留空随机生成", 0.80f);
            _dropdownMapSize = RuntimeUIBuilder.CreateDropdown("MapSize", transform,
                "地图大小", new[] { "小型 (1024x1024)", "中型 (3072x3072)", "大型 (5120x5120)" }, 0.72f);
            _dropdownTileSize = RuntimeUIBuilder.CreateDropdown("TileSize", transform,
                "瓦片大小", new[] { "32x32", "64x64" }, 0.64f);
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

            // 按钮
            var btnLaunch = RuntimeUIBuilder.CreateButton("Launch", transform,
                "发射!", new Color(0.8f, 0.5f, 0.1f), 0.25f, 0.06f, 0.45f, 0.12f);
            btnLaunch.onClick.AddListener(OnLaunch);

            var btnBack = RuntimeUIBuilder.CreateButton("Back", transform,
                "返回", new Color(0.3f, 0.3f, 0.3f), 0.55f, 0.06f, 0.75f, 0.12f);
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

        /// <summary>点击发射按钮</summary>
        private void OnLaunch()
        {
            // 构建配置
            var config = ScriptableObject.CreateInstance<MapConfig>();
            config.PlanetName = _inputPlanetName != null ? _inputPlanetName.text : "未命名星球";
            config.MapSize = _dropdownMapSize.value switch
            {
                0 => MapSize.Small, 1 => MapSize.Medium, _ => MapSize.Large
            };
            config.TileSize = _dropdownTileSize.value == 0 ? TilePixelSize.Size32 : TilePixelSize.Size64;
            config.Terrain = (TerrainComplexity)_dropdownTerrain.value;
            config.Resources = (ResourceAbundance)_dropdownResources.value;
            config.Risk = (RiskLevel)_dropdownRisk.value;
            config.Weather = (WeatherPattern)_dropdownWeather.value;
            config.DayNight = (DayNightMode)_dropdownDayNight.value;

            int seed;
            if (_inputSeed == null || string.IsNullOrEmpty(_inputSeed.text) || !int.TryParse(_inputSeed.text, out seed))
                seed = UnityEngine.Random.Range(1, 999999);

            Debug.Log($"[MapGenUI] 发射！星球:{config.PlanetName}, 种子:{seed}");

            // 生成地图
            var mapGen = new MapGenerator(config, seed);
            mapGen.Generate();

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

            // 切换到游戏场景
            GameManager.Instance.StartNewGame(config, seed, saveId);
        }

        /// <summary>返回主菜单</summary>
        private void OnBack()
        {
            SceneLoader.LoadScene(Constants.SCENE_MAIN_MENU);
        }
    }
}
