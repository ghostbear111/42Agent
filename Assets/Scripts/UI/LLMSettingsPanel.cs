/// <summary>
/// LLM 设置面板（主菜单用）
/// 在 MainMenu 点击「设置」按钮打开，配置 Ollama 服务地址与模型，并测试连接。
///
/// 内容：URL 输入框 + 模型下拉（从 Ollama 拉取已安装模型）+ 连接测试按钮 + 实时连接状态 + 保存/关闭。
/// 保存时把 url/model 写入 GameConfigManager（持久化 game_config.json）并调用 LLMManager.Configure 重连，
/// 因 LLMManager 是跨场景单例（DontDestroyOnLoad），设置在进入游戏后依然生效。
///
/// 结构：
/// ┌──────── 全屏半透明遮罩 ────────┐
/// │ ┌────── 居中设置窗口 ──────┐   │
/// │ │ 标题：LLM 设置            │   │
/// │ │ 服务地址 [输入框]         │   │
/// │ │ 模型     [下拉]           │   │
/// │ │ ● 连接状态                │   │
/// │ │ [刷新模型][测试连接]      │   │
/// │ │ [保存][关闭]              │   │
/// │ └──────────────────────────┘   │
/// └────────────────────────────────┘
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Config;
using GalaxyAgent.Core;
using GalaxyAgent.LLM;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class LLMSettingsPanel : MonoBehaviour
    {
        // ==================== UI 引用 ====================
        private GameObject _root;              // 全屏遮罩根
        private InputField _urlInput;
        private Dropdown _modelDropdown;
        private Button _refreshBtn;
        private Button _testBtn;
        private Button _saveBtn;
        private Button _closeBtn;
        private Text _statusText;

        // 状态轮询计时器
        private float _timer;

        /// <summary>面板是否可见</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        // ==================== 构建 ====================

        /// <summary>运行时构建设置面板（幂等，由 MainMenuUI 调用一次）</summary>
        public void BuildUI(Transform parent)
        {
            RuntimeUIBuilder.EnsureEventSystem();

            // 全屏半透明遮罩
            _root = MakeFull("SettingsOverlay", parent);
            _root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // 居中窗口
            var win = RuntimeUIBuilder.CreatePanel("SettingsWindow", _root.transform,
                new Color(0.07f, 0.07f, 0.13f, 0.98f), 0.25f, 0.20f, 0.75f, 0.82f);

            // 标题
            RuntimeUIBuilder.CreateText("Title", win.transform, "LLM 设置", 22,
                new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleCenter, 0f, 0.88f, 1f, 0.97f);

            // 服务地址输入行
            _urlInput = RuntimeUIBuilder.CreateInputField("Url", win.transform,
                "服务地址", "http://localhost:11434", 0.68f);

            // 模型下拉行（选项在 Show 时异步刷新）
            _modelDropdown = RuntimeUIBuilder.CreateDropdown("Model", win.transform,
                "模型", new[] { "(点击下方刷新)" }, 0.52f);

            // 连接状态行
            _statusText = RuntimeUIBuilder.CreateText("Status", win.transform, "检测中…", 14,
                new Color(0.9f, 0.85f, 0.4f), TextAnchor.MiddleLeft, 0.05f, 0.36f, 0.95f, 0.46f);

            // 按钮行1：刷新模型 / 测试连接
            _refreshBtn = RuntimeUIBuilder.CreateButton("BtnRefresh", win.transform, "刷新模型",
                new Color(0.2f, 0.4f, 0.5f), 0.06f, 0.21f, 0.46f, 0.33f);
            _testBtn = RuntimeUIBuilder.CreateButton("BtnTest", win.transform, "测试连接",
                new Color(0.25f, 0.3f, 0.45f), 0.50f, 0.21f, 0.94f, 0.33f);

            // 按钮行2：保存 / 关闭
            _saveBtn = RuntimeUIBuilder.CreateButton("BtnSave", win.transform, "保存",
                new Color(0.15f, 0.45f, 0.25f), 0.06f, 0.06f, 0.46f, 0.18f);
            _closeBtn = RuntimeUIBuilder.CreateButton("BtnClose", win.transform, "关闭",
                new Color(0.35f, 0.2f, 0.2f), 0.50f, 0.06f, 0.94f, 0.18f);

            // 绑定事件
            _refreshBtn.onClick.AddListener(OnRefreshModels);
            _testBtn.onClick.AddListener(OnTestConnection);
            _saveBtn.onClick.AddListener(OnSave);
            _closeBtn.onClick.AddListener(Hide);

            _root.SetActive(false);
            Debug.Log("[LLMSettingsPanel] UI 构建完成");
        }

        // ==================== 显示/隐藏 ====================

        /// <summary>显示面板：载入当前 url/model 并刷新模型列表</summary>
        public void Show()
        {
            var mgr = LLMManager.Instance;
            if (_urlInput != null)
                _urlInput.text = mgr != null ? mgr.CurrentUrl : Constants.OLLAMA_DEFAULT_URL;

            OnRefreshModels();

            if (_root != null) _root.SetActive(true);
            _timer = 0.5f; // 触发立即状态更新
        }

        /// <summary>隐藏面板</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        // ==================== 状态轮询 ====================

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            _timer += Time.unscaledDeltaTime;
            if (_timer >= 0.5f)
            {
                _timer = 0f;
                UpdateStatus();
            }
        }

        /// <summary>根据 LLMManager 可用性刷新状态文本（颜色区分已连接/未连接）</summary>
        private void UpdateStatus()
        {
            var mgr = LLMManager.Instance;
            if (_statusText == null || mgr == null) return;

            if (mgr.IsAvailable)
            {
                _statusText.text = $"● 已连接 [{mgr.ProviderName}]  当前模型: {mgr.CurrentModel}";
                _statusText.color = new Color(0.4f, 0.85f, 0.5f);
            }
            else
            {
                _statusText.text = "○ 未连接（请确认本地 Ollama 服务已启动，或检查地址/模型）";
                _statusText.color = new Color(0.9f, 0.6f, 0.4f);
            }
        }

        // ==================== 按钮事件 ====================

        /// <summary>从 Ollama 拉取已安装模型并填充下拉框；失败回退预设列表</summary>
        private void OnRefreshModels()
        {
            var mgr = LLMManager.Instance;
            if (mgr == null || _modelDropdown == null) return;

            // 临时提示
            _modelDropdown.ClearOptions();
            _modelDropdown.AddOptions(new List<string> { "加载中…" });

            mgr.GetInstalledModelsAsync(models =>
            {
                if (_modelDropdown == null) return;
                _modelDropdown.ClearOptions();

                var opts = new List<string>();
                if (models != null && models.Length > 0)
                    opts.AddRange(models);
                else
                    opts.AddRange(Constants.OLLAMA_MODEL_OPTIONS); // 拉取失败回退预设

                _modelDropdown.AddOptions(opts);
                SelectCurrentModel();
            });
        }

        /// <summary>测试连接：用当前 URL + 选中模型重新配置（内部异步重检，Update 会反映结果）</summary>
        private void OnTestConnection()
        {
            var mgr = LLMManager.Instance;
            if (mgr == null) return;

            string url = ReadUrl();
            string model = ReadSelectedModel(mgr.CurrentModel);
            if (_statusText != null)
            {
                _statusText.text = "正在测试连接…";
                _statusText.color = new Color(0.9f, 0.85f, 0.4f);
            }
            mgr.Configure(url, model);
            _timer = 0.5f; // 触发尽快刷新状态
        }

        /// <summary>保存：写入 GameConfig 并持久化，调 Configure 重连，关闭面板</summary>
        private void OnSave()
        {
            var cfgMgr = GameConfigManager.Instance;
            if (cfgMgr != null)
            {
                string url = ReadUrl();
                string model = cfgMgr.Config.Llm.Model;
                if (_modelDropdown != null && _modelDropdown.options.Count > 0)
                    model = _modelDropdown.options[_modelDropdown.value].text;

                cfgMgr.Config.Llm.Url = url;
                cfgMgr.Config.Llm.Model = model;
                cfgMgr.Save();
                Debug.Log("[LLMSettingsPanel] 配置已保存");
            }

            // 立即应用到 LLMManager（重连）
            var llm = LLMManager.Instance;
            if (llm != null)
                llm.Configure(ReadUrl(), cfgMgr != null ? cfgMgr.Config.Llm.Model : llm.CurrentModel);

            Hide();
        }

        // ==================== 辅助 ====================

        /// <summary>把下拉框选中项设为当前模型</summary>
        private void SelectCurrentModel()
        {
            var mgr = LLMManager.Instance;
            if (mgr == null || _modelDropdown == null) return;
            string cur = mgr.CurrentModel;
            for (int i = 0; i < _modelDropdown.options.Count; i++)
            {
                if (_modelDropdown.options[i].text == cur)
                {
                    _modelDropdown.SetValueWithoutNotify(i);
                    _modelDropdown.RefreshShownValue();
                    return;
                }
            }
            _modelDropdown.SetValueWithoutNotify(0);
            _modelDropdown.RefreshShownValue();
        }

        /// <summary>读取 URL 输入框值（空则回退当前/默认）</summary>
        private string ReadUrl()
        {
            if (_urlInput != null && !string.IsNullOrWhiteSpace(_urlInput.text))
                return _urlInput.text.Trim();
            var mgr = LLMManager.Instance;
            return mgr != null ? mgr.CurrentUrl : Constants.OLLAMA_DEFAULT_URL;
        }

        /// <summary>读取下拉框选中的模型名；下拉无内容时回退 fallback</summary>
        private string ReadSelectedModel(string fallback)
        {
            if (_modelDropdown != null && _modelDropdown.options.Count > 0)
                return _modelDropdown.options[_modelDropdown.value].text;
            return fallback;
        }

        /// <summary>创建撑满父级的 RectTransform 容器</summary>
        private static GameObject MakeFull(string name, Transform parent)
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
    }
}
