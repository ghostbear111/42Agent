/// <summary>
/// 可交互世界对象数据模型
/// 所有Agent能理解的物体都转化为此结构化数据
/// Agent不需要看Unity画面，它读取结构化世界状态
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyAgent.Data.Models
{
    [System.Serializable]
    public class WorldObjectData
    {
        /// <summary>对象唯一标识</summary>
        public string ObjectId;
        /// <summary>对象类型（resource_node, threat, building, anomaly 等）</summary>
        public string Type;
        /// <summary>显示名称</summary>
        public string Name;
        /// <summary>世界坐标位置</summary>
        public Vector2Int Position;
        /// <summary>对象属性字典</summary>
        public Dictionary<string, object> Properties = new Dictionary<string, object>();
        /// <summary>可执行动作列表（如 scan, mine, mark, attack, investigate）</summary>
        public List<string> AvailableActions = new List<string>();

        /// <summary>
        /// 转换为Agent可读的结构化描述文本
        /// </summary>
        public string ToAgentDescription()
        {
            var props = "";
            foreach (var kv in Properties)
            {
                props += $"\n    {kv.Key}: {kv.Value}";
            }
            var actions = string.Join(", ", AvailableActions);
            return $"对象 {ObjectId}:\n" +
                   $"  类型: {Type}\n" +
                   $"  名称: {Name}\n" +
                   $"  位置: ({Position.x}, {Position.y})\n" +
                   $"  属性:{props}\n" +
                   $"  可执行动作: {actions}";
        }
    }
}
