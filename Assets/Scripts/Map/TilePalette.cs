/// <summary>
/// 瓦片调色板
/// 在运行时生成不同颜色的Tile资产，用于地图渲染
/// 每种地形/资源类型对应一种颜色的Tile
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GalaxyAgent.Map
{
    public static class TilePalette
    {
        // 缓存已创建的Tile，避免重复创建
        private static readonly Dictionary<TileType, Tile> _terrainTiles = new Dictionary<TileType, Tile>();
        private static readonly Dictionary<ResourceType, Tile> _resourceTiles = new Dictionary<ResourceType, Tile>();
        private static bool _initialized = false;

        /// <summary>
        /// 获取地形类型的Tile（带缓存）
        /// </summary>
        public static Tile GetTerrainTile(TileType tileType)
        {
            EnsureInitialized();

            if (!_terrainTiles.ContainsKey(tileType))
            {
                Color color = GetTerrainColor(tileType);
                _terrainTiles[tileType] = CreateColorTile(color);
            }

            return _terrainTiles[tileType];
        }

        /// <summary>
        /// 获取资源类型的Tile（带缓存）
        /// </summary>
        public static Tile GetResourceTile(ResourceType resourceType)
        {
            EnsureInitialized();

            if (!_resourceTiles.ContainsKey(resourceType))
            {
                _resourceTiles[resourceType] = CreateColorTile(GetResourceColor(resourceType));
            }

            return _resourceTiles[resourceType];
        }

        /// <summary>
        /// 根据地形类型获取对应颜色
        /// </summary>
        public static Color GetTerrainColor(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.Plain: return Constants.COLOR_TERRAIN_PLAIN;
                case TileType.Mountain: return Constants.COLOR_TERRAIN_MOUNTAIN;
                case TileType.Canyon: return Constants.COLOR_TERRAIN_CANYON;
                case TileType.Lake: return Constants.COLOR_TERRAIN_LAKE;
                case TileType.Volcano: return Constants.COLOR_TERRAIN_VOLCANO;
                case TileType.Ruins: return Constants.COLOR_TERRAIN_RUINS;
                case TileType.CrystalDesert: return Constants.COLOR_TERRAIN_CRYSTAL_DESERT;
                case TileType.Impassable: return new Color(0.15f, 0.12f, 0.1f);
                default: return Color.gray;
            }
        }

        /// <summary>
        /// 根据资源类型获取对应颜色
        /// </summary>
        public static Color GetResourceColor(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Mineral: return Constants.COLOR_MINERAL;
                case ResourceType.Crystal: return Constants.COLOR_CRYSTAL;
                case ResourceType.Water: return Constants.COLOR_WATER;
                case ResourceType.Organic: return Constants.COLOR_ORGANIC;
                case ResourceType.RuinData: return Constants.COLOR_RUIN;
                default: return Color.white;
            }
        }

        /// <summary>
        /// 创建纯色Tile资产
        /// </summary>
        private static Tile CreateColorTile(Color color)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = CreateColorSprite(color);
            tile.color = color;
            return tile;
        }

        /// <summary>
        /// 创建纯色Sprite
        /// </summary>
        private static Sprite CreateColorSprite(Color color)
        {
            // 创建32×32的纯色纹理
            int size = 32;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];

            // 填充颜色
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white; // Tile本身设白色，通过tile.color着色
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point; // 像素风格，无模糊
            texture.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f); // pivot居中, 32像素=1单位
        }

        /// <summary>
        /// 确保已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            // 预创建所有地形Tile
            _terrainTiles[TileType.Plain] = CreateColorTile(Constants.COLOR_TERRAIN_PLAIN);
            _terrainTiles[TileType.Mountain] = CreateColorTile(Constants.COLOR_TERRAIN_MOUNTAIN);
            _terrainTiles[TileType.Canyon] = CreateColorTile(Constants.COLOR_TERRAIN_CANYON);
            _terrainTiles[TileType.Lake] = CreateColorTile(Constants.COLOR_TERRAIN_LAKE);
            _terrainTiles[TileType.Volcano] = CreateColorTile(Constants.COLOR_TERRAIN_VOLCANO);
            _terrainTiles[TileType.Ruins] = CreateColorTile(Constants.COLOR_TERRAIN_RUINS);
            _terrainTiles[TileType.CrystalDesert] = CreateColorTile(Constants.COLOR_TERRAIN_CRYSTAL_DESERT);
            _terrainTiles[TileType.Impassable] = CreateColorTile(new Color(0.15f, 0.12f, 0.1f));

            // 预创建所有资源Tile
            _resourceTiles[ResourceType.Mineral] = CreateColorTile(Constants.COLOR_MINERAL);
            _resourceTiles[ResourceType.Crystal] = CreateColorTile(Constants.COLOR_CRYSTAL);
            _resourceTiles[ResourceType.Water] = CreateColorTile(Constants.COLOR_WATER);
            _resourceTiles[ResourceType.Organic] = CreateColorTile(Constants.COLOR_ORGANIC);
            _resourceTiles[ResourceType.RuinData] = CreateColorTile(Constants.COLOR_RUIN);
        }

        /// <summary>
        /// 清除缓存（场景切换时调用）
        /// </summary>
        public static void ClearCache()
        {
            _terrainTiles.Clear();
            _resourceTiles.Clear();
            _initialized = false;
        }
    }
}
