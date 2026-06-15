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
        private float _lastOrthoSize = -1f;
        // 地图配置
        private MapConfig _config;
        // 每维总块数
        private int _chunkCount;
        // 等待加载的块队列
        private Queue<Vector2Int> _loadQueue = new Queue<Vector2Int>();
        // 等待卸载的块队列
        private List<string> _unloadList = new List<string>();
        // 当前地图视觉风格(决定每格颜色 + 动画 shader 参数)
        private MapStyleProfile _currentProfile;
        // 动画材质(ScanlineFx shader)，每帧推进 _FxTime
        private Material _fxMaterial;
        private Shader _fxShader;

        /// <summary>当前已加载的块数量</summary>
        public int LoadedChunkCount => _loadedChunks.Count;

        /// <summary>
        /// 初始化分块管理器
        /// </summary>
        /// <param name="mapGenerator">地图生成器实例</param>
        /// <param name="terrainTilemap">地形Tilemap组件</param>
        /// <param name="config">地图配置</param>
        public void Initialize(MapGenerator mapGenerator, Tilemap terrainTilemap, MapConfig config,
            MapStyleProfile profile = null)
        {
            _mapGenerator = mapGenerator;
            _terrainTilemap = terrainTilemap;
            _config = config;
            _chunkCount = Mathf.CeilToInt((float)_config.MapWidth / Constants.CHUNK_SIZE);
            _currentProfile = profile ?? MapStyleProfilePalette.GetById(MapStyleProfilePalette.DefaultId);

            // 风格渲染 per-cell SetColor 性能不足，已暂时停用：RenderChunk 用批量 SetTiles 纯色块。
            // shader 动画材质暂不挂载；SetupFxMaterial/SetStyle/ApplyFxParams 代码保留备用。

            Debug.Log($"[ChunkManager] 初始化完成 - 总块数: {_chunkCount}×{_chunkCount} (纯色块模式)");
        }

        /// <summary>
        /// 每帧更新：检测摄像机位置变化，加载/卸载块
        /// </summary>
        private void Update()
        {
            if (_mapGenerator == null || _terrainTilemap == null || Camera.main == null) return;

            Vector3 camPos = Camera.main.transform.position;
            int camChunkX = Mathf.FloorToInt(camPos.x / Constants.CHUNK_SIZE);
            int camChunkY = Mathf.FloorToInt(camPos.y / Constants.CHUNK_SIZE);
            float orthoSize = Camera.main.orthographicSize;

            bool moved = camChunkX != _lastCamChunkX || camChunkY != _lastCamChunkY;
            bool zoomed = !Mathf.Approximately(orthoSize, _lastOrthoSize);

            // 摄像机/缩放变化 → 重新计算加载范围 + 卸载列表
            // (缩放只改 orthoSize 不改 camChunk，必须单独检测 orthoSize)
            if (moved || zoomed)
            {
                _lastCamChunkX = camChunkX;
                _lastCamChunkY = camChunkY;
                _lastOrthoSize = orthoSize;

                // 动态加载半径：覆盖当前视野(orthoSize×aspect) + 边距
                float aspect = Camera.main.aspect;
                int visRadius = Mathf.CeilToInt(orthoSize * Mathf.Max(1f, aspect) / Constants.CHUNK_SIZE);
                int loadRadius = Mathf.Max(3, visRadius) + Constants.CHUNK_LOAD_MARGIN;

                // 收集需要加载的块 + 未加载的入队
                _loadQueue.Clear();
                var chunksToLoad = new HashSet<string>();
                for (int dx = -loadRadius; dx <= loadRadius; dx++)
                {
                    for (int dy = -loadRadius; dy <= loadRadius; dy++)
                    {
                        int cx = camChunkX + dx;
                        int cy = camChunkY + dy;
                        if (cx < 0 || cx >= _chunkCount || cy < 0 || cy >= _chunkCount) continue;
                        string key = GetChunkKey(cx, cy);
                        chunksToLoad.Add(key);
                        if (!_loadedChunks.ContainsKey(key))
                            _loadQueue.Enqueue(new Vector2Int(cx, cy));
                    }
                }

                // 收集需要卸载的块(不在视野范围)
                _unloadList.Clear();
                foreach (var kvp in _loadedChunks)
                    if (!chunksToLoad.Contains(kvp.Key))
                        _unloadList.Add(kvp.Key);
            }

            // 每帧持续处理加载/卸载队列(budget)，直到队列空。
            // 关键：不再因 moved/zoomed=false 提前 return —— 否则未加载完的队列会被永久搁置 → 大片空白不恢复。
            int processed = 0;
            for (int i = 0; i < _unloadList.Count && processed < Constants.CHUNK_BUDGET_PER_FRAME; i++)
            {
                UnloadChunk(_unloadList[i]);
                processed++;
            }
            if (processed > 0) _unloadList.Clear();

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
        /// <summary>
        /// 将块数据渲染到Tilemap：批量 SetTiles + 纯色Tile(每 TileType 一个，自带 color)。
        /// 性能优先：批量 SetTiles 远快于 per-cell SetColor。
        /// (风格渲染的 per-cell SetColor 性能不足，已暂时停用；MapStyleProfile 等代码保留备用，
        ///  待加 chunk 颜色缓存优化后可恢复风格切换。)
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
            int radius = 6; // 初始加载更大范围(覆盖初始视野+余量)

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

        /// <summary>
        /// 切换地图视觉风格：更新材质参数 + 重渲染所有已加载块。由设置面板调用。
        /// </summary>
        public void SetStyle(MapStyleProfile profile)
        {
            if (profile == null) return;
            _currentProfile = profile;
            ApplyFxParams();

            // 重渲染所有已加载块(用新风格的配色)
            var keys = new List<string>(_loadedChunks.Keys);
            foreach (var key in keys)
            {
                var parts = key.Split('_');
                int cx = int.Parse(parts[0]);
                int cy = int.Parse(parts[1]);
                if (_mapGenerator.Chunks.TryGetValue(key, out var chunkData))
                    RenderChunk(chunkData);
            }
            Debug.Log($"[ChunkManager] 切换风格: {profile.Name}，重渲染 {keys.Count} 块");
        }

        /// <summary>给 TilemapRenderer 赋 ScanlineFx 动画材质</summary>
        private void SetupFxMaterial()
        {
            if (_fxShader == null) _fxShader = Resources.Load<Shader>("Shaders/ScanlineFx");
            if (_fxShader == null || _terrainTilemap == null)
            {
                Debug.LogWarning("[ChunkManager] ScanlineFx shader 未找到，地图将无动画");
                return;
            }
            var renderer = _terrainTilemap.GetComponent<TilemapRenderer>();
            if (renderer == null) return;
            _fxMaterial = new Material(_fxShader);
            renderer.material = _fxMaterial;
            ApplyFxParams();
        }

        /// <summary>把当前风格的 Fx 参数写入材质</summary>
        private void ApplyFxParams()
        {
            if (_fxMaterial == null || _currentProfile?.Fx == null) return;
            var fx = _currentProfile.Fx;
            _fxMaterial.SetFloat("_Mode", fx.Mode);
            _fxMaterial.SetFloat("_ScanFreq", fx.ScanFreq);
            _fxMaterial.SetFloat("_ScanSpeed", fx.ScanSpeed);
            _fxMaterial.SetFloat("_ScanAmp", fx.ScanAmp);
            _fxMaterial.SetFloat("_ScanWidth", fx.ScanWidth);
            _fxMaterial.SetColor("_ScanColor", fx.ScanColor);
            _fxMaterial.SetFloat("_CenterX", fx.CenterX);
            _fxMaterial.SetFloat("_CenterY", fx.CenterY);
            _fxMaterial.SetFloat("_PulseAmp", fx.PulseAmp);
            _fxMaterial.SetFloat("_PulseSpeed", fx.PulseSpeed);
            _fxMaterial.SetFloat("_FlickerAmp", fx.FlickerAmp);
            _fxMaterial.SetFloat("_FlickerSpeed", fx.FlickerSpeed);
            _fxMaterial.SetFloat("_FxTime", 0f);
        }

        private static string GetChunkKey(int cx, int cy) => $"{cx}_{cy}";
    }
}
