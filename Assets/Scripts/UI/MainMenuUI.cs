/// <summary>
/// 主菜单界面控制器
/// 处理"开始游戏"、"加载游戏"按钮逻辑
/// 在Start()中通过RuntimeUIBuilder动态构建完整UI
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
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

        // 数据库和存档管理器
        private DatabaseManager _dbManager;
        private SaveLoadManager _saveLoadManager;

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

            // 背景
            RuntimeUIBuilder.CreatePanel("Background", transform, new Color(0.05f, 0.05f, 0.15f),
                0f, 0f, 1f, 1f);

            // 标题
            RuntimeUIBuilder.CreateText("Title", transform,
                "42Agent - 星球生存探索模拟器", 40, new Color(0.4f, 0.8f, 1f),
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

            // 存档列表容器
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(_panelSaveList.transform, false);
            contentObj.AddComponent<RectTransform>();
            var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var cr = contentObj.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.05f, 0.12f);
            cr.anchorMax = new Vector2(0.95f, 0.92f);
            cr.sizeDelta = Vector2.zero;
            _saveListContent = contentObj.transform;

            // 返回按钮
            _buttonBackFromSaves = RuntimeUIBuilder.CreateButton("BtnBack", _panelSaveList.transform,
                "返回", new Color(0.3f, 0.3f, 0.3f),
                0.35f, 0.02f, 0.65f, 0.1f);
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
                var btn = RuntimeUIBuilder.CreateButton($"Save_{save.SaveId}",
                    _saveListContent,
                    $"{save.PlanetName} | 第{save.GameDay}天 | {save.CreatedAt}",
                    new Color(0.2f, 0.2f, 0.3f), 0f, 0f, 1f, 1f);
                var layout = btn.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = 40;
                string saveId = save.SaveId;
                btn.onClick.AddListener(() => OnSaveSelected(saveId));
            }

            _panelSaveList.SetActive(true);
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
