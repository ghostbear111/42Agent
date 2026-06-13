/// <summary>
/// 资源节点数据模型
/// 表示地图上可被Agent采集的资源点
/// </summary>
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class ResourceNodeData
    {
        /// <summary>资源节点唯一ID</summary>
        public int ResourceId;
        /// <summary>资源类型</summary>
        public ResourceType ResourceType;
        /// <summary>资源名称</summary>
        public string Name;
        /// <summary>世界坐标位置</summary>
        public Vector2Int Position;
        /// <summary>当前剩余数量</summary>
        public float Amount;
        /// <summary>最大数量</summary>
        public float MaxAmount;
        /// <summary>采集难度（0-10）</summary>
        public float Hardness = 1f;
        /// <summary>是否已被采尽</summary>
        public bool IsDepleted => Amount <= 0;

        /// <summary>
        /// 采集指定数量的资源，返回实际采集量
        /// </summary>
        public float Harvest(float requestedAmount)
        {
            float actual = Mathf.Min(requestedAmount, Amount);
            Amount -= actual;
            return actual;
        }
    }
}
