/// <summary>
/// 资源配置 CSV ⇄ ResourceConfigData 双向转换（纯静态函数）
/// 供编辑器窗口的"导入 CSV / 导出 CSV"使用，一行一资源。
///
/// CSV 表头：Type,DisplayName,Description,Color,Gatherable,RequiredTech,CivLevel
/// - Color 用 hex（#RRGGBB 或 RRGGBB），Excel 友好
/// - RequiredTech 引用 TechNode.Id（空=无条件采集）
/// - 含逗号/引号的字段自动双引号转义
/// </summary>
using System;
using System.Collections.Generic;
using System.Text;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Tech
{
    public static class ResourceConfigCsvConverter
    {
        private static readonly string[] Header =
        {
            "Type", "DisplayName", "Description", "Color", "Gatherable", "RequiredTech", "CivLevel"
        };

        /// <summary>导出为 CSV 文本（一行一资源）</summary>
        public static string ToCsv(ResourceConfigData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Header));
            if (data?.Resources == null) return sb.ToString();
            foreach (var r in data.Resources)
            {
                sb.AppendLine(string.Join(",",
                    Enc(r.Type.ToString()), Enc(r.DisplayName), Enc(r.Description),
                    Enc(ColorToHex(r.Color)), Enc(r.Gatherable.ToString()),
                    Enc(r.RequiredTech ?? ""), Enc(r.CivLevel.ToString())));
            }
            return sb.ToString();
        }

        /// <summary>从 CSV 文本导入</summary>
        public static ResourceConfigData FromCsv(string text)
        {
            var data = new ResourceConfigData();
            if (string.IsNullOrEmpty(text)) return data;
            var lines = SplitLines(text);
            if (lines.Count == 0) return data;
            int start = IsHeader(lines[0]) ? 1 : 0;
            for (int li = start; li < lines.Count; li++)
            {
                var f = SplitCsvLine(lines[li]);
                if (f.Length < Header.Length) continue;
                string tStr = f[0].Trim();
                if (string.IsNullOrEmpty(tStr)) continue;
                if (!Enum.TryParse(tStr, true, out ResourceType rt)) continue;

                var cfg = new ResourceTypeConfig { Type = rt };
                if (!string.IsNullOrWhiteSpace(f[1])) cfg.DisplayName = f[1];
                if (!string.IsNullOrWhiteSpace(f[2])) cfg.Description = f[2];
                if (!string.IsNullOrWhiteSpace(f[3])) cfg.Color = HexToColor(f[3]);
                if (bool.TryParse(f[4], out bool g)) cfg.Gatherable = g;
                cfg.RequiredTech = f[5] ?? "";
                if (Enum.TryParse(f[6], true, out CivLevel cv)) cfg.CivLevel = cv;
                data.Resources.Add(cfg);
            }
            return data;
        }

        // ==================== Color hex ====================

        private static string ColorToHex(Color c)
        {
            var c32 = (Color32)c;
            return $"{c32.r:X2}{c32.g:X2}{c32.b:X2}";
        }

        private static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            hex = hex.TrimStart('#').Trim();
            if (hex.Length < 6) return Color.white;
            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new Color32(r, g, b, 255);
            }
            catch { return Color.white; }
        }

        // ==================== CSV 解析辅助（与 TechCsvConverter 同款） ====================

        private static bool IsHeader(string line)
            => line != null && line.StartsWith("Type", StringComparison.Ordinal);

        private static List<string> SplitLines(string text)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"') { inQuote = !inQuote; sb.Append(c); }
                else if (c == '\r') continue;
                else if (c == '\n' && !inQuote) { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

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

        private static string Enc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
