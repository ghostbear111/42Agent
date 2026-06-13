/// <summary>
/// 基地信息面板
/// 点击基地时显示仓库资源、基地状态等信息
/// 支持运行时自构建：当panelRoot为空时调用BuildUI()创建完整UI
/// 每种资源前有对应颜色的方块标识
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.World.Base;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class BaseInfoPanel : MonoBehaviour
    {
        // ==================== Inspector字段（可运行时赋值） ====================

        [Header("UI组件")]
        [Tooltip("面板根节点")]
        public GameObject panelRoot;
        [Tooltip("基地生命值文本")]
        public Text textHealth;
        [Tooltip("基地位置文本")]
        public Text textPosition;
        [Tooltip("关闭按钮")]
        public Button buttonClose;

        // 单独的资源文本（运行时自构建时使用）
        [Tooltip("矿物数量文本")]
        public Text textMineral;
        [Tooltip("晶体数量文本")]
        public Text textCrystal;
        [Tooltip("水数量文本")]
        public Text textWater;
        [Tooltip("有机物数量文本")]
        public Text textOrganic;
        [Tooltip("遗迹数据文本")]
        public Text textRuin;

        // 兼容旧字段（Inspector赋值时仍可用）
        [Tooltip("仓库资源文本（旧版兼容）")]
        public Text textStorage;

        // ==================== 生命周期 ====================

        private void Start()
        {
            if (buttonClose != null)
                buttonClose.onClick.AddListener(Hide);

            // 初始隐藏（如果面板已构建）
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        // ==================== 运行时UI构建 ====================

        /// <summary>
        /// 运行时自构建基地信息面板
        /// 在父节点下创建完整的面板UI结构
        /// 布局：右侧面板 anchor(0.78, 0.08) ~ (1.0, 0.92)
        /// 每种资源前有对应颜色的方块标识：
        ///   矿物=棕色 晶体=黄色 水=蓝色 有机=绿色 遗迹=紫色
        /// </summary>
        /// <param name="parent">父节点</param>
        public void BuildUI(Transform parent)
        {
            // ---------- 容器面板 ----------
            panelRoot = RuntimeUIBuilder.CreatePanel("BaseInfoPanel", parent,
                new Color(0.06f, 0.06f, 0.12f, 0.92f),
                0.78f, 0.08f, 1.0f, 0.92f);

            // ---------- 标题栏 ----------
            RuntimeUIBuilder.CreateText("Title", panelRoot.transform,
                "基地信息", 20, new Color(1f, 0.95f, 0.6f),
                TextAnchor.MiddleCenter, 0f, 0.92f, 0.8f, 1f);

            buttonClose = RuntimeUIBuilder.CreateButton("BtnClose", panelRoot.transform,
                "X", new Color(0.6f, 0.2f, 0.2f),
                0.8f, 0.92f, 1f, 1f);

            // ---------- 基地生命值（红色方块标识） ----------
            RuntimeUIBuilder.CreateColorBlock("HealthBlock", panelRoot.transform,
                new Color(0.9f, 0.2f, 0.2f), 0.06f, 0.84f, 0.10f, 0.89f);
            textHealth = RuntimeUIBuilder.CreateText("Health", panelRoot.transform,
                "基地生命: --/--", 15, Color.white,
                TextAnchor.MiddleLeft, 0.12f, 0.83f, 0.94f, 0.89f);

            // ---------- 分隔线 ----------
            RuntimeUIBuilder.CreatePanel("Sep1", panelRoot.transform,
                new Color(0.4f, 0.6f, 0.3f, 0.6f),
                0.06f, 0.79f, 0.94f, 0.81f);
            RuntimeUIBuilder.CreateText("StorageTitle", panelRoot.transform,
                "── 仓库 ──", 14, new Color(0.5f, 0.75f, 0.5f),
                TextAnchor.MiddleCenter, 0f, 0.75f, 1f, 0.80f);

            // ---------- 资源列表（每种资源一行，前有颜色方块） ----------

            // 矿物 - 棕色方块
            RuntimeUIBuilder.CreateColorBlock("MineralBlock", panelRoot.transform,
                Constants.COLOR_MINERAL, 0.06f, 0.67f, 0.10f, 0.72f);
            textMineral = RuntimeUIBuilder.CreateText("MineralText", panelRoot.transform,
                "矿物: 0", 14, new Color(0.85f, 0.7f, 0.5f),
                TextAnchor.MiddleLeft, 0.12f, 0.66f, 0.94f, 0.72f);

            // 晶体 - 黄色方块
            RuntimeUIBuilder.CreateColorBlock("CrystalBlock", panelRoot.transform,
                Constants.COLOR_CRYSTAL, 0.06f, 0.58f, 0.10f, 0.63f);
            textCrystal = RuntimeUIBuilder.CreateText("CrystalText", panelRoot.transform,
                "晶体: 0", 14, new Color(1f, 0.95f, 0.5f),
                TextAnchor.MiddleLeft, 0.12f, 0.57f, 0.94f, 0.63f);

            // 水 - 蓝色方块
            RuntimeUIBuilder.CreateColorBlock("WaterBlock", panelRoot.transform,
                Constants.COLOR_WATER, 0.06f, 0.49f, 0.10f, 0.54f);
            textWater = RuntimeUIBuilder.CreateText("WaterText", panelRoot.transform,
                "水: 0", 14, new Color(0.5f, 0.7f, 1f),
                TextAnchor.MiddleLeft, 0.12f, 0.48f, 0.94f, 0.54f);

            // 有机物 - 绿色方块
            RuntimeUIBuilder.CreateColorBlock("OrganicBlock", panelRoot.transform,
                Constants.COLOR_ORGANIC, 0.06f, 0.40f, 0.10f, 0.45f);
            textOrganic = RuntimeUIBuilder.CreateText("OrganicText", panelRoot.transform,
                "有机: 0", 14, new Color(0.5f, 0.9f, 0.5f),
                TextAnchor.MiddleLeft, 0.12f, 0.39f, 0.94f, 0.45f);

            // 遗迹数据 - 紫色方块
            RuntimeUIBuilder.CreateColorBlock("RuinBlock", panelRoot.transform,
                Constants.COLOR_RUIN, 0.06f, 0.31f, 0.10f, 0.36f);
            textRuin = RuntimeUIBuilder.CreateText("RuinText", panelRoot.transform,
                "遗迹: 0", 14, new Color(0.8f, 0.6f, 1f),
                TextAnchor.MiddleLeft, 0.12f, 0.30f, 0.94f, 0.36f);

            // ---------- 分隔线 ----------
            RuntimeUIBuilder.CreatePanel("Sep2", panelRoot.transform,
                new Color(0.3f, 0.3f, 0.4f, 0.5f),
                0.06f, 0.25f, 0.94f, 0.27f);

            // ---------- 位置 ----------
            textPosition = RuntimeUIBuilder.CreateText("Position", panelRoot.transform,
                "位置: (--, --)", 13, new Color(0.6f, 0.6f, 0.6f),
                TextAnchor.MiddleLeft, 0.06f, 0.19f, 0.94f, 0.24f);

            // 绑定关闭按钮
            if (buttonClose != null)
                buttonClose.onClick.AddListener(Hide);

            // 初始隐藏
            panelRoot.SetActive(false);

            Debug.Log("[BaseInfoPanel] UI构建完成");
        }

        // ==================== 显示/隐藏 ====================

        /// <summary>
        /// 显示基地信息
        /// 支持两种模式：独立资源文本（自构建）或合并文本（Inspector赋值）
        /// </summary>
        public void Show(BaseController baseController)
        {
            if (baseController == null) return;

            var storage = baseController.Storage;

            // 基地生命值
            if (textHealth != null)
                textHealth.text = $"基地生命: {baseController.Health:F0}/{baseController.MaxHealth:F0}";

            // 资源显示：优先使用独立文本字段（自构建模式）
            if (textMineral != null)
            {
                textMineral.text = $"矿物: {GetValue(storage, ResourceType.Mineral):F0}";
                textCrystal.text = $"晶体: {GetValue(storage, ResourceType.Crystal):F0}";
                textWater.text = $"水: {GetValue(storage, ResourceType.Water):F0}";
                textOrganic.text = $"有机: {GetValue(storage, ResourceType.Organic):F0}";
                textRuin.text = $"遗迹: {GetValue(storage, ResourceType.RuinData):F0}";
            }
            else if (textStorage != null)
            {
                // 兼容模式：使用单个合并文本
                string storageText = "仓库:\n";
                storageText += $"  矿物: {GetValue(storage, ResourceType.Mineral):F0}\n";
                storageText += $"  晶体: {GetValue(storage, ResourceType.Crystal):F0}\n";
                storageText += $"  水: {GetValue(storage, ResourceType.Water):F0}\n";
                storageText += $"  有机: {GetValue(storage, ResourceType.Organic):F0}\n";
                storageText += $"  遗迹: {GetValue(storage, ResourceType.RuinData):F0}";
                textStorage.text = storageText;
            }

            // 基地位置
            if (textPosition != null)
                textPosition.text = $"位置: ({baseController.transform.position.x:F0}, {baseController.transform.position.y:F0})";

            if (panelRoot != null) panelRoot.SetActive(true);
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 从仓库字典中安全获取资源数量
        /// </summary>
        private static float GetValue(Dictionary<ResourceType, float> storage, ResourceType type)
        {
            return storage != null && storage.ContainsKey(type) ? storage[type] : 0f;
        }
    }
}
