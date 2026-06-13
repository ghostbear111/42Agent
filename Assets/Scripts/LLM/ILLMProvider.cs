/// <summary>
/// LLM提供者接口
/// 定义统一的LLM调用规范，支持不同的后端实现
/// 后续可添加 OpenAI、Claude、自定义服务器等实现
/// </summary>
using System.Threading.Tasks;

namespace GalaxyAgent.LLM
{
    public interface ILLMProvider
    {
        /// <summary>提供者名称（如"Ollama"）</summary>
        string ProviderName { get; }

        /// <summary>检查LLM服务是否可用</summary>
        Task<bool> IsAvailableAsync();

        /// <summary>发送聊天请求，获取回复</summary>
        Task<LLMResponse> ChatAsync(LLMRequest request);

        /// <summary>获取可用模型列表</summary>
        Task<string[]> GetAvailableModelsAsync();

        /// <summary>中止当前请求</summary>
        void CancelRequest();
    }
}
