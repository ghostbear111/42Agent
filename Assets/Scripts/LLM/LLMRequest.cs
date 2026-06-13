/// <summary>
/// LLM请求数据模型
/// </summary>
namespace GalaxyAgent.LLM
{
    public class LLMRequest
    {
        /// <summary>模型名称</summary>
        public string Model = "qwen3:8b";
        /// <summary>系统提示词</summary>
        public string SystemPrompt = "";
        /// <summary>用户提示词</summary>
        public string UserPrompt = "";
        /// <summary>创造性温度（0-1）</summary>
        public float Temperature = 0.7f;
        /// <summary>最大输出Token数</summary>
        public int MaxTokens = 512;
        /// <summary>是否流式输出</summary>
        public bool Stream = false;
    }
}
