/// <summary>
/// 科技树 ScriptableObject 资产（编辑期可视化存储）
/// 右键 Create > GalaxyAgent > 科技树 可创建实例，在 Inspector 直接编辑 Nodes 列表。
/// 运行时不直接读取本资产——运行时唯一真相是 tech_tree.json（由编辑器窗口"烘焙"生成）。
/// 本资产是 CSV ⇄ 资产 ⇄ JSON 三态中的"可视化编辑"一态。
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyAgent.Tech
{
    [CreateAssetMenu(fileName = "TechTreeAsset", menuName = "GalaxyAgent/科技树")]
    public class TechTreeAsset : ScriptableObject
    {
        /// <summary>科技树全部节点（Inspector 可直接增删编辑）</summary>
        public List<TechNode> Nodes = new List<TechNode>();
    }
}
