/// <summary>
/// Ollama LLM提供者实现
/// 通过HTTP调用本地Ollama API进行Agent高层决策
/// API文档: https://github.com/ollama/ollama/blob/main/docs/api.md
/// </summary>
using System;
using System.Collections;
using System.Text;
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

        public string ProviderName => "Ollama";

        /// <summary>
        /// 构造Ollama提供者
        /// </summary>
        /// <param name="baseUrl">Ollama API地址</param>
        /// <param name="defaultModel">默认模型</param>
        /// <param name="timeout">超时时间（秒）</param>
        public OllamaProvider(string baseUrl = null, string defaultModel = null, float timeout = 0)
        {
            _baseUrl = baseUrl ?? Constants.OLLAMA_DEFAULT_URL;
            _defaultModel = defaultModel ?? Constants.OLLAMA_DEFAULT_MODEL;
            _timeout = timeout > 0 ? timeout : Constants.LLM_REQUEST_TIMEOUT;
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
            string userMsg = EscapeJson(request.UserPrompt);

            return $"{{\"model\":\"{model}\"," +
                   $"\"messages\":[{{\"role\":\"system\",\"content\":\"{systemMsg}\"}}," +
                   $"{{\"role\":\"user\",\"content\":\"{userMsg}\"}}]," +
                   $"\"stream\":false," +
                   $"\"options\":{{\"temperature\":{request.Temperature},\"num_predict\":{request.MaxTokens}}}}}";
        }

        /// <summary>
        /// 解析Ollama响应
        /// </summary>
        private static LLMResponse ParseOllamaResponse(string json)
        {
            var response = new LLMResponse { Success = true };

            // 简易JSON解析（Ollama返回 {"message":{"content":"..."},...} ）
            string contentKey = "\"content\":\"";
            int contentStart = json.IndexOf(contentKey, StringComparison.Ordinal);
            if (contentStart >= 0)
            {
                contentStart += contentKey.Length;
                int contentEnd = json.IndexOf("\"", contentStart, StringComparison.Ordinal);
                if (contentEnd > contentStart)
                {
                    response.Content = UnescapeJson(json.Substring(contentStart, contentEnd - contentStart));
                }
            }

            if (string.IsNullOrEmpty(response.Content))
            {
                response.Success = false;
                response.Error = "无法解析LLM响应";
            }

            return response;
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
