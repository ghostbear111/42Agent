/// <summary>
/// 噪声生成器
/// 使用种子化的Perlin噪声生成地形高度图
/// 用于地图生成的核心算法，相同种子始终产生相同结果
/// </summary>
using UnityEngine;

namespace GalaxyAgent.Map
{
    public static class NoiseGenerator
    {
        /// <summary>
        /// 生成2D噪声值（0-1范围）
        /// 使用多层Perlin噪声叠加（分形噪声）
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="seed">随机种子</param>
        /// <param name="scale">噪声缩放（值越大越平滑）</param>
        /// <param name="octaves">叠加层数</param>
        /// <param name="persistence">振幅衰减系数</param>
        /// <param name="lacunarity">频率增长系数</param>
        /// <returns>噪声值 0-1</returns>
        public static float GenerateNoise(float x, float y, int seed, float scale = 0.02f,
            int octaves = 4, float persistence = 0.5f, float lacunarity = 2f)
        {
            // 根据种子生成偏移量，确保不同种子产生不同结果
            float seedOffsetX = (seed % 1000) * 1.37f;
            float seedOffsetY = (seed % 1000) * 2.61f;

            float totalNoise = 0f;
            float amplitude = 1f;    // 当前层振幅
            float frequency = 1f;    // 当前层频率
            float maxAmplitude = 0f; // 振幅总和（用于归一化）

            for (int i = 0; i < octaves; i++)
            {
                // 计算采样坐标
                float sampleX = (x + seedOffsetX) * scale * frequency;
                float sampleY = (y + seedOffsetY) * scale * frequency;

                // Unity的Perlin噪声在0-1之间采样效果最好
                // 使用cos/sin偏移避免网格对齐问题
                float noise = Mathf.PerlinNoise(sampleX, sampleY);

                totalNoise += noise * amplitude;
                maxAmplitude += amplitude;

                // 每层频率增加，振幅减小
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // 归一化到0-1范围
            return totalNoise / maxAmplitude;
        }

        /// <summary>
        /// 生成温度噪声值
        /// </summary>
        public static float GenerateTemperature(float x, float y, int seed)
        {
            return GenerateNoise(x, y, seed + 5000, scale: 0.005f, octaves: 2);
        }

        /// <summary>
        /// 生成辐射噪声值
        /// </summary>
        public static float GenerateRadiation(float x, float y, int seed)
        {
            return GenerateNoise(x, y, seed + 10000, scale: 0.008f, octaves: 2);
        }

        /// <summary>
        /// 生成湿度噪声值（用于生物群系判定）
        /// </summary>
        public static float GenerateMoisture(float x, float y, int seed)
        {
            return GenerateNoise(x, y, seed + 15000, scale: 0.01f, octaves: 3);
        }
    }
}
