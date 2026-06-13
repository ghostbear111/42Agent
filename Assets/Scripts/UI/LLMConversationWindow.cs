/// <summary>
/// LLM对话查看窗口
/// 点击GameHUD"LLM对话"按钮打开，展示Agent与LLM的全部交互历史。
///
/// 功能：
/// - 左侧切换查看不同Agent（含"global"全局手动对话）的记录
/// - 右侧滚动展示该Agent的完整对话（用户输入 / LLM回复 / 错误），按时间正序
/// - 顶部显示LLM连接状态、系统提示词
/// - 底部支持手动输入消息与LLM对话（调试/观察LLM反应）
///
/// 性能：仅在窗口可见时每0.5秒自动刷新一次（用unscaledDeltaTime，暂停时也能看）。
/// 所有数据从LLMManager读取，本窗口不持有对话数据。
/// </summary>
using System.Collections.Generic;
using System.Text;
using GalaxyAgent.Core;
using GalaxyAgent.LLM;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class LLMConversationWindow : MonoBehaviour
    {
        // ==================== UI组件引用 ====================

        private GameObject _root;                  // 窗口根
        private Text _statusText;                  // 连接状态文本
        private Text _systemPromptText;            // 系统提示词预览
        private Text _conversationText;            // 对话历史正文
        private InputField _inputField;            // 手动输入框
        private Button _buttonClose;
        private Button _buttonSend;
        private Button _buttonRefresh;
        private Transform _agentButtonContainer;   // Agent选择按钮容器

        // ==================== 状态 ====================

        // 当前查看的AgentId
        private string _currentAgentId = "global";
        // 可查看的AgentId列表（由GameHUD注入或用默认值）
        private readonly List<string> _agentIds = new List<string>();
        // 已创建的Agent选择按钮（key=agentId）
        private readonly Dictionary<string, Button> _agentButtons = new Dictionary<string, Button>();
        // 模型选择按钮容器、URL输入框、模型按钮映射
        private Transform _modelButtonContainer;
        private InputField _urlInput;
        private readonly Dictionary<string, Button> _modelButtons = new Dictionary<string, Button>();
        // 自动刷新计时器
        private float _refreshTimer;

        // 默认Agent列表（GameHUD未注入时使用，与CreateAgents中的id一致）
        private static readonly string[] DEFAULT_AGENT_IDS = { "global", "scout_01", "worker_01", "guard_01" };

        // ==================== 运行时构建 ====================

        /// <summary>运行时构建窗口UI（由GameHUD.BuildUI调用）</summary>
        public void BuildUI(Transform parent)
        {
            RuntimeUIBuilder.EnsureEventSystem();

            // ---------- 窗口根：覆盖屏幕中央大区域 ----------
            _root = RuntimeUIBuilder.CreatePanel("LLMConversationWindow", parent,
                new Color(0.05f, 0.05f, 0.1f, 0.97f),
                0.08f, 0.06f, 0.92f, 0.94f);

            // ---------- 标题栏 ----------
            var titleBar = RuntimeUIBuilder.CreatePanel("TitleBar", _root.transform,
                new Color(0.1f, 0.15f, 0.25f, 1f),
                0f, 0.93f, 1f, 1f);

            RuntimeUIBuilder.CreateText("Title", titleBar.transform,
                "LLM 对话记录", 22, new Color(0.5f, 0.85f, 1f),
                TextAnchor.MiddleLeft, 0.02f, 0.1f, 0.32f, 0.9f);

            _statusText = RuntimeUIBuilder.CreateText("Status", titleBar.transform,
                "状态: 检测中...", 14, new Color(0.9f, 0.9f, 0.5f),
                TextAnchor.MiddleLeft, 0.33f, 0.1f, 0.76f, 0.9f);

            _buttonRefresh = RuntimeUIBuilder.CreateButton("BtnRefresh", titleBar.transform,
                "刷新", new Color(0.2f, 0.4f, 0.5f),
                0.78f, 0.15f, 0.88f, 0.85f);

            _buttonClose = RuntimeUIBuilder.CreateButton("BtnClose", titleBar.transform,
                "关闭", new Color(0.6f, 0.2f, 0.2f),
                0.89f, 0.15f, 0.99f, 0.85f);

            // ---------- 左侧栏：Agent选择 + 系统提示 ----------
            var leftPanel = RuntimeUIBuilder.CreatePanel("LeftPanel", _root.transform,
                new Color(0.08f, 0.08f, 0.14f, 1f),
                0f, 0f, 0.24f, 0.93f);

            RuntimeUIBuilder.CreateText("LeftTitle", leftPanel.transform,
                "选择Agent", 15, new Color(0.7f, 0.85f, 1f),
                TextAnchor.MiddleCenter, 0.05f, 0.91f, 0.95f, 0.98f);

            // Agent按钮容器（垂直布局自动排列）
            var agentListObj = new GameObject("AgentButtons");
            agentListObj.transform.SetParent(leftPanel.transform, false);
            var agentListRect = agentListObj.AddComponent<RectTransform>();
            agentListRect.anchorMin = new Vector2(0.05f, 0.70f);
            agentListRect.anchorMax = new Vector2(0.95f, 0.89f);
            agentListRect.sizeDelta = Vector2.zero;
            // 必须在AddComponent<RectTransform>之后再缓存引用：前者会替换掉new GameObject自带的Transform，
            // 若提前缓存，旧引用会失效（变成destroyed），导致后续RebuildAgentButtons误判为null而不创建按钮。
            _agentButtonContainer = agentListObj.transform;
            var agentVlg = agentListObj.AddComponent<VerticalLayoutGroup>();
            agentVlg.spacing = 4;
            agentVlg.childForceExpandWidth = true;
            agentVlg.childForceExpandHeight = false;
            agentVlg.childControlWidth = true;
            agentVlg.childControlHeight = true;

            // ---------- LLM模型配置区 ----------
            RuntimeUIBuilder.CreateText("ModelTitle", leftPanel.transform,
                "LLM模型（点击切换）", 13, new Color(0.95f, 0.8f, 0.35f),
                TextAnchor.MiddleCenter, 0.05f, 0.64f, 0.95f, 0.69f);

            // 模型按钮容器（垂直布局）
            var modelListObj = new GameObject("ModelButtons");
            modelListObj.transform.SetParent(leftPanel.transform, false);
            var modelListRect = modelListObj.AddComponent<RectTransform>();
            modelListRect.anchorMin = new Vector2(0.05f, 0.38f);
            modelListRect.anchorMax = new Vector2(0.95f, 0.63f);
            modelListRect.sizeDelta = Vector2.zero;
            // 同上：在AddComponent<RectTransform>之后再缓存引用
            _modelButtonContainer = modelListObj.transform;
            var modelVlg = modelListObj.AddComponent<VerticalLayoutGroup>();
            modelVlg.spacing = 3;
            modelVlg.childForceExpandWidth = true;
            modelVlg.childForceExpandHeight = false;
            modelVlg.childControlWidth = true;
            modelVlg.childControlHeight = true;

            // 服务地址标签 + 输入框（回车应用）
            RuntimeUIBuilder.CreateText("UrlLabel", leftPanel.transform,
                "服务地址:", 11, new Color(0.6f, 0.6f, 0.7f),
                TextAnchor.MiddleLeft, 0.05f, 0.32f, 0.95f, 0.36f);
            _urlInput = CreateFullInputField(leftPanel.transform, "Ollama地址",
                0.05f, 0.27f, 0.95f, 0.32f);

            // ---------- 系统提示词（左侧底部） ----------
            RuntimeUIBuilder.CreateText("SysPromptTitle", leftPanel.transform,
                "系统提示词:", 12, new Color(0.6f, 0.6f, 0.7f),
                TextAnchor.UpperLeft, 0.05f, 0.21f, 0.95f, 0.25f);
            _systemPromptText = RuntimeUIBuilder.CreateText("SysPrompt", leftPanel.transform,
                "", 11, new Color(0.6f, 0.65f, 0.75f),
                TextAnchor.UpperLeft, 0.05f, 0.02f, 0.95f, 0.20f);

            // ---------- 右侧：对话历史滚动区 ----------
            var rightPanel = RuntimeUIBuilder.CreatePanel("RightPanel", _root.transform,
                new Color(0.04f, 0.05f, 0.08f, 1f),
                0.24f, 0.1f, 1f, 0.93f);

            // 滚动视图（对话正文）
            var contentRect = CreateScrollView(rightPanel.transform,
                0.01f, 0.12f, 0.99f, 1f);
            _conversationText = CreateWrappedText("Conversation", contentRect, "",
                13, new Color(0.88f, 0.92f, 0.98f));

            // ---------- 底部：手动输入区 ----------
            var inputBar = RuntimeUIBuilder.CreatePanel("InputBar", rightPanel.transform,
                new Color(0.1f, 0.12f, 0.18f, 1f),
                0.01f, 0f, 0.99f, 0.1f);

            _inputField = CreateFullInputField(inputBar.transform,
                "输入消息与LLM对话，回车或点击发送...",
                0.01f, 0.12f, 0.8f, 0.88f);

            _buttonSend = RuntimeUIBuilder.CreateButton("BtnSend", inputBar.transform,
                "发送", new Color(0.15f, 0.45f, 0.25f),
                0.82f, 0.1f, 0.99f, 0.9f);

            // ---------- 绑定事件 ----------
            _buttonClose.onClick.AddListener(Hide);
            _buttonRefresh.onClick.AddListener(Refresh);
            _buttonSend.onClick.AddListener(OnSendClicked);
            _inputField.onEndEdit.AddListener(OnInputEndEdit);

            // 初始化默认Agent按钮
            SetAgentIds(new List<string>(DEFAULT_AGENT_IDS));

            // 初始化模型选择按钮 + URL输入框当前值
            RebuildModelButtons();
            if (_urlInput != null)
            {
                var cfgMgr = LLMManager.Instance;
                _urlInput.text = cfgMgr != null ? cfgMgr.CurrentUrl : Constants.OLLAMA_DEFAULT_URL;
                _urlInput.onEndEdit.AddListener(OnUrlEdited);
            }

            // 初始隐藏
            _root.SetActive(false);

            Debug.Log("[LLMConversationWindow] UI构建完成");
        }

        // ==================== 外部接口 ====================

        /// <summary>设置可查看的AgentId列表（由GameHUD.Initialize注入）</summary>
        public void SetAgentIds(List<string> ids)
        {
            _agentIds.Clear();
            _agentIds.Add("global"); // 全局始终在首位
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (id != "global" && !_agentIds.Contains(id))
                        _agentIds.Add(id);
                }
            }
            RebuildAgentButtons();
        }

        /// <summary>显示窗口</summary>
        public void Show()
        {
            if (_root == null) return;
            _root.SetActive(true);
            _refreshTimer = 0.5f; // 触发立即刷新
        }

        /// <summary>显示窗口并定位到指定Agent</summary>
        public void Show(string agentId)
        {
            if (!string.IsNullOrEmpty(agentId))
            {
                // 若该agent不在列表中，临时加入
                if (!_agentIds.Contains(agentId))
                {
                    _agentIds.Add(agentId);
                    RebuildAgentButtons();
                }
                _currentAgentId = agentId;
            }
            Show();
        }

        /// <summary>隐藏窗口</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>窗口是否可见</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        // ==================== 生命周期 ====================

        private void Update()
        {
            // 仅在可见时定时刷新（节省性能）
            if (_root == null || !_root.activeSelf) return;

            // 用unscaledDeltaTime，暂停时也能查看刷新
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.5f)
            {
                _refreshTimer = 0f;
                Refresh();
            }
        }

        // ==================== 刷新逻辑 ====================

        /// <summary>从LLMManager拉取最新数据并更新UI</summary>
        private void Refresh()
        {
            var mgr = LLMManager.Instance;
            if (mgr == null)
            {
                if (_statusText != null) _statusText.text = "状态: LLM管理器未就绪";
                return;
            }

            // ---------- 状态行 ----------
            string status = mgr.IsAvailable
                ? $"状态: 已连接[{mgr.ProviderName}]  进行中:{mgr.ActiveCount}  排队:{mgr.PendingCount}"
                : $"状态: 未连接(使用本地规则)  排队:{mgr.PendingCount}";
            if (_statusText != null) _statusText.text = status;

            // ---------- 系统提示词 ----------
            if (_systemPromptText != null)
                _systemPromptText.text = Truncate(mgr.GetSystemPromptPreview(), 260);

            // ---------- 对话历史 ----------
            var log = mgr.GetLog(_currentAgentId);
            var entries = log.GetAll();
            if (_conversationText != null)
            {
                if (entries.Count == 0)
                {
                    _conversationText.text = $"< Agent({_currentAgentId})暂无对话记录 >\n\n" +
                        "对话会在以下时机自动产生：\n" +
                        "  - 每30秒的高层战略决策\n" +
                        "  - 遭遇威胁/受重创等重大事件\n" +
                        "  - 在下方手动发送消息\n\n" +
                        (mgr.IsAvailable ? "LLM已连接，等待Agent产生决策..." : "LLM未连接，请确认本地Ollama服务已启动。");
                }
                else
                {
                    var sb = new StringBuilder();
                    foreach (var e in entries)
                        AppendEntry(sb, e);
                    _conversationText.text = sb.ToString();
                }
            }

            UpdateAgentButtonHighlight();
            UpdateModelButtonHighlight();
        }

        /// <summary>拼接单条对话记录到StringBuilder</summary>
        private static void AppendEntry(StringBuilder sb, LLMConversationEntry e)
        {
            string roleLabel = e.EntryRole switch
            {
                LLMConversationEntry.Role.User => "[发送]",
                LLMConversationEntry.Role.Assistant => "[回复]",
                LLMConversationEntry.Role.Error => "[错误]",
                _ => "[系统]"
            };
            string dur = e.DurationMs > 0 ? $" ({e.DurationMs:F0}ms)" : "";
            sb.AppendLine($"[{e.Timestamp}] {roleLabel}  {e.Tag}{dur}");
            sb.AppendLine(e.Content);
            sb.AppendLine("----------------------------------------");
        }

        // ==================== Agent选择 ====================

        private void SelectAgent(string agentId)
        {
            _currentAgentId = agentId;
            _refreshTimer = 0.5f; // 立即刷新
        }

        /// <summary>重建左侧Agent选择按钮</summary>
        private void RebuildAgentButtons()
        {
            if (_agentButtonContainer == null) return;

            // 清除旧按钮
            _agentButtons.Clear();
            for (int i = _agentButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(_agentButtonContainer.GetChild(i).gameObject);

            // 创建新按钮
            foreach (var id in _agentIds)
            {
                string captured = id;
                var btn = RuntimeUIBuilder.CreateButton($"Btn_{id}", _agentButtonContainer,
                    id, new Color(0.18f, 0.22f, 0.32f),
                    0f, 0f, 1f, 1f);
                // 在VerticalLayoutGroup下指定固定高度
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 36;
                btn.onClick.AddListener(() => SelectAgent(captured));
                _agentButtons[id] = btn;
            }

            UpdateAgentButtonHighlight();
        }

        /// <summary>更新Agent按钮高亮（选中项变色）</summary>
        private void UpdateAgentButtonHighlight()
        {
            foreach (var kvp in _agentButtons)
            {
                var img = kvp.Value.GetComponent<Image>();
                if (img != null)
                {
                    img.color = (kvp.Key == _currentAgentId)
                        ? new Color(0.3f, 0.55f, 0.8f)   // 选中高亮蓝
                        : new Color(0.18f, 0.22f, 0.32f); // 未选中深灰
                }
            }
        }

        // ==================== LLM配置 ====================

        /// <summary>重建模型选择按钮：异步从Ollama拉取已安装模型，失败则回退到预设</summary>
        private void RebuildModelButtons()
        {
            if (_modelButtonContainer == null) return;
            ClearModelButtons();

            var mgr = LLMManager.Instance;
            if (mgr == null)
            {
                BuildModelButtons(new List<string>(Constants.OLLAMA_MODEL_OPTIONS));
                return;
            }

            // 异步获取已安装模型，确保只能选已pull的模型
            mgr.GetInstalledModelsAsync(models =>
            {
                var list = new List<string>();
                if (models != null && models.Length > 0)
                    list.AddRange(models);
                else
                    list.AddRange(Constants.OLLAMA_MODEL_OPTIONS); // 获取失败回退预设
                BuildModelButtons(list);
            });
        }

        /// <summary>清空模型按钮</summary>
        private void ClearModelButtons()
        {
            _modelButtons.Clear();
            if (_modelButtonContainer == null) return;
            for (int i = _modelButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(_modelButtonContainer.GetChild(i).gameObject);
        }

        /// <summary>根据模型名列表构建模型按钮</summary>
        private void BuildModelButtons(List<string> models)
        {
            ClearModelButtons();
            foreach (var model in models)
            {
                string captured = model;
                var btn = RuntimeUIBuilder.CreateButton($"Model_{model}", _modelButtonContainer,
                    model, new Color(0.18f, 0.24f, 0.30f),
                    0f, 0f, 1f, 1f);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 26;
                btn.onClick.AddListener(() => ApplyConfig(captured));
                _modelButtons[model] = btn;
            }
            UpdateModelButtonHighlight();
        }

        /// <summary>应用选中的模型（配合URL输入框的值重新配置LLMManager）</summary>
        private void ApplyConfig(string model)
        {
            var mgr = LLMManager.Instance;
            if (mgr == null) return;
            string url = _urlInput != null ? _urlInput.text : "";
            mgr.Configure(url, model);
            _refreshTimer = 0.5f; // 立即刷新状态显示
        }

        /// <summary>URL输入框回车提交（用当前模型 + 新地址重新配置，并刷新模型列表）</summary>
        private void OnUrlEdited(string text)
        {
            var mgr = LLMManager.Instance;
            if (mgr == null) return;
            mgr.Configure(text, mgr.CurrentModel);
            _refreshTimer = 0.5f;
            RebuildModelButtons(); // 地址变化后重新拉取该服务上的模型列表
        }

        /// <summary>更新模型按钮高亮（当前模型绿色高亮）</summary>
        private void UpdateModelButtonHighlight()
        {
            var mgr = LLMManager.Instance;
            string current = mgr != null ? mgr.CurrentModel : "";
            foreach (var kvp in _modelButtons)
            {
                var img = kvp.Value.GetComponent<Image>();
                if (img != null)
                    img.color = (kvp.Key == current)
                        ? new Color(0.3f, 0.6f, 0.35f)    // 当前模型绿色
                        : new Color(0.18f, 0.24f, 0.30f);
            }
        }

        // ==================== 手动对话 ====================

        private void OnSendClicked()
        {
            SendCurrentInput();
        }

        /// <summary>输入框回车提交（onEndEdit在回车和失焦都触发，这里只在回车时发送）</summary>
        private void OnInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SendCurrentInput();
        }

        /// <summary>发送当前输入框内容给LLM</summary>
        private void SendCurrentInput()
        {
            if (_inputField == null) return;
            string text = _inputField.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            var mgr = LLMManager.Instance;
            if (mgr == null) return;

            // 立即清空输入框，避免重复发送
            _inputField.text = "";
            _inputField.ActivateInputField();

            // 以当前选中Agent身份发送，完成后刷新
            mgr.SendManualMessage(_currentAgentId, text, _ => Refresh());
            // 立即刷新一次以显示已发送的用户消息
            _refreshTimer = 0.5f;
        }

        // ==================== UI构建辅助方法 ====================

        /// <summary>创建带Mask裁剪的垂直滚动视图，返回Content的RectTransform</summary>
        private static RectTransform CreateScrollView(Transform parent,
            float xMin, float yMin, float xMax, float yMax)
        {
            // ScrollRect容器
            var scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 1f);
            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            var scrollRt = scrollObj.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(xMin, yMin);
            scrollRt.anchorMax = new Vector2(xMax, yMax);
            scrollRt.sizeDelta = Vector2.zero;

            // Viewport（裁剪可视区域）
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f); // Mask需要一个Graphic
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.sizeDelta = Vector2.zero;

            // Content（顶部对齐，自动撑开高度）
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewport.transform, false);
            var content = contentObj.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);
            var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 6;
            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 绑定ScrollRect
            scrollRect.viewport = vpRt;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 20f;

            return content;
        }

        /// <summary>创建自动换行、高度自适应的Text（作为滚动Content的子物体）</summary>
        private static Text CreateWrappedText(string name, Transform parent, string text,
            int fontSize, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var t = obj.AddComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            t.fontSize = fontSize;
            t.color = color;
            t.text = text;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = true; // 允许滚动事件捕获
            // VerticalLayoutGroup的childControlHeight=true会使用preferred height撑开Content
            return t;
        }

        /// <summary>创建全宽InputField（自定义锚点，不受RuntimeUIBuilder的Row布局限制）</summary>
        private static InputField CreateFullInputField(Transform parent, string placeholder,
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject("InputField");
            obj.transform.SetParent(parent, false);
            obj.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f);
            var inp = obj.AddComponent<InputField>();
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.sizeDelta = Vector2.zero;

            // 输入文本
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var t = textObj.AddComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", 15);
            t.fontSize = 15;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            var tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.sizeDelta = new Vector2(-10, -4);
            inp.textComponent = t;

            // 占位提示
            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(obj.transform, false);
            var p = phObj.AddComponent<Text>();
            p.font = Font.CreateDynamicFontFromOSFont("Arial", 15);
            p.fontSize = 15;
            p.color = Color.gray;
            p.text = placeholder;
            p.alignment = TextAnchor.MiddleLeft;
            var pr = phObj.GetComponent<RectTransform>();
            pr.anchorMin = Vector2.zero;
            pr.anchorMax = Vector2.one;
            pr.sizeDelta = new Vector2(-10, -4);
            inp.placeholder = p;

            return inp;
        }

        /// <summary>截断字符串到指定长度（超出加省略号）</summary>
        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
        }
    }
}
