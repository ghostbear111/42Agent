/// <summary>
/// 科技树面板（运行时自构建）
/// 由 GameHUD 在 BuildUI 时创建（全屏容器 + AddComponent + BuildUI），初始隐藏。
/// 入口：基地信息面板的"科技树"按钮 → GameHUD.ShowTechTree()。
///
/// 结构：
/// ┌──────── 全屏半透明遮罩 ────────┐
/// │ ┌──────── 居中科技树窗口 ────────┐ │
/// │ │ 标题：科技树            [X]    │ │
/// │ │ ┌──── 可滚动节点列表 ────┐    │ │
/// │ │ │ [已解锁/可解锁/锁定 行] │    │ │
/// │ │ │ 名称 状态 / 描述 消耗 / 解锁 │ │
/// │ │ │ ...                      │    │ │
/// │ │ └────────────────────────┘    │ │
/// │ └──────────────────────────────┘ │
/// └──────────────────────────────────┘
///
/// 三态：已解锁(绿底,无按钮) / 可解锁(蓝底,解锁按钮) / 锁定(暗底,显示前置)。
/// 解锁点击 → TechTreeManager.TryUnlock → 发 TechUnlockedEvent → 面板订阅后刷新。
/// 与 BaseInfoPanel/GameConfigPanel 同构（MakeFull + CreateScrollView + 行布局）。
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Tech;
using GalaxyAgent.World.Base;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class TechTreePanel : MonoBehaviour
    {
        /// <summary>全屏遮罩根（控制显隐）</summary>
        private GameObject _root;
        /// <summary>节点列表 ScrollView 的 Content</summary>
        private Transform _content;
        /// <summary>当前关联的基地（解锁扣资源用）</summary>
        private BaseController _base;
        private bool _built;
        private bool _subscribed;

        /// <summary>面板是否可见</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>构建UI（幂等，由 GameHUD.BuildUI 调用）</summary>
        public void BuildUI(Transform parent)
        {
            if (_built) return;
            _built = true;

            // 全屏半透明遮罩，阻挡地图点击
            _root = MakeFull("TechTreeOverlay", parent);
            var overlay = _root.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.6f);

            // 居中科技树窗口
            var win = RuntimeUIBuilder.CreatePanel("TechTreeWindow", _root.transform,
                new Color(0.07f, 0.07f, 0.13f, 0.98f), 0.1f, 0.05f, 0.9f, 0.95f);

            // 标题
            RuntimeUIBuilder.CreateText("Title", win.transform,
                "科技树", 22, new Color(0.4f, 0.8f, 1f),
                TextAnchor.MiddleCenter, 0f, 0.94f, 0.85f, 0.99f);

            // 关闭按钮
            var btnClose = RuntimeUIBuilder.CreateButton("BtnClose", win.transform,
                "X", new Color(0.6f, 0.2f, 0.2f), 0.85f, 0.94f, 1f, 0.99f);
            btnClose.onClick.AddListener(Hide);

            // 可滚动节点列表
            _content = RuntimeUIBuilder.CreateScrollView("NodeList", win.transform,
                new Color(0.05f, 0.05f, 0.1f, 0.9f), 0.03f, 0.05f, 0.97f, 0.92f);

            _root.SetActive(false);
        }

        private void Start()
        {
            // 订阅科技解锁事件，解锁后若面板可见则刷新
            if (!_subscribed)
            {
                EventBus.Subscribe<TechUnlockedEvent>(OnTechUnlocked);
                _subscribed = true;
            }
        }

        private void OnTechUnlocked(TechUnlockedEvent e)
        {
            if (IsVisible) RefreshList();
        }

        /// <summary>显示面板并刷新节点列表</summary>
        public void Show(BaseController baseController)
        {
            _base = baseController;
            RefreshList();
            if (_root != null) _root.SetActive(true);
            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content as RectTransform);
        }

        /// <summary>隐藏面板</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>清空并重建节点列表（三态显示）</summary>
        private void RefreshList()
        {
            if (_content == null) return;

            foreach (Transform child in _content)
                Destroy(child.gameObject);

            var mgr = TechTreeManager.Instance;
            var nodes = mgr?.AllNodes;
            if (nodes == null) return;

            foreach (var node in nodes)
                CreateNodeRow(node, mgr);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content as RectTransform);
        }

        /// <summary>创建单个节点行（背景色按三态 + 标题/状态 + 描述/成本 + 解锁按钮）</summary>
        private void CreateNodeRow(TechNode node, TechTreeManager mgr)
        {
            bool unlocked = mgr.IsUnlocked(node.Id);
            bool canUnlock = !unlocked && mgr.CanUnlock(node.Id, _base);

            // 行容器（VerticalLayoutGroup + 背景色）
            var row = new GameObject("row_" + node.Id);
            row.transform.SetParent(_content, false);
            row.AddComponent<RectTransform>();
            var vlg = row.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.padding = new RectOffset(6, 6, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var bg = row.AddComponent<Image>();
            bg.color = unlocked ? new Color(0.1f, 0.25f, 0.15f, 0.6f)
                     : canUnlock ? new Color(0.1f, 0.2f, 0.3f, 0.6f)
                     : new Color(0.15f, 0.1f, 0.1f, 0.5f);

            var rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = canUnlock ? 96 : 72;
            rowLayout.minHeight = canUnlock ? 96 : 72;

            // 标题行：名称（左伸缩） + 状态（右固定）
            var titleRow = new GameObject("title");
            titleRow.transform.SetParent(row.transform, false);
            titleRow.AddComponent<RectTransform>();
            var thlg = titleRow.AddComponent<HorizontalLayoutGroup>();
            thlg.childControlWidth = true;
            thlg.childControlHeight = true;
            thlg.childForceExpandWidth = true;
            thlg.childForceExpandHeight = false;

            var titleText = RuntimeUIBuilder.CreateText("name", titleRow.transform,
                node.DisplayName, 15, Color.white, TextAnchor.MiddleLeft);
            titleText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            string status = unlocked ? "[已解锁]" : canUnlock ? "[可解锁]" : "[锁定]";
            Color statusColor = unlocked ? new Color(0.4f, 0.9f, 0.5f)
                                : canUnlock ? new Color(0.9f, 0.85f, 0.3f)
                                : new Color(0.7f, 0.5f, 0.5f);
            var statusText = RuntimeUIBuilder.CreateText("status", titleRow.transform,
                status, 13, statusColor, TextAnchor.MiddleRight);
            var statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 80;
            statusLayout.minWidth = 80;
            statusLayout.flexibleWidth = 0f;

            // 详情：描述 + 成本（+ 锁定时显示缺失前置）
            string detail = node.Description;
            if (!unlocked)
            {
                var missing = node.Prerequisites.FindAll(p => !mgr.IsUnlocked(p));
                if (missing.Count > 0) detail += $"  (需先解锁: {string.Join(",", missing)})";
            }
            string costStr = node.Cost.Count == 0 ? "免费"
                : string.Join(" ", node.Cost.ConvertAll(c => $"{GetResourceName(c.Resource)}×{c.Amount:F0}"));
            detail += $"\n消耗: {costStr}";

            var descText = RuntimeUIBuilder.CreateText("desc", row.transform,
                detail, 12, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft);
            var descLayout = descText.gameObject.AddComponent<LayoutElement>();
            descLayout.preferredHeight = 34;
            descLayout.minHeight = 34;

            // 解锁按钮（仅可解锁时）
            if (canUnlock)
            {
                var btn = RuntimeUIBuilder.CreateButton("btnUnlock", row.transform,
                    "解锁", new Color(0.15f, 0.45f, 0.25f), 0f, 0f, 1f, 1f);
                var btnLayout = btn.gameObject.AddComponent<LayoutElement>();
                btnLayout.preferredHeight = 26;
                btnLayout.minHeight = 26;
                btn.onClick.AddListener(() => OnUnlockClicked(node.Id));
            }
        }

        /// <summary>解锁按钮回调：尝试解锁并刷新列表</summary>
        private void OnUnlockClicked(string techId)
        {
            var mgr = TechTreeManager.Instance;
            if (mgr == null || _base == null) return;
            if (mgr.TryUnlock(techId, _base, out string reason))
                Debug.Log($"[TechTreePanel] 解锁成功: {techId}");
            else
                Debug.LogWarning($"[TechTreePanel] 解锁失败 {techId}: {reason}");
            RefreshList();
        }

        /// <summary>资源类型中文名（读自 ResourceConfigStore）</summary>
        private static string GetResourceName(ResourceType t)
            => ResourceConfigStore.GetDisplayName(t);

        /// <summary>创建撑满父级的 RectTransform 容器（普通 Transform 在 Canvas 下无尺寸）</summary>
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

        private void OnDestroy()
        {
            if (_subscribed)
            {
                EventBus.Unsubscribe<TechUnlockedEvent>(OnTechUnlocked);
                _subscribed = false;
            }
        }
    }
}
