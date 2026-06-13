/// <summary>
/// Agent信息面板
/// 点击Agent时显示其属性、状态、当前任务等信息
/// 支持运行时自构建：当panelRoot为空时调用BuildUI()创建完整UI
/// </summary>
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class AgentInfoPanel : MonoBehaviour
    {
        // ==================== Inspector字段（可运行时赋值） ====================

        [Header("UI组件")]
        [Tooltip("面板根节点")]
        public GameObject panelRoot;
        [Tooltip("Agent名称文本")]
        public Text textName;
        [Tooltip("Agent类型文本")]
        public Text textType;
        [Tooltip("生命值")] public Text textHealth;
        [Tooltip("饥饿值")] public Text textHunger;
        [Tooltip("能量值")] public Text textEnergy;
        [Tooltip("状态")] public Text textStatus;
        [Tooltip("当前任务")] public Text textTask;
        [Tooltip("携带物品")] public Text textCarrying;
        [Tooltip("位置")] public Text textPosition;
        [Tooltip("关闭按钮")]
        public Button buttonClose;

        // ==================== 生命周期 ====================

        private void Start()
        {
            // 绑定关闭按钮
            if (buttonClose != null)
                buttonClose.onClick.AddListener(Hide);

            // 初始隐藏（如果面板已构建）
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        // ==================== 运行时UI构建 ====================

        /// <summary>
        /// 运行时自构建Agent信息面板
        /// 在父节点下创建完整的面板UI结构，所有字段通过锚点定位
        /// 布局：右侧面板 anchor(0.78, 0.08) ~ (1.0, 0.92)
        /// </summary>
        /// <param name="parent">父节点（通常是Canvas或侧边区域）</param>
        public void BuildUI(Transform parent)
        {
            // ---------- 容器面板 ----------
            // 右侧信息区域，半透明深色背景
            panelRoot = RuntimeUIBuilder.CreatePanel("AgentInfoPanel", parent,
                new Color(0.06f, 0.06f, 0.12f, 0.92f),
                0.78f, 0.08f, 1.0f, 0.92f);

            // ---------- 标题栏 ----------
            // 面板顶部标题 "Agent信息" + 关闭按钮 [X]
            RuntimeUIBuilder.CreateText("Title", panelRoot.transform,
                "Agent信息", 20, new Color(0.4f, 0.8f, 1f),
                TextAnchor.MiddleCenter, 0f, 0.92f, 0.8f, 1f);

            buttonClose = RuntimeUIBuilder.CreateButton("BtnClose", panelRoot.transform,
                "X", new Color(0.6f, 0.2f, 0.2f),
                0.8f, 0.92f, 1f, 1f);

            // ---------- 内容文本行 ----------
            // 每行用相对锚点定位，从上到下排列
            // y坐标从0.84开始，每行间隔约0.085

            // 行0: Agent名称（较大字体，醒目）
            textName = RuntimeUIBuilder.CreateText("Name", panelRoot.transform,
                "", 18, new Color(1f, 0.95f, 0.6f),
                TextAnchor.MiddleLeft, 0.06f, 0.83f, 0.94f, 0.90f);

            // 行1: 类型
            textType = RuntimeUIBuilder.CreateText("Type", panelRoot.transform,
                "类型: --", 14, Color.white,
                TextAnchor.MiddleLeft, 0.06f, 0.76f, 0.94f, 0.82f);

            // ---------- 分隔线 ----------
            RuntimeUIBuilder.CreatePanel("Sep1", panelRoot.transform,
                new Color(0.3f, 0.3f, 0.4f, 0.5f),
                0.06f, 0.74f, 0.94f, 0.755f);

            // 行2: 生命值（前面加红色方块标识）
            RuntimeUIBuilder.CreateColorBlock("HealthBlock", panelRoot.transform,
                new Color(0.9f, 0.2f, 0.2f), 0.06f, 0.68f, 0.10f, 0.73f);
            textHealth = RuntimeUIBuilder.CreateText("Health", panelRoot.transform,
                "生命: --/--", 14, Color.white,
                TextAnchor.MiddleLeft, 0.12f, 0.67f, 0.94f, 0.73f);

            // 行3: 饥饿值（橙色方块）
            RuntimeUIBuilder.CreateColorBlock("HungerBlock", panelRoot.transform,
                new Color(0.9f, 0.6f, 0.1f), 0.06f, 0.60f, 0.10f, 0.65f);
            textHunger = RuntimeUIBuilder.CreateText("Hunger", panelRoot.transform,
                "饥饿: --/100", 14, Color.white,
                TextAnchor.MiddleLeft, 0.12f, 0.59f, 0.94f, 0.65f);

            // 行4: 能量值（黄色方块）
            RuntimeUIBuilder.CreateColorBlock("EnergyBlock", panelRoot.transform,
                new Color(0.9f, 0.9f, 0.2f), 0.06f, 0.52f, 0.10f, 0.57f);
            textEnergy = RuntimeUIBuilder.CreateText("Energy", panelRoot.transform,
                "能量: --/100", 14, Color.white,
                TextAnchor.MiddleLeft, 0.12f, 0.51f, 0.94f, 0.57f);

            // ---------- 分隔线 ----------
            RuntimeUIBuilder.CreatePanel("Sep2", panelRoot.transform,
                new Color(0.3f, 0.3f, 0.4f, 0.5f),
                0.06f, 0.47f, 0.94f, 0.485f);

            // 行5: 状态
            textStatus = RuntimeUIBuilder.CreateText("Status", panelRoot.transform,
                "状态: --", 14, new Color(0.7f, 0.85f, 1f),
                TextAnchor.MiddleLeft, 0.06f, 0.41f, 0.94f, 0.46f);

            // 行6: 当前任务
            textTask = RuntimeUIBuilder.CreateText("Task", panelRoot.transform,
                "任务: --", 14, new Color(0.7f, 0.85f, 1f),
                TextAnchor.MiddleLeft, 0.06f, 0.34f, 0.94f, 0.39f);

            // 行7: 携带物品
            textCarrying = RuntimeUIBuilder.CreateText("Carrying", panelRoot.transform,
                "携带: --", 14, new Color(0.8f, 0.8f, 0.6f),
                TextAnchor.MiddleLeft, 0.06f, 0.27f, 0.94f, 0.32f);

            // ---------- 分隔线 ----------
            RuntimeUIBuilder.CreatePanel("Sep3", panelRoot.transform,
                new Color(0.3f, 0.3f, 0.4f, 0.5f),
                0.06f, 0.23f, 0.94f, 0.245f);

            // 行8: 位置坐标
            textPosition = RuntimeUIBuilder.CreateText("Position", panelRoot.transform,
                "位置: (--, --)", 13, new Color(0.6f, 0.6f, 0.6f),
                TextAnchor.MiddleLeft, 0.06f, 0.17f, 0.94f, 0.22f);

            // 绑定关闭按钮事件
            if (buttonClose != null)
                buttonClose.onClick.AddListener(Hide);

            // 初始隐藏
            panelRoot.SetActive(false);

            Debug.Log("[AgentInfoPanel] UI构建完成");
        }

        // ==================== 显示/隐藏 ====================

        /// <summary>
        /// 显示指定Agent的信息
        /// </summary>
        public void Show(AgentData data)
        {
            if (data == null) return;

            if (textName != null) textName.text = $"{data.DisplayName} ({data.AgentId})";
            if (textType != null) textType.text = $"类型: {GetTypeName(data.AgentType)}";
            if (textHealth != null) textHealth.text = $"生命: {data.Health:F0}/{data.MaxHealth:F0}";
            if (textHunger != null) textHunger.text = $"饥饿: {data.Hunger:F0}/100";
            if (textEnergy != null) textEnergy.text = $"能量: {data.Energy:F0}/100";
            if (textStatus != null) textStatus.text = $"状态: {GetStateName(data.CurrentState)}";
            if (textTask != null) textTask.text = $"任务: {data.CurrentTask}";
            if (textCarrying != null)
            {
                string carry = data.CarryingType.HasValue
                    ? $"{data.CarryingType.Value} ×{data.CarryingAmount:F0}"
                    : "空手";
                textCarrying.text = $"携带: {carry}";
            }
            if (textPosition != null) textPosition.text = $"位置: ({data.Position.x:F0}, {data.Position.y:F0})";

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
        /// 获取Agent类型的中文名称
        /// </summary>
        private static string GetTypeName(AgentType type)
        {
            return type switch
            {
                AgentType.Scout => "探索者",
                AgentType.Worker => "采集者",
                AgentType.Guard => "守卫",
                AgentType.Engineer => "工程师",
                AgentType.Archivist => "记录者",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取状态的中文名称
        /// </summary>
        private static string GetStateName(AgentState state)
        {
            return state switch
            {
                AgentState.Idle => "闲置",
                AgentState.Exploring => "探索中",
                AgentState.Gathering => "采集中",
                AgentState.ReturningToBase => "返回基地",
                AgentState.InCombat => "战斗中",
                AgentState.Fleeing => "逃跑中",
                AgentState.Resting => "休息中",
                AgentState.Guarding => "护卫中",
                AgentState.Patrolling => "巡逻中",
                _ => "未知"
            };
        }
    }
}
