/// <summary>
/// 游戏配置运行时面板（游戏内）
/// 在 GameScene 顶栏/底栏的"配置"按钮触发，以可滚动表单编辑 GameConfig 各分组，
/// 保存后写入 GameConfigManager 并持久化到 game_config.json，修改即时生效。
/// LLM 的 url/model 变更会自动调用 LLMManager.Configure 重新连接。
///
/// 结构：
/// ┌──────────── 全屏半透明遮罩 ────────────┐
/// │ ┌──────── 居中配置窗口 ────────┐       │
/// │ │ 标题：游戏配置                │       │
/// │ │ ┌──── 可滚动字段表单 ────┐    │       │
/// │ │ │ [分组头]                │    │       │
/// │ │ │ 标签 [输入框]           │    │       │
/// │ │ │ ...                     │    │       │
/// │ │ └─────────────────────────┘    │       │
/// │ │ [保存] [重置默认] [关闭]       │       │
/// │ └──────────────────────────────┘       │
/// └────────────────────────────────────────┘
/// </summary>
using System;
using System.Collections.Generic;
using System.Globalization;
using GalaxyAgent.Config;
using GalaxyAgent.LLM;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class GameConfigPanel : MonoBehaviour
    {
        // 运行时配置访问（每次读取最新Config，重置后仍指向新对象）
        private static GameConfig Cfg => GameConfigManager.Instance != null
            ? GameConfigManager.Instance.Config : new GameConfig();

        private GameObject _root;
        private Transform _content;
        private bool _built;

        // 每个字段的绑定：输入框 + 重载取值 + 应用赋值
        private readonly List<Binding> _bindings = new List<Binding>();

        private struct Binding
        {
            public InputField Input;
            public Toggle Toggle;
            public Func<string> Reload;
            public Func<bool> ReloadBool;
            public Action Apply;
        }

        /// <summary>面板是否可见</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>构建UI（幂等）</summary>
        public void BuildUI(Transform parent)
        {
            if (_built) return;
            _built = true;

            // 全屏半透明遮罩，阻挡地图点击
            _root = MakeFull("ConfigOverlay", parent);
            var overlayImg = _root.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.55f);

            // 居中配置窗口
            var win = RuntimeUIBuilder.CreatePanel("ConfigWindow", _root.transform,
                new Color(0.07f, 0.07f, 0.13f, 0.98f),
                0.12f, 0.05f, 0.88f, 0.95f);

            // 标题
            RuntimeUIBuilder.CreateText("Title", win.transform,
                "游戏配置", 22, new Color(0.4f, 0.8f, 1f),
                TextAnchor.MiddleCenter, 0f, 0.94f, 1f, 0.99f);

            // 可滚动字段表单
            _content = RuntimeUIBuilder.CreateScrollView("Fields", win.transform,
                new Color(0.05f, 0.05f, 0.1f, 0.9f),
                0.03f, 0.12f, 0.97f, 0.9f);

            // 底部按钮：保存 / 重置默认 / 关闭
            var btnSave = RuntimeUIBuilder.CreateButton("Save", win.transform,
                "保存", new Color(0.15f, 0.45f, 0.25f),
                0.1f, 0.02f, 0.32f, 0.1f);
            btnSave.onClick.AddListener(OnSaveClicked);

            var btnReset = RuntimeUIBuilder.CreateButton("Reset", win.transform,
                "重置默认", new Color(0.5f, 0.4f, 0.15f),
                0.38f, 0.02f, 0.6f, 0.1f);
            btnReset.onClick.AddListener(OnResetClicked);

            var btnClose = RuntimeUIBuilder.CreateButton("Close", win.transform,
                "关闭", new Color(0.35f, 0.2f, 0.2f),
                0.66f, 0.02f, 0.9f, 0.1f);
            btnClose.onClick.AddListener(Hide);

            BuildFields();
            _root.SetActive(false);
        }

        /// <summary>显示面板（重载当前配置值到输入框）</summary>
        public void Show()
        {
            ReloadAll();
            if (_root != null) _root.SetActive(true);
            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content as RectTransform);
        }

        /// <summary>隐藏面板</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        // ==================== 字段定义 ====================

        /// <summary>构建所有配置字段行（按分组）</summary>
        private void BuildFields()
        {
            AddHeader("— Agent 平衡 —");
            IntRow("感知半径(格)", () => Cfg.Agent.PerceptionRadius, v => Cfg.Agent.PerceptionRadius = v);
            FloatRow("移动速度(格/秒)", () => Cfg.Agent.MoveSpeed, v => Cfg.Agent.MoveSpeed = v);
            FloatRow("最大生命", () => Cfg.Agent.MaxHealth, v => Cfg.Agent.MaxHealth = v);
            FloatRow("最大饥饿", () => Cfg.Agent.MaxHunger, v => Cfg.Agent.MaxHunger = v);
            FloatRow("最大能量", () => Cfg.Agent.MaxEnergy, v => Cfg.Agent.MaxEnergy = v);
            FloatRow("最大携带量", () => Cfg.Agent.MaxCarry, v => Cfg.Agent.MaxCarry = v);
            FloatRow("饥饿消耗/秒", () => Cfg.Agent.HungerDrain, v => Cfg.Agent.HungerDrain = v);
            FloatRow("能量消耗/秒", () => Cfg.Agent.EnergyDrain, v => Cfg.Agent.EnergyDrain = v);
            FloatRow("中层决策间隔(秒)", () => Cfg.Agent.MidLevelDecisionInterval, v => Cfg.Agent.MidLevelDecisionInterval = v);
            FloatRow("高层决策最小间隔(秒)", () => Cfg.Agent.HighLevelMinInterval, v => Cfg.Agent.HighLevelMinInterval = v);
            FloatRow("高层决策最大间隔(秒)", () => Cfg.Agent.HighLevelMaxInterval, v => Cfg.Agent.HighLevelMaxInterval = v);

            AddHeader("— 世界 / 时间 —");
            FloatRow("时间比例(288=5分/天)", () => Cfg.World.TimeRatio, v => Cfg.World.TimeRatio = v);
            IntRow("白天开始小时", () => Cfg.World.DayStartHour, v => Cfg.World.DayStartHour = v);
            FloatRow("夜晚开始小时", () => Cfg.World.NightStartHour, v => Cfg.World.NightStartHour = v);

            AddHeader("— 战斗 / 升级 —");
            FloatRow("攻击冷却(秒)", () => Cfg.Combat.AttackCooldown, v => Cfg.Combat.AttackCooldown = v);
            FloatRow("最低伤害", () => Cfg.Combat.MinDamage, v => Cfg.Combat.MinDamage = v);
            FloatRow("威胁攻击范围(格)", () => Cfg.Combat.ThreatAttackRange, v => Cfg.Combat.ThreatAttackRange = v);
            FloatRow("击杀威胁经验", () => Cfg.Combat.KillThreatXP, v => Cfg.Combat.KillThreatXP = v);
            FloatRow("每级所需经验倍数", () => Cfg.Combat.XpPerLevel, v => Cfg.Combat.XpPerLevel = v);
            FloatRow("升级回血比例", () => Cfg.Combat.LevelUpHealPercent, v => Cfg.Combat.LevelUpHealPercent = v);

            AddHeader("— 采集 —");
            FloatRow("基础采集时间(秒)", () => Cfg.Gather.BaseGatherTime, v => Cfg.Gather.BaseGatherTime = v);
            FloatRow("采集经验", () => Cfg.Gather.GatherResourceXP, v => Cfg.Gather.GatherResourceXP = v);

            AddHeader("— 探索发现 —");
            FloatRow("发现物密度(0-1)", () => Cfg.Discovery.Density, v => Cfg.Discovery.Density = v);
            IntRow("发现物采样间隔(格)", () => Cfg.Discovery.SampleInterval, v => Cfg.Discovery.SampleInterval = v);
            FloatRow("调查发现经验", () => Cfg.Discovery.DiscoveryXP, v => Cfg.Discovery.DiscoveryXP = v);

            AddHeader("— LLM —");
            TextRow("服务地址", () => Cfg.Llm.Url, v => Cfg.Llm.Url = v);
            TextRow("模型名", () => Cfg.Llm.Model, v => Cfg.Llm.Model = v);
            FloatRow("请求超时(秒)", () => Cfg.Llm.RequestTimeout, v => Cfg.Llm.RequestTimeout = v);
            IntRow("最大Token", () => Cfg.Llm.MaxTokens, v => Cfg.Llm.MaxTokens = v);
            IntRow("对话记录上限", () => Cfg.Llm.ConversationLogMax, v => Cfg.Llm.ConversationLogMax = v);
            FloatRow("事件触发冷却(秒)", () => Cfg.Llm.EventTriggerCooldown, v => Cfg.Llm.EventTriggerCooldown = v);

            AddHeader("— 存档 / 自动保存 —");
            BoolRow("启用自动保存", () => Cfg.Save.AutoSaveEnabled, v => Cfg.Save.AutoSaveEnabled = v);
            FloatRow("自动保存间隔(秒)", () => Cfg.Save.AutoSaveInterval, v => Cfg.Save.AutoSaveInterval = v);
        }

        // ==================== 行构建辅助 ====================

        private void AddHeader(string title)
        {
            var h = RuntimeUIBuilder.CreateText("hdr", _content, title, 16,
                new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleLeft);
            var hl = h.gameObject.AddComponent<LayoutElement>();
            hl.preferredHeight = 30;
            hl.minHeight = 30;
        }

        /// <summary>浮点字段行</summary>
        private void FloatRow(string label, Func<float> get, Action<float> set)
        {
            var inp = AddFieldRow(label, get().ToString(CultureInfo.InvariantCulture));
            _bindings.Add(new Binding
            {
                Input = inp,
                Reload = () => get().ToString(CultureInfo.InvariantCulture),
                Apply = () =>
                {
                    if (float.TryParse(inp.text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        set(v);
                }
            });
        }

        /// <summary>整数字段行</summary>
        private void IntRow(string label, Func<int> get, Action<int> set)
        {
            var inp = AddFieldRow(label, get().ToString(CultureInfo.InvariantCulture));
            _bindings.Add(new Binding
            {
                Input = inp,
                Reload = () => get().ToString(CultureInfo.InvariantCulture),
                Apply = () =>
                {
                    if (int.TryParse(inp.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        set(v);
                }
            });
        }

        /// <summary>文本字段行</summary>
        private void TextRow(string label, Func<string> get, Action<string> set)
        {
            var inp = AddFieldRow(label, get() ?? "");
            _bindings.Add(new Binding
            {
                Input = inp,
                Reload = () => get() ?? "",
                Apply = () => set(inp.text ?? "")
            });
        }

        /// <summary>布尔字段行（标签 + 勾选框），暂存到Toggle，保存时才应用到配置</summary>
        private void BoolRow(string label, Func<bool> get, Action<bool> set)
        {
            // 行容器：标签（固定宽） + Toggle（左对齐填充）
            var row = new GameObject("row_" + label);
            row.transform.SetParent(_content, false);
            row.AddComponent<RectTransform>();
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            var rl = row.AddComponent<LayoutElement>();
            rl.preferredHeight = 34;
            rl.minHeight = 34;

            // 标签（与其它行对齐）
            var lbl = RuntimeUIBuilder.CreateText("lbl", row.transform, label, 13,
                new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft);
            var lblL = lbl.gameObject.AddComponent<LayoutElement>();
            lblL.preferredWidth = 200;
            lblL.minWidth = 200;
            lblL.flexibleWidth = 0;

            var toggle = MakeToggle(row.transform, get());
            _bindings.Add(new Binding
            {
                Toggle = toggle,
                ReloadBool = () => get(),
                Apply = () => set(toggle.isOn)
            });
        }

        /// <summary>构建一个深色风格的勾选框（背景 + 勾选标记）</summary>
        private static Toggle MakeToggle(Transform parent, bool initialValue)
        {
            // 背景容器，同时作为Toggle点击区域
            var bgObj = new GameObject("Toggle");
            bgObj.transform.SetParent(parent, false);
            var bgRt = bgObj.AddComponent<RectTransform>();
            bgRt.sizeDelta = new Vector2(28, 28);
            var bg = bgObj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.2f);

            // 勾选标记（默认隐藏，Toggle.isOn 时显示）
            var markObj = new GameObject("Checkmark");
            markObj.transform.SetParent(bgObj.transform, false);
            var markRt = markObj.AddComponent<RectTransform>();
            markRt.anchorMin = Vector2.zero;
            markRt.anchorMax = Vector2.one;
            markRt.offsetMin = new Vector2(4, 4);
            markRt.offsetMax = new Vector2(-4, -4);
            var mark = markObj.AddComponent<Image>();
            mark.color = new Color(0.3f, 0.8f, 0.4f);

            var toggle = bgObj.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = mark;
            toggle.isOn = initialValue;
            return toggle;
        }

        /// <summary>创建标签+输入框的一行（适配VerticalLayoutGroup）</summary>
        private InputField AddFieldRow(string label, string initial)
        {
            var row = new GameObject("row_" + label);
            row.transform.SetParent(_content, false);
            row.AddComponent<RectTransform>();
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            var rl = row.AddComponent<LayoutElement>();
            rl.preferredHeight = 34;
            rl.minHeight = 34;

            // 标签（固定宽度）
            var lbl = RuntimeUIBuilder.CreateText("lbl", row.transform, label, 13,
                new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleLeft);
            var lblL = lbl.gameObject.AddComponent<LayoutElement>();
            lblL.preferredWidth = 200;
            lblL.minWidth = 200;
            lblL.flexibleWidth = 0;

            // 输入框（填充剩余宽度）
            return MakeInputField(row.transform, initial);
        }

        /// <summary>创建一个布局友好的InputField（深色背景+文字+占位符，锚点撑满父级）</summary>
        private static InputField MakeInputField(Transform parent, string initialValue)
        {
            var obj = new GameObject("Input");
            obj.transform.SetParent(parent, false);
            obj.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.2f);
            var inp = obj.AddComponent<InputField>();

            // 文字
            var tObj = new GameObject("Text");
            tObj.transform.SetParent(obj.transform, false);
            var t = tObj.AddComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            t.fontSize = 14;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            var trt = tObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 0);
            trt.offsetMax = new Vector2(-6, 0);
            inp.textComponent = t;

            // 占位符
            var pObj = new GameObject("Placeholder");
            pObj.transform.SetParent(obj.transform, false);
            var p = pObj.AddComponent<Text>();
            p.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            p.fontSize = 14;
            p.color = new Color(0.5f, 0.5f, 0.5f);
            var prt = pObj.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(6, 0);
            prt.offsetMax = new Vector2(-6, 0);
            inp.placeholder = p;

            inp.text = initialValue ?? "";
            return inp;
        }

        // ==================== 按钮事件 ====================

        /// <summary>保存：应用所有输入到配置、持久化、LLM变更重连、关闭</summary>
        private void OnSaveClicked()
        {
            ApplyAll();

            var mgr = GameConfigManager.Instance;
            if (mgr != null)
            {
                mgr.Save();
                // LLM地址/模型变化时重新配置客户端（自动重检可用性）
                var llm = LLMManager.Instance;
                if (llm != null &&
                    (mgr.Config.Llm.Url != llm.CurrentUrl || mgr.Config.Llm.Model != llm.CurrentModel))
                {
                    llm.Configure(mgr.Config.Llm.Url, mgr.Config.Llm.Model);
                }
                Debug.Log("[GameConfigPanel] 配置已保存并应用");
            }
            Hide();
        }

        /// <summary>重置默认：恢复默认配置并重新载入输入框（不关闭面板）</summary>
        private void OnResetClicked()
        {
            GameConfigManager.Instance?.ResetToDefaults();
            ReloadAll();
            Debug.Log("[GameConfigPanel] 已重置为默认配置");
        }

        // ==================== 绑定批处理 ====================

        private void ReloadAll()
        {
            foreach (var b in _bindings)
            {
                if (b.Input != null) b.Input.text = b.Reload();
                if (b.Toggle != null && b.ReloadBool != null) b.Toggle.SetIsOnWithoutNotify(b.ReloadBool());
            }
        }

        private void ApplyAll()
        {
            foreach (var b in _bindings) b.Apply();
        }

        // ==================== 辅助 ====================

        /// <summary>创建撑满父级的RectTransform容器（普通Transform在Canvas下无尺寸）</summary>
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
