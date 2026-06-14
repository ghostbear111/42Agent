/// <summary>
/// 科技树 / 资源配置编辑器窗口（仅编辑器）
/// 菜单 Tools/科技树 (TechTree) 打开。两个 Tab：
/// - 科技树：CSV ⇄ 科技树资产(ScriptableObject) ⇄ tech_tree.json，节点按文明等级(CivLevel)分组显示
/// - 资源：CSV ⇄ resource_config.json，配置每种资源的展示属性/可采集性/采集所需科技/文明归属
///
/// CSV 导出统一用 UTF-8 with BOM，避免 Excel 打开中文乱码。
/// 改资产或 CSV 后必须「烘焙到 JSON」运行时才生效。
/// </summary>
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GalaxyAgent.Editor
{
    public class TechTreeEditorWindow : EditorWindow
    {
        private int _tab; // 0=科技树, 1=资源
        private GalaxyAgent.Tech.TechTreeData _data;
        private GalaxyAgent.Tech.ResourceConfigData _resData;
        private Vector2 _scroll;
        private GalaxyAgent.Tech.TechTreeAsset _asset;

        [MenuItem("Tools/科技树 (TechTree)")]
        public static void Open()
        {
            GetWindow<TechTreeEditorWindow>("科技树 / 资源");
        }

        private void OnEnable()
        {
            _data = GalaxyAgent.Tech.TechTreeStore.Load();
            _resData = GalaxyAgent.Tech.ResourceConfigStore.Load();
        }

        private void OnGUI()
        {
            if (_data == null) _data = GalaxyAgent.Tech.TechTreeStore.Load();
            if (_resData == null) _resData = GalaxyAgent.Tech.ResourceConfigStore.Load();

            GUILayout.Label("科技树 / 资源配置", EditorStyles.boldLabel);
            _tab = GUILayout.Toolbar(_tab, new[] { "科技树", "资源" });
            EditorGUILayout.Space();
            if (_tab == 0) DrawTechTreeTab();
            else DrawResourceTab();
        }

        // ==================== 科技树 Tab ====================

        private void DrawTechTreeTab()
        {
            EditorGUILayout.HelpBox(
                "运行时真相：\n" + GalaxyAgent.Tech.TechTreeStore.GetPath() + "\n\n" +
                "三态：CSV ⇄ 科技树资产(ScriptableObject) ⇄ JSON。改后需「烘焙到JSON」生效。\n" +
                "节点按文明等级(CivLevel)分组显示。",
                MessageType.Info);
            DrawTechButtons();
            EditorGUILayout.Space();
            GUILayout.Label($"节点列表（按文明分组，{_data?.Nodes?.Count ?? 0} 项）", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, true);
            DrawNodesByCiv();
            EditorGUILayout.EndScrollView();
        }

        private void DrawTechButtons()
        {
            GUILayout.Label("CSV 表格", EditorStyles.miniBoldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("导入 CSV", GUILayout.Height(26)))
            {
                string p = EditorUtility.OpenFilePanel("选择科技树 CSV", "", "csv");
                if (!string.IsNullOrEmpty(p))
                {
                    _data = GalaxyAgent.Tech.TechCsvConverter.FromCsv(System.IO.File.ReadAllText(p));
                    Debug.Log($"[TechTreeEditor] 导入 CSV: {_data.Nodes.Count} 节点");
                }
            }
            if (GUILayout.Button("导出 CSV", GUILayout.Height(26)))
            {
                string p = EditorUtility.SaveFilePanel("保存科技树 CSV", "", "tech_tree", "csv");
                if (!string.IsNullOrEmpty(p))
                {
                    System.IO.File.WriteAllText(p, GalaxyAgent.Tech.TechCsvConverter.ToCsv(_data),
                        new System.Text.UTF8Encoding(true));
                    Debug.Log($"[TechTreeEditor] 导出 CSV: {p}");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("JSON（运行时真相）", EditorStyles.miniBoldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("烘焙到 JSON（运行时生效）", GUILayout.Height(26)))
                GalaxyAgent.Tech.TechTreeStore.Save(_data);
            if (GUILayout.Button("从 JSON 载入", GUILayout.Height(26)))
                _data = GalaxyAgent.Tech.TechTreeStore.Load();
            if (GUILayout.Button("重置默认", GUILayout.Height(26)))
                _data = GalaxyAgent.Tech.TechTreeStore.CreateDefault();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            _asset = (GalaxyAgent.Tech.TechTreeAsset)EditorGUILayout.ObjectField(
                "科技树资产", _asset, typeof(GalaxyAgent.Tech.TechTreeAsset), false);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("从资产载入", GUILayout.Height(22)) && _asset != null)
                _data = DeepCopy(new GalaxyAgent.Tech.TechTreeData { Nodes = _asset.Nodes });
            if (GUILayout.Button("保存到资产", GUILayout.Height(22)) && _asset != null)
            {
                _asset.Nodes = DeepCopy(_data).Nodes;
                EditorUtility.SetDirty(_asset);
                AssetDatabase.SaveAssets();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>按 CivLevel 分组绘制节点（文明分组）</summary>
        private void DrawNodesByCiv()
        {
            var groups = new Dictionary<GalaxyAgent.Tech.CivLevel, List<GalaxyAgent.Tech.TechNode>>();
            foreach (var n in _data?.Nodes ?? new List<GalaxyAgent.Tech.TechNode>())
            {
                if (!groups.ContainsKey(n.CivLevel)) groups[n.CivLevel] = new List<GalaxyAgent.Tech.TechNode>();
                groups[n.CivLevel].Add(n);
            }
            foreach (GalaxyAgent.Tech.CivLevel civ in System.Enum.GetValues(typeof(GalaxyAgent.Tech.CivLevel)))
            {
                if (!groups.ContainsKey(civ)) continue;
                EditorGUILayout.LabelField($"── 文明等级：{civ}（{groups[civ].Count} 项）──",
                    EditorStyles.boldLabel);
                foreach (var n in groups[civ]) DrawNode(n);
                EditorGUILayout.Space();
            }
        }

        private void DrawNode(GalaxyAgent.Tech.TechNode n)
        {
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.LabelField($"{n.Id} — {n.DisplayName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("描述", n.Description);
            EditorGUILayout.LabelField("类别/文明", $"{n.Category} / {n.CivLevel}");
            EditorGUILayout.LabelField("前置", n.Prerequisites.Count == 0 ? "无" : string.Join(";", n.Prerequisites));
            EditorGUILayout.LabelField("消耗", n.Cost.Count == 0
                ? "无"
                : string.Join(", ", n.Cost.ConvertAll(c => $"{c.Resource}×{c.Amount}")));
            EditorGUILayout.LabelField("效果", n.Effects.Count == 0
                ? "无"
                : string.Join(", ", n.Effects.ConvertAll(e => $"{e.Type}={e.Value}@{e.Target}")));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // ==================== 资源 Tab ====================

        private void DrawResourceTab()
        {
            EditorGUILayout.HelpBox(
                "运行时真相：\n" + GalaxyAgent.Tech.ResourceConfigStore.GetPath() + "\n\n" +
                "配置每种资源的展示属性、可采集性、采集所需科技、文明归属。\n" +
                "RequiredTech 填 TechNode.Id（如 perception_boost），留空=无条件可采。\n" +
                "改后需「烘焙到JSON」生效。",
                MessageType.Info);
            DrawResourceButtons();
            EditorGUILayout.Space();
            GUILayout.Label($"资源列表（{_resData?.Resources?.Count ?? 0} 项）", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, true);
            DrawResourceList();
            EditorGUILayout.EndScrollView();
        }

        private void DrawResourceButtons()
        {
            GUILayout.Label("CSV 表格", EditorStyles.miniBoldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("导入 CSV", GUILayout.Height(26)))
            {
                string p = EditorUtility.OpenFilePanel("选择资源 CSV", "", "csv");
                if (!string.IsNullOrEmpty(p))
                {
                    _resData = GalaxyAgent.Tech.ResourceConfigCsvConverter.FromCsv(System.IO.File.ReadAllText(p));
                    Debug.Log($"[ResourceEditor] 导入 CSV: {_resData.Resources.Count} 资源");
                }
            }
            if (GUILayout.Button("导出 CSV", GUILayout.Height(26)))
            {
                string p = EditorUtility.SaveFilePanel("保存资源 CSV", "", "resource_config", "csv");
                if (!string.IsNullOrEmpty(p))
                {
                    System.IO.File.WriteAllText(p, GalaxyAgent.Tech.ResourceConfigCsvConverter.ToCsv(_resData),
                        new System.Text.UTF8Encoding(true));
                    Debug.Log($"[ResourceEditor] 导出 CSV: {p}");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("JSON（运行时真相）", EditorStyles.miniBoldLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("烘焙到 JSON（运行时生效）", GUILayout.Height(26)))
            {
                GalaxyAgent.Tech.ResourceConfigStore.Save(_resData);
                GalaxyAgent.Tech.ResourceConfigStore.InvalidateCache();
            }
            if (GUILayout.Button("从 JSON 载入", GUILayout.Height(26)))
            {
                GalaxyAgent.Tech.ResourceConfigStore.InvalidateCache();
                _resData = GalaxyAgent.Tech.ResourceConfigStore.Load();
            }
            if (GUILayout.Button("重置默认", GUILayout.Height(26)))
                _resData = GalaxyAgent.Tech.ResourceConfigStore.CreateDefault();
            GUILayout.EndHorizontal();
        }

        /// <summary>资源列表（颜色色块 + 属性显示 + Gatherable/RequiredTech/CivLevel 可编辑）</summary>
        private void DrawResourceList()
        {
            if (_resData?.Resources == null) return;
            foreach (var r in _resData.Resources)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                // 颜色色块 + 类型/名称
                var rect = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 22, 22), r.Color);
                EditorGUI.LabelField(new Rect(rect.x + 28, rect.y + 2, rect.width - 28, 20),
                    $"{r.Type} — {r.DisplayName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("描述", r.Description);
                // 可编辑字段
                r.Gatherable = EditorGUILayout.Toggle("可采集", r.Gatherable);
                r.RequiredTech = EditorGUILayout.TextField("采集所需科技 Id", r.RequiredTech);
                r.CivLevel = (GalaxyAgent.Tech.CivLevel)EditorGUILayout.EnumPopup("文明归属", r.CivLevel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        // ==================== 辅助 ====================

        private static GalaxyAgent.Tech.TechTreeData DeepCopy(GalaxyAgent.Tech.TechTreeData src)
        {
            if (src == null) return new GalaxyAgent.Tech.TechTreeData();
            string json = JsonUtility.ToJson(src);
            var copy = JsonUtility.FromJson<GalaxyAgent.Tech.TechTreeData>(json);
            if (copy.Nodes == null) copy.Nodes = new List<GalaxyAgent.Tech.TechNode>();
            return copy;
        }
    }
}
#endif
