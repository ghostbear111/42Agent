/// <summary>
/// StyleTest 风格试验场控制器
/// ----------------------------------------------------------------------------
/// 用同一份小地图(64×64,固定种子)数据，并排渲染 N 种科幻/地图绘制风格供用户挑选。
/// 行数按风格总数动态计算(MapStylePalette.All.Count / COLS 向上取整)，目前 20 种 → 4 行×5 列。
/// 每种风格带 GPU 动画(ScanlineFx shader)：扫描带/雷达旋转/呼吸/闪烁。
/// 自包含：Awake 里自建相机/灯光/Grid——场景只需一个挂了本脚本的空 GameObject。
/// 数据层完全复用 MapGenerator(生成) + NoiseGenerator(补高度噪声图)，不改动正式游戏代码。
/// 渲染原理：所有格子共用一个白色基础 Tile，靠 Tilemap 的 per-cell SetColor 染色(连续伪色零资产膨胀)；
///           动画由每风格独立材质(ScanlineFx shader)在 GPU 处理，零 CPU 每帧开销。
/// 动画时间：shader 用自定义 _FxTime，本脚本 Update() 每帧设为 Time.time 推进动画。
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GalaxyAgent.StyleTest
{
    public class StyleTestController : MonoBehaviour
    {
        // ---- 试验参数 ----
        private const int MAP = 64;             // 小地图边长(格)，与 MapSize.Preview 对应
        private const int SEED = 1337;          // 固定种子：所有风格对比必须用同一份数据
        private const int COLS = 5;             // 网格列数(行数按风格总数动态算)
        private const float GAP = 16f;          // 预览之间间距(世界单位)

        // 共享白色基础 Tile(所有格子共用，靠 SetColor 染色)
        private Tile _whiteTile;
        // 动画 shader(所有动态风格共用，每风格建独立材质实例)
        private Shader _fxShader;
        // 所有动态材质引用：Update 每帧推进 _FxTime，让 shader 动画随游戏时间播放
        private readonly List<Material> _fxMats = new List<Material>();

        /// <summary>
        /// 启动：自建场景基础设施 + 生成数据 + 并排渲染所有风格(含动画材质)
        /// </summary>
        private void Awake()
        {
            BuildWhiteTile();
            _fxShader = Resources.Load<Shader>("Shaders/ScanlineFx");
            if (_fxShader == null)
                Debug.LogWarning("[StyleTest] 未找到 ScanlineFx shader，将无动画效果(检查 Assets/Resources/Shaders/ScanlineFx.shader)");

            EnsureCamera();
            EnsureLight();

            // 1. 生成小地图数据(复用正式地图生成器)
            var config = ScriptableObject.CreateInstance<MapConfig>();
            config.MapSize = MapSize.Preview;   // 64×64
            config.TileSize = TilePixelSize.Size32;
            config.Seed = SEED;
            config.Terrain = TerrainComplexity.Rich;
            config.Resources = ResourceAbundance.Moderate;
            config.Risk = RiskLevel.Medium;

            var mapGen = new MapGenerator(config, SEED);
            mapGen.Generate();

            // 2. 补一份高度噪声图(确定性与 MapGenerator 内部一致)，供高度相关风格用
            var heightMap = new float[MAP, MAP];
            for (int x = 0; x < MAP; x++)
            {
                for (int y = 0; y < MAP; y++)
                {
                    heightMap[x, y] = NoiseGenerator.GenerateNoise(x, y, SEED);
                }
            }

            // 3. 建 Grid 容器
            var gridGO = new GameObject("StyleGrid");
            gridGO.AddComponent<Grid>();

            // 4. 布局参数：网格居中于原点(行数按风格总数动态算)
            var styles = MapStylePalette.All;
            float step = MAP + GAP;
            int rows = Mathf.CeilToInt(styles.Count / (float)COLS);
            float originX = -(COLS - 1) * step * 0.5f;
            float originY = (rows - 1) * step * 0.5f;

            // 5. 逐风格创建 Tilemap 并渲染
            for (int i = 0; i < styles.Count; i++)
            {
                int col = i % COLS;
                int row = i / COLS;
                var style = styles[i];

                var go = new GameObject($"S{i}_{style.Name}");
                go.transform.SetParent(gridGO.transform, false);
                var tm = go.AddComponent<Tilemap>();
                var tr = go.AddComponent<TilemapRenderer>();
                tr.sortOrder = TilemapRenderer.SortOrder.TopLeft;

                go.transform.position = new Vector3(originX + col * step, originY - row * step, 0f);

                RenderStyle(tm, mapGen, heightMap, style);
                ApplyFx(tr, style.Fx);   // 赋动态材质

                MakeLabel(go.transform, style.Name, new Vector3(MAP * 0.5f, MAP + 4f, 0f));
            }

            Debug.Log($"[StyleTest] 渲染完成：{styles.Count} 种风格({rows}×{COLS})，地图 {MAP}×{MAP}，种子 {SEED}，动画shader={(_fxShader!=null?"OK":"缺失")}");
        }

        /// <summary>
        /// 每帧推进动画时间(正常播放时 Time.time 随帧增长，shader 动画随之播放)
        /// </summary>
        private void Update()
        {
            if (_fxMats.Count == 0) return;
            float t = Time.time;
            for (int i = 0; i < _fxMats.Count; i++)
            {
                if (_fxMats[i] != null) _fxMats[i].SetFloat("_FxTime", t);
            }
        }

        /// <summary>
        /// 用指定风格把整张地图渲染到一个 Tilemap
        /// 所有格共用 _whiteTile，per-cell SetColor 实现连续伪色
        /// </summary>
        private void RenderStyle(Tilemap tm, MapGenerator mapGen, float[,] heightMap, MapStyle style)
        {
            const TileFlags flags = TileFlags.None; // 必须解锁，否则 SetColor 被锁定不生效
            for (int x = 0; x < MAP; x++)
            {
                for (int y = 0; y < MAP; y++)
                {
                    var pos = new Vector3Int(x, y, 0);
                    var tileData = mapGen.GetTileAt(x, y);
                    if (tileData == null) continue;

                    Color c = style.ColorOf(tileData, heightMap, x, y);
                    tm.SetTile(pos, _whiteTile);
                    tm.SetTileFlags(pos, flags);
                    tm.SetColor(pos, c);
                }
            }
        }

        /// <summary>
        /// 给 TilemapRenderer 赋动态材质(按风格 Fx 设 ScanlineFx shader 参数)
        /// 每风格独立材质实例，参数各异；shader 缺失则跳过(降级为静态)
        /// </summary>
        private void ApplyFx(TilemapRenderer tr, StyleFx fx)
        {
            if (_fxShader == null || fx == null) return;
            var mat = new Material(_fxShader);
            mat.SetFloat("_Mode", fx.Mode);
            mat.SetFloat("_ScanFreq", fx.ScanFreq);
            mat.SetFloat("_ScanSpeed", fx.ScanSpeed);
            mat.SetFloat("_ScanAmp", fx.ScanAmp);
            mat.SetFloat("_ScanWidth", fx.ScanWidth);
            mat.SetColor("_ScanColor", fx.ScanColor);
            mat.SetFloat("_CenterX", fx.CenterX);
            mat.SetFloat("_CenterY", fx.CenterY);
            mat.SetFloat("_PulseAmp", fx.PulseAmp);
            mat.SetFloat("_PulseSpeed", fx.PulseSpeed);
            mat.SetFloat("_FlickerAmp", fx.FlickerAmp);
            mat.SetFloat("_FlickerSpeed", fx.FlickerSpeed);
            mat.SetFloat("_FxTime", 0f);
            tr.material = mat;
            _fxMats.Add(mat);   // 登记，供 Update 推进动画时间
        }

        /// <summary>
        /// 在预览上方创建风格名标签(世界空间 3D 文字)
        /// </summary>
        private void MakeLabel(Transform parent, string text, Vector3 localPos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 1.1f;
            tm.fontSize = 48;
            tm.color = new Color(0.85f, 0.9f, 1f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 10;
        }

        // ==================== 基础设施自建 ====================

        /// <summary>创建共享白色基础 Tile(32×32 纯白纹理，靠 SetColor 染色)</summary>
        private void BuildWhiteTile()
        {
            int size = 32;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);

            _whiteTile = ScriptableObject.CreateInstance<Tile>();
            _whiteTile.sprite = sprite;
            _whiteTile.color = Color.white;
        }

        /// <summary>确保场景有正交相机，自适应看全所有预览(按风格总数算行数)，深空底色</summary>
        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.08f);

            int rows = Mathf.CeilToInt(MapStylePalette.All.Count / (float)COLS);
            float totalW = COLS * MAP + (COLS - 1) * GAP;
            float totalH = rows * MAP + (rows - 1) * GAP;
            float aspect = Mathf.Max(0.0001f, (float)Screen.width / Screen.height);
            float halfHbyWidth = (totalW * 0.5f + 12f) / aspect;
            float halfHbyHeight = totalH * 0.5f + 10f;
            cam.orthographicSize = Mathf.Max(halfHbyWidth, halfHbyHeight);

            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        /// <summary>确保场景有方向光(2D Tilemap 默认不受光，留作氛围与惯例)</summary>
        private void EnsureLight()
        {
            if (FindAnyObjectByType<Light>() != null) return;
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1f;
        }
    }
}
