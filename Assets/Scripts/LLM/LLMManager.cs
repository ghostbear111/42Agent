/// <summary>
/// LLM管理器（全局单例）
/// 统一管理唯一的LLM客户端、各Agent对话历史、以及请求串行化。
///
/// 三大职责：
/// 1. 持有唯一LLMClient —— 避免每个Agent各自创建连接，统一可用性检查
/// 2. 串行化请求队列 —— 本地Ollama一次最好只处理一个请求，防止多个Agent
///    同时发起的高层决策请求压垮服务（默认并发上限=1，见Constants.LLM_MAX_CONCURRENT_REQUESTS）
/// 3. 记录对话历史 —— 每次交互自动写入对应用户日志，供"查看对话"窗口展示
///
/// 线程模型：所有排队、回调、日志写入都在Unity主线程完成（UnityWebRequest协程在主线程推进），
/// 因此队列与计数无需加锁。
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Config;
using GalaxyAgent.Core;
using UnityEngine;

namespace GalaxyAgent.LLM
{
    public class LLMManager : Singleton<LLMManager>
    {
        // 运行时游戏配置访问（null安全回退）
        private static readonly GameConfig _fallbackConfig = new GameConfig();
        private static GameConfig Cfg => GameConfigManager.Instance != null
            ? GameConfigManager.Instance.Config : _fallbackConfig;

        // ==================== 状态 ====================

        /// <summary>唯一LLM客户端</summary>
        private LLMClient _client;
        /// <summary>各Agent对话历史（key=AgentId，value=该Agent的对话记录容器）</summary>
        private readonly Dictionary<string, LLMConversationLog> _logs = new Dictionary<string, LLMConversationLog>();

        // 串行化请求队列与活跃计数
        private readonly Queue<PendingRequest> _queue = new Queue<PendingRequest>();
        private int _activeCount = 0;

        /// <summary>当前系统提示词（供窗口预览）</summary>
        private string _currentSystemPrompt = "";
        /// <summary>当前LLM服务地址（可运行时切换）</summary>
        private string _currentUrl = Constants.OLLAMA_DEFAULT_URL;
        /// <summary>当前模型（可运行时切换，小模型推理更快）</summary>
        private string _currentModel = Constants.OLLAMA_DEFAULT_MODEL;

        // 游戏外（主菜单）配置快照：进入游戏场景前保存，离开时恢复，
        // 实现「游戏外配置」与「游戏内存档配置」的运行时隔离，避免存档 Configure 污染全局单例
        private string _outerUrl;
        private string _outerModel;
        private bool _hasOuterSnapshot;

        // ==================== 对外属性 ====================

        /// <summary>LLM是否可用（已连接）</summary>
        public bool IsAvailable => _client != null && _client.IsAvailable;
        /// <summary>当前提供者名称</summary>
        public string ProviderName => _client?.ProviderName ?? "未配置";
        /// <summary>当前LLM服务地址</summary>
        public string CurrentUrl => _currentUrl;
        /// <summary>当前模型名称</summary>
        public string CurrentModel => _currentModel;
        /// <summary>排队等待中的请求数（供UI显示忙碌状态）</summary>
        public int PendingCount => _queue.Count;
        /// <summary>正在执行的请求数</summary>
        public int ActiveCount => _activeCount;
        /// <summary>是否有请求正在进行或排队</summary>
        public bool IsBusy => _activeCount > 0 || _queue.Count > 0;

        // ==================== 生命周期 ====================

        protected override void Awake()
        {
            base.Awake();

            // 从游戏配置读取LLM服务地址与模型（覆盖字段默认值），并用其创建客户端
            _currentUrl = Cfg.Llm.Url;
            _currentModel = Cfg.Llm.Model;
            _client = new LLMClient(_currentUrl, _currentModel);
            _currentSystemPrompt = PromptBuilder.BuildSystemPrompt();

            // 异步检查可用性（不阻塞主线程，结果通过IsAvailable暴露）
            _client.CheckAvailability(available =>
            {
                Debug.Log($"[LLMManager] {_client.ProviderName} {(available ? "已连接，高层决策将启用LLM" : "未连接，高层决策使用本地规则替代")}");
            });

            Debug.Log("[LLMManager] 初始化完成");
        }

        // ==================== 对话历史访问 ====================

        /// <summary>获取（必要时创建）指定Agent的对话历史</summary>
        public LLMConversationLog GetLog(string agentId)
        {
            if (string.IsNullOrEmpty(agentId)) agentId = "global";
            if (!_logs.ContainsKey(agentId))
                _logs[agentId] = new LLMConversationLog(Cfg.Llm.ConversationLogMax);
            return _logs[agentId];
        }

        /// <summary>获取所有已有对话记录的AgentId列表</summary>
        public List<string> GetAgentIdsWithLogs()
        {
            return new List<string>(_logs.Keys);
        }

        /// <summary>当前系统提示词预览（供窗口展示LLM身份设定）</summary>
        public string GetSystemPromptPreview() => _currentSystemPrompt;

        // ==================== 请求入口 ====================

        /// <summary>
        /// 排入一个LLM请求（自动串行化执行）
        /// 调用方无需关心并发，完成时通过onComplete回调（主线程）返回响应。
        /// 即使LLM不可用也会回调一个失败响应，调用方可据此回退到本地规则。
        /// </summary>
        /// <param name="agentId">发起请求的AgentId</param>
        /// <param name="request">请求内容（含system/user prompt）</param>
        /// <param name="tag">场景标签（如"高层决策"、"重大事件"、"手动对话"）</param>
        /// <param name="onComplete">完成回调，可能为失败响应</param>
        public void EnqueueRequest(string agentId, LLMRequest request, string tag, Action<LLMResponse> onComplete)
        {
            if (request == null)
            {
                onComplete?.Invoke(new LLMResponse { Success = false, Error = "空请求" });
                return;
            }

            // 先记录Agent输入（立即写入，无论后续是否成功）
            LogEntry(agentId, LLMConversationEntry.Role.User, request.UserPrompt, tag, 0f, "");

            _queue.Enqueue(new PendingRequest
            {
                AgentId = agentId,
                Request = request,
                Tag = tag,
                OnComplete = onComplete
            });

            TryProcessNext();
        }

        /// <summary>
        /// 手动对话入口（供"查看对话"窗口的发送按钮）
        /// 使用当前系统提示词，以指定Agent身份发送一条玩家输入的消息。
        /// </summary>
        public void SendManualMessage(string agentId, string userText, Action<LLMResponse> onComplete)
        {
            if (string.IsNullOrWhiteSpace(userText))
            {
                onComplete?.Invoke(new LLMResponse { Success = false, Error = "消息为空" });
                return;
            }

            var req = new LLMRequest
            {
                Model = "", // 留空，使用当前配置的模型（见OllamaProvider._defaultModel）
                SystemPrompt = _currentSystemPrompt,
                UserPrompt = userText.Trim(),
                Temperature = 0.7f,
                MaxTokens = Cfg.Llm.MaxTokens
            };
            EnqueueRequest(agentId, req, "手动对话", onComplete);
        }

        /// <summary>
        /// 运行时切换LLM服务地址与模型。
        /// 重建底层客户端并重新检测可用性；切换后所有后续请求使用新配置。
        /// 用于配置面板动态选择更快的模型等场景。
        /// </summary>
        public void Configure(string url, string model)
        {
            _currentUrl = string.IsNullOrEmpty(url) ? Cfg.Llm.Url : url.Trim();
            _currentModel = string.IsNullOrEmpty(model) ? Cfg.Llm.Model : model.Trim();
            _client = new LLMClient(_currentUrl, _currentModel);
            Debug.Log($"[LLMManager] 已切换LLM配置: url={_currentUrl} model={_currentModel}");
            _client.CheckAvailability(available =>
            {
                Debug.Log($"[LLMManager] 切换后 {_client.ProviderName} 可用性: {available}");
            });
        }

        /// <summary>
        /// 保存当前配置为「游戏外配置」快照（进入游戏场景前调用）。
        /// 幂等：已有快照则不覆盖，确保保留的是最早（真正游戏外）的配置。
        /// 配合 <see cref="RestoreOuterConfig"/> 使用，使存档的 Configure 只影响游戏内运行，
        /// 返回主菜单时全局配置自动还原到进入游戏前的状态。
        /// </summary>
        public void SaveOuterConfig()
        {
            if (_hasOuterSnapshot)
            {
                Debug.Log("[LLMManager] 游戏外配置快照已存在，保留最早快照");
                return;
            }
            _outerUrl = _currentUrl;
            _outerModel = _currentModel;
            _hasOuterSnapshot = true;
            Debug.Log($"[LLMManager] 已保存游戏外配置快照: url={_outerUrl} model={_outerModel}");
        }

        /// <summary>
        /// 恢复游戏外配置（离开游戏场景时调用，如返回主菜单触发 GameScene 销毁）。
        /// 取出快照重新 Configure，使全局 LLM 配置回到进入游戏前的状态，
        /// 避免加载存档时的 Configure 把主菜单的模型配置「串掉」。无快照时空操作。
        /// </summary>
        public void RestoreOuterConfig()
        {
            if (!_hasOuterSnapshot) return;
            _hasOuterSnapshot = false;
            Debug.Log($"[LLMManager] 恢复游戏外配置: url={_outerUrl} model={_outerModel}");
            Configure(_outerUrl, _outerModel);
        }

        /// <summary>
        /// 异步获取当前Ollama服务上已安装的模型列表。
        /// 供配置面板动态填充，确保只能选择已安装的模型（避免选到未pull的模型导致请求失败）。
        /// </summary>
        public async void GetInstalledModelsAsync(Action<string[]> callback)
        {
            try
            {
                var provider = new Providers.OllamaProvider(_currentUrl);
                var models = await provider.GetAvailableModelsAsync();
                callback?.Invoke(models ?? System.Array.Empty<string>());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LLMManager] 获取模型列表失败: {e.Message}");
                callback?.Invoke(System.Array.Empty<string>());
            }
        }

        // ==================== 内部：串行化执行 ====================

        /// <summary>
        /// 尝试处理队列中待执行的请求，受全局并发上限约束
        /// </summary>
        private void TryProcessNext()
        {
            while (_activeCount < Constants.LLM_MAX_CONCURRENT_REQUESTS && _queue.Count > 0)
            {
                var pending = _queue.Dequeue();
                _activeCount++;

                if (_client == null || !_client.IsAvailable)
                {
                    // LLM不可用，立即以失败响应结束（调用方据此回退规则）
                    FinishRequest(pending, new LLMResponse { Success = false, Error = "LLM不可用，使用本地决策替代" });
                }
                else
                {
                    // 异步发送，完成后在回调里继续推进队列
                    _client.ChatAsync(pending.Request, response => FinishRequest(pending, response));
                }
            }
        }

        /// <summary>
        /// 单个请求收尾：记录回复日志、回调调用方、推进队列
        /// </summary>
        private void FinishRequest(PendingRequest pending, LLMResponse response)
        {
            _activeCount--;

            if (response != null && response.Success)
            {
                LogEntry(pending.AgentId, LLMConversationEntry.Role.Assistant,
                    response.Content, pending.Tag, response.DurationMs, "");
            }
            else
            {
                string err = response?.Error ?? "未知错误";
                LogEntry(pending.AgentId, LLMConversationEntry.Role.Error, err, pending.Tag, 0f, err);
            }

            // 调用方回调（吞掉异常避免影响队列推进）
            try { pending.OnComplete?.Invoke(response); }
            catch (Exception e) { Debug.LogWarning($"[LLMManager] 请求回调异常: {e.Message}"); }

            // 继续处理下一个排队请求
            TryProcessNext();
        }

        // ==================== 内部：日志记录 ====================

        /// <summary>统一写入一条对话记录</summary>
        private void LogEntry(string agentId, LLMConversationEntry.Role role, string content,
            string tag, float durationMs, string error)
        {
            GetLog(agentId).Add(new LLMConversationEntry
            {
                AgentId = agentId,
                EntryRole = role,
                Content = content ?? "",
                Tag = tag ?? "",
                Timestamp = NowTimestamp(),
                DurationMs = durationMs,
                Error = error ?? ""
            });
        }

        private static string NowTimestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        // ==================== 待处理请求结构 ====================

        private struct PendingRequest
        {
            public string AgentId;
            public LLMRequest Request;
            public string Tag;
            public Action<LLMResponse> OnComplete;
        }
    }
}
