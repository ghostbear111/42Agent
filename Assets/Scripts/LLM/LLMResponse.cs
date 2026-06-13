/// <summary>
/// LLM响应数据模型
/// </summary>
namespace GalaxyAgent.LLM
{
    public class LLMResponse
    {
        /// <summary>是否成功</summary>
        public bool Success;
        /// <summary>回复文本内容</summary>
        public string Content = "";
        /// <summary>错误信息</summary>
        public string Error = "";
        /// <summary>提示Token数</summary>
        public int PromptTokens;
        /// <summary>补全Token数</summary>
        public int CompletionTokens;
        /// <summary>请求耗时（毫秒）</summary>
        public float DurationMs;
    }
}
