/// <summary>
/// 游戏HUD主控制器
/// 显示游戏时间、资源、速度控制、保存/返回按钮
/// 处理Agent和基地的点击交互
/// 支持运行时自构建：当关键字段为空时自动调用BuildUI()创建完整HUD
///
/// 布局结构：
/// ┌─────────────────────────────────────────────────┐
/// │ [时间] [速度]  [棕■矿物][黄■晶体][蓝■水][绿■有机][紫■遗迹] │  ← 顶栏
/// ├────────────────────────────┬────────────────────┤
/// │                            │                    │
/// │       (地图渲染区域)         │   信息面板(隐藏)    │  ← 右侧
/// │                            │   Agent/Base详情    │
/// │                            │                    │
/// ├────────────────────────────┴────────────────────┤
/// │ [暂停][1x][2x][5x]              [保存] [返回菜单] │  ← 底栏
/// └─────────────────────────────────────────────────┘
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Config;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
using GalaxyAgent.LLM;
using GalaxyAgent.Map;
using GalaxyAgent.World;
using GalaxyAgent.World.Base;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class GameHUD : MonoBehaviour
    {
        // ==================== Inspector字段（可运行时赋值） ====================

        [Header("时间显示")]
        [Tooltip("时间文本（第X天 HH:MM）")]
        public Text textTime;
        [Tooltip("时间倍率文本")]
        public Text textSpeed;

        [Header("资源显示")]
        [Tooltip("资源文本（矿物/晶体/水/有机/遗迹）——旧版兼容")]
        public Text textResources;

        [Header("操作按钮")]
        [Tooltip("保存按钮")]
        public Button buttonSave;
        [Tooltip("返回主菜单按钮")]
        public Button buttonMainMenu;
        [Tooltip("暂停按钮")]
        public Button buttonPause;
        [Tooltip("1倍速按钮")]
        public Button buttonSpeed1x;
        [Tooltip("2倍速按钮")]
        public Button buttonSpeed2x;
        [Tooltip("5倍速按钮")]
        public Button buttonSpeed5x;
        [Tooltip("LLM对话查看按钮")]
        public Button buttonLLMChat;
        [Tooltip("游戏配置按钮（打开运行时配置面板）")]
        public Button buttonConfig;

        [Header("信息面板")]
        [Tooltip("Agent信息面板")]
        public AgentInfoPanel agentInfoPanel;
        [Tooltip("基地信息面板")]
        public BaseInfoPanel baseInfoPanel;

        // ==================== 运行时状态 ====================

        // 独立资源文本数组（自构建模式使用）
        private Text[] _resourceTexts = new Text[5];
        // 防重复构建守卫
        private bool _uiBuilt = false;
        // LLM对话查看窗口
        private LLMConversationWindow _llmWindow;
        // 游戏配置运行时面板
        private GameConfigPanel _configPanel;
        // 科技树面板（基地"科技树"按钮入口）
        private TechTreePanel _techTreePanel;
        // 自动保存状态文本（底栏中部，显示开关/倒计时/上次保存时间）
        private Text _autoSaveText;
        // 距离下次自动保存的倒计时（现实秒）
        private float _autoSaveTimer = 0f;
        // 上次自动/手动保存时的游戏天数（用于显示）
        private int _lastSaveDay = 0;

        // Agent头像选择条：顶栏下方左上角，点击头像可选中Agent并让摄像机跟随
        private GameObject _agentBar;
        // AgentId → 头像外框Image（用于切换选中高亮）
        private readonly Dictionary<string, Image> _avatarFrames = new Dictionary<string, Image>();
        // 基地头像外框Image（与Agent头像一起放在选择条末尾）
        private Image _baseAvatarFrame;
        // 当前选中的AgentId
        private string _selectedAgentId;
        // 摄像机控制器（选中Agent后让其跟随目标）
        private CameraController _cameraController;

        // 头像外框颜色：未选中(深色) / 选中(亮黄)
        private static readonly Color AvatarFrameNormal = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        private static readonly Color AvatarFrameSelected = new Color(1f, 0.82f, 0.2f, 1f);

        // 运行时子系统引用（由GameSceneController.Initialize设置）
        private TimeSystem _timeSystem;
        private BaseController _baseController;
        private Dictionary<string, AgentController> _agents;
        private DatabaseManager _dbManager;
        private SaveLoadManager _saveManager;
        private MapGenerator _mapGenerator;
        private MapConfig _mapConfig;

        // ==================== 生命周期 ====================

        private void Start()
        {
            // 如果关键UI引用缺失，自动构建UI
            if (!_uiBuilt && (textTime == null || buttonSave == null))
            {
                BuildUI();
            }

            // 绑定按钮事件
            if (buttonSave != null) buttonSave.onClick.AddListener(OnSaveClicked);
            if (buttonMainMenu != null) buttonMainMenu.onClick.AddListener(OnMainMenuClicked);
            if (buttonPause != null) buttonPause.onClick.AddListener(() => SetSpeed(0f));
            if (buttonSpeed1x != null) buttonSpeed1x.onClick.AddListener(() => SetSpeed(1f));
            if (buttonSpeed2x != null) buttonSpeed2x.onClick.AddListener(() => SetSpeed(2f));
            if (buttonSpeed5x != null) buttonSpeed5x.onClick.AddListener(() => SetSpeed(5f));
            if (buttonLLMChat != null) buttonLLMChat.onClick.AddListener(ToggleLLMWindow);
            if (buttonConfig != null) buttonConfig.onClick.AddListener(ToggleConfigPanel);

            // 订阅事件
            EventBus.Subscribe<AgentClickedEvent>(OnAgentClicked);
            EventBus.Subscribe<BaseClickedEvent>(OnBaseClicked);
            EventBus.Subscribe<MapClickedEvent>(OnMapClicked);

            // 自动保存倒计时初始化（间隔从配置读取，Update中循环）
            _autoSaveTimer = GetAutoSaveInterval();
        }

        private void Update()
        {
            UpdateTimeDisplay();
            UpdateResourceDisplay();
            UpdateAutoSave();
        }

        // ==================== 运行时UI构建 ====================

        /// <summary>
        /// 运行时自构建完整游戏HUD
        /// 创建顶栏（时间+资源）、底栏（速度+操作按钮）、侧边信息面板
        /// 幂等方法：多次调用不会重复创建
        /// </summary>
        private void BuildUI()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;

            Debug.Log("[GameHUD] 开始运行时构建UI...");
            RuntimeUIBuilder.EnsureEventSystem();

            // ==================== 顶栏 ====================
            // 显示时间和资源信息，anchor(0, 0.92) ~ (1, 1)
            var topBar = RuntimeUIBuilder.CreatePanel("TopBar", transform,
                new Color(0.04f, 0.04f, 0.1f, 0.9f),
                0f, 0.92f, 1f, 1f);

            // 时间文本（顶栏左侧）
            textTime = RuntimeUIBuilder.CreateText("Time", topBar.transform,
                "第1天 00:00", 18, new Color(0.85f, 0.9f, 1f),
                TextAnchor.MiddleLeft, 0.01f, 0.05f, 0.22f, 0.95f);

            // 速度文本
            textSpeed = RuntimeUIBuilder.CreateText("Speed", topBar.transform,
                "1x", 16, new Color(0.7f, 0.8f, 1f),
                TextAnchor.MiddleLeft, 0.22f, 0.05f, 0.32f, 0.95f);

            // ---------- 资源显示区域（顶栏右侧，颜色方块+数值） ----------
            // 5种资源：矿物(棕) 晶体(黄) 水(蓝) 有机(绿) 遗迹(紫)
            var resourceColors = new[]
            {
                Constants.COLOR_MINERAL,  // 矿物 - 棕色
                Constants.COLOR_CRYSTAL,  // 晶体 - 黄色
                Constants.COLOR_WATER,    // 水 - 蓝色
                Constants.COLOR_ORGANIC,  // 有机 - 绿色
                Constants.COLOR_RUIN      // 遗迹 - 紫色
            };
            var resourceNames = new[] { "矿物", "晶体", "水", "有机", "遗迹" };

            // 资源区域占顶栏右侧 0.34 ~ 1.0，每个资源占约0.13宽度
            float resStartX = 0.34f;
            float resWidth = 0.13f;

            for (int i = 0; i < 5; i++)
            {
                float x0 = resStartX + i * resWidth;
                float x1 = x0 + resWidth;

                // 颜色方块（资源行左侧小方块）
                RuntimeUIBuilder.CreateColorBlock($"ResBlock_{resourceNames[i]}",
                    topBar.transform, resourceColors[i],
                    x0, 0.25f, x0 + 0.03f, 0.75f);

                // 资源数值文本
                _resourceTexts[i] = RuntimeUIBuilder.CreateText($"ResText_{resourceNames[i]}",
                    topBar.transform, $"{resourceNames[i]}:0", 13,
                    new Color(0.85f, 0.85f, 0.85f),
                    TextAnchor.MiddleLeft, x0 + 0.035f, 0.05f, x1, 0.95f);
            }

            // ==================== Agent头像选择条 ====================
            // 挂在顶栏下方，水平排列各Agent头像。
            // 头像在Initialize()时根据实际Agent填充（BuildUI先于Agent创建执行）。
            CreateAgentBar(topBar);

            // ==================== 底栏 ====================
            // 速度控制按钮 + 保存/返回按钮，anchor(0, 0) ~ (1, 0.08)
            var bottomBar = RuntimeUIBuilder.CreatePanel("BottomBar", transform,
                new Color(0.04f, 0.04f, 0.1f, 0.9f),
                0f, 0f, 1f, 0.08f);

            // 速度控制按钮（底栏左侧）
            buttonPause = RuntimeUIBuilder.CreateButton("BtnPause", bottomBar.transform,
                "暂停", new Color(0.35f, 0.35f, 0.4f),
                0.01f, 0.1f, 0.1f, 0.9f);

            buttonSpeed1x = RuntimeUIBuilder.CreateButton("Btn1x", bottomBar.transform,
                "1x", new Color(0.2f, 0.4f, 0.6f),
                0.11f, 0.1f, 0.18f, 0.9f);

            buttonSpeed2x = RuntimeUIBuilder.CreateButton("Btn2x", bottomBar.transform,
                "2x", new Color(0.2f, 0.4f, 0.6f),
                0.19f, 0.1f, 0.26f, 0.9f);

            buttonSpeed5x = RuntimeUIBuilder.CreateButton("Btn5x", bottomBar.transform,
                "5x", new Color(0.2f, 0.4f, 0.6f),
                0.27f, 0.1f, 0.34f, 0.9f);

            // 游戏配置按钮（底栏左侧空白区，打开运行时配置面板）
            buttonConfig = RuntimeUIBuilder.CreateButton("BtnConfig", bottomBar.transform,
                "配置", new Color(0.3f, 0.35f, 0.2f),
                0.35f, 0.1f, 0.45f, 0.9f);

            // LLM对话查看按钮（配置右侧，紫色标识）
            buttonLLMChat = RuntimeUIBuilder.CreateButton("BtnLLMChat", bottomBar.transform,
                "LLM对话", new Color(0.35f, 0.25f, 0.55f),
                0.46f, 0.1f, 0.58f, 0.9f);

            // 自动保存状态文本（底栏中部，显示开关/倒计时/上次保存游戏天数）
            _autoSaveText = RuntimeUIBuilder.CreateText("AutoSaveStatus", bottomBar.transform,
                "自动保存: --", 12, new Color(0.7f, 0.75f, 0.6f),
                TextAnchor.MiddleCenter, 0.59f, 0.1f, 0.71f, 0.9f);

            // 操作按钮（底栏右侧）
            buttonSave = RuntimeUIBuilder.CreateButton("BtnSave", bottomBar.transform,
                "保存", new Color(0.15f, 0.45f, 0.25f),
                0.72f, 0.1f, 0.84f, 0.9f);

            buttonMainMenu = RuntimeUIBuilder.CreateButton("BtnMenu", bottomBar.transform,
                "返回菜单", new Color(0.5f, 0.15f, 0.15f),
                0.85f, 0.1f, 0.99f, 0.9f);

            // ==================== LLM对话查看窗口 ====================
            // 运行时自构建，初始隐藏，点击"LLM对话"按钮切换显示
            // 注意：中间层容器必须用RectTransform撑满父级，否则普通Transform在Canvas下无尺寸，
            // 其下窗口面板的锚点会坍缩为0导致整个窗口不可见
            var llmWindowObj = MakeFullScreenContainer("LLMConversationWindow", transform);
            _llmWindow = llmWindowObj.AddComponent<LLMConversationWindow>();
            _llmWindow.BuildUI(llmWindowObj.transform);

            // ==================== 游戏配置运行时面板 ====================
            // 点击"配置"按钮切换显示，编辑后保存到 game_config.json 并即时生效
            var configPanelObj = MakeFullScreenContainer("GameConfigPanel", transform);
            _configPanel = configPanelObj.AddComponent<GameConfigPanel>();
            _configPanel.BuildUI(configPanelObj.transform);

            // ==================== 侧边信息面板区域 ====================
            // Agent信息面板和基地信息面板共享右侧区域，同时只显示一个
            // anchor(0.78, 0.08) ~ (1.0, 0.92)

            // Agent信息面板
            var agentPanelObj = MakeFullScreenContainer("AgentInfoPanel", transform);
            agentInfoPanel = agentPanelObj.AddComponent<AgentInfoPanel>();
            agentInfoPanel.BuildUI(agentPanelObj.transform);

            // 基地信息面板
            var basePanelObj = MakeFullScreenContainer("BaseInfoPanel", transform);
            baseInfoPanel = basePanelObj.AddComponent<BaseInfoPanel>();
            baseInfoPanel.BuildUI(basePanelObj.transform);

            // 科技树面板（基地"科技树"按钮入口，初始隐藏）
            var techPanelObj = MakeFullScreenContainer("TechTreePanel", transform);
            _techTreePanel = techPanelObj.AddComponent<TechTreePanel>();
            _techTreePanel.BuildUI(techPanelObj.transform);

            Debug.Log("[GameHUD] UI构建完成");
        }

        /// <summary>
        /// 在Canvas下创建一个撑满父级的RectTransform容器，作为运行时面板的中间层。
        /// 普通Transform在Canvas下没有尺寸，会导致子面板（用锚点定位）坍缩为0而不可见。
        /// </summary>
        private static GameObject MakeFullScreenContainer(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return obj;
        }

        // ==================== Agent头像选择条 ====================

        /// <summary>
        /// 创建头像选择条容器（空），挂在顶栏下方，按内容横向自适应宽度。
        /// 实际头像在Initialize()中由PopulateAgentBar()填充。
        /// 挂在顶栏下作为子物体，锚定顶栏左下角向下延伸，任意分辨率都紧贴顶栏下方。
        /// </summary>
        private void CreateAgentBar(GameObject topBar)
        {
            var barObj = new GameObject("AgentBar");
            barObj.transform.SetParent(topBar.transform, false);
            var br = barObj.AddComponent<RectTransform>();

            // 锚定顶栏左下角，pivot设为左上 → 头像条从顶栏底部向下、向右延伸
            br.anchorMin = new Vector2(0f, 0f);
            br.anchorMax = new Vector2(0f, 0f);
            br.pivot = new Vector2(0f, 1f);
            br.anchoredPosition = new Vector2(8f, -8f);

            // 水平布局 + 随内容自适应宽高
            var hlg = barObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.UpperLeft;
            var csf = barObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _agentBar = barObj;
        }

        /// <summary>
        /// 根据当前所有Agent(及基地)填充头像（清空后重建）
        /// </summary>
        private void PopulateAgentBar()
        {
            if (_agentBar == null) return;

            _avatarFrames.Clear();
            _baseAvatarFrame = null;
            foreach (Transform child in _agentBar.transform)
                Destroy(child.gameObject);

            // 基地头像（排在最左，白底）—— 放在所有Agent头像之前
            if (_baseController != null)
            {
                var baseAvatar = CreateAvatarButton("Avatar_Base", "基地",
                    Constants.COLOR_BASE, SelectBase);
                _baseAvatarFrame = baseAvatar.GetComponent<Image>();
            }

            // Agent头像（基地之后，Agent之间相对顺序不变）
            if (_agents != null)
            {
                foreach (var kvp in _agents)
                {
                    var data = kvp.Value.AgentData;
                    var avatar = CreateAvatarButton($"Avatar_{data.AgentId}",
                        data.DisplayName, GetAgentTypeColor(data.AgentType),
                        () => SelectAgent(data.AgentId));
                    _avatarFrames[kvp.Key] = avatar.GetComponent<Image>();
                }
            }

            // 运行时构建后强制立即重建布局，确保头像条首帧就显示正确尺寸
            LayoutRebuilder.ForceRebuildLayoutImmediate(_agentBar.transform as RectTransform);
        }

        /// <summary>
        /// 创建单个头像：外框(选中高亮) + 色块 + 名称，整体可点击。
        /// Agent头像与基地头像共用此方法。
        /// </summary>
        private GameObject CreateAvatarButton(string objName, string displayName,
            Color blockColor, UnityAction onClick)
        {
            // 外层：Image作为外框背景与按钮点击区域，VerticalLayoutGroup排列色块和名称
            var avatar = new GameObject(objName);
            avatar.transform.SetParent(_agentBar.transform, false);
            avatar.AddComponent<RectTransform>();

            var frame = avatar.AddComponent<Image>();
            frame.color = AvatarFrameNormal;

            var vlg = avatar.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var avatarLayout = avatar.AddComponent<LayoutElement>();
            avatarLayout.preferredWidth = 96;
            avatarLayout.preferredHeight = 96;
            avatarLayout.minWidth = 96;
            avatarLayout.minHeight = 96;

            // 色块（占主体高度）
            var blockObj = new GameObject("ColorBlock");
            blockObj.transform.SetParent(avatar.transform, false);
            var block = blockObj.AddComponent<Image>();
            block.color = blockColor;
            var blockLayout = blockObj.AddComponent<LayoutElement>();
            blockLayout.flexibleHeight = 1f;

            // 名称（固定高度）
            var nameText = RuntimeUIBuilder.CreateText("Name", avatar.transform,
                displayName, 13, Color.white, TextAnchor.MiddleCenter);
            var nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = 20;
            nameLayout.minHeight = 20;
            nameLayout.flexibleHeight = 0f;

            // 按钮交互
            var btn = avatar.AddComponent<Button>();
            btn.targetGraphic = frame;
            btn.onClick.AddListener(onClick);

            return avatar;
        }

        /// <summary>
        /// 清除所有头像高亮（Agent头像 + 基地头像）恢复为未选中色
        /// </summary>
        private void ClearAllHighlights()
        {
            foreach (var kvp in _avatarFrames)
            {
                if (kvp.Value != null) kvp.Value.color = AvatarFrameNormal;
            }
            if (_baseAvatarFrame != null) _baseAvatarFrame.color = AvatarFrameNormal;
        }

        /// <summary>
        /// 选中指定Agent：高亮头像 + 显示信息面板 + 摄像机跟随
        /// </summary>
        private void SelectAgent(string agentId)
        {
            _selectedAgentId = agentId;

            // 更新头像高亮（仅当前Agent高亮）
            ClearAllHighlights();
            if (_avatarFrames.TryGetValue(agentId, out var frame) && frame != null)
                frame.color = AvatarFrameSelected;

            if (_agents == null || !_agents.TryGetValue(agentId, out var controller)) return;

            // 显示Agent信息面板，隐藏基地面板
            if (baseInfoPanel != null) baseInfoPanel.Hide();
            if (agentInfoPanel != null) agentInfoPanel.Show(controller.AgentData);

            // 摄像机跟随该Agent
            if (_cameraController != null)
                _cameraController.SetFollowTarget(controller.transform);
        }

        /// <summary>
        /// 选中基地：高亮基地头像 + 显示基地信息面板 + 摄像机移到基地
        /// </summary>
        private void SelectBase()
        {
            _selectedAgentId = null;

            ClearAllHighlights();
            if (_baseAvatarFrame != null) _baseAvatarFrame.color = AvatarFrameSelected;

            // 显示基地信息面板，隐藏Agent面板
            if (agentInfoPanel != null) agentInfoPanel.Hide();
            if (baseInfoPanel != null && _baseController != null)
                baseInfoPanel.Show(_baseController);

            // 摄像机移到基地（基地静止，即镜头居中基地）
            if (_cameraController != null && _baseController != null)
                _cameraController.SetFollowTarget(_baseController.transform);
        }

        /// <summary>
        /// 取消选中：清除所有头像高亮 + 摄像机恢复自由视角
        /// </summary>
        private void DeselectAgent()
        {
            _selectedAgentId = null;
            ClearAllHighlights();
            if (_cameraController != null)
                _cameraController.ClearFollowTarget();
        }

        /// <summary>Agent类型对应的标识色（与AgentController保持一致）</summary>
        private static Color GetAgentTypeColor(AgentType type)
        {
            return type switch
            {
                AgentType.Scout => Constants.COLOR_AGENT_SCOUT,
                AgentType.Worker => Constants.COLOR_AGENT_WORKER,
                AgentType.Guard => Constants.COLOR_AGENT_GUARD,
                _ => Color.gray
            };
        }

        // ==================== 初始化（由GameSceneController调用） ====================

        /// <summary>
        /// 初始化HUD（由GameSceneController调用）
        /// 如果UI尚未构建，先触发构建
        /// </summary>
        public void Initialize(TimeSystem timeSystem, BaseController baseController,
            Dictionary<string, AgentController> agents, DatabaseManager dbManager,
            SaveLoadManager saveManager, MapGenerator mapGenerator, MapConfig mapConfig)
        {
            // 确保UI已构建（解决GameSceneController.Start先于GameHUD.Start的顺序问题）
            if (!_uiBuilt)
            {
                BuildUI();
            }

            _timeSystem = timeSystem;
            _baseController = baseController;
            _agents = agents;
            _dbManager = dbManager;
            _saveManager = saveManager;
            _mapGenerator = mapGenerator;
            _mapConfig = mapConfig;

            // 获取摄像机控制器引用（用于选中Agent后跟随）
            var cam = Camera.main;
            _cameraController = cam != null ? cam.GetComponent<CameraController>() : null;

            // 把所有AgentId注入LLM对话窗口，供左侧选择查看
            if (_llmWindow != null && _agents != null)
            {
                _llmWindow.SetAgentIds(new List<string>(_agents.Keys));
            }

            // 填充头像选择条（此时_agents已知）
            PopulateAgentBar();

            Debug.Log("[GameHUD] 初始化完成，子系统已连接");
        }

        // ==================== 显示更新 ====================

        /// <summary>
        /// 更新时间显示
        /// </summary>
        private void UpdateTimeDisplay()
        {
            if (textTime != null && _timeSystem != null)
            {
                textTime.text = $"第{_timeSystem.GameDay}天 {_timeSystem.GetTimeString()}";
            }
            if (textSpeed != null)
            {
                float speed = GameManager.Instance.TimeMultiplier;
                textSpeed.text = speed <= 0 ? "已暂停" : $"{speed}x";
            }
        }

        /// <summary>
        /// 更新资源显示
        /// 支持两种模式：独立文本数组（自构建）或合并文本（Inspector赋值）
        /// </summary>
        private void UpdateResourceDisplay()
        {
            if (_baseController == null) return;
            var storage = _baseController.Storage;

            // 优先使用独立资源文本（自构建模式）—— 资源名读自 ResourceConfigStore（resource_config.json）
            if (_resourceTexts[0] != null)
            {
                var types = new[] { ResourceType.Mineral, ResourceType.Crystal, ResourceType.Water, ResourceType.Organic, ResourceType.RuinData };
                for (int i = 0; i < 5; i++)
                {
                    if (_resourceTexts[i] != null)
                        _resourceTexts[i].text = $"{GalaxyAgent.Tech.ResourceConfigStore.GetDisplayName(types[i])}:{GetValue(storage, types[i]):F0}";
                }
            }
            else if (textResources != null)
            {
                // 兼容模式：使用单个合并文本
                textResources.text =
                    $"矿物:{GetValue(storage, ResourceType.Mineral):F0} " +
                    $"晶体:{GetValue(storage, ResourceType.Crystal):F0} " +
                    $"水:{GetValue(storage, ResourceType.Water):F0} " +
                    $"有机:{GetValue(storage, ResourceType.Organic):F0} " +
                    $"遗迹:{GetValue(storage, ResourceType.RuinData):F0}";
            }
        }

        private static float GetValue(Dictionary<ResourceType, float> storage, ResourceType type)
        {
            return storage != null && storage.ContainsKey(type) ? storage[type] : 0f;
        }

        // ==================== 按钮事件 ====================

        /// <summary>
        /// 手动保存按钮回调
        /// </summary>
        private void OnSaveClicked()
        {
            if (SaveGame())
                Debug.Log("[GameHUD] 游戏已保存");
        }

        /// <summary>
        /// 执行一次保存（手动按钮 / 自动保存共用）。
        /// 收集所有Agent状态、基地仓库、游戏时间、LLM配置写入数据库，返回是否保存成功。
        /// </summary>
        private bool SaveGame()
        {
            if (_saveManager == null || _agents == null) return false;
            if (string.IsNullOrEmpty(GameManager.Instance.CurrentSaveId))
            {
                Debug.LogError("[GameHUD] 保存失败：当前没有有效存档ID");
                return false;
            }

            var agentArray = new AgentData[_agents.Count];
            int i = 0;
            foreach (var kvp in _agents)
            {
                agentArray[i++] = kvp.Value.AgentData;
            }

            _saveManager.SaveGame(
                GameManager.Instance.CurrentSaveId,
                agentArray,
                _baseController != null ? _baseController.transform.position : Vector2.zero,
                _timeSystem != null ? _timeSystem.PlayTimeSeconds : 0f,
                _timeSystem != null ? _timeSystem.GameDay : 1,
                _baseController != null ? _baseController.Storage : new Dictionary<ResourceType, float>(),
                _timeSystem != null ? _timeSystem.GameTimeSeconds : 0f,
                LLMManager.Instance != null ? LLMManager.Instance.CurrentUrl : "",
                LLMManager.Instance != null ? LLMManager.Instance.CurrentModel : ""
            );

            // 记录上次保存时的游戏天数（供自动保存状态文本显示）
            if (_timeSystem != null)
                _lastSaveDay = _timeSystem.GameDay;
            return true;
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        private void OnMainMenuClicked()
        {
            GameManager.Instance.ReturnToMainMenu();
        }

        /// <summary>
        /// 设置时间倍率
        /// </summary>
        private void SetSpeed(float speed)
        {
            GameManager.Instance.SetTimeSpeed(speed);
        }

        /// <summary>
        /// 切换LLM对话窗口的显示/隐藏
        /// </summary>
        private void ToggleLLMWindow()
        {
            if (_llmWindow == null)
            {
                Debug.LogWarning("[GameHUD] LLM对话窗口未初始化");
                return;
            }
            if (_llmWindow.IsVisible)
                _llmWindow.Hide();
            else
                _llmWindow.Show();
            Debug.Log($"[GameHUD] LLM对话窗口已{(_llmWindow.IsVisible ? "打开" : "关闭")}");
        }

        /// <summary>
        /// 切换游戏配置面板的显示/隐藏
        /// </summary>
        private void ToggleConfigPanel()
        {
            if (_configPanel == null)
            {
                Debug.LogWarning("[GameHUD] 配置面板未初始化");
                return;
            }
            if (_configPanel.IsVisible) _configPanel.Hide();
            else _configPanel.Show();
        }

        /// <summary>
        /// 打开科技树面板（由基地信息面板的"科技树"按钮调用）
        /// </summary>
        public void ShowTechTree()
        {
            if (_techTreePanel == null)
            {
                Debug.LogWarning("[GameHUD] 科技树面板未初始化");
                return;
            }
            _techTreePanel.Show(_baseController);
        }

        /// <summary>隐藏科技树面板</summary>
        public void HideTechTree()
        {
            if (_techTreePanel != null) _techTreePanel.Hide();
        }

        // ==================== 自动保存 ====================

        /// <summary>
        /// 读取自动保存开关（每次读取最新配置，运行时改配置立即生效）
        /// </summary>
        private bool IsAutoSaveEnabled()
        {
            var cfg = GameConfigManager.Instance != null ? GameConfigManager.Instance.Config?.Save : null;
            return cfg != null && cfg.AutoSaveEnabled;
        }

        /// <summary>
        /// 读取自动保存间隔（现实秒），非法值兜底为默认60秒
        /// </summary>
        private float GetAutoSaveInterval()
        {
            var cfg = GameConfigManager.Instance != null ? GameConfigManager.Instance.Config?.Save : null;
            float interval = cfg != null ? cfg.AutoSaveInterval : Constants.AUTOSAVE_DEFAULT_INTERVAL;
            return interval > 1f ? interval : Constants.AUTOSAVE_DEFAULT_INTERVAL;
        }

        /// <summary>
        /// 自动保存驱动：按现实时间倒计时（不受游戏暂停影响），归零时自动存档，并持续刷新状态文本。
        /// 使用 Time.unscaledDeltaTime —— 即使游戏暂停，自动保存仍按现实秒工作，防止数据丢失。
        /// </summary>
        private void UpdateAutoSave()
        {
            bool enabled = IsAutoSaveEnabled();
            float interval = GetAutoSaveInterval();

            if (enabled)
            {
                _autoSaveTimer -= Time.unscaledDeltaTime;
                if (_autoSaveTimer <= 0f)
                {
                    if (SaveGame())
                        Debug.Log("[GameHUD] 自动保存完成");
                    _autoSaveTimer = interval;
                }
            }
            else
            {
                // 关闭时持续重置倒计时，避免下次开启瞬间触发一次
                _autoSaveTimer = interval;
            }

            // 更新底栏状态文本
            if (_autoSaveText != null)
            {
                if (enabled)
                {
                    int remain = Mathf.CeilToInt(Mathf.Max(0f, _autoSaveTimer));
                    string last = _lastSaveDay > 0 ? $"上次:第{_lastSaveDay}天" : "未保存";
                    _autoSaveText.text = $"自动保存 {remain}s {last}";
                }
                else
                {
                    _autoSaveText.text = "自动保存 已关闭";
                }
            }
        }

        // ==================== 点击事件 ====================

        /// <summary>
        /// Agent被点击：与点击头像走同一选中逻辑（高亮+信息面板+摄像机跟随）
        /// </summary>
        private void OnAgentClicked(AgentClickedEvent e)
        {
            if (_agents != null && _agents.ContainsKey(e.AgentId))
            {
                SelectAgent(e.AgentId);
            }
        }

        /// <summary>
        /// 基地被点击：与点击基地头像走同一选中逻辑（高亮+信息面板+摄像机移到基地）
        /// </summary>
        private void OnBaseClicked(BaseClickedEvent e)
        {
            if (_baseController != null)
            {
                SelectBase();
            }
        }

        /// <summary>
        /// 地图空白被点击：关闭所有信息面板并取消选中（摄像机恢复自由视角）
        /// </summary>
        private void OnMapClicked(MapClickedEvent e)
        {
            if (agentInfoPanel != null) agentInfoPanel.Hide();
            if (baseInfoPanel != null) baseInfoPanel.Hide();
            if (_techTreePanel != null) _techTreePanel.Hide();
            DeselectAgent();
        }

        // ==================== 清理 ====================

        private void OnDestroy()
        {
            EventBus.Unsubscribe<AgentClickedEvent>(OnAgentClicked);
            EventBus.Unsubscribe<BaseClickedEvent>(OnBaseClicked);
            EventBus.Unsubscribe<MapClickedEvent>(OnMapClicked);
        }
    }
}
