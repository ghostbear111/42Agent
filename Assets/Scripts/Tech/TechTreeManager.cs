/// <summary>
/// 科技树运行时管理器（全局单例）
/// 持有当前 tech_tree.json 配置 + 本局已解锁科技集合，供 AgentController 聚合读取效果、
/// 供基地解锁 UI 调用 TryUnlock、供存档系统 GetUnlockedForSave/RestoreUnlocked。
///
/// 生命周期（对齐 GameConfigManager）：
/// - 首次访问 Instance 时自动创建（Singleton 懒加载 + DontDestroyOnLoad），Awake 中 TechTreeStore.Load()
/// - 解锁状态本局内存态；持久化由 SaveLoadManager 读写 unlocked_techs 表
/// - 效果聚合：遍历已解锁节点，按 EffectType 累乘匹配 Target 的 Value（公式集中在 Mul 内，调用方只取最终乘数）
///
/// 时序注意：单例懒加载，GameSceneController 会在 CreateBase 后显式 _= TechTreeManager.Instance 触发初始化；
/// AgentController 每帧调用 GetXxxMultiplier 时用 ?. + ?? 1f 兜底（应用退出阶段单例可能为 null）。
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.World.Base;
using UnityEngine;

namespace GalaxyAgent.Tech
{
    public class TechTreeManager : Singleton<TechTreeManager>
    {
        /// <summary>当前科技树配置（运行时可读，Reload 后替换）</summary>
        private TechTreeData _tree;
        /// <summary>本局已解锁的节点 Id 集合（全局共享，非每 Agent 独立）</summary>
        private readonly HashSet<string> _unlocked = new HashSet<string>();

        /// <summary>当前科技树数据</summary>
        public TechTreeData Tree => _tree;
        /// <summary>全部节点（只读，供 UI 遍历）</summary>
        public IReadOnlyList<TechNode> AllNodes => _tree?.Nodes;
        /// <summary>已解锁 Id（只读，供 UI 显示）</summary>
        public IReadOnlyCollection<string> UnlockedIds => _unlocked;

        protected override void Awake()
        {
            base.Awake();
            _tree = TechTreeStore.Load();
            Debug.Log($"[TechTreeManager] 初始化完成，加载 {_tree?.Nodes?.Count ?? 0} 个科技节点");
        }

        /// <summary>从磁盘重新加载科技树配置（不改变已解锁集合）</summary>
        public void Reload()
        {
            _tree = TechTreeStore.Load();
            Debug.Log("[TechTreeManager] 科技树配置已重新加载");
        }

        private TechNode Find(string id) => _tree?.Nodes?.Find(n => n.Id == id);

        /// <summary>某科技是否已解锁</summary>
        public bool IsUnlocked(string id) => id != null && _unlocked.Contains(id);

        /// <summary>
        /// 是否可解锁：节点存在 + 未解锁 + 前置全解锁 + 资源充足
        /// </summary>
        public bool CanUnlock(string id, BaseController b)
        {
            var node = Find(id);
            if (node == null || IsUnlocked(id)) return false;
            foreach (var pre in node.Prerequisites)
                if (!IsUnlocked(pre)) return false;
            return b != null && b.HasEnough(ToDict(node.Cost));
        }

        /// <summary>
        /// 尝试解锁：校验前置 → 扣资源 → 写记录 → 发 TechUnlockedEvent。
        /// 失败时 reason 返回中文原因，返回 false。
        /// </summary>
        public bool TryUnlock(string id, BaseController b, out string reason)
        {
            reason = "";
            var node = Find(id);
            if (node == null) { reason = "未知科技"; return false; }
            if (IsUnlocked(id)) { reason = "已解锁"; return false; }
            foreach (var pre in node.Prerequisites)
                if (!IsUnlocked(pre)) { reason = $"需先解锁前置: {pre}"; return false; }
            var cost = ToDict(node.Cost);
            if (b == null || !b.HasEnough(cost)) { reason = "资源不足"; return false; }
            if (!b.SpendResource(cost)) { reason = "资源扣除失败"; return false; }
            _unlocked.Add(id);
            EventBus.Publish(new TechUnlockedEvent { TechId = id });
            Debug.Log($"[TechTreeManager] 解锁科技: {id} ({node.DisplayName})");
            return true;
        }

        /// <summary>导出已解锁 Id 列表（供存档持久化）</summary>
        public List<string> GetUnlockedForSave() => new List<string>(_unlocked);

        /// <summary>从存档恢复已解锁集合（加载存档时调用，清空后重置）</summary>
        public void RestoreUnlocked(IEnumerable<string> ids)
        {
            _unlocked.Clear();
            if (ids == null) return;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
            Debug.Log($"[TechTreeManager] 恢复已解锁科技 {_unlocked.Count} 项");
        }

        // ==================== 效果聚合 ====================

        /// <summary>
        /// 按 EffectType 聚合：遍历所有已解锁节点，累乘匹配 Target 的 Effect Value。
        /// 公式集中在此，调用方只取最终乘数，杜绝"忘记取反"散落坑。
        /// </summary>
        private float Mul(EffectType t, AgentData a)
        {
            float m = 1f;
            if (_tree == null || _tree.Nodes == null) return m;
            foreach (var n in _tree.Nodes)
            {
                if (!_unlocked.Contains(n.Id) || n.Effects == null) continue;
                foreach (var e in n.Effects)
                {
                    if (e.Type != t) continue;
                    if (!MatchesTarget(e.Target, a)) continue;
                    m *= e.Value;
                }
            }
            return m;
        }

        /// <summary>判断效果 Target 是否作用于指定 Agent</summary>
        private static bool MatchesTarget(EffectTarget t, AgentData a)
        {
            if (t == EffectTarget.Global || t == EffectTarget.AllAgents) return true;
            if (a == null) return false;
            return t switch
            {
                EffectTarget.Scout => a.AgentType == AgentType.Scout,
                EffectTarget.Worker => a.AgentType == AgentType.Worker,
                EffectTarget.Guard => a.AgentType == AgentType.Guard,
                _ => true
            };
        }

        public float GetAttackMultiplier(AgentData a) => Mul(EffectType.AttackMul, a);
        public float GetDefenseMultiplier(AgentData a) => Mul(EffectType.DefenseMul, a);
        public float GetSpeedMultiplier(AgentData a) => Mul(EffectType.SpeedMul, a);
        public float GetCarryMultiplier(AgentData a) => Mul(EffectType.CarryMul, a);
        public float GetGatherMultiplier(AgentData a) => Mul(EffectType.GatherMul, a);
        public float GetPerceptionMultiplier(AgentData a) => Mul(EffectType.PerceptionMul, a);
        /// <summary>能量消耗倍率（0.8=降低20%，调用方直接乘，无需取反）</summary>
        public float GetEnergyDrainMultiplier(AgentData a) => Mul(EffectType.EnergyDrainMul, a);

        // ==================== 辅助 ====================

        /// <summary>CostEntry 列表转字典（供 BaseController.HasEnough/SpendResource）</summary>
        private static Dictionary<ResourceType, float> ToDict(List<CostEntry> c)
        {
            var d = new Dictionary<ResourceType, float>();
            if (c == null) return d;
            foreach (var e in c) d[e.Resource] = e.Amount;
            return d;
        }
    }
}
