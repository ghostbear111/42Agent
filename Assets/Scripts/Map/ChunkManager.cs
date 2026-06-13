/// <summary>
/// 分块渲染管理器
/// 根据摄像机位置动态加载/卸载地图块到Tilemap
/// 核心职责：只渲染摄像机可见区域附近的块，优化大地图性能
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Models;
using UnityEngine;
using UnityEngine.Tilemaps;
// 消除TileData歧义：GalaxyAgent.Data.Models.TileData vs UnityEngine.Tilemaps.TileData
using TileData = GalaxyAgent.Data.Models.TileData;

namespace GalaxyAgent.Map
{
    public class ChunkManager : MonoBehaviour
    {
        // 地图生成器引用
        private MapGenerator _mapGenerator;
        // Tilemap组件引用
        private Tilemap _terrainTilemap;
        // 已加载的块（正在渲染中的）
        private Dictionary<string, bool> _loadedChunks = new Dictionary<string, bool>();
        // 上一次更新的摄像机块坐标（用于检测是否需要更新）
        private int _lastCamChunkX = int.MinValue;
        private int _lastCamChunkY = int.MinValue;
        // 地图配置
        private MapConfig _config;
        // 每维总块数
        private int _chunkCount;
        // 等待加载的块队列
        private Queue<Vector2Int> _loadQueue = new Queue<Vector2Int>();
        // 等待卸载的块队列
        private List<string> _unloadList = new List<string>();

        /// <summary>当前已加载的块数量</summary>
        public int LoadedChunkCount => _loadedChunks.Count;

        /// <summary>
        /// 初始化分块管理器
        /// </summary>
        /// <param name="mapGenerator">地图生成器实例</param>
        /// <param name="terrainTilemap">地形Tilemap组件</param>
        /// <param name="config">地图配置</param>
        public void Initialize(MapGenerator mapGenerator, Tilemap terrainTilemap, MapConfig config)
        {
            _mapGenerator = mapGenerator;
            _terrainTilemap = terrainTilemap;
            _config = config;
            _chunkCount = Mathf.CeilToInt((float)_config.MapWidth / Constants.CHUNK_SIZE);

            Debug.Log($"[ChunkManager] 初始化完成 - 总块数: {_chunkCount}×{_chunkCount}");
        }

        /// <summary>
        /// 每帧更新：检测摄像机位置变化，加载/卸载块
        /// </summary>
        private void Update()
        {
            if (_mapGenerator == null || _terrainTilemap == null || Camera.main == null) return;

            // 获取摄像机位置对应的块坐标
            Vector3 camPos = Camera.main.transform.position;
            int camChunkX = Mathf.FloorToInt(camPos.x / Constants.CHUNK_SIZE);
            int camChunkY = Mathf.FloorToInt(camPos.y / Constants.CHUNK_SIZE);

            // 摄像机未移动到新块，跳过
            if (camChunkX == _lastCamChunkX && camChunkY == _lastCamChunkY) return;

            _lastCamChunkX = camChunkX;
            _lastCamChunkY = camChunkY;

            // 计算需要加载的块范围
            int loadRadius = 3 + Constants.CHUNK_LOAD_MARGIN; // 视野半径 + 额外边距

            // 收集需要加载的块
            var chunksToLoad = new HashSet<string>();
            for (int dx = -loadRadius; dx <= loadRadius; dx++)
            {
                for (int dy = -loadRadius; dy <= loadRadius; dy++)
                {
                    int cx = camChunkX + dx;
                    int cy = camChunkY + dy;

                    // 边界检查
                    if (cx < 0 || cx >= _chunkCount || cy < 0 || cy >= _chunkCount) continue;

                    string key = GetChunkKey(cx, cy);
                    chunksToLoad.Add(key);

                    // 未加载的块加入队列
                    if (!_loadedChunks.ContainsKey(key))
                    {
                        _loadQueue.Enqueue(new Vector2Int(cx, cy));
                    }
                }
            }

            // 收集需要卸载的块（不在视野范围内的）
            _unloadList.Clear();
            foreach (var kvp in _loadedChunks)
            {
                if (!chunksToLoad.Contains(kvp.Key))
                {
                    _unloadList.Add(kvp.Key);
                }
            }

            // 每帧限制处理的块数
            int processed = 0;

            // 卸载块
            foreach (var key in _unloadList)
            {
                if (processed >= Constants.CHUNK_BUDGET_PER_FRAME) break;
                UnloadChunk(key);
                processed++;
            }

            // 加载块
            while (_loadQueue.Count > 0 && processed < Constants.CHUNK_BUDGET_PER_FRAME)
            {
                var pos = _loadQueue.Dequeue();
                string key = GetChunkKey(pos.x, pos.y);
                if (!_loadedChunks.ContainsKey(key))
                {
                    LoadChunk(pos.x, pos.y);
                    processed++;
                }
            }
        }

        /// <summary>
        /// 加载指定块：生成数据 + 渲染到Tilemap
        /// </summary>
        private void LoadChunk(int chunkX, int chunkY)
        {
            string key = GetChunkKey(chunkX, chunkY);

            // 确保块数据已生成
            _mapGenerator.GenerateChunk(chunkX, chunkY);

            var chunkData = _mapGenerator.Chunks.ContainsKey(key) ? _mapGenerator.Chunks[key] : null;
            if (chunkData == null) return;

            // 将块数据渲染到Tilemap
            RenderChunk(chunkData);

            _loadedChunks[key] = true;
        }

        /// <summary>
        /// 卸载指定块：从Tilemap清除
        /// </summary>
        private void UnloadChunk(string key)
        {
            // 解析块坐标
            var parts = key.Split('_');
            int cx = int.Parse(parts[0]);
            int cy = int.Parse(parts[1]);

            // 清除Tilemap上对应区域
            int startX = cx * Constants.CHUNK_SIZE;
            int startY = cy * Constants.CHUNK_SIZE;

            // 使用BoundsInt批量清除
            var bounds = new BoundsInt(
                new Vector3Int(startX, startY, 0),
                new Vector3Int(Constants.CHUNK_SIZE, Constants.CHUNK_SIZE, 1));
            _terrainTilemap.SetTilesBlock(bounds, new TileBase[Constants.CHUNK_SIZE * Constants.CHUNK_SIZE]);

            _loadedChunks.Remove(key);
        }

        /// <summary>
        /// 将块数据渲染到Tilemap
        /// 使用批量设置优化性能
        /// </summary>
        private void RenderChunk(ChunkData chunkData)
        {
            int size = chunkData.Width * chunkData.Height;
            var positions = new Vector3Int[size];
            var tiles = new TileBase[size];

            int index = 0;
            for (int x = 0; x < chunkData.Width; x++)
            {
                for (int y = 0; y < chunkData.Height; y++)
                {
                    var tileData = chunkData.GetTile(x, y);
                    if (tileData != null)
                    {
                        positions[index] = new Vector3Int(tileData.X, tileData.Y, 0);
                        tiles[index] = TilePalette.GetTerrainTile(tileData.TileType);
                    }
                    index++;
                }
            }

            // 批量设置Tile（性能远优于逐个SetTile）
            _terrainTilemap.SetTiles(positions, tiles);
        }

        /// <summary>
        /// 获取指定世界坐标的瓦片数据
        /// </summary>
        public TileData GetTileAt(int worldX, int worldY)
        {
            return _mapGenerator?.GetTileAt(worldX, worldY);
        }

        /// <summary>
        /// 立即加载摄像机周围所有块（场景初始化时使用）
        /// </summary>
        public void LoadInitialChunks(Vector2 centerPosition)
        {
            int centerChunkX = Mathf.FloorToInt(centerPosition.x / Constants.CHUNK_SIZE);
            int centerChunkY = Mathf.FloorToInt(centerPosition.y / Constants.CHUNK_SIZE);
            int radius = 4; // 初始加载更大范围

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int cx = centerChunkX + dx;
                    int cy = centerChunkY + dy;
                    if (cx >= 0 && cx < _chunkCount && cy >= 0 && cy < _chunkCount)
                    {
                        LoadChunk(cx, cy);
                    }
                }
            }

            // 更新摄像机块坐标
            _lastCamChunkX = centerChunkX;
            _lastCamChunkY = centerChunkY;
        }

        /// <summary>
        /// 清除所有加载的块
        /// </summary>
        public void ClearAll()
        {
            foreach (var key in new List<string>(_loadedChunks.Keys))
            {
                UnloadChunk(key);
            }
            _loadedChunks.Clear();
            _loadQueue.Clear();
            _unloadList.Clear();
        }

        private static string GetChunkKey(int cx, int cy) => $"{cx}_{cy}";
    }
}
