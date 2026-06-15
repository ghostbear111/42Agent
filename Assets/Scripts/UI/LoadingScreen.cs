/// <summary>
/// 全屏 Loading 遮罩（运行时自构建）
/// 用于地图生成、场景加载、存档创建等同步耗时阶段的视觉反馈，避免界面黑屏/卡死无提示。
///
/// 设计：
/// - 单例 MonoBehaviour，懒加载 + DontDestroyOnLoad，跨场景存活。
///   地图生成在 MapGeneration 场景开始，场景切换后到 GameScene 才完成初始化，
///   遮罩必须跨场景存活才能持续覆盖整个过程（由 GameScene 在初始化完成后收起）。
/// - 自带独立 ScreenSpaceOverlay Canvas（sortingOrder 极高），盖在所有 UI 之上，
///   且 raycastTarget 拦截点击，防止生成期间误触底层 UI。
/// - 全部 UI 用 RuntimeUIBuilder 运行时自构建（无 prefab），符合项目 UI 规范。
/// - 进度条用 Image.Type.Filled + fillAmount，调用方传 0~1 的进度估值即可。
///
/// 用法：
///   LoadingScreen.Show("正在生成星球…", 0.1f);   // 显示并更新文本/进度
///   ... 耗时操作 ...
///   LoadingScreen.Show("正在创建存档…", 0.7f);
///   LoadingScreen.Hide();                          // 收起
///
/// 注意：地图生成（MapGenerator.Generate）是同步阻塞的，生成期间进度条不会动；
/// 因此调用方应在生成前先 Show 并 yield 一帧让遮罩渲染出来，再执行生成。
/// </summary>
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class LoadingScreen : MonoBehaviour
    {
        // ==================== UI 引用 ====================

        private GameObject _root;       // 全屏遮罩根（含半透明背景）
        private Text _tipText;          // 阶段提示文本（动态更新）
        private Image _barFill;         // 进度条填充（fillAmount 控制）
        private Text _percentText;      // 百分比文本
        private bool _built;            // 遮罩是否已构建（幂等）

        // ==================== 单例 ====================

        private static LoadingScreen _instance;

        private static LoadingScreen Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[LoadingScreen]");

                    // 自带独立 Overlay Canvas：sortingOrder 极高，盖住所有场景 UI
                    var canvas = go.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 10000;
                    var scaler = go.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    scaler.matchWidthOrHeight = 0.5f;
                    go.AddComponent<GraphicRaycaster>();

                    _instance = go.AddComponent<LoadingScreen>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ==================== 构建 ====================

        /// <summary>运行时构建全屏遮罩（幂等）</summary>
        private void Build()
        {
            if (_built) return;
            _built = true;

            RuntimeUIBuilder.EnsureEventSystem();

            // 全屏暗色背景（拦截点击，防止误触底层 UI）
            _root = new GameObject("LoadingOverlay");
            _root.transform.SetParent(transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.06f, 0.96f);
            bg.raycastTarget = true;

            // 标题（固定）
            RuntimeUIBuilder.CreateText("Title", _root.transform, "加载中", 34,
                new Color(0.9f, 0.85f, 0.4f), TextAnchor.MiddleCenter,
                0.2f, 0.58f, 0.8f, 0.66f);

            // 阶段提示文本（动态）
            _tipText = RuntimeUIBuilder.CreateText("Tip", _root.transform, "", 22,
                new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter,
                0.1f, 0.46f, 0.9f, 0.54f);

            // 进度条底
            MakeImage("BarBg", _root.transform, new Color(0.1f, 0.1f, 0.14f, 1f),
                0.15f, 0.40f, 0.85f, 0.435f);
            // 进度条填充（Filled，左侧起填）
            _barFill = MakeImage("BarFill", _root.transform, new Color(0.4f, 0.7f, 0.95f, 1f),
                0.15f, 0.40f, 0.85f, 0.435f);
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillOrigin = 0;
            _barFill.fillAmount = 0f;

            // 百分比文本（进度条下方）
            _percentText = RuntimeUIBuilder.CreateText("Percent", _root.transform, "0%", 16,
                new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleCenter,
                0.15f, 0.35f, 0.85f, 0.40f);

            Debug.Log("[LoadingScreen] 遮罩构建完成");
        }

        // ==================== 对外静态接口 ====================

        /// <summary>
        /// 显示/更新 Loading 遮罩。
        /// </summary>
        /// <param name="tip">当前阶段提示文本</param>
        /// <param name="progress">进度估值 0~1（&lt;0 时仅更新文本、不动进度条）</param>
        public static void Show(string tip, float progress)
        {
            var inst = Instance;
            inst.Build();
            inst._root.SetActive(true);
            if (inst._tipText != null) inst._tipText.text = tip ?? "";
            if (progress >= 0f)
            {
                float clamped = Mathf.Clamp01(progress);
                if (inst._barFill != null) inst._barFill.fillAmount = clamped;
                if (inst._percentText != null)
                    inst._percentText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
        }

        /// <summary>隐藏遮罩（不销毁，便于复用）</summary>
        public static void Hide()
        {
            if (_instance == null || _instance._root == null) return;
            _instance._root.SetActive(false);
        }

        // ==================== 辅助 ====================

        /// <summary>创建一个带 Image 的矩形 UI 元素（用于进度条底/填充）</summary>
        private static Image MakeImage(string name, Transform parent, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
