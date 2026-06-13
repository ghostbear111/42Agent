/// <summary>
/// LLM对话记录数据模型
/// 记录每个Agent与LLM的完整交互历史（系统提示词 / Agent输入 / LLM回复 / 错误）
/// 供"查看对话"窗口展示，以及调试高层决策链路
/// </summary>
using System.Collections.Generic;

namespace GalaxyAgent.LLM
{
    /// <summary>
    /// 单条LLM对话记录
    /// 一次高层决策通常产生 User(输入) + Assistant(回复) 两条记录
    /// </summary>
    [System.Serializable]
    public class LLMConversationEntry
    {
        /// <summary>对话角色</summary>
        public enum Role
        {
            /// <summary>系统提示词（设定LLM身份与约束）</summary>
            System,
            /// <summary>Agent发送给LLM的状态与请求</summary>
            User,
            /// <summary>LLM返回的回复</summary>
            Assistant,
            /// <summary>错误/不可用提示</summary>
            Error
        }

        /// <summary>所属Agent ID（"global"表示全局/手动对话）</summary>
        public string AgentId;
        /// <summary>对话角色</summary>
        public Role EntryRole;
        /// <summary>文本内容</summary>
        public string Content = "";
        /// <summary>触发场景标签（如"高层决策"、"重大事件"、"手动对话"）</summary>
        public string Tag = "";
        /// <summary>真实时间戳（HH:mm:ss）</summary>
        public string Timestamp = "";
        /// <summary>请求耗时（毫秒，仅Assistant有意义）</summary>
        public float DurationMs;
        /// <summary>错误信息（仅Error角色有意义）</summary>
        public string Error = "";
    }

    /// <summary>
    /// 单个Agent的LLM对话历史容器
    /// 线程安全地添加/读取记录，超出上限自动裁剪最早的记录
    /// </summary>
    public class LLMConversationLog
    {
        // 对话记录列表（按时间正序）
        private readonly List<LLMConversationEntry> _entries = new List<LLMConversationEntry>();
        // 线程安全锁
        private readonly object _lock = new object();
        // 最大保留条数
        private readonly int _maxEntries;

        /// <param name="maxEntries">最大保留条数（<=0则默认50）</param>
        public LLMConversationLog(int maxEntries)
        {
            _maxEntries = maxEntries > 0 ? maxEntries : 50;
        }

        /// <summary>添加一条记录（线程安全），超出上限自动裁剪最早的</summary>
        public void Add(LLMConversationEntry entry)
        {
            if (entry == null) return;
            lock (_lock)
            {
                _entries.Add(entry);
                while (_entries.Count > _maxEntries)
                    _entries.RemoveAt(0);
            }
        }

        /// <summary>获取所有记录的副本（线程安全），用于UI遍历展示</summary>
        public List<LLMConversationEntry> GetAll()
        {
            lock (_lock)
            {
                return new List<LLMConversationEntry>(_entries);
            }
        }

        /// <summary>当前记录条数</summary>
        public int Count
        {
            get { lock (_lock) return _entries.Count; }
        }

        /// <summary>清空所有记录</summary>
        public void Clear()
        {
            lock (_lock) _entries.Clear();
        }
    }
}
