/// <summary>
/// 寻路节点
/// A*算法中使用的数据结构
/// </summary>
namespace GalaxyAgent.Pathfinding
{
    public class PathNode
    {
        /// <summary>X坐标</summary>
        public int X;
        /// <summary>Y坐标</summary>
        public int Y;
        /// <summary>从起点到此节点的实际代价</summary>
        public float gCost;
        /// <summary>从此节点到终点的预估代价（启发式）</summary>
        public float hCost;
        /// <summary>总代价 f = g + h</summary>
        public float fCost;
        /// <summary>父节点（用于回溯路径）</summary>
        public PathNode Parent;

        public PathNode(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
