/// <summary>
/// 科技树 CSV ⇄ TechTreeData 双向转换（纯静态函数）
/// 供编辑器窗口的"导入 CSV / 导出 CSV"使用，让策划在 Excel 里编辑整棵科技树。
///
/// CSV 格式（一行 = 一个 Cost 或一个 Effect，同一节点跨多行按 Id 合并）：
///   Id,Category,DisplayName,Description,CivLevel,Prerequisites,CostResource,CostAmount,EffectTarget,EffectType,EffectValue
/// - Prerequisites 用分号分隔（如 "a;b;c"），无前置留空
/// - 一个节点的多成本/多效果跨多行；导入时按 Id 分组合并
/// - Cost 按 Resource 去重；Effect 按 (Type,Target,Value) 去重
/// - 含逗号/引号/换行的字段自动用双引号转义（标准 CSV）
///
/// 与 ScriptableObject 资产、tech_tree.json 三态协同：CSV 是给非程序员的"速配表格"。
/// </summary>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GalaxyAgent.Data.Enums;

namespace GalaxyAgent.Tech
{
    public static class TechCsvConverter
    {
        /// <summary>固定列顺序（增删列需同步 FromCsv 索引）</summary>
        private static readonly string[] Header =
        {
            "Id", "Category", "DisplayName", "Description", "CivLevel",
            "Prerequisites", "CostResource", "CostAmount", "EffectTarget", "EffectType", "EffectValue"
        };

        /// <summary>把科技树导出为 CSV 文本。每个节点展开为 max(Cost数, Effect数, 1) 行。</summary>
        public static string ToCsv(TechTreeData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Header));
            if (data == null || data.Nodes == null) return sb.ToString();

            foreach (var n in data.Nodes)
            {
                int costCount = n.Cost?.Count ?? 0;
                int effCount = n.Effects?.Count ?? 0;
                int rows = Math.Max(1, Math.Max(costCount, effCount));

                for (int i = 0; i < rows; i++)
                {
                    string costRes = "", costAmt = "";
                    if (n.Cost != null && i < costCount)
                    {
                        costRes = n.Cost[i].Resource.ToString();
                        costAmt = n.Cost[i].Amount.ToString(CultureInfo.InvariantCulture);
                    }
                    string eType = "", eTarget = "", eVal = "";
                    if (n.Effects != null && i < effCount)
                    {
                        eType = n.Effects[i].Type.ToString();
                        eTarget = n.Effects[i].Target.ToString();
                        eVal = n.Effects[i].Value.ToString(CultureInfo.InvariantCulture);
                    }
                    // 前置只在首行列出
                    string prereq = i == 0
                        ? string.Join(";", n.Prerequisites ?? new List<string>())
                        : "";

                    sb.AppendLine(string.Join(",",
                        Enc(n.Id), Enc(n.Category.ToString()), Enc(n.DisplayName), Enc(n.Description),
                        Enc(n.CivLevel.ToString()), Enc(prereq),
                        Enc(costRes), Enc(costAmt), Enc(eTarget), Enc(eType), Enc(eVal)));
                }
            }
            return sb.ToString();
        }

        /// <summary>从 CSV 文本导入科技树。按 Id 分组合并，Cost/Effect 去重。</summary>
        public static TechTreeData FromCsv(string text)
        {
            var data = new TechTreeData();
            if (string.IsNullOrEmpty(text)) return data;

            var lines = SplitLines(text);
            if (lines.Count == 0) return data;

            int start = IsHeader(lines[0]) ? 1 : 0;
            var order = new List<string>();
            var map = new Dictionary<string, TechNode>();

            for (int li = start; li < lines.Count; li++)
            {
                var f = SplitCsvLine(lines[li]);
                if (f.Length < Header.Length) continue;
                string id = f[0].Trim();
                if (string.IsNullOrEmpty(id)) continue;

                if (!map.TryGetValue(id, out var node))
                {
                    node = new TechNode
                    {
                        Id = id,
                        Prerequisites = new List<string>(),
                        Cost = new List<CostEntry>(),
                        Effects = new List<TechEffect>()
                    };
                    map[id] = node;
                    order.Add(id);
                }

                // 元数据（每行解析，后行覆盖前行；正常情况下各行一致）
                if (Enum.TryParse(f[1], true, out TechCategory cat)) node.Category = cat;
                if (!string.IsNullOrWhiteSpace(f[2])) node.DisplayName = f[2];
                if (!string.IsNullOrWhiteSpace(f[3])) node.Description = f[3];
                if (Enum.TryParse(f[4], true, out CivLevel civ)) node.CivLevel = civ;

                // 前置（分号分隔，去重）
                if (!string.IsNullOrWhiteSpace(f[5]))
                {
                    foreach (var p in f[5].Split(';'))
                    {
                        var pt = p.Trim();
                        if (pt.Length > 0 && !node.Prerequisites.Contains(pt))
                            node.Prerequisites.Add(pt);
                    }
                }

                // Cost（按 Resource 去重）
                if (!string.IsNullOrWhiteSpace(f[6])
                    && Enum.TryParse(f[6], true, out ResourceType rt)
                    && float.TryParse(f[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float amt))
                {
                    if (!node.Cost.Exists(c => c.Resource == rt))
                        node.Cost.Add(new CostEntry { Resource = rt, Amount = amt });
                }

                // Effect（按 Type+Target+Value 去重）
                if (!string.IsNullOrWhiteSpace(f[9])
                    && Enum.TryParse(f[9], true, out EffectType et)
                    && Enum.TryParse(f[8], true, out EffectTarget etg)
                    && float.TryParse(f[10], NumberStyles.Float, CultureInfo.InvariantCulture, out float ev))
                {
                    bool dup = node.Effects.Exists(e =>
                        e.Type == et && e.Target == etg && Math.Abs(e.Value - ev) < 0.0001f);
                    if (!dup)
                        node.Effects.Add(new TechEffect { Type = et, Target = etg, Value = ev });
                }
            }

            foreach (var id in order) data.Nodes.Add(map[id]);
            return data;
        }

        // ==================== CSV 解析辅助 ====================

        private static bool IsHeader(string line)
            => line != null && line.StartsWith("Id", StringComparison.Ordinal);

        /// <summary>按行拆分（处理引号内的换行；\r\n 与 \n 统一为一次断行）</summary>
        private static List<string> SplitLines(string text)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"') { inQuote = !inQuote; sb.Append(c); }
                else if (c == '\r') { continue; } // 跳过 \r，由 \n 统一断行
                else if (c == '\n' && !inQuote) { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        /// <summary>拆分单行字段（处理双引号转义，"" 表示字面引号）</summary>
        private static string[] SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuote = !inQuote;
                }
                else if (c == ',' && !inQuote) { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        /// <summary>CSV 字段转义：含逗号/引号/换行则双引号包裹，内部引号双写</summary>
        private static string Enc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
