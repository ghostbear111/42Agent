/// <summary>
/// A*寻路算法
/// 在网格地图上进行启发式寻路，支持不同地形移动代价
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Map;
using UnityEngine;

namespace GalaxyAgent.Pathfinding
{
    public static class AStarPathfinder
    {
        /// <summary>
        /// 在网格上寻找从起点到终点的路径
        /// </summary>
        /// <param name="start">起点格子坐标</param>
        /// <param name="end">终点格子坐标</param>
        /// <param name="mapWidth">地图宽度</param>
        /// <param name="getTileCost">获取指定格子移动代价的函数（-1表示不可通行）</param>
        /// <returns>路径点列表（包含起点和终点），找不到返回null</returns>
        public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int end,
            int mapWidth, System.Func<int, int, float> getTileCost)
        {
            // 起点等于终点
            if (start == end) return new List<Vector2Int> { start };

            // 开放列表（待探索节点）和关闭列表（已探索节点）
            var openSet = new List<PathNode>();
            var closedSet = new HashSet<int>();
            var nodeMap = new Dictionary<int, PathNode>();

            // 创建起点节点
            var startNode = new PathNode(start.x, start.y)
            {
                gCost = 0,
                hCost = GetHeuristic(start, end)
            };
            startNode.fCost = startNode.gCost + startNode.hCost;

            openSet.Add(startNode);
            nodeMap[GetKey(start.x, start.y, mapWidth)] = startNode;

            int maxIterations = 10000; // 防止无限循环
            int iterations = 0;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // 找到fCost最小的节点
                int bestIndex = 0;
                for (int i = 1; i < openSet.Count; i++)
                {
                    if (openSet[i].fCost < openSet[bestIndex].fCost ||
                        (openSet[i].fCost == openSet[bestIndex].fCost &&
                         openSet[i].hCost < openSet[bestIndex].hCost))
                    {
                        bestIndex = i;
                    }
                }

                var currentNode = openSet[bestIndex];

                // 到达终点
                if (currentNode.X == end.x && currentNode.Y == end.y)
                {
                    return ReconstructPath(currentNode);
                }

                // 移到关闭列表
                openSet.RemoveAt(bestIndex);
                closedSet.Add(GetKey(currentNode.X, currentNode.Y, mapWidth));

                // 探索四个方向的邻居（上下左右）
                var neighbors = new (int dx, int dy)[]
                {
                    (0, 1), (0, -1), (1, 0), (-1, 0)
                };

                foreach (var (dx, dy) in neighbors)
                {
                    int nx = currentNode.X + dx;
                    int ny = currentNode.Y + dy;

                    // 边界检查
                    if (nx < 0 || ny < 0 || nx >= mapWidth || ny >= mapWidth) continue;

                    int neighborKey = GetKey(nx, ny, mapWidth);

                    // 已在关闭列表中
                    if (closedSet.Contains(neighborKey)) continue;

                    // 获取移动代价
                    float tileCost = getTileCost(nx, ny);
                    if (tileCost < 0) continue; // 不可通行

                    // 计算新的gCost
                    float newGCost = currentNode.gCost + tileCost;

                    // 检查是否已在开放列表中
                    if (!nodeMap.ContainsKey(neighborKey))
                    {
                        var neighbor = new PathNode(nx, ny)
                        {
                            gCost = newGCost,
                            hCost = GetHeuristic(new Vector2Int(nx, ny), end),
                            Parent = currentNode
                        };
                        neighbor.fCost = neighbor.gCost + neighbor.hCost;

                        nodeMap[neighborKey] = neighbor;
                        openSet.Add(neighbor);
                    }
                    else
                    {
                        var existing = nodeMap[neighborKey];
                        if (newGCost < existing.gCost)
                        {
                            existing.gCost = newGCost;
                            existing.fCost = existing.gCost + existing.hCost;
                            existing.Parent = currentNode;

                            // 确保在开放列表中
                            if (!openSet.Contains(existing))
                                openSet.Add(existing);
                        }
                    }
                }
            }

            // 未找到路径
            return null;
        }

        /// <summary>
        /// 启发式距离（曼哈顿距离）
        /// </summary>
        private static float GetHeuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>
        /// 从终点回溯重建路径
        /// </summary>
        private static List<Vector2Int> ReconstructPath(PathNode endNode)
        {
            var path = new List<Vector2Int>();
            var current = endNode;
            while (current != null)
            {
                path.Add(new Vector2Int(current.X, current.Y));
                current = current.Parent;
            }
            path.Reverse(); // 从起点到终点
            return path;
        }

        /// <summary>
        /// 将2D坐标转换为一维键
        /// </summary>
        private static int GetKey(int x, int y, int mapWidth) => y * mapWidth + x;
    }
}
