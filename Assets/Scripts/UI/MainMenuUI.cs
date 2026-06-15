/// <summary>
/// 主菜单界面控制器
/// 处理"开始游戏"、"加载游戏"按钮逻辑
/// 在Start()中通过RuntimeUIBuilder动态构建完整UI
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
using GalaxyAgent.LLM;
using GalaxyAgent.Modding;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        // 运行时创建的组件引用
        private Button _buttonNewGame;
        private Button _buttonLoadGame;
        private Button _buttonQuit;
        private GameObject _panelSaveList;
        private Transform _saveListContent;
        private Button _buttonBackFromSaves;

        // 删除存档确认对话框
        private GameObject _confirmDialog;
        private Text _confirmMessage;
        private string _pendingDeleteSaveId;

        // 数据库和存档管理器
        private DatabaseManager _dbManager;
        private SaveLoadManager _saveLoadManager;

        // LLM 相关：连接状态显示、设置按钮、设置面板
        private Text _llmStatusText;
        private Button _btnSettings;
        private LLMSettingsPanel _settingsPanel;

        // Mod 换图：Mod 按钮 + Mod 面板
        private Button _btnMod;
        private ModPanel _modPanel;
        private float _statusTimer;

        private void Start()
        {
            // 始终构建UI（按钮引用为空说明尚未构建）
            if (_buttonNewGame == null)
            {
                BuildUI();
            }

            // 初始化数据库
            _dbManager = new DatabaseManager();
            _dbManager.Initialize();
            _saveLoadManager = new SaveLoadManager(_dbManager);

            // 绑定按钮事件
            if (_buttonNewGame != null) _buttonNewGame.onClick.AddListener(OnNewGameClicked);
            if (_buttonLoadGame != null) _buttonLoadGame.onClick.AddListener(OnLoadGameClicked);
            if (_buttonQuit != null) _buttonQuit.onClick.AddListener(OnQuitClicked);
            if (_buttonBackFromSaves != null) _buttonBackFromSaves.onClick.AddListener(OnBackFromSavesClicked);
            if (_btnSettings != null) _btnSettings.onClick.AddListener(OnSettingsClicked);
            if (_btnMod != null) _btnMod.onClick.AddListener(OnModClicked);

            // 触发 LLMManager 初始化（Singleton 自动创建 + 跨场景 DontDestroyOnLoad），启动连接检测
            _ = LLMManager.Instance;

            // Mod 换图：首次启动自动创建 Mods 目录 + 导出默认模板（玩家进游戏目录即可见现成模板）
            ModManager.EnsureModSetup();

            // 检查存档状态
            UpdateLoadButtonState();

            // 隐藏存档列表
            if (_panelSaveList != null) _panelSaveList.SetActive(false);

            Debug.Log("[MainMenuUI] 初始化完成");
        }

        /// <summary>
        /// 动态构建主菜单UI
        /// </summary>
        private void BuildUI()
        {
            RuntimeUIBuilder.EnsureEventSystem();

            // 背景（贴场景背景图，覆盖默认面板底纹）
            var bg = RuntimeUIBuilder.CreatePanel("Background", transform, new Color(0.05f, 0.05f, 0.15f),
                0f, 0f, 1f, 1f);
            RuntimeUIBuilder.ApplySceneBackground(bg, "mainmenu");

            // 标题 42Agent - 星球生存探索模拟器
            RuntimeUIBuilder.CreateText("Title", transform,
                " ", 40, new Color(0.4f, 0.8f, 1f),
                TextAnchor.MiddleCenter, 0.1f, 0.75f, 0.9f, 0.9f);

            // 按钮
            _buttonNewGame = RuntimeUIBuilder.CreateButton("BtnNewGame", transform,
                "开始游戏", new Color(0.15f, 0.45f, 0.25f),
                0.35f, 0.47f, 0.65f, 0.535f);

            _buttonLoadGame = RuntimeUIBuilder.CreateButton("BtnLoadGame", transform,
                "加载游戏", new Color(0.25f, 0.25f, 0.45f),
                0.35f, 0.37f, 0.65f, 0.435f);

            _buttonQuit = RuntimeUIBuilder.CreateButton("BtnQuit", transform,
                "退出游戏", new Color(0.45f, 0.15f, 0.15f),
                0.35f, 0.27f, 0.65f, 0.335f);

            // 存档面板
            _panelSaveList = RuntimeUIBuilder.CreatePanel("PanelSaveList", transform,
                new Color(0.08f, 0.08f, 0.18f, 0.95f),
                0.2f, 0.12f, 0.8f, 0.88f);
            _panelSaveList.SetActive(false);

            // 面板标题
            RuntimeUIBuilder.CreateText("SaveListTitle", _panelSaveList.transform,
                "选择存档", 26, new Color(0.7f, 0.85f, 1f),
                TextAnchor.MiddleCenter, 0f, 0.93f, 1f, 0.99f);

            // 可滚动的存档列表（返回的Content已带VerticalLayoutGroup + ContentSizeFitter，
            // 存档过多时自动出现竖向滚动条）
            _saveListContent = RuntimeUIBuilder.CreateScrollView("SaveScrollView",
                _panelSaveList.transform, new Color(0.06f, 0.06f, 0.14f, 0.9f),
                0.05f, 0.12f, 0.95f, 0.92f);

            // 返回按钮
            _buttonBackFromSaves = RuntimeUIBuilder.CreateButton("BtnBack", _panelSaveList.transform,
                "返回", new Color(0.3f, 0.3f, 0.3f),
                0.35f, 0.02f, 0.65f, 0.1f);

            // LLM 连接状态指示（右上角，颜色区分已连接/未连接）
            _llmStatusText = RuntimeUIBuilder.CreateText("LLMStatus", transform,
                "LLM: 检测中…", 16, new Color(0.9f, 0.85f, 0.4f),
                TextAnchor.MiddleRight, 0.55f, 0.90f, 0.80f, 0.96f);

            // 设置按钮（右上角，打开 LLM 设置面板，可切换服务地址与模型）
            _btnSettings = RuntimeUIBuilder.CreateButton("BtnSettings", transform,
                "设置", new Color(0.2f, 0.3f, 0.45f),
                0.82f, 0.89f, 0.97f, 0.96f,
                SpriteRegistry.GetButtonIcon("config"));

            // Mod 按钮（设置按钮正下方，打开 Mod 换图面板，玩家用游戏目录下的文件替换图片）
            _btnMod = RuntimeUIBuilder.CreateButton("BtnMod", transform,
                "Mod", new Color(0.35f, 0.25f, 0.15f),
                0.82f, 0.80f, 0.97f, 0.87f);

            // 删除确认对话框（初始隐藏）
            BuildConfirmDialog();
        }

        // ==================== 按钮事件 ====================

        private void OnNewGameClicked()
        {
            Debug.Log("[MainMenu] 点击开始游戏");
            _dbManager?.Close();
            SceneLoader.LoadScene(Constants.SCENE_MAP_GENERATION);
        }

        private void OnLoadGameClicked()
        {
            Debug.Log("[MainMenu] 点击加载游戏");
            ShowSaveList();
        }

        private void OnQuitClicked()
        {
            Debug.Log("[MainMenu] 退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            // 轮询 LLM 连接状态（IsAvailable 为异步更新，0.5s 刷新一次显示）
            _statusTimer += Time.unscaledDeltaTime;
            if (_statusTimer >= 0.5f)
            {
                _statusTimer = 0f;
                UpdateLlmStatus();
            }
        }

        /// <summary>刷新右上角 LLM 连接状态文本与颜色</summary>
        private void UpdateLlmStatus()
        {
            if (_llmStatusText == null) return;
            var mgr = LLMManager.Instance;
            if (mgr == null)
            {
                _llmStatusText.text = "LLM: 未初始化";
                _llmStatusText.color = new Color(0.6f, 0.6f, 0.6f);
                return;
            }
            if (mgr.IsAvailable)
            {
                _llmStatusText.text = $"LLM: 已连接 ({mgr.CurrentModel})";
                _llmStatusText.color = new Color(0.4f, 0.85f, 0.5f);
            }
            else
            {
                _llmStatusText.text = "LLM: 未连接";
                _llmStatusText.color = new Color(0.9f, 0.6f, 0.4f);
            }
        }

        /// <summary>点击设置：打开 LLM 设置面板（首次按需构建并挂在同一 Canvas 下）</summary>
        private void OnSettingsClicked()
        {
            if (_settingsPanel == null)
            {
                _settingsPanel = gameObject.AddComponent<LLMSettingsPanel>();
                _settingsPanel.BuildUI(transform);
            }
            _settingsPanel.Show();
        }

        /// <summary>点击 Mod：打开 Mod 换图面板（首次按需构建），玩家用游戏目录下的文件替换图片</summary>
        private void OnModClicked()
        {
            if (_modPanel == null)
            {
                _modPanel = gameObject.AddComponent<ModPanel>();
                _modPanel.BuildUI(transform);
            }
            _modPanel.Show();
        }

        private void UpdateLoadButtonState()
        {
            if (_buttonLoadGame != null)
            {
                bool hasSaves = _saveLoadManager != null && _saveLoadManager.HasAnySave();
                _buttonLoadGame.interactable = hasSaves;
            }
        }

        private void ShowSaveList()
        {
            if (_panelSaveList == null || _saveListContent == null) return;

            // 清空旧列表
            foreach (Transform child in _saveListContent)
                Destroy(child.gameObject);

            List<GameSaveData> saves = _saveLoadManager.GetAllSaves();
            if (saves.Count == 0) return;

            foreach (var save in saves)
            {
                CreateSaveRow(save);
            }

            _panelSaveList.SetActive(true);

            // 运行时构建的ScrollRect+ContentSizeFitter需要立即重建布局，
            // 否则首帧内容高度未计算，列表可能不显示
            LayoutRebuilder.ForceRebuildLayoutImmediate(_saveListContent as RectTransform);
        }

        /// <summary>
        /// 创建单个存档行：左侧"加载"按钮 + 右侧"删除"按钮
        /// </summary>
        private void CreateSaveRow(GameSaveData save)
        {
            // 行容器：水平布局
            var row = new GameObject($"Row_{save.SaveId}");
            row.transform.SetParent(_saveListContent, false);
            row.AddComponent<RectTransform>();
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            // 行高：必须同时设preferredHeight，否则Content的ContentSizeFitter(PreferredSize)
            // 会把内容总高算成0，导致整个存档列表坍缩不可见（minHeight只在不滚动时生效）
            hlg.childForceExpandHeight = true;
            var rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 44;
            rowLayout.preferredHeight = 44;

            string label = $"{save.PlanetName} | 第{save.GameDay}天 | {save.CreatedAt}";

            // 加载按钮（约占行宽80%）
            var loadBtn = RuntimeUIBuilder.CreateButton($"Load_{save.SaveId}",
                row.transform, label, new Color(0.2f, 0.2f, 0.3f), 0f, 0f, 1f, 1f);
            var loadLayout = loadBtn.gameObject.AddComponent<LayoutElement>();
            loadLayout.flexibleWidth = 4f; // 与删除按钮1f按4:1分配 → 约80%:20%
            string saveId = save.SaveId;
            loadBtn.onClick.AddListener(() => OnSaveSelected(saveId));

            // 删除按钮（约占行宽20%）
            var delBtn = RuntimeUIBuilder.CreateButton($"Del_{save.SaveId}",
                row.transform, "删除", new Color(0.55f, 0.15f, 0.15f), 0f, 0f, 1f, 1f);
            var delLayout = delBtn.gameObject.AddComponent<LayoutElement>();
            delLayout.flexibleWidth = 1f;
            string display = label;
            delBtn.onClick.AddListener(() => RequestDeleteSave(saveId, display));
        }

        // ==================== 删除存档确认 ====================

        /// <summary>
        /// 构建删除确认对话框（全屏遮罩 + 居中确认框），初始隐藏
        /// </summary>
        private void BuildConfirmDialog()
        {
            // 全屏半透明遮罩：位于存档面板之上，阻止背景点击
            _confirmDialog = RuntimeUIBuilder.CreatePanel("ConfirmOverlay",
                _panelSaveList.transform, new Color(0f, 0f, 0f, 0.6f),
                0f, 0f, 1f, 1f);

            // 居中确认框
            var box = RuntimeUIBuilder.CreatePanel("ConfirmBox", _confirmDialog.transform,
                new Color(0.08f, 0.08f, 0.14f, 0.98f),
                0.15f, 0.32f, 0.85f, 0.68f);

            // 提示文字
            _confirmMessage = RuntimeUIBuilder.CreateText("Msg", box.transform,
                "确定要删除这个存档吗？\n此操作不可恢复。",
                22, new Color(0.95f, 0.8f, 0.8f),
                TextAnchor.MiddleCenter, 0.05f, 0.5f, 0.95f, 0.95f);

            // 确认删除按钮
            var btnConfirm = RuntimeUIBuilder.CreateButton("BtnConfirm", box.transform,
                "确认删除", new Color(0.6f, 0.15f, 0.15f),
                0.08f, 0.1f, 0.46f, 0.38f);
            btnConfirm.onClick.AddListener(ConfirmDelete);

            // 取消按钮
            var btnCancel = RuntimeUIBuilder.CreateButton("BtnCancel", box.transform,
                "取消", new Color(0.3f, 0.3f, 0.35f),
                0.54f, 0.1f, 0.92f, 0.38f);
            btnCancel.onClick.AddListener(CancelDelete);

            _confirmDialog.SetActive(false);
        }

        /// <summary>点击删除按钮：记录待删存档ID，弹出确认对话框</summary>
        private void RequestDeleteSave(string saveId, string displayLabel)
        {
            _pendingDeleteSaveId = saveId;
            if (_confirmMessage != null)
                _confirmMessage.text = $"确定要删除这个存档吗？\n{displayLabel}\n此操作不可恢复。";
            if (_confirmDialog != null)
                _confirmDialog.SetActive(true);
        }

        /// <summary>确认删除：调用SaveLoadManager删除并刷新列表</summary>
        private void ConfirmDelete()
        {
            if (string.IsNullOrEmpty(_pendingDeleteSaveId)) return;

            string id = _pendingDeleteSaveId;
            _pendingDeleteSaveId = null;
            if (_confirmDialog != null) _confirmDialog.SetActive(false);

            if (_saveLoadManager != null)
            {
                _saveLoadManager.DeleteSave(id);
                Debug.Log($"[MainMenu] 存档已删除: {id}");
            }

            // 刷新"加载游戏"按钮可用状态
            UpdateLoadButtonState();

            // 删除后无存档则关闭面板；否则刷新剩余列表
            if (_saveLoadManager == null || !_saveLoadManager.HasAnySave())
            {
                if (_panelSaveList != null) _panelSaveList.SetActive(false);
            }
            else
            {
                ShowSaveList();
            }
        }

        /// <summary>取消删除：隐藏对话框</summary>
        private void CancelDelete()
        {
            _pendingDeleteSaveId = null;
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
        }

        private void OnSaveSelected(string saveId)
        {
            Debug.Log($"[MainMenu] 选择存档: {saveId}");
            _dbManager?.Close();
            GameManager.Instance.LoadGame(saveId);
        }

        private void OnBackFromSavesClicked()
        {
            if (_panelSaveList != null) _panelSaveList.SetActive(false);
        }

        private void OnDestroy()
        {
            _dbManager?.Close();
        }
    }
}
