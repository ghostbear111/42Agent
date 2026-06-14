/// <summary>
/// Ollama LLM提供者实现
/// 通过HTTP调用本地Ollama API进行Agent高层决策
/// API文档: https://github.com/ollama/ollama/blob/main/docs/api.md
/// </summary>
using System;
using System.Collections;
using System.Text;
using GalaxyAgent.Config;
using GalaxyAgent.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace GalaxyAgent.LLM.Providers
{
    public class OllamaProvider : ILLMProvider
    {
        private readonly string _baseUrl;
        private readonly string _defaultModel;
        private readonly float _timeout;
        private bool _isCancelled;

        // 运行时游戏配置访问（null安全回退）：构造时未显式传参则用配置中的LLM默认值
        private static readonly GameConfig _fallbackConfig = new GameConfig();
        private static GameConfig Cfg => GameConfigManager.Instance != null
            ? GameConfigManager.Instance.Config : _fallbackConfig;

        public string ProviderName => "Ollama";

        /// <summary>
        /// 构造Ollama提供者
        /// </summary>
        /// <param name="baseUrl">Ollama API地址</param>
        /// <param name="defaultModel">默认模型</param>
        /// <param name="timeout">超时时间（秒）</param>
        public OllamaProvider(string baseUrl = null, string defaultModel = null, float timeout = 0)
        {
            _baseUrl = baseUrl ?? Cfg.Llm.Url;
            _defaultModel = defaultModel ?? Cfg.Llm.Model;
            _timeout = timeout > 0 ? timeout : Cfg.Llm.RequestTimeout;
        }

        /// <summary>
        /// 检查Ollama服务是否可用
        /// </summary>
        public async System.Threading.Tasks.Task<bool> IsAvailableAsync()
        {
            try
            {
                using var request = UnityWebRequest.Get($"{_baseUrl}/api/tags");
                request.timeout = 3;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await System.Threading.Tasks.Task.Yield();

                return request.result == UnityWebRequest.Result.Success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 发送聊天请求到Ollama
        /// 使用协程在主线程执行UnityWebRequest
        /// </summary>
        public async System.Threading.Tasks.Task<LLMResponse> ChatAsync(LLMRequest request)
        {
            _isCancelled = false;
            var response = new LLMResponse();
            float startTime = Time.realtimeSinceStartup;

            try
            {
                // 构建Ollama请求体
                string jsonBody = BuildChatRequestBody(request);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

                using var webRequest = new UnityWebRequest($"{_baseUrl}/api/chat", "POST");
                webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.timeout = (int)_timeout;

                var operation = webRequest.SendWebRequest();
                while (!operation.isDone)
                {
                    if (_isCancelled)
                    {
                        webRequest.Abort();
                        response.Success = false;
                        response.Error = "请求已取消";
                        return response;
                    }
                    await System.Threading.Tasks.Task.Yield();
                }

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    response = ParseOllamaResponse(responseText);
                    response.DurationMs = (Time.realtimeSinceStartup - startTime) * 1000f;
                }
                else
                {
                    response.Success = false;
                    response.Error = $"HTTP错误: {webRequest.error}";
                }
            }
            catch (Exception e)
            {
                response.Success = false;
                response.Error = e.Message;
            }

            return response;
        }

        /// <summary>
        /// 获取可用模型列表
        /// </summary>
        public async System.Threading.Tasks.Task<string[]> GetAvailableModelsAsync()
        {
            try
            {
                using var request = UnityWebRequest.Get($"{_baseUrl}/api/tags");
                request.timeout = 5;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await System.Threading.Tasks.Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // 简易JSON解析提取模型名称
                    string json = request.downloadHandler.text;
                    return ParseModelList(json);
                }
            }
            catch { }

            return Array.Empty<string>();
        }

        /// <summary>
        /// 取消当前请求
        /// </summary>
        public void CancelRequest()
        {
            _isCancelled = true;
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 构建Ollama chat API请求体
        /// </summary>
        private string BuildChatRequestBody(LLMRequest request)
        {
            string model = string.IsNullOrEmpty(request.Model) ? _defaultModel : request.Model;
            // 构建JSON（不用Newtonsoft，手写简单JSON）
            string systemMsg = EscapeJson(request.SystemPrompt);
            // qwen3优化：追加 /no_think 关闭内置思考过程，使回复更短更快、更易完整输出JSON。
            // （非qwen3模型会将其当作普通文本忽略，无副作用）
            string userMsg = EscapeJson(request.UserPrompt) + " /no_think";

            return $"{{\"model\":\"{model}\"," +
                   $"\"messages\":[{{\"role\":\"system\",\"content\":\"{systemMsg}\"}}," +
                   $"{{\"role\":\"user\",\"content\":\"{userMsg}\"}}]," +
                   $"\"stream\":false," +
                   $"\"options\":{{\"temperature\":{request.Temperature},\"num_predict\":{request.MaxTokens}}}}}";
        }

        /// <summary>
        /// 解析Ollama响应
        /// 提取 message.content 字段。
        /// 注意：qwen3等模型把思考过程放在独立的 thinking 字段，content 才是最终答案；
        /// 且 content 内容常含转义引号（如LLM回复JSON时 content="{"action":...}"），
        /// 必须用逐字符读取、正确处理 \" 转义的方式提取，否则会在第一个转义引号处截断。
        /// </summary>
        private static LLMResponse ParseOllamaResponse(string json)
        {
            var response = new LLMResponse { Success = true };
            response.Content = ExtractJsonStringValue(json, "content");

            if (string.IsNullOrEmpty(response.Content))
            {
                response.Success = false;
                response.Error = "无法解析LLM响应";
            }

            return response;
        }

        /// <summary>
        /// 从JSON文本中提取字符串字段的值。
        /// 逐字符读取，正确处理 \" \\ \/ \n \t 等转义序列以及冒号后的空白，
        /// 适用于Ollama响应中含转义引号的content字段（如LLM回复JSON内容）。
        /// </summary>
        private static string ExtractJsonStringValue(string json, string key)
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
            idx++; // 跳过开头的引号

            // 逐字符读取，直到未转义的结束引号
            var sb = new StringBuilder();
            while (idx < json.Length)
            {
                char c = json[idx];
                if (c == '\\' && idx + 1 < json.Length)
                {
                    char next = json[idx + 1];
                    // \uXXXX Unicode 转义（如 > = '>'，LLM回复中常见，必须解码否则显示成 u003e）
                    if (next == 'u' && idx + 6 <= json.Length)
                    {
                        string hex = json.Substring(idx + 2, 4);
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out int code))
                            sb.Append((char)code);
                        else
                            sb.Append(json.Substring(idx, 6)); // 解析失败则原样保留
                        idx += 6;
                        continue;
                    }
                    // 处理转义序列
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
        /// 解析模型列表
        /// </summary>
        private static string[] ParseModelList(string json)
        {
            var models = new System.Collections.Generic.List<string>();
            string nameKey = "\"name\":\"";
            int index = 0;
            while ((index = json.IndexOf(nameKey, index, StringComparison.Ordinal)) >= 0)
            {
                int start = index + nameKey.Length;
                int end = json.IndexOf("\"", start, StringComparison.Ordinal);
                if (end > start)
                {
                    models.Add(json.Substring(start, end - start));
                }
                index = start;
            }
            return models.ToArray();
        }

        /// <summary>JSON字符串转义</summary>
        private static string EscapeJson(string value)
        {
            return value?.Replace("\\", "\\\\").Replace("\"", "\\\"")
                         .Replace("\n", "\\n").Replace("\r", "\\r") ?? "";
        }

        /// <summary>JSON字符串反转义</summary>
        private static string UnescapeJson(string value)
        {
            return value?.Replace("\\\"", "\"").Replace("\\\\", "\\")
                         .Replace("\\n", "\n").Replace("\\r", "\r") ?? "";
        }
    }
}
