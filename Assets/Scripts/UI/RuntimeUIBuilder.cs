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
            obj.AddComponent<Image>().color = color;
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
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
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
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<Image>().color = color;
            var btn = obj.AddComponent<Button>();
            var r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(xMin, yMin);
            r.anchorMax = new Vector2(xMax, yMax);
            r.sizeDelta = Vector2.zero;
            // 按钮文字
            CreateText("Text", obj.transform, label, 20, Color.white, TextAnchor.MiddleCenter);
            return btn;
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
            captionText.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
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
            textComp.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
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
            phComp.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
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
    }
}
