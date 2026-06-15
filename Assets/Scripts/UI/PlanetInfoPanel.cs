/// <summary>
/// 星球介绍档案面板（游戏内）
/// 由 GameHUD 顶栏「星球名」按钮触发，展示 LLM 生成的星球介绍 + 各环境参数。
/// 介绍文本来自存档 PlanetDescription（LLM 创建星球时生成，加载存档时恢复）。
///
/// 结构：
/// ┌──────── 全屏半透明遮罩 ────────┐
/// │ ┌────── 居中档案窗口 ──────┐   │
/// │ │ 标题：星球档案：xxx        │   │
/// │ │ ┌── 介绍滚动区 ──┐         │   │
/// │ │ │ 环境参数 + 介绍  │         │   │
/// │ │ └────────────────┘         │   │
/// │ │ [关闭]                     │   │
/// │ └────────────────────────────┘   │
/// └────────────────────────────────┘
/// </summary>
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class PlanetInfoPanel : MonoBehaviour
    {
        private GameObject _root;
        private Text _titleText;
        private Text _descText;

        /// <summary>面板是否可见</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>构建面板UI（幂等，由GameHUD.BuildUI调用一次）</summary>
        public void BuildUI(Transform parent)
        {
            RuntimeUIBuilder.EnsureEventSystem();

            // 全屏遮罩
            _root = MakeFull("PlanetInfoOverlay", parent);
            _root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // 居中窗口
            var win = RuntimeUIBuilder.CreatePanel("PlanetInfoWindow", _root.transform,
                new Color(0.07f, 0.07f, 0.13f, 0.98f), 0.2f, 0.12f, 0.8f, 0.88f);

            // 标题
            _titleText = RuntimeUIBuilder.CreateText("Title", win.transform, "星球档案", 24,
                new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleCenter, 0f, 0.91f, 1f, 0.99f);

            // 介绍滚动区（CreateScrollView 返回带 VerticalLayoutGroup+ContentSizeFitter 的 Content）
            var content = RuntimeUIBuilder.CreateScrollView("DescScroll", win.transform,
                new Color(0.04f, 0.05f, 0.08f, 0.9f), 0.04f, 0.30f, 0.96f, 0.89f);
            _descText = RuntimeUIBuilder.CreateText("Desc", content, "", 17,
                new Color(0.9f, 0.92f, 0.98f), TextAnchor.UpperLeft, 0, 0, 1, 1);
            // 自动换行 + 高度自适应，复用共享字体
            _descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _descText.verticalOverflow = VerticalWrapMode.Overflow;

            // 关闭按钮
            var btnClose = RuntimeUIBuilder.CreateButton("BtnClose", win.transform, "关闭",
                new Color(0.35f, 0.2f, 0.2f), 0.38f, 0.05f, 0.62f, 0.16f);
            btnClose.onClick.AddListener(Hide);

            _root.SetActive(false);
            Debug.Log("[PlanetInfoPanel] UI 构建完成");
        }

        /// <summary>显示面板：填充星球名、介绍、各参数</summary>
        public void Show(string planetName, string description, MapConfig config)
        {
            if (_root == null) return;

            if (_titleText != null)
                _titleText.text = $"星球档案：{planetName}";

            if (_descText != null)
            {
                string desc = string.IsNullOrWhiteSpace(description) ? "（暂无星球介绍）" : description;
                // 介绍正文前置参数概览，方便玩家一眼看到环境设定
                _descText.text = (config != null ? BuildParamText(config) + "\n\n" : "") + desc;
            }

            _root.SetActive(true);
            // 介绍可能较长，立即重建布局让滚动区正确计算高度
            if (_descText != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_descText.transform.parent as RectTransform);
        }

        /// <summary>隐藏面板</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>把 MapConfig 各环境字段拼成中文参数概览</summary>
        private static string BuildParamText(MapConfig c)
        {
            return $"<b>环境参数</b>\n" +
                   $"地图大小：{MapSizeText(c.MapSize)}    瓦片精度：{(int)c.TileSize}px\n" +
                   $"地形复杂度：{TerrainText(c.Terrain)}    资源丰富度：{ResourceText(c.Resources)}\n" +
                   $"风险等级：{RiskText(c.Risk)}    天气模式：{WeatherText(c.Weather)}\n" +
                   $"昼夜模式：{DayNightText(c.DayNight)}    种子：{c.Seed}";
        }

        private static string MapSizeText(MapSize s) => s switch
        {
            MapSize.Tiny => "微型(128²)", MapSize.Small => "小型(256²)", MapSize.Medium => "中型(512²)",
            MapSize.Large => "大型(1024²)", _ => "巨型(2048²)"
        };
        private static string TerrainText(TerrainComplexity t) => t switch
        {
            TerrainComplexity.Flat => "平坦", TerrainComplexity.Rich => "丰富", _ => "凶险"
        };
        private static string ResourceText(ResourceAbundance r) => r switch
        {
            ResourceAbundance.Scarce => "贫乏", ResourceAbundance.Moderate => "适中", _ => "富饶"
        };
        private static string RiskText(RiskLevel r) => r switch
        {
            RiskLevel.Low => "低", RiskLevel.Medium => "中", _ => "高"
        };
        private static string WeatherText(WeatherPattern w) => w switch
        {
            WeatherPattern.Mild => "温和", WeatherPattern.Variable => "多变", _ => "恶劣"
        };
        private static string DayNightText(DayNightMode d) => d switch
        {
            DayNightMode.EternalDay => "永昼", DayNightMode.Alternating => "交替", _ => "永夜"
        };

        /// <summary>创建撑满父级的RectTransform容器</summary>
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
