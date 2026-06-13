/// <summary>
/// 天气系统
/// 根据星球天气参数和当前时间管理天气状态变化
/// </summary>
using System;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.World
{
    public class WeatherSystem
    {
        // 天气参数
        private WeatherPattern _pattern;
        // 当前天气
        private WeatherType _currentWeather = WeatherType.Clear;
        // 天气变化计时器
        private float _weatherTimer;
        // 下次天气变化间隔
        private float _nextChangeInterval;
        // 随机数生成器
        private System.Random _rng;

        /// <summary>当前天气</summary>
        public WeatherType CurrentWeather => _currentWeather;

        /// <summary>
        /// 初始化天气系统
        /// </summary>
        public void Initialize(WeatherPattern pattern, int seed)
        {
            _pattern = pattern;
            _rng = new System.Random(seed + 40000);
            _currentWeather = WeatherType.Clear;

            // 根据模式设置天气变化频率
            _nextChangeInterval = pattern switch
            {
                WeatherPattern.Mild => 600f,      // 温和：10分钟可能变化
                WeatherPattern.Variable => 180f,   // 多变：3分钟
                WeatherPattern.Harsh => 60f,       // 恶劣：1分钟
                _ => 300f
            };

            _weatherTimer = _nextChangeInterval;

            Debug.Log($"[WeatherSystem] 初始化 - 模式:{pattern}, 变化间隔:{_nextChangeInterval}秒");
        }

        /// <summary>
        /// 每帧更新天气
        /// </summary>
        public void Tick(float gameDeltaTime)
        {
            _weatherTimer -= gameDeltaTime;
            if (_weatherTimer <= 0)
            {
                ChangeWeather();
                _weatherTimer = _nextChangeInterval * (0.5f + (float)_rng.NextDouble());
            }
        }

        /// <summary>
        /// 随机切换天气
        /// </summary>
        private void ChangeWeather()
        {
            WeatherType oldWeather = _currentWeather;

            // 根据模式决定天气变化概率
            float clearChance = _pattern switch
            {
                WeatherPattern.Mild => 0.7f,
                WeatherPattern.Variable => 0.3f,
                WeatherPattern.Harsh => 0.1f,
                _ => 0.5f
            };

            float roll = (float)_rng.NextDouble();
            if (roll < clearChance)
            {
                _currentWeather = WeatherType.Clear;
            }
            else
            {
                // 随机选择恶劣天气
                var badWeathers = new[] {
                    WeatherType.Sandstorm, WeatherType.AcidRain,
                    WeatherType.ColdWave, WeatherType.MagneticStorm, WeatherType.Blizzard
                };
                _currentWeather = badWeathers[_rng.Next(badWeathers.Length)];
            }

            if (_currentWeather != oldWeather)
            {
                Debug.Log($"[WeatherSystem] 天气变化: {oldWeather} → {_currentWeather}");
                EventBus.Publish(new WeatherChangedEvent { NewWeather = _currentWeather });
            }
        }

        /// <summary>
        /// 获取当前天气对Agent的影响系数
        /// 返回(视野修正, 移动修正, 能量消耗修正)
        /// </summary>
        public (float visibility, float moveSpeed, float energyDrain) GetWeatherEffects()
        {
            return _currentWeather switch
            {
                WeatherType.Clear => (1f, 1f, 1f),
                WeatherType.Sandstorm => (0.3f, 0.5f, 1.5f),
                WeatherType.AcidRain => (0.6f, 0.7f, 1.3f),
                WeatherType.ColdWave => (0.5f, 0.6f, 1.8f),
                WeatherType.MagneticStorm => (0.4f, 0.8f, 2f),
                WeatherType.Blizzard => (0.2f, 0.3f, 2f),
                _ => (1f, 1f, 1f)
            };
        }
    }
}
