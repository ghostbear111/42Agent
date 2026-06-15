/// <summary>
/// 地图生成器
/// 根据种子和配置参数生成整个星球地图
/// 核心流程：种子 → 噪声 → 生物群系 → 瓦片类型 → 资源分布 → 威胁分布
/// </summary>
using System;
using System.Collections;
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.Map
{
    public class MapGenerator
    {
        // 地图配置
        private MapConfig _config;
        // 实际使用的种子
        private int _seed;
        // 所有分块数据（按chunkX, chunkY索引）
        private Dictionary<string, ChunkData> _chunks = new Dictionary<string, ChunkData>();
        // 所有资源节点
        private List<ResourceNodeData> _resources = new List<ResourceNodeData>();
        // 所有威胁实体
        private List<ThreatData> _threats = new List<ThreatData>();
        // 所有发现点
        private List<DiscoveryData> _discoveries = new List<DiscoveryData>();
        // 分块数量（每维）
        private int _chunkCountX;
        private int _chunkCountY;

        /// <summary>地图宽度（格数）</summary>
        public int MapWidth => _config.MapWidth;
        /// <summary>所有资源节点</summary>
        public List<ResourceNodeData> Resources => _resources;
        /// <summary>所有威胁</summary>
        public List<ThreatData> Threats => _threats;
        /// <summary>所有发现点</summary>
        public List<DiscoveryData> Discoveries => _discoveries;
        /// <summary>所有分块</summary>
        public Dictionary<string, ChunkData> Chunks => _chunks;

        /// <summary>
        /// 构造地图生成器
        /// </summary>
        public MapGenerator(MapConfig config, int seed)
        {
            _config = config;
            _seed = seed;
            _chunkCountX = Mathf.CeilToInt((float)_config.MapWidth / Constants.CHUNK_SIZE);
            _chunkCountY = _chunkCountX;
        }

        /// <summary>
        /// 生成完整地图数据
        /// 按需调用：仅在进入游戏场景时生成
        /// </summary>
        public void Generate()
        {
            Debug.Log($"[MapGenerator] 开始生成地图 - 种子:{_seed}, " +
                      $"大小:{_config.MapWidth}×{_config.MapWidth}, " +
                      $"分块数:{_chunkCountX}×{_chunkCountY}");

            // 初始化Tile调色板
            TilePalette.ClearCache();

            // 生成所有块的地形数据
            for (int cx = 0; cx < _chunkCountX; cx++)
            {
                for (int cy = 0; cy < _chunkCountY; cy++)
                {
                    GenerateChunk(cx, cy);
                }
            }

            // 分布资源节点
            DistributeResources();

            // 分布威胁实体
            DistributeThreats();

            Debug.Log($"[MapGenerator] 地图生成完成 - 资源:{_resources.Count}, 威胁:{_threats.Count}");
        }

        /// <summary>
        /// 协程版地图生成：按地形行（cx）分批 yield，让调用方在生成期间刷新 Loading 进度。
        /// 计算顺序与 <see cref="Generate"/> 完全一致，保证种子确定性。
        /// 适用于大地图：同步 Generate 会阻塞主线程导致进度条卡死，协程版按行让出帧，进度条平滑推进。
        /// 进度区间：地形行 0→0.75，资源 0.78，威胁 0.82，生成完成 0.85（后续阶段由调用方填到 1）。
        /// </summary>
        /// <param name="onProgress">进度回调：(阶段提示, 0~0.85 进度)</param>
        public IEnumerator GenerateCoroutine(Action<string, float> onProgress = null)
        {
            // 初始化Tile调色板
            TilePalette.ClearCache();

            int totalChunks = _chunkCountX * _chunkCountY;
            int done = 0;
            // 自适应 yield 间隔：约 40 次进度更新，避免行数过多时帧开销累积
            int yieldInterval = Math.Max(1, _chunkCountX / 40);

            // 按行生成所有块的地形数据
            for (int cx = 0; cx < _chunkCountX; cx++)
            {
                for (int cy = 0; cy < _chunkCountY; cy++)
                {
                    GenerateChunk(cx, cy);
                }
                done += _chunkCountY;

                // 每隔若干行报进度并让出一帧（让 Loading 渲染 + 进度推进）
                if (cx % yieldInterval == 0 || cx == _chunkCountX - 1)
                {
                    onProgress?.Invoke("正在生成地形…", 0.75f * done / totalChunks);
                    yield return null;
                }
            }

            // 分布资源节点
            onProgress?.Invoke("正在分布资源…", 0.78f);
            yield return null;
            DistributeResources();

            // 分布威胁实体
            onProgress?.Invoke("正在分布威胁…", 0.82f);
            yield return null;
            DistributeThreats();

            onProgress?.Invoke("地图生成完成", 0.85f);
            Debug.Log($"[MapGenerator] 地图生成完成(协程) - 资源:{_resources.Count}, 威胁:{_threats.Count}");
        }

        /// <summary>
        /// 只生成指定区域的块（按需加载模式）
        /// </summary>
        public void GenerateChunk(int chunkX, int chunkY)
        {
            string key = GetChunkKey(chunkX, chunkY);
            if (_chunks.ContainsKey(key)) return;

            var chunk = new ChunkData(chunkX, chunkY, Constants.CHUNK_SIZE, Constants.CHUNK_SIZE);

            // 遍历块内每个格子
            for (int lx = 0; lx < chunk.Width; lx++)
            {
                for (int ly = 0; ly < chunk.Height; ly++)
                {
                    // 计算世界坐标
                    int worldX = chunk.WorldStartX + lx;
                    int worldY = chunk.WorldStartY + ly;

                    // 超出地图边界的格子设为不可通行
                    if (worldX >= _config.MapWidth || worldY >= _config.MapWidth)
                    {
                        chunk.SetTile(lx, ly, new TileData(worldX, worldY, TileType.Impassable));
                        continue;
                    }

                    // 生成各种噪声值
                    float heightNoise = NoiseGenerator.GenerateNoise(worldX, worldY, _seed);
                    float moistureNoise = NoiseGenerator.GenerateMoisture(worldX, worldY, _seed);
                    float tempNoise = NoiseGenerator.GenerateTemperature(worldX, worldY, _seed);
                    float radNoise = NoiseGenerator.GenerateRadiation(worldX, worldY, _seed);

                    // 判定生物群系
                    BiomeType biome = BiomeManager.DetermineBiome(
                        heightNoise, moistureNoise, tempNoise, _config.Terrain);

                    // 判定瓦片地形
                    TileType tileType = BiomeManager.DetermineTileType(
                        heightNoise, biome, _config.Terrain);

                    // 创建瓦片数据
                    var tile = new TileData(worldX, worldY, tileType)
                    {
                        Biome = biome,
                        Height = heightNoise,
                        Temperature = BiomeManager.GetBiomeTemperature(biome) + (tempNoise - 0.5f) * 20f,
                        Radiation = Mathf.Clamp01(
                            BiomeManager.GetBiomeRadiation(biome) + (radNoise - 0.5f) * 0.3f)
                    };

                    chunk.SetTile(lx, ly, tile);
                }
            }

            chunk.IsGenerated = true;
            _chunks[key] = chunk;
        }

        /// <summary>
        /// 分布资源节点
        /// 根据资源丰富度参数在地图上放置资源点
        /// </summary>
        private void DistributeResources()
        {
            _resources.Clear();

            // 根据资源丰富度确定资源密度
            float density = _config.Resources == ResourceAbundance.Scarce ? 0.003f :
                            _config.Resources == ResourceAbundance.Moderate ? 0.007f : 0.012f;

            // 使用种子化的随机数
            var rng = new System.Random(_seed + 20000);

            // 资源类型列表
            ResourceType[] types = (ResourceType[])Enum.GetValues(typeof(ResourceType));

            int resourceId = 0;

            for (int x = 0; x < _config.MapWidth; x += 8) // 每8格采样一次
            {
                for (int y = 0; y < _config.MapWidth; y += 8)
                {
                    // 随机决定此区域是否有资源
                    if (rng.NextDouble() > density) continue;

                    // 获取此位置的瓦片数据
                    var tile = GetTileAt(x, y);
                    if (tile == null || !tile.IsWalkable || tile.TileType == TileType.Lake) continue;

                    // 根据生物群系倾向选择资源类型
                    ResourceType resType = GetBiomeResourcePreference(tile.Biome, rng);

                    // 在采样点附近随机偏移放置资源
                    int resX = x + rng.Next(-3, 4);
                    int resY = y + rng.Next(-3, 4);
                    resX = Mathf.Clamp(resX, 0, _config.MapWidth - 1);
                    resY = Mathf.Clamp(resY, 0, _config.MapWidth - 1);

                    float maxAmount = 50f + (float)rng.NextDouble() * 150f;

                    var resource = new ResourceNodeData
                    {
                        ResourceId = resourceId++,
                        ResourceType = resType,
                        Name = GetResourceName(resType),
                        Position = new Vector2Int(resX, resY),
                        Amount = maxAmount,
                        MaxAmount = maxAmount,
                        Hardness = resType == ResourceType.Mineral ? 3f :
                                   resType == ResourceType.Crystal ? 2f : 1f
                    };

                    _resources.Add(resource);

                    // 标记瓦片上的资源ID
                    var resTile = GetTileAt(resX, resY);
                    if (resTile != null)
                    {
                        resTile.ResourceNodeId = resource.ResourceId;
                    }
                }
            }
        }

        /// <summary>
        /// 分布威胁实体
        /// 根据风险等级参数在地图上放置敌人/危险
        /// </summary>
        private void DistributeThreats()
        {
            _threats.Clear();

            // 根据风险等级确定威胁密度
            float density = _config.Risk == RiskLevel.Low ? 0.001f :
                            _config.Risk == RiskLevel.Medium ? 0.003f : 0.006f;

            var rng = new System.Random(_seed + 30000);
            int threatId = 0;

            // 威胁类型列表
            string[] threatTypes = { "burrow_beast", "hostile_robot", "swarm_insect", "radiation_zone" };

            for (int x = 0; x < _config.MapWidth; x += 12)
            {
                for (int y = 0; y < _config.MapWidth; y += 12)
                {
                    if (rng.NextDouble() > density) continue;

                    var tile = GetTileAt(x, y);
                    if (tile == null || tile.TileType == TileType.Lake) continue;

                    // 危险地形增加威胁等级
                    float threatLevel = tile.TileType == TileType.Volcano ? 0.8f :
                                        tile.TileType == TileType.Canyon ? 0.6f :
                                        tile.TileType == TileType.Ruins ? 0.5f : 0.3f;

                    string tType = threatTypes[rng.Next(threatTypes.Length)];
                    float maxHp = 50f + threatLevel * 100f;

                    var threat = new ThreatData
                    {
                        ThreatId = threatId++,
                        ThreatType = tType,
                        Name = GetThreatName(tType),
                        Position = new Vector2Int(
                            Mathf.Clamp(x + rng.Next(-5, 6), 0, _config.MapWidth - 1),
                            Mathf.Clamp(y + rng.Next(-5, 6), 0, _config.MapWidth - 1)),
                        Health = maxHp,
                        MaxHealth = maxHp,
                        Damage = 5f + threatLevel * 15f,
                        DetectionRange = 3f + threatLevel * 5f,
                        AttackRange = 1f + threatLevel * 2f,
                        IsAlive = true,
                        ThreatLevel = threatLevel
                    };

                    _threats.Add(threat);
                }
            }
        }

        /// <summary>
        /// 获取指定世界坐标的瓦片数据
        /// </summary>
        public TileData GetTileAt(int worldX, int worldY)
        {
            if (worldX < 0 || worldX >= _config.MapWidth || worldY < 0 || worldY >= _config.MapWidth)
                return null;

            int cx = worldX / Constants.CHUNK_SIZE;
            int cy = worldY / Constants.CHUNK_SIZE;
            string key = GetChunkKey(cx, cy);

            if (!_chunks.TryGetValue(key, out var chunk)) return null;

            chunk.WorldToLocal(worldX, worldY, out int lx, out int ly);
            return chunk.GetTile(lx, ly);
        }

        // ==================== 辅助方法 ====================

        /// <summary>获取块索引键</summary>
        private static string GetChunkKey(int cx, int cy) => $"{cx}_{cy}";

        /// <summary>根据生物群系倾向选择资源类型</summary>
        private ResourceType GetBiomeResourcePreference(BiomeType biome, System.Random rng)
        {
            float roll = (float)rng.NextDouble();
            switch (biome)
            {
                case BiomeType.Volcanic:
                    return roll < 0.6f ? ResourceType.Crystal : ResourceType.Mineral;
                case BiomeType.Desert:
                    return roll < 0.5f ? ResourceType.Crystal : ResourceType.Mineral;
                case BiomeType.CrystalWaste:
                    return roll < 0.7f ? ResourceType.Crystal : ResourceType.RuinData;
                case BiomeType.RuinField:
                    return roll < 0.5f ? ResourceType.RuinData : ResourceType.Mineral;
                case BiomeType.Swamp:
                    return roll < 0.5f ? ResourceType.Organic : ResourceType.Water;
                case BiomeType.Forest:
                    return roll < 0.5f ? ResourceType.Organic : ResourceType.Water;
                case BiomeType.Tundra:
                    return roll < 0.5f ? ResourceType.Water : ResourceType.Mineral;
                default:
                    return (ResourceType)rng.Next(5);
            }
        }

        /// <summary>获取资源显示名称</summary>
        private static string GetResourceName(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Mineral: return "铁矿石";
                case ResourceType.Crystal: return "能源晶体";
                case ResourceType.Water: return "水源";
                case ResourceType.Organic: return "有机物";
                case ResourceType.RuinData: return "遗迹数据";
                default: return "未知资源";
            }
        }

        /// <summary>获取威胁显示名称</summary>
        private static string GetThreatName(string type)
        {
            switch (type)
            {
                case "burrow_beast": return "潜地兽";
                case "hostile_robot": return "敌对机器人";
                case "swarm_insect": return "虫群";
                case "radiation_zone": return "辐射区";
                default: return "未知威胁";
            }
        }
    }
}
