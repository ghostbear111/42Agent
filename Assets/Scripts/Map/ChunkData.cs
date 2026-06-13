/// <summary>
/// 分块数据
/// 纯C#数据类，表示地图中一个64×64格子的数据块
/// 不依赖Unity组件，可以在后台线程生成
/// </summary>
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;

namespace GalaxyAgent.Map
{
    public class ChunkData
    {
        /// <summary>块在网格中的X索引</summary>
        public int ChunkX;
        /// <summary>块在网格中的Y索引</summary>
        public int ChunkY;
        /// <summary>块的宽度（格子数，通常64）</summary>
        public int Width;
        /// <summary>块的高度（格子数，通常64）</summary>
        public int Height;
        /// <summary>块内所有瓦片数据（二维数组 [x,y]）</summary>
        public TileData[,] Tiles;
        /// <summary>是否已加载到Tilemap渲染</summary>
        public bool IsRendered = false;
        /// <summary>是否数据已生成</summary>
        public bool IsGenerated = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ChunkData(int chunkX, int chunkY, int width, int height)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            Width = width;
            Height = height;
            Tiles = new TileData[width, height];
        }

        /// <summary>
        /// 获取块在世界坐标中的起始X（格子坐标）
        /// </summary>
        public int WorldStartX => ChunkX * Width;

        /// <summary>
        /// 获取块在世界坐标中的起始Y（格子坐标）
        /// </summary>
        public int WorldStartY => ChunkY * Height;

        /// <summary>
        /// 获取指定局部坐标的瓦片数据
        /// </summary>
        public TileData GetTile(int localX, int localY)
        {
            if (localX >= 0 && localX < Width && localY >= 0 && localY < Height)
            {
                return Tiles[localX, localY];
            }
            return null;
        }

        /// <summary>
        /// 设置指定局部坐标的瓦片数据
        /// </summary>
        public void SetTile(int localX, int localY, TileData tile)
        {
            if (localX >= 0 && localX < Width && localY >= 0 && localY < Height)
            {
                Tiles[localX, localY] = tile;
            }
        }

        /// <summary>
        /// 将世界格子坐标转换为局部坐标
        /// </summary>
        public void WorldToLocal(int worldX, int worldY, out int localX, out int localY)
        {
            localX = worldX - WorldStartX;
            localY = worldY - WorldStartY;
        }

        /// <summary>
        /// 检查指定世界坐标是否在此块内
        /// </summary>
        public bool ContainsWorldPosition(int worldX, int worldY)
        {
            return worldX >= WorldStartX && worldX < WorldStartX + Width &&
                   worldY >= WorldStartY && worldY < WorldStartY + Height;
        }
    }
}
