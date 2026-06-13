/// <summary>
/// 记忆管理器
/// 统一管理五种记忆系统的入口
/// 短期记忆、长期记忆、地图记忆、过程记忆、共享团队记忆
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Database;
using UnityEngine;

namespace GalaxyAgent.Memory
{
    public class MemoryManager
    {
        // 短期记忆（内存中，按Agent分开）
        private Dictionary<string, List<MemoryEntry>> _shortTermMemories = new Dictionary<string, List<MemoryEntry>>();
        // 数据库引用
        private DatabaseManager _db;
        private string _saveId;

        /// <summary>
        /// 初始化记忆系统
        /// </summary>
        public void Initialize(DatabaseManager db, string saveId)
        {
            _db = db;
            _saveId = saveId;
            _shortTermMemories.Clear();
            Debug.Log("[MemoryManager] 记忆系统初始化完成");
        }

        // ==================== 短期记忆 ====================

        /// <summary>
        /// 添加短期记忆（当前任务相关信息）
        /// </summary>
        public void AddShortTermMemory(string agentId, string summary, MemoryCategory category)
        {
            if (!_shortTermMemories.ContainsKey(agentId))
                _shortTermMemories[agentId] = new List<MemoryEntry>();

            var entry = new MemoryEntry
            {
                AgentId = agentId,
                MemoryType = MemoryType.ShortTerm,
                Category = category,
                Summary = summary,
                CreatedAt = DateTime.Now.ToString("HH:mm:ss")
            };

            var list = _shortTermMemories[agentId];
            list.Add(entry);

            // 超出容量时移除最旧的
            if (list.Count > Constants.SHORT_TERM_MEMORY_CAPACITY)
                list.RemoveAt(0);
        }

        /// <summary>
        /// 获取Agent的短期记忆
        /// </summary>
        public List<MemoryEntry> GetShortTermMemories(string agentId)
        {
            if (_shortTermMemories.ContainsKey(agentId))
                return _shortTermMemories[agentId];
            return new List<MemoryEntry>();
        }

        /// <summary>
        /// 清除Agent的短期记忆（任务切换时）
        /// </summary>
        public void ClearShortTermMemory(string agentId)
        {
            if (_shortTermMemories.ContainsKey(agentId))
                _shortTermMemories[agentId].Clear();
        }

        // ==================== 长期记忆 ====================

        /// <summary>
        /// 保存长期记忆到数据库
        /// </summary>
        public void SaveLongTermMemory(string agentId, string summary, MemoryCategory category,
            float importance, int posX = -1, int posY = -1)
        {
            string safeId = DatabaseManager.Escape(_saveId);
            string safeAgent = DatabaseManager.Escape(agentId);
            string safeSummary = DatabaseManager.Escape(summary);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            _db.ExecuteNonQuery($@"
                INSERT INTO agent_memories (save_id, agent_id, memory_type, category,
                    importance, content_json, position_x, position_y, created_at)
                VALUES ('{safeId}', '{safeAgent}', 'LongTerm', '{category}',
                    {importance}, '{{""summary"":""{safeSummary}""}}',
                    {posX}, {posY}, '{now}')");

            Debug.Log($"[Memory] 长期记忆: {agentId} - {summary.Substring(0, Math.Min(30, summary.Length))}");
        }

        /// <summary>
        /// 获取Agent的长期记忆
        /// </summary>
        public List<MemoryEntry> GetLongTermMemories(string agentId, int limit = 10)
        {
            var memories = new List<MemoryEntry>();
            string safeId = DatabaseManager.Escape(_saveId);
            string safeAgent = DatabaseManager.Escape(agentId);

            _db.ExecuteQuery(
                $"SELECT memory_id, category, importance, content_json, position_x, position_y, created_at " +
                $"FROM agent_memories WHERE save_id = '{safeId}' AND agent_id = '{safeAgent}' " +
                $"AND memory_type = 'LongTerm' ORDER BY importance DESC LIMIT {limit}",
                columns =>
                {
                    memories.Add(new MemoryEntry
                    {
                        MemoryId = int.Parse(columns[0]),
                        AgentId = agentId,
                        MemoryType = MemoryType.LongTerm,
                        Category = ParseEnum<MemoryCategory>(columns[1]),
                        Importance = ParseFloat(columns[2]),
                        Summary = ExtractSummary(columns[3]),
                        PositionX = int.TryParse(columns[4], out int px) ? px : -1,
                        PositionY = int.TryParse(columns[5], out int py) ? py : -1,
                        CreatedAt = columns[6]
                    });
                });

            return memories;
        }

        // ==================== 共享团队记忆 ====================

        /// <summary>
        /// 添加共享记忆
        /// </summary>
        public void AddSharedMemory(string sourceAgentId, string content, MemoryCategory category, float importance)
        {
            string safeId = DatabaseManager.Escape(_saveId);
            string safeAgent = DatabaseManager.Escape(sourceAgentId);
            string safeContent = DatabaseManager.Escape(content);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            _db.ExecuteNonQuery($@"
                INSERT INTO shared_memories (save_id, source_agent_id, category, content_json, importance, created_at)
                VALUES ('{safeId}', '{safeAgent}', '{category}',
                    '{{""content"":""{safeContent}""}}', {importance}, '{now}')");
        }

        /// <summary>
        /// 获取共享团队记忆
        /// </summary>
        public List<MemoryEntry> GetSharedMemories(int limit = 10)
        {
            var memories = new List<MemoryEntry>();
            string safeId = DatabaseManager.Escape(_saveId);

            _db.ExecuteQuery(
                $"SELECT entry_id, source_agent_id, category, importance, content_json, created_at " +
                $"FROM shared_memories WHERE save_id = '{safeId}' " +
                $"ORDER BY importance DESC LIMIT {limit}",
                columns =>
                {
                    memories.Add(new MemoryEntry
                    {
                        MemoryId = int.Parse(columns[0]),
                        AgentId = columns[1],
                        MemoryType = MemoryType.Shared,
                        Category = ParseEnum<MemoryCategory>(columns[2]),
                        Importance = ParseFloat(columns[3]),
                        Summary = ExtractSummary(columns[4]),
                        CreatedAt = columns[5]
                    });
                });

            return memories;
        }

        // ==================== 过程记忆（学习规则） ====================

        /// <summary>
        /// 记录学习规则
        /// </summary>
        public void SaveProceduralMemory(string agentId, string ruleName, string context, float weight)
        {
            string safeId = DatabaseManager.Escape(_saveId);
            string safeAgent = DatabaseManager.Escape(agentId);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            _db.ExecuteNonQuery($@"
                INSERT INTO procedural_memory (save_id, agent_id, rule_name, rule_context, weight, updated_at)
                VALUES ('{safeId}', '{safeAgent}', '{DatabaseManager.Escape(ruleName)}',
                    '{DatabaseManager.Escape(context)}', {weight}, '{now}')");
        }

        // ==================== 辅助方法 ====================

        private static float ParseFloat(string v) => float.TryParse(v, out float r) ? r : 0f;
        private static T ParseEnum<T>(string v) where T : struct =>
            Enum.TryParse<T>(v, out T r) ? r : default;
        private static string ExtractSummary(string json)
        {
            // 简易JSON提取
            string key = "\"summary\":\"";
            int idx = json.IndexOf(key);
            if (idx < 0) key = "\"content\":\"";
            idx = json.IndexOf(key);
            if (idx < 0) return json;
            int start = idx + key.Length;
            int end = json.IndexOf("\"", start);
            return end > start ? json.Substring(start, end - start) : json;
        }
    }

    /// <summary>
    /// 记忆条目数据结构
    /// </summary>
    [Serializable]
    public class MemoryEntry
    {
        public int MemoryId;
        public string AgentId;
        public MemoryType MemoryType;
        public MemoryCategory Category;
        public float Importance = 0.5f;
        public string Summary;
        public int PositionX = -1;
        public int PositionY = -1;
        public string CreatedAt;
    }
}
