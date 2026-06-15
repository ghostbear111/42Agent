/// <summary>
/// LLM 星球创建对话窗口
/// 在 MapGeneration 界面点击「LLM 创建星球」时弹出，以自然语言对话引导玩家描述想要的星球，
/// 调用 LLMPlanetCreator 让 LLM 理解描述、对比地球基准并推断星球参数。
///
/// 交互流程（单轮 + 可重述）：
///   1. 弹出后向导先问候："您想寻找怎样的星球？"
///   2. 玩家输入描述（如"我想创建一个和地球相似的星球"）→ 发送
///   3. 显示 loading → LLM 返回 → 在对话区显示"通过您的信息为您找到了「名称」星球..."
///   4. 成功则触发 OnPlanetCreated 事件，由 MapGenUI 回填表单字段
///   5. 玩家不满意可再次输入重新描述；满意则关闭窗口回到表单微调或直接发射
///
/// 注意：所有 Text 均通过 RuntimeUIBuilder.CreateText 创建，复用全局 SharedFont，
/// 避免每处新建动态字体导致的渲染空白坑（见项目记忆）。
/// </summary>
using System;
using GalaxyAgent.LLM;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class LLMPlanetDialog : MonoBehaviour
    {
        // ==================== UI 引用 ====================
        private GameObject _root;              // 对话框根
        private Transform _dialogContent;      // 对话历史容器（ScrollView 的 Content）
        private InputField _input;             // 描述输入框
        private Button _sendBtn;
        private Button _closeBtn;
        private Text _statusText;              // 状态/提示行

        // ==================== 状态 ====================
        private readonly LLMPlanetCreator _creator = new LLMPlanetCreator();
        private bool _waiting;                 // 是否正在等待 LLM 响应（防重复发送）

        /// <summary>星球创建成功时触发；订阅方（MapGenUI）据此回填表单</summary>
        public event Action<PlanetCreationResult> OnPlanetCreated;

        /// <summary>窗口是否可见</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        // ==================== 构建 ====================

        /// <summary>运行时构建对话框 UI（幂等，由 MapGenUI 调用一次）</summary>
        public void BuildUI(Transform parent)
        {
            RuntimeUIBuilder.EnsureEventSystem();

            // 居中半透明对话框
            _root = RuntimeUIBuilder.CreatePanel("LLMPlanetDialog", parent,
                new Color(0.06f, 0.07f, 0.12f, 0.98f), 0.12f, 0.08f, 0.88f, 0.92f);

            // 标题（左）+ 关闭按钮（右）
            RuntimeUIBuilder.CreateText("Title", _root.transform, "LLM 星球向导", 24,
                new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleCenter, 0f, 0.93f, 0.82f, 0.99f);

            _closeBtn = RuntimeUIBuilder.CreateButton("BtnClose", _root.transform, "关闭",
                new Color(0.6f, 0.2f, 0.2f), 0.84f, 0.935f, 0.98f, 0.985f);

            // 状态/提示行
            _statusText = RuntimeUIBuilder.CreateText("Status", _root.transform, "", 14,
                new Color(0.9f, 0.85f, 0.4f), TextAnchor.MiddleLeft, 0.02f, 0.88f, 0.98f, 0.93f);

            // 对话历史滚动区（CreateScrollView 返回带 VerticalLayoutGroup+ContentSizeFitter 的 Content）
            _dialogContent = RuntimeUIBuilder.CreateScrollView("DialogScroll", _root.transform,
                new Color(0.04f, 0.05f, 0.08f, 0.9f), 0.02f, 0.18f, 0.98f, 0.87f);

            // 描述输入框（左大）
            _input = CreateDialogInputField(_root.transform, "描述您想要的星球，回车或点发送…",
                0.02f, 0.06f, 0.80f, 0.16f);

            // 发送按钮（右）
            _sendBtn = RuntimeUIBuilder.CreateButton("BtnSend", _root.transform, "发送",
                new Color(0.15f, 0.45f, 0.25f), 0.82f, 0.06f, 0.98f, 0.16f);

            // 绑定事件
            _closeBtn.onClick.AddListener(Hide);
            _sendBtn.onClick.AddListener(OnSendClicked);
            _input.onEndEdit.AddListener(OnInputEndEdit);

            _root.SetActive(false);
            Debug.Log("[LLMPlanetDialog] UI 构建完成");
        }

        /// <summary>显示对话框：清空历史、问候、聚焦输入框</summary>
        public void Show()
        {
            if (_root == null) return;
            _root.SetActive(true);

            // 清空旧对话
            for (int i = _dialogContent.childCount - 1; i >= 0; i--)
                Destroy(_dialogContent.GetChild(i).gameObject);

            if (_input != null) _input.text = "";
            SetWaiting(false);
            if (_statusText != null) _statusText.text = "";

            // 向导开场问候
            AppendMessage("向导", "您好！您想寻找怎样的星球？\n（例如：我想创建一个和地球相似的星球）",
                new Color(0.5f, 0.85f, 1f));

            if (_input != null) _input.ActivateInputField();
        }

        /// <summary>隐藏对话框</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        // ==================== 发送逻辑 ====================

        private void OnSendClicked()
        {
            SendCurrentInput();
        }

        /// <summary>输入框回车提交（onEndEdit 在回车与失焦都触发，仅回车时发送）</summary>
        private void OnInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SendCurrentInput();
        }

        /// <summary>发送当前输入：显示玩家消息 → loading → 请求 LLM → 显示回复/触发回填</summary>
        private void SendCurrentInput()
        {
            if (_waiting || _input == null) return;
            string text = _input.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            // 先展示玩家输入
            AppendMessage("您", text.Trim(), new Color(0.85f, 0.9f, 0.95f));
            _input.text = "";
            _input.ActivateInputField();

            SetWaiting(true);
            if (_statusText != null) _statusText.text = "向导正在为您寻找星球…";

            // 请求 LLM 创建（回调在主线程）
            _creator.RequestCreation(text, result =>
            {
                SetWaiting(false);

                if (result != null && result.Success)
                {
                    // 三段式展示：找到星球 → 我理解您 → 创造依据 → 星球介绍
                    AppendMessage("向导", BuildAssistantReply(result), new Color(0.5f, 0.85f, 1f));
                    if (_statusText != null)
                        _statusText.text = "✓ 参数已填入配置表单，关闭后可微调或直接「发射」";
                    // 通知订阅方回填表单
                    try { OnPlanetCreated?.Invoke(result); }
                    catch (Exception e) { Debug.LogWarning($"[LLMPlanetDialog] 回填回调异常: {e.Message}"); }
                }
                else
                {
                    string err = result != null ? result.Error : "未知错误";
                    AppendMessage("向导", "抱歉，未能生成星球：" + err, new Color(0.95f, 0.6f, 0.6f));
                    if (_statusText != null)
                        _statusText.text = "生成失败，请确认 LLM 已连接后重新描述";
                }
            });
        }

        /// <summary>设置等待状态：禁用发送按钮，防止重复发送</summary>
        private void SetWaiting(bool waiting)
        {
            _waiting = waiting;
            if (_sendBtn != null) _sendBtn.interactable = !waiting;
        }

        /// <summary>把 LLM 三段式结果拼成一条向导回复：找到星球 → 我理解您 → 创造依据 → 星球介绍</summary>
        private static string BuildAssistantReply(PlanetCreationResult r)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"通过您的信息为您找到了「{r.PlanetName}」星球。");
            if (!string.IsNullOrWhiteSpace(r.Understanding))
                sb.Append("\n\n【我理解您】").Append(r.Understanding);
            if (!string.IsNullOrWhiteSpace(r.Reasoning))
                sb.Append("\n\n【创造依据】").Append(r.Reasoning);
            if (!string.IsNullOrWhiteSpace(r.Description))
                sb.Append("\n\n【星球介绍】").Append(r.Description);
            return sb.ToString();
        }

        // ==================== UI 辅助 ====================

        /// <summary>往对话历史追加一条消息（自动换行、撑高，并立即重建布局）</summary>
        private void AppendMessage(string speaker, string text, Color color)
        {
            if (_dialogContent == null || string.IsNullOrEmpty(text)) return;

            string line = $"【{speaker}】{text}";
            var t = RuntimeUIBuilder.CreateText($"Msg_{_dialogContent.childCount}",
                _dialogContent, line, 15, color, TextAnchor.UpperLeft, 0, 0, 1, 1);
            // 自动换行 + 高度自适应：VerticalLayoutGroup 的 childControlHeight 会用 preferredHeight 撑开 Content
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            // 立即重建布局，让新消息当帧撑开 Content 高度（避免首帧不显示）
            LayoutRebuilder.ForceRebuildLayoutImmediate(_dialogContent as RectTransform);
        }

        /// <summary>
        /// 创建对话框用的输入框（自定义锚点 + 复用 SharedFont，不带左侧 label）。
        /// 不直接用 RuntimeUIBuilder.CreateInputField，因其锚点固定且带 label 占位；
        /// 也不新建动态字体，Text/Placeholder 均经 CreateText 复用全局共享字体。
        /// </summary>
        private static InputField CreateDialogInputField(Transform parent, string placeholder,
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

            // 输入文本（复用共享字体）
            var t = RuntimeUIBuilder.CreateText("Text", obj.transform, "", 15, Color.white,
                TextAnchor.MiddleLeft, 0, 0, 1, 1);
            ApplyPadding(t.GetComponent<RectTransform>(), 6, 4);
            inp.textComponent = t;

            // 占位提示（复用共享字体）
            var p = RuntimeUIBuilder.CreateText("Placeholder", obj.transform, placeholder, 15,
                new Color(0.5f, 0.5f, 0.55f), TextAnchor.MiddleLeft, 0, 0, 1, 1);
            ApplyPadding(p.GetComponent<RectTransform>(), 6, 4);
            inp.placeholder = p;

            return inp;
        }

        /// <summary>给撑满父级的 RectTransform 设置四向内边距</summary>
        private static void ApplyPadding(RectTransform rt, float h, float v)
        {
            rt.offsetMin = new Vector2(h, v);
            rt.offsetMax = new Vector2(-h, -v);
        }
    }
}
