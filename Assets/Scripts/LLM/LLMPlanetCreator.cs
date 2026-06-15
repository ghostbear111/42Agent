/// <summary>
/// LLM 星球创建器
/// 通过自然语言对话，让 LLM 理解玩家对星球的描述、与地球基准对比，
/// 推断出与「手动创建」完全相同的 MapConfig 字段（大小/瓦片/地形/资源/风险/天气/昼夜）+ 星球名，
/// 并产出一段给玩家看的自然语言汇报（"通过您的信息为您找到了「名称」星球"）。
///
/// 流程：
///   RequestCreation(玩家描述) → 构造 LLMRequest → 经 LLMManager 串行队列发送 →
///   回调里手写解析 content 中的 JSON → 枚举 TryParse 映射（失败回退地球基准）→ PlanetCreationResult
///
/// 设计要点：
/// - 复用 LLMManager 单一串行队列，与 Agent 高层决策共用同一 Ollama 连接，避免压垮服务
/// - agentId 用 "planet_creator"，对话记录独立存放，不污染各 Agent 日志
/// - JSON 解析不依赖 Newtonsoft（项目手写），逐字符提取，正确处理 \" \n \uXXXX 等转义
/// - 任一字段解析失败都回退地球基准，保证返回的结果始终可直接用于生成地图
/// </summary>
using System;
using GalaxyAgent.Config;
using GalaxyAgent.Data.Enums;

namespace GalaxyAgent.LLM
{
    public class LLMPlanetCreator
    {
        // 运行时配置访问（null安全回退）
        private static readonly GameConfig _fallbackConfig = new GameConfig();
        private static GameConfig Cfg => GameConfigManager.Instance != null
            ? GameConfigManager.Instance.Config : _fallbackConfig;

        /// <summary>地球基准星球名（解析不到名称时的兜底）</summary>
        private const string FALLBACK_NAME = "未知星球";

        /// <summary>
        /// 发起一次星球创建请求。
        /// 即使 LLM 不可用 / 描述为空，也会回调一个失败结果，调用方据此提示玩家。
        /// </summary>
        /// <param name="userDescription">玩家对星球的自然语言描述</param>
        /// <param name="callback">完成回调（主线程），传入解析后的结果</param>
        public void RequestCreation(string userDescription, Action<PlanetCreationResult> callback)
        {
            var mgr = LLMManager.Instance;
            if (mgr == null || !mgr.IsAvailable)
            {
                callback?.Invoke(new PlanetCreationResult
                {
                    Success = false,
                    Error = "LLM 未连接，请先在主菜单「设置」中连接 LLM"
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(userDescription))
            {
                callback?.Invoke(new PlanetCreationResult { Success = false, Error = "描述为空" });
                return;
            }

            var req = new LLMRequest
            {
                Model = "", // 留空，使用当前配置的模型
                SystemPrompt = PromptBuilder.BuildPlanetCreationSystemPrompt(),
                UserPrompt = PromptBuilder.BuildPlanetCreationUserPrompt(userDescription.Trim()),
                // 温度略低，让 JSON 输出更稳定
                Temperature = 0.6f,
                MaxTokens = Cfg.Llm.MaxTokens
            };

            mgr.EnqueueRequest("planet_creator", req, "星球创建",
                response => callback?.Invoke(Parse(response)));
        }

        // ==================== 响应解析 ====================

        /// <summary>
        /// 解析 LLM 响应为星球创建结果。
        /// 逐字段提取 + 枚举 TryParse，任一字段失败回退地球基准，保证最终结果可用。
        /// </summary>
        private static PlanetCreationResult Parse(LLMResponse response)
        {
            if (response == null || !response.Success || string.IsNullOrWhiteSpace(response.Content))
            {
                return new PlanetCreationResult
                {
                    Success = false,
                    Error = response?.Error ?? "LLM 未返回有效内容"
                };
            }

            string json = StripCodeFence(response.Content);

            // 以地球基准为模板，再逐字段覆盖
            var result = EarthBaseline();
            result.PlanetName = ExtractString(json, "planetName");
            result.Understanding = ExtractString(json, "understanding");
            result.Reasoning = ExtractString(json, "reasoning");
            result.Description = ExtractString(json, "description");

            // reasoning/description 最可能含枚举决策文字，作为 ParseEnum 的兜底全文
            string fullText = (result.Reasoning ?? "") + " " + (result.Description ?? "");

            result.MapSize = ParseEnum(json, "mapSize", result.MapSize, fullText);
            result.TileSize = ParseEnum(json, "tileSize", result.TileSize, fullText);
            result.Terrain = ParseEnum(json, "terrain", result.Terrain, fullText);
            result.Resources = ParseEnum(json, "resources", result.Resources, fullText);
            result.Risk = ParseEnum(json, "risk", result.Risk, fullText);
            result.Weather = ParseEnum(json, "weather", result.Weather, fullText);
            result.DayNight = ParseEnum(json, "dayNight", result.DayNight, fullText);

            // 名称 / 汇报为空时的兜底，保证 UI 始终有内容可显示
            if (string.IsNullOrWhiteSpace(result.PlanetName))
                result.PlanetName = FALLBACK_NAME;

            // 介绍兜底：description 为空时用一句话占位，保证游戏内档案不空白
            if (string.IsNullOrWhiteSpace(result.Description))
                result.Description = $"「{result.PlanetName}」是一颗等待探索的未知星球。";

            result.Success = true;
            return result;
        }

        /// <summary>地球基准结果模板（各项=地球值），作为解析失败的回退</summary>
        private static PlanetCreationResult EarthBaseline() => new PlanetCreationResult
        {
            MapSize = MapSize.Medium,
            TileSize = TilePixelSize.Size64,
            Terrain = TerrainComplexity.Rich,
            Resources = ResourceAbundance.Moderate,
            Risk = RiskLevel.Medium,
            Weather = WeatherPattern.Mild,
            DayNight = DayNightMode.Alternating
        };

        // ==================== 简易 JSON 解析（项目不用 Newtonsoft，手写逐字符提取） ====================

        /// <summary>剥离可能的 markdown 代码块围栏（```json ... ```），并截取第一个 { 到最后一个 }</summary>
        private static string StripCodeFence(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            string s = raw.Trim();

            // 剥离开头 ```json 或 ```
            if (s.StartsWith("```", StringComparison.Ordinal))
            {
                int nl = s.IndexOf('\n');
                s = nl >= 0 ? s.Substring(nl + 1) : s.Substring(3);
            }
            // 剥离结尾 ```
            if (s.EndsWith("```", StringComparison.Ordinal))
                s = s.Substring(0, s.Length - 3);

            s = s.Trim();
            int start = s.IndexOf('{');
            int end = s.LastIndexOf('}');
            if (start >= 0 && end > start)
                return s.Substring(start, end - start + 1);
            return s;
        }

        /// <summary>
        /// 提取字符串字段值。逐字符读取，正确处理 \" \\ \/ \n \t \uXXXX 等转义，
        /// 复用与 OllamaProvider.ExtractJsonStringValue 同款的稳健逻辑（应对 LLM 回复 JSON 内嵌转义引号）。
        /// </summary>
        private static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            string keyPattern = "\"" + key + "\"";
            int idx = json.IndexOf(keyPattern, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += keyPattern.Length;

            // 跳过冒号与空白
            while (idx < json.Length)
            {
                char c = json[idx];
                if (c == ':' || c == ' ' || c == '\t' || c == '\n' || c == '\r') { idx++; continue; }
                break;
            }
            if (idx >= json.Length || json[idx] != '"') return null;
            idx++; // 跳过开引号

            var sb = new System.Text.StringBuilder();
            while (idx < json.Length)
            {
                char c = json[idx];
                if (c == '\\' && idx + 1 < json.Length)
                {
                    char next = json[idx + 1];
                    // \uXXXX Unicode 转义（中文等），必须解码否则显示成 uXXXX
                    if (next == 'u' && idx + 6 <= json.Length)
                    {
                        string hex = json.Substring(idx + 2, 4);
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out int code))
                            sb.Append((char)code);
                        else
                            sb.Append(json.Substring(idx, 6));
                        idx += 6;
                        continue;
                    }
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        default: sb.Append(next); break;
                    }
                    idx += 2;
                    continue;
                }
                if (c == '"') break; // 未转义的结束引号
                sb.Append(c);
                idx++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 解析枚举字段（双保险）：
        /// 1) 优先从 JSON 独立字段提取（mapSize 等作为键）；
        /// 2) 失败则从 reasoning/description 全文用正则兜底——应对 LLM 把枚举值融进文字
        ///    （如 "mapSize保持Medium"、"terrain选Dangerous"）而未输出独立字段的情况；
        /// 3) 仍失败返回 fallback（地球基准）。
        /// </summary>
        private static T ParseEnum<T>(string json, string key, T fallback, string fullText) where T : struct
        {
            // 1. JSON 独立字段
            string raw = ExtractString(json, key);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                raw = raw.Trim().Trim('"');
                if (Enum.TryParse(raw, true, out T v)) return v;
            }

            // 2. reasoning/description 全文兜底：匹配 "字段名 + 少量中文字符/标点 + 枚举值"
            if (!string.IsNullOrEmpty(fullText))
            {
                string[] names = Enum.GetNames(typeof(T));
                // key 后允许 0~15 个非字母数字字符（中文/标点/空格），再匹配某个枚举名
                string pattern = key + "[^a-zA-Z0-9]{0,15}(" + string.Join("|", names) + ")";
                var m = System.Text.RegularExpressions.Regex.Match(
                    fullText, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success && m.Groups[1].Success &&
                    Enum.TryParse(m.Groups[1].Value, true, out T v2))
                    return v2;
            }

            return fallback;
        }
    }

    /// <summary>
    /// LLM 星球创建结果。
    /// 字段与手动创建（MapConfig）一一对应，外加给玩家看的对话文本（名称/汇报/对比）。
    /// </summary>
    [Serializable]
    public class PlanetCreationResult
    {
        /// <summary>是否成功</summary>
        public bool Success;
        /// <summary>失败原因（仅失败时有意义）</summary>
        public string Error = "";

        // ---- 给玩家看的自然语言（三段式）----
        /// <summary>星球名称（LLM 命名）</summary>
        public string PlanetName = "";
        /// <summary>对玩家意图的理解</summary>
        public string Understanding = "";
        /// <summary>为何如此创造（各维度相对地球的选择与理由）</summary>
        public string Reasoning = "";
        /// <summary>星球介绍档案（游戏内顶栏点击星球名可查看）</summary>
        public string Description = "";

        // ---- 与 MapConfig 对应的星球环境字段 ----
        public MapSize MapSize = MapSize.Medium;
        public TilePixelSize TileSize = TilePixelSize.Size64;
        public TerrainComplexity Terrain = TerrainComplexity.Rich;
        public ResourceAbundance Resources = ResourceAbundance.Moderate;
        public RiskLevel Risk = RiskLevel.Medium;
        public WeatherPattern Weather = WeatherPattern.Mild;
        public DayNightMode DayNight = DayNightMode.Alternating;
    }
}
