/// <summary>
/// 运行时UI构建器
/// 在场景加载时动态构建完整的UGUI界面
/// 解决通过execute_code创建的Dropdown等组件模板缺失问题
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
using GalaxyAgent.Map;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public static class RuntimeUIBuilder
    {
        // ==================== 全局共享字体 ====================

        /// <summary>
        /// 全局共享动态字体（所有 Text / Dropdown / InputField 复用同一实例）。
        ///
        /// 为什么必须共享：Font.CreateDynamicFontFromOSFont 每次都新建一个独立动态字体，
        /// 各自维护一张字形纹理图集。若为每个 Text 各建一个 Font（旧实现即如此），则每次
        /// 刷新面板（如科技树点击"解锁"后 RefreshList 重建所有节点行，且事件回调 + 手动调用
        /// 同帧重建两轮）会瞬间创建/销毁大量孤立字体对象，触发 Unity 底层字体纹理重建，
        /// 导致全部 Text 渲染为空白 —— 表现为"点击解锁后所有文字消失"。复用单一实例即可彻底规避。
        ///
        /// 字号安全：动态字体会按 Text.fontSize 动态请求对应大小字形烘焙进图集，
        /// 故一个以 size=16 建立的共享字体能正确渲染任意字号文本。
        /// </summary>
        private static Font _sharedFont;
        private static Font SharedFont =>
            _sharedFont != null ? _sharedFont : (_sharedFont = Font.CreateDynamicFontFromOSFont("Arial", 16));

        // ==================== 通用辅助方法 ====================

        /// <summary>创建标准Canvas</summary>
        public static GameObject CreateCanvas()
        {
            var obj = new GameObject("Canvas");
            var canvas = obj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            obj.AddComponent<CanvasScaler>();
            obj.AddComponent<GraphicRaycaster>();
            return obj;
        }

        /// <summary>创建EventSystem（如果没有的话）</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        /// <summary>创建带背景的面板</summary>
        public static GameObject CreatePanel(string name, Transform parent, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            // 默认贴通用面板底纹（Sliced+tint，保留各面板原色）；无皮肤则纯色
            SpriteRegistry.ApplySkin(img, SpriteRegistry.GetPanelSkin(), color);
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.sizeDelta = Vector2.zero;
            return obj;
        }

        /// <summary>创建文本</summary>
        public static Text CreateText(string name, Transform parent, string text, int fontSize,
            Color color, TextAnchor alignment = TextAnchor.MiddleLeft,
            float xMin = 0, float yMin = 0, float xMax = 1, float yMax = 1)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var t = obj.AddComponent<Text>();
            t.font = SharedFont;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.text = text;
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.sizeDelta = Vector2.zero;
            return t;
        }

        /// <summary>创建按钮</summary>
        public static Button CreateButton(string name, Transform parent, string label, Color color,
            float xMin, float yMin, float xMax, float yMax, Sprite iconSprite = null)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            // 默认贴通用按钮皮肤（Sliced+tint，保留按钮原色）；无皮肤则纯色
            SpriteRegistry.ApplySkin(img, SpriteRegistry.GetButtonSkin(), color);
            var btn = obj.AddComponent<Button>();
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.sizeDelta = Vector2.zero;

            // 未显式传图标时，按 label 自动匹配功能图标（如"保存"→icon_save），实现零调用点接入
            iconSprite ??= AutoIconForLabel(label);
            if (iconSprite != null)
            {
                // 有图标：水平排列 [图标 + 文字]
                var row = new GameObject("Content");
                row.transform.SetParent(obj.transform, false);
                var rowRt = row.AddComponent<RectTransform>();
                rowRt.anchorMin = Vector2.zero; rowRt.anchorMax = Vector2.one;
                rowRt.offsetMin = Vector2.zero; rowRt.offsetMax = Vector2.zero;
                var hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true; hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 6;

                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(row.transform, false);
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
                var iconLe = iconObj.AddComponent<LayoutElement>();
                iconLe.preferredWidth = 20;
                iconLe.preferredHeight = 20;

                CreateText("Text", row.transform, label, 20, Color.white, TextAnchor.MiddleLeft);
            }
            else
            {
                // 无图标：居中文字（保持原样）
                CreateText("Text", obj.transform, label, 20, Color.white, TextAnchor.MiddleCenter);
            }
            return btn;
        }

        /// <summary>
        /// 按按钮 label 文本自动匹配功能图标文件名（无匹配返回 null）。
        /// 让所有功能按钮零调用点自动获得图标；动态列表项（存档行/Agent选择等 label 不匹配）自然无图标。
        /// 新增功能按钮只需在此 switch 加一条 label→图标名 映射。
        /// </summary>
        private static Sprite AutoIconForLabel(string label)
        {
            string key = label switch
            {
                "暂停" => "pause",
                "配置" => "config",
                "LLM对话" => "chat",
                "保存" => "save",
                "返回菜单" => "home",
                "返回" => "home",
                "Back" => "home",
                "X" => "close",
                "关闭" => "close",
                "Close" => "close",
                "科技树" => "tech",
                "解锁" => "unlock",
                "确认" => "confirm",
                "取消" => "cancel",
                "发射" => "launch",
                "Launch" => "launch",
                "刷新" => "refresh",
                "发送" => "send",
                "新游戏" => "newgame",
                "加载游戏" => "load",
                "加载" => "load",
                "退出" => "quit",
                _ => null
            };
            return key != null ? SpriteRegistry.GetButtonIcon(key) : null;
        }

        /// <summary>
        /// 把 CreatePanel 创建的面板改为贴场景背景图（Simple 全屏，覆盖默认 panelSkin）。
        /// 调用方无需引用 Image 类型。
        /// </summary>
        public static void ApplySceneBackground(GameObject panel, string bgName)
        {
            if (panel == null) return;
            var img = panel.GetComponent<Image>();
            if (img == null) return;
            SpriteRegistry.ApplySpriteOrColor(img, SpriteRegistry.GetSceneBg(bgName));
        }

        /// <summary>
        /// 创建完整可用的Dropdown（包含正确的模板结构）
        /// Unity的Dropdown组件要求Template有严格层级：Template > Viewport > Content > Item(Toggle)
        /// 每层都需要正确的组件引用和布局设置
        /// </summary>
        public static Dropdown CreateDropdown(string name, Transform parent, string label,
            string[] options, float y)
        {
            // ---- 行容器 ----
            var row = new GameObject("Row_" + name);
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.15f, y);
            rr.anchorMax = new Vector2(0.85f, y + 0.055f);
            rr.sizeDelta = Vector2.zero;

            // 标签文本
            CreateText("Label", row.transform, label, 18, Color.white,
                TextAnchor.MiddleLeft, 0f, 0f, 0.38f, 1f);

            // ---- Dropdown主体 ----
            var ddObj = new GameObject("Dropdown");
            ddObj.transform.SetParent(row.transform, false);
            ddObj.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.35f);
            var dd = ddObj.AddComponent<Dropdown>();
            var ddRect = ddObj.GetComponent<RectTransform>();
            ddRect.anchorMin = new Vector2(0.4f, 0f);
            ddRect.anchorMax = Vector2.one;
            ddRect.sizeDelta = Vector2.zero;

            // ---- Template（下拉列表容器） ----
            // 必须初始为active，Dropdown会在Setup时自动禁用
            var template = CreatePanel("Template", ddObj.transform, new Color(0.15f, 0.15f, 0.25f),
                0f, -2f, 1f, -0.5f);

            // ScrollRect：负责滚动功能，必须设置viewport和content引用
            var scrollRect = template.AddComponent<ScrollRect>();
            template.AddComponent<Mask>().showMaskGraphic = false;

            // ---- Viewport（可视区域，裁剪溢出内容） ----
            var viewport = CreatePanel("Viewport", template.transform, new Color(0.15f, 0.15f, 0.25f),
                0f, 0f, 1f, 1f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            // ---- Content（选项列表容器） ----
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);

            // 垂直布局：自动排列子选项
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 0;

            // 自动调整高度以适应所有选项
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- Item（单个选项模板，必须包含Toggle组件） ----
            var item = CreatePanel("Item", content.transform, new Color(0.2f, 0.2f, 0.3f),
                0f, 0f, 1f, 1f);

            // LayoutElement：设置选项的最小高度
            var itemLayout = item.AddComponent<LayoutElement>();
            itemLayout.minHeight = 25;

            var toggle = item.AddComponent<Toggle>();

            // Item Background（Toggle的目标Graphic，点击/高亮区域）
            var itemBg = CreatePanel("Item Background", item.transform, new Color(0.2f, 0.2f, 0.35f),
                0f, 0f, 1f, 1f);
            toggle.targetGraphic = itemBg.GetComponent<Image>();

            // Item Checkmark（选中时显示的勾号图形）
            var checkObj = CreatePanel("Item Checkmark", item.transform, new Color(0.4f, 0.8f, 0.4f),
                0f, 0f, 0.1f, 1f);
            toggle.graphic = checkObj.GetComponent<Image>();

            // Item Label（选项文字，Dropdown组件通过itemText引用它）
            var itemLabel = CreateText("Item Label", item.transform, "", 16, Color.white,
                TextAnchor.MiddleLeft, 0.12f, 0f, 1f, 1f);

            // ---- Caption（Dropdown当前显示的选中项文本） ----
            // 注意：必须先添加Text组件（自动创建RectTransform），再设置锚点
            var captionObj = new GameObject("Caption");
            captionObj.transform.SetParent(ddObj.transform, false);
            var captionText = captionObj.AddComponent<Text>();
            captionText.font = SharedFont;
            captionText.fontSize = 16;
            captionText.color = Color.white;
            captionText.alignment = TextAnchor.MiddleLeft;
            var captionRect = captionObj.GetComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = new Vector2(0.9f, 1f);
            captionRect.sizeDelta = Vector2.zero;

            // ---- Arrow（下拉箭头指示器） ----
            // 注意：先添加Image（自动创建RectTransform），再设置锚点
            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(ddObj.transform, false);
            arrowObj.AddComponent<Image>().color = Color.gray;
            var arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0.9f, 0.3f);
            arrowRect.anchorMax = new Vector2(1f, 0.7f);
            arrowRect.sizeDelta = Vector2.zero;

            // ---- 关键：设置ScrollRect的viewport和content引用 ----
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;

            // ---- 设置Dropdown组件的所有引用 ----
            dd.template = template.GetComponent<RectTransform>();
            dd.captionText = captionText;
            dd.itemText = itemLabel;

            // 设置选项列表
            dd.ClearOptions();
            dd.AddOptions(new List<string>(options));

            return dd;
        }

        /// <summary>创建InputField</summary>
        public static InputField CreateInputField(string name, Transform parent, string label,
            string placeholder, float y)
        {
            var row = new GameObject("Row_" + name);
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.15f, y);
            rr.anchorMax = new Vector2(0.85f, y + 0.055f);
            rr.sizeDelta = Vector2.zero;

            CreateText("Label", row.transform, label, 18, Color.white,
                TextAnchor.MiddleLeft, 0f, 0f, 0.38f, 1f);

            var inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(row.transform, false);
            inputObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.25f);
            var inp = inputObj.AddComponent<InputField>();
            var ir = inputObj.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.4f, 0f);
            ir.anchorMax = Vector2.one;
            ir.sizeDelta = Vector2.zero;

            // Text组件
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            var textComp = textObj.AddComponent<Text>();
            textComp.font = SharedFont;
            textComp.fontSize = 16;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleLeft;
            var tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = new Vector2(-10, 0);
            inp.textComponent = textComp;

            // Placeholder
            var phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(inputObj.transform, false);
            var phComp = phObj.AddComponent<Text>();
            phComp.font = SharedFont;
            phComp.fontSize = 16;
            phComp.color = Color.gray;
            phComp.text = placeholder;
            var pr = phObj.GetComponent<RectTransform>();
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
            pr.sizeDelta = new Vector2(-10, 0);
            inp.placeholder = phComp;

            return inp;
        }

        /// <summary>
        /// 创建颜色标识方块（用于资源类型等视觉标识）
        /// 在文本标签旁显示一个纯色小方块，直观区分不同资源类型
        /// </summary>
        /// <param name="name">对象名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="color">方块颜色</param>
        /// <param name="xMin">左锚点X</param>
        /// <param name="yMin">下锚点Y</param>
        /// <param name="xMax">右锚点X</param>
        /// <param name="yMax">上锚点Y</param>
        /// <returns>方块Image组件引用</returns>
        public static Image CreateColorBlock(string name, Transform parent, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            img.color = color;
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.sizeDelta = Vector2.zero;
            return img;
        }

        /// <summary>
        /// 创建可复用的竖向滚动视图（ScrollView + Viewport + Content + 竖向滚动条）
        /// 子项应添加到返回的Content（已带VerticalLayoutGroup + ContentSizeFitter，随内容自动增高）
        ///
        /// 结构：
        ///   ScrollView(Image+ScrollRect)
        ///     ├─ Viewport(Image+Mask)
        ///     │    └─ Content(VerticalLayoutGroup+ContentSizeFitter) ← 返回此Transform
        ///     └─ Scrollbar Vertical(Image+Scrollbar)
        ///          └─ Sliding Area
        ///               └─ Handle(Image)
        /// </summary>
        /// <param name="bgColor">滚动视图背景色</param>
        /// <param name="xMin/yMin/xMax/yMax">相对父级的锚点比例（0~1）</param>
        /// <returns>Content容器Transform，往里添加子项即可</returns>
        public static Transform CreateScrollView(string name, Transform parent, Color bgColor,
            float xMin, float yMin, float xMax, float yMax)
        {
            const float BarWidth = 16f; // 滚动条宽度

            // ---- ScrollView 容器：背景 + ScrollRect ----
            var svObj = new GameObject(name);
            svObj.transform.SetParent(parent, false);
            svObj.AddComponent<Image>().color = bgColor;
            var svRect = svObj.GetComponent<RectTransform>();
            svRect.anchorMin = new Vector2(xMin, yMin);
            svRect.anchorMax = new Vector2(xMax, yMax);
            svRect.sizeDelta = Vector2.zero;
            var scrollRect = svObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            // ---- Viewport：可视裁剪区域，右侧留出滚动条空间 ----
            var vpObj = new GameObject("Viewport");
            vpObj.transform.SetParent(svObj.transform, false);
            var vpImg = vpObj.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f); // 透明背景
            var vpRect = vpObj.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.pivot = new Vector2(0.5f, 0.5f);
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = new Vector2(-BarWidth, 0f);
            // 用RectMask2D按矩形裁剪：比Mask更稳健，不依赖Graphic的alpha，
            // 避免因透明遮罩导致内容被误裁而整列表不可见
            vpObj.AddComponent<RectMask2D>();

            // ---- Content：内容容器，顶部对齐 + 随内容自动增高 ----
            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(vpObj.transform, false);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- 竖向滚动条 ----
            var sbObj = new GameObject("Scrollbar Vertical");
            sbObj.transform.SetParent(svObj.transform, false);
            sbObj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            var sbRect = sbObj.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 0.5f);
            sbRect.sizeDelta = new Vector2(BarWidth, 0f);
            var scrollbar = sbObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Sliding Area（手柄活动区域）
            var saObj = new GameObject("Sliding Area");
            saObj.transform.SetParent(sbObj.transform, false);
            var saRect = saObj.AddComponent<RectTransform>();
            saRect.anchorMin = Vector2.zero;
            saRect.anchorMax = Vector2.one;
            saRect.pivot = new Vector2(0.5f, 0.5f);
            saRect.offsetMin = new Vector2(4f, 4f);
            saRect.offsetMax = new Vector2(-4f, -4f);

            // Handle（滑块）
            var hObj = new GameObject("Handle");
            hObj.transform.SetParent(saObj.transform, false);
            var hImg = hObj.AddComponent<Image>();
            hImg.color = new Color(0.6f, 0.6f, 0.6f, 0.9f);
            var hRect = hObj.GetComponent<RectTransform>();
            hRect.anchorMin = Vector2.zero;
            hRect.anchorMax = Vector2.one;
            hRect.pivot = new Vector2(0.5f, 0.5f);
            hRect.sizeDelta = Vector2.zero;
            scrollbar.handleRect = hRect;
            scrollbar.targetGraphic = hImg;

            // ---- 连接ScrollRect的引用 ----
            scrollRect.content = contentRect;
            scrollRect.viewport = vpRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarSpacing = 2f;

            return contentObj.transform;
        }
    }
}
