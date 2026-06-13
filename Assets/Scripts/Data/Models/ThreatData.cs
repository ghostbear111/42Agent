/// <summary>
/// 威胁/敌人数据模型
/// 表示地图上可能对Agent造成危险的实体
/// </summary>
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class ThreatData
    {
        /// <summary>威胁唯一ID</summary>
        public int ThreatId;
        /// <summary>威胁类型（如 burrow_beast, hostile_robot 等）</summary>
        public string ThreatType;
        /// <summary>威胁名称</summary>
        public string Name;
        /// <summary>世界坐标位置</summary>
        public Vector2Int Position;
        /// <summary>当前生命值</summary>
        public float Health;
        /// <summary>最大生命值</summary>
        public float MaxHealth;
        /// <summary>攻击伤害</summary>
        public float Damage;
        /// <summary>感知范围（格子数）</summary>
        public float DetectionRange = 5f;
        /// <summary>攻击范围（格子数）</summary>
        public float AttackRange = 1.5f;
        /// <summary>是否存活</summary>
        public bool IsAlive;
        /// <summary>威胁等级（0-1）</summary>
        public float ThreatLevel = 0.5f;

        /// <summary>
        /// 受到伤害，返回是否死亡
        /// </summary>
        public bool TakeDamage(float damage)
        {
            Health -= damage;
            if (Health <= 0)
            {
                Health = 0;
                IsAlive = false;
                return true;
            }
            return false;
        }
    }
}
