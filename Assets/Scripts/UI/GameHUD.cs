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
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
using GalaxyAgent.Map;
using GalaxyAgent.World;
using GalaxyAgent.World.Base;
using UnityEngine;
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
        [Tooltip("测试决策按钮（立即触发高层LLM决策）")]
        public Button buttonTestDecision;

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
            if (buttonTestDecision != null) buttonTestDecision.onClick.AddListener(OnTestDecisionClicked);

            // 订阅事件
            EventBus.Subscribe<AgentClickedEvent>(OnAgentClicked);
            EventBus.Subscribe<BaseClickedEvent>(OnBaseClicked);
            EventBus.Subscribe<MapClickedEvent>(OnMapClicked);
        }

        private void Update()
        {
            UpdateTimeDisplay();
            UpdateResourceDisplay();
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

            // 操作按钮（底栏右侧）
            buttonSave = RuntimeUIBuilder.CreateButton("BtnSave", bottomBar.transform,
                "保存", new Color(0.15f, 0.45f, 0.25f),
                0.72f, 0.1f, 0.84f, 0.9f);

            buttonMainMenu = RuntimeUIBuilder.CreateButton("BtnMenu", bottomBar.transform,
                "返回菜单", new Color(0.5f, 0.15f, 0.15f),
                0.85f, 0.1f, 0.99f, 0.9f);

            // LLM对话查看按钮（底栏中部空白区，紫色标识）
            buttonLLMChat = RuntimeUIBuilder.CreateButton("BtnLLMChat", bottomBar.transform,
                "LLM对话", new Color(0.35f, 0.25f, 0.55f),
                0.58f, 0.1f, 0.70f, 0.9f);

            // 测试决策按钮（立即触发所有Agent高层LLM决策，便于快速观察，无需等30秒）
            buttonTestDecision = RuntimeUIBuilder.CreateButton("BtnTestDecision", bottomBar.transform,
                "测试决策", new Color(0.7f, 0.5f, 0.15f),
                0.46f, 0.1f, 0.57f, 0.9f);

            // ==================== LLM对话查看窗口 ====================
            // 运行时自构建，初始隐藏，点击"LLM对话"按钮切换显示
            // 注意：中间层容器必须用RectTransform撑满父级，否则普通Transform在Canvas下无尺寸，
            // 其下窗口面板的锚点会坍缩为0导致整个窗口不可见
            var llmWindowObj = MakeFullScreenContainer("LLMConversationWindow", transform);
            _llmWindow = llmWindowObj.AddComponent<LLMConversationWindow>();
            _llmWindow.BuildUI(llmWindowObj.transform);

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

            // 把所有AgentId注入LLM对话窗口，供左侧选择查看
            if (_llmWindow != null && _agents != null)
            {
                _llmWindow.SetAgentIds(new List<string>(_agents.Keys));
            }

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

            // 优先使用独立资源文本（自构建模式）
            if (_resourceTexts[0] != null)
            {
                var types = new[] { ResourceType.Mineral, ResourceType.Crystal, ResourceType.Water, ResourceType.Organic, ResourceType.RuinData };
                var names = new[] { "矿物", "晶体", "水", "有机", "遗迹" };
                for (int i = 0; i < 5; i++)
                {
                    if (_resourceTexts[i] != null)
                        _resourceTexts[i].text = $"{names[i]}:{GetValue(storage, types[i]):F0}";
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
        /// 保存游戏：收集所有Agent状态和基地数据写入数据库
        /// </summary>
        private void OnSaveClicked()
        {
            if (_saveManager == null || _agents == null) return;
            if (string.IsNullOrEmpty(GameManager.Instance.CurrentSaveId))
            {
                Debug.LogError("[GameHUD] 保存失败：当前没有有效存档ID");
                return;
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
                _timeSystem != null ? _timeSystem.GameTimeSeconds : 0f
            );

            Debug.Log("[GameHUD] 游戏已保存");
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
        /// 测试按钮回调：立即触发所有Agent的高层LLM决策（跳过30秒定时），并自动打开对话窗口查看结果。
        /// </summary>
        private void OnTestDecisionClicked()
        {
            if (_agents == null || _agents.Count == 0)
            {
                Debug.LogWarning("[GameHUD] 测试决策失败：当前无Agent");
                return;
            }

            string firstAgent = "global";
            int count = 0;
            foreach (var kvp in _agents)
            {
                if (count == 0) firstAgent = kvp.Key;
                kvp.Value.TriggerHighLevelDecisionForTest();
                count++;
            }
            Debug.Log($"[GameHUD] 已触发 {count} 个Agent的高层LLM决策测试");

            // 自动打开对话窗口并切到第一个Agent，方便立即查看决策结果
            if (_llmWindow != null)
                _llmWindow.Show(firstAgent);
        }

        // ==================== 点击事件 ====================

        /// <summary>
        /// Agent被点击：显示Agent信息面板，隐藏基地面板
        /// </summary>
        private void OnAgentClicked(AgentClickedEvent e)
        {
            if (agentInfoPanel == null) return;

            // 先隐藏基地面板
            if (baseInfoPanel != null) baseInfoPanel.Hide();

            // 显示Agent面板
            if (_agents != null && _agents.ContainsKey(e.AgentId))
            {
                agentInfoPanel.Show(_agents[e.AgentId].AgentData);
            }
        }

        /// <summary>
        /// 基地被点击：显示基地信息面板，隐藏Agent面板
        /// </summary>
        private void OnBaseClicked(BaseClickedEvent e)
        {
            // 先隐藏Agent面板
            if (agentInfoPanel != null) agentInfoPanel.Hide();

            // 显示基地面板
            if (baseInfoPanel != null && _baseController != null)
            {
                baseInfoPanel.Show(_baseController);
            }
        }

        /// <summary>
        /// 地图空白被点击：关闭所有信息面板
        /// </summary>
        private void OnMapClicked(MapClickedEvent e)
        {
            if (agentInfoPanel != null) agentInfoPanel.Hide();
            if (baseInfoPanel != null) baseInfoPanel.Hide();
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
