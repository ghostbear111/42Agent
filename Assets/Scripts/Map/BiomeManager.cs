/// <summary>
/// 生物群系管理器
/// 根据噪声值和地图参数判定每个区域的生物群系类型
/// 以及温度、辐射、资源分布等环境特性
/// </summary>
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.Map
{
    public static class BiomeManager
    {
        /// <summary>
        /// 根据噪声值判定生物群系
        /// </summary>
        /// <param name="heightNoise">高度噪声值（0-1）</param>
        /// <param name="moistureNoise">湿度噪声值（0-1）</param>
        /// <param name="temperatureNoise">温度噪声值（0-1）</param>
        /// <param name="terrain">地形复杂度参数</param>
        /// <returns>生物群系类型</returns>
        public static BiomeType DetermineBiome(float heightNoise, float moistureNoise,
            float temperatureNoise, TerrainComplexity terrain)
        {
            // 根据地形复杂度调整阈值
            float heightModifier = terrain == TerrainComplexity.Flat ? 0.1f :
                                   terrain == TerrainComplexity.Dangerous ? -0.1f : 0f;

            float adjustedHeight = heightNoise + heightModifier;

            // 高海拔区域
            if (adjustedHeight > 0.75f)
            {
                return temperatureNoise > 0.6f ? BiomeType.Volcanic : BiomeType.Tundra;
            }

            // 中高海拔
            if (adjustedHeight > 0.6f)
            {
                return moistureNoise > 0.5f ? BiomeType.Forest : BiomeType.Grassland;
            }

            // 中海拔
            if (adjustedHeight > 0.35f)
            {
                if (moistureNoise < 0.3f) return BiomeType.Desert;
                if (moistureNoise > 0.7f) return BiomeType.Swamp;
                if (temperatureNoise > 0.7f) return BiomeType.CrystalWaste;
                return BiomeType.Grassland;
            }

            // 低海拔
            if (adjustedHeight > 0.25f)
            {
                return moistureNoise > 0.6f ? BiomeType.Swamp : BiomeType.RuinField;
            }

            // 极低海拔（水域或特殊区域）
            return BiomeType.Grassland;
        }

        /// <summary>
        /// 根据生物群系和参数判定瓦片地形类型
        /// </summary>
        public static TileType DetermineTileType(float heightNoise, BiomeType biome,
            TerrainComplexity terrain)
        {
            // 极低高度 = 不可通行（深水/悬崖）
            if (heightNoise < 0.15f) return TileType.Impassable;

            // 低高度 = 湖泊
            if (heightNoise < 0.22f) return TileType.Lake;

            // 高海拔根据生物群系决定
            if (heightNoise > 0.78f)
            {
                if (biome == BiomeType.Volcanic) return TileType.Volcano;
                return TileType.Mountain;
            }

            if (heightNoise > 0.65f) return TileType.Mountain;

            // 中海拔根据生物群系
            switch (biome)
            {
                case BiomeType.CrystalWaste:
                    return TileType.CrystalDesert;
                case BiomeType.RuinField:
                    return TileType.Ruins;
                case BiomeType.Volcanic:
                    return heightNoise > 0.5f ? TileType.Volcano : TileType.Plain;
                default:
                    // 峡谷在中海拔较低处随机出现
                    if (heightNoise < 0.3f && terrain != TerrainComplexity.Flat)
                        return TileType.Canyon;
                    return TileType.Plain;
            }
        }

        /// <summary>
        /// 获取生物群系的基线温度
        /// </summary>
        public static float GetBiomeTemperature(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Tundra: return -15f;
                case BiomeType.Volcanic: return 85f;
                case BiomeType.Desert: return 55f;
                case BiomeType.CrystalWaste: return 40f;
                case BiomeType.Swamp: return 30f;
                case BiomeType.Forest: return 22f;
                case BiomeType.RuinField: return 18f;
                default: return 25f;
            }
        }

        /// <summary>
        /// 获取生物群系的基线辐射值
        /// </summary>
        public static float GetBiomeRadiation(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Volcanic: return 0.6f;
                case BiomeType.RuinField: return 0.4f;
                case BiomeType.CrystalWaste: return 0.3f;
                default: return 0.05f;
            }
        }

        /// <summary>
        /// 获取生物群系的可见度
        /// </summary>
        public static float GetBiomeVisibility(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Swamp: return 0.5f;
                case BiomeType.Desert: return 0.9f;
                case BiomeType.Forest: return 0.6f;
                default: return 0.8f;
            }
        }
    }
}
