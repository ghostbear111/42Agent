/// <summary>
/// LLM客户端
/// 管理LLM提供者实例，提供统一的调用入口
/// 支持运行时切换提供者（Ollama/其他）
/// </summary>
using System;
using GalaxyAgent.Core;
using UnityEngine;

namespace GalaxyAgent.LLM
{
    public class LLMClient
    {
        // 当前LLM提供者
        private ILLMProvider _provider;
        // 是否已连接
        private bool? _isAvailable;

        /// <summary>当前提供者名称</summary>
        public string ProviderName => _provider?.ProviderName ?? "未配置";
        /// <summary>LLM是否可用</summary>
        public bool IsAvailable => _isAvailable == true;

        /// <summary>
        /// 使用默认配置初始化（Ollama）
        /// </summary>
        public LLMClient()
        {
            SetProvider(new Providers.OllamaProvider());
        }

        /// <summary>
        /// 使用自定义URL和模型初始化
        /// </summary>
        public LLMClient(string url, string model)
        {
            SetProvider(new Providers.OllamaProvider(url, model));
        }

        /// <summary>
        /// 设置LLM提供者
        /// </summary>
        public void SetProvider(ILLMProvider provider)
        {
            _provider = provider;
            _isAvailable = null; // 需要重新检查
            Debug.Log($"[LLMClient] 切换提供者: {provider.ProviderName}");
        }

        /// <summary>
        /// 异步检查LLM可用性
        /// </summary>
        public async void CheckAvailability(Action<bool> callback)
        {
            if (_provider == null)
            {
                _isAvailable = false;
                callback?.Invoke(false);
                return;
            }

            try
            {
                bool available = await _provider.IsAvailableAsync();
                _isAvailable = available;
                Debug.Log($"[LLMClient] {_provider.ProviderName} 可用性: {available}");
                callback?.Invoke(available);
            }
            catch (Exception e)
            {
                _isAvailable = false;
                Debug.LogWarning($"[LLMClient] 检查可用性失败: {e.Message}");
                callback?.Invoke(false);
            }
        }

        /// <summary>
        /// 发送聊天请求
        /// 如果LLM不可用，返回null
        /// </summary>
        public async void ChatAsync(LLMRequest request, Action<LLMResponse> callback)
        {
            if (_provider == null || _isAvailable != true)
            {
                callback?.Invoke(new LLMResponse
                {
                    Success = false,
                    Error = "LLM不可用，使用本地决策替代"
                });
                return;
            }

            try
            {
                var response = await _provider.ChatAsync(request);
                callback?.Invoke(response);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LLMClient] 请求失败: {e.Message}");
                callback?.Invoke(new LLMResponse
                {
                    Success = false,
                    Error = e.Message
                });
            }
        }
    }
}
