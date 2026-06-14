/// <summary>
/// 游戏时间系统
/// 管理游戏内时间流逝、天数计数、昼夜循环
/// 支持时间加速和暂停
/// </summary>
using GalaxyAgent.Config;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.World
{
    public class TimeSystem
    {
        // 运行时游戏配置访问（null安全回退）
        private static readonly GameConfig _fallbackConfig = new GameConfig();
        private static GameConfig Cfg => GameConfigManager.Instance != null
            ? GameConfigManager.Instance.Config : _fallbackConfig;
        // 时间参数
        private DayNightMode _dayNightMode;
        private float _timeRatio; // 现实秒 → 游戏秒的转换比例
        private float _gameTimeSeconds; // 游戏内累计秒数
        private float _realPlayTime; // 现实游玩总时间（秒）

        /// <summary>当前游戏天数（从1开始）</summary>
        public int GameDay { get; private set; } = 1;
        /// <summary>当前游戏内小时（0-23）</summary>
        public float GameHour { get; private set; }
        /// <summary>当前时段</summary>
        public TimeOfDay CurrentTimeOfDay { get; private set; } = TimeOfDay.Day;
        /// <summary>现实游玩总时间（秒）</summary>
        public float PlayTimeSeconds => _realPlayTime;
        /// <summary>时间是否在流动</summary>
        public bool IsRunning => GameManager.Instance != null &&
                                 !GameManager.Instance.IsPaused &&
                                 GameManager.Instance.IsInGame;

        /// <summary>
        /// 初始化时间系统
        /// </summary>
        /// <param name="mode">昼夜模式</param>
        /// <param name="timeRatio">时间比例（默认288 = 5分钟现实=1游戏日）</param>
        /// <param name="startDay">起始天数</param>
        public void Initialize(DayNightMode mode, float timeRatio = 288f, int startDay = 1)
        {
            _dayNightMode = mode;
            _timeRatio = timeRatio;
            GameDay = startDay;
            _gameTimeSeconds = 0f;
            _realPlayTime = 0f;

            // 永昼模式时间固定在中午
            if (mode == DayNightMode.EternalDay) GameHour = 12f;
            // 永夜模式时间固定在午夜
            else if (mode == DayNightMode.EternalNight) GameHour = 0f;
            else GameHour = Cfg.World.DayStartHour; // 交替模式从黎明开始

            UpdateTimeOfDay();

            Debug.Log($"[TimeSystem] 初始化 - 昼夜:{mode}, 时间比例:{timeRatio}, 第{startDay}天");
        }

        /// <summary>
        /// 每帧更新时间（由GameSceneController的Update调用）
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning) return;

            // 现实游玩时间
            _realPlayTime += deltaTime;

            // 根据昼夜模式决定是否更新时间
            if (_dayNightMode == DayNightMode.EternalDay ||
                _dayNightMode == DayNightMode.EternalNight)
            {
                // 永昼/永夜模式下时间仍然流逝（天数增加），但时段不变
                _gameTimeSeconds += deltaTime * _timeRatio * GameManager.Instance.TimeMultiplier;
            }
            else
            {
                // 交替模式：正常时间流逝
                _gameTimeSeconds += deltaTime * _timeRatio * GameManager.Instance.TimeMultiplier;
            }

            // 计算游戏小时数
            float dayProgress = _gameTimeSeconds / 86400f; // 86400秒 = 1天
            int newDay = Mathf.FloorToInt(dayProgress) + 1;

            // 新的一天
            if (newDay > GameDay)
            {
                GameDay = newDay;
                EventBus.Publish(new NewDayEvent { Day = GameDay });
            }

            // 当天内的小时
            GameHour = (_gameTimeSeconds % 86400f) / 3600f;

            // 更新时段
            TimeOfDay oldTime = CurrentTimeOfDay;
            UpdateTimeOfDay();
            if (CurrentTimeOfDay != oldTime)
            {
                EventBus.Publish(new TimeOfDayChangedEvent { NewTimeOfDay = CurrentTimeOfDay });
            }
        }

        /// <summary>
        /// 获取格式化的时间字符串
        /// </summary>
        public string GetTimeString()
        {
            int hours = Mathf.FloorToInt(GameHour);
            int minutes = Mathf.FloorToInt((GameHour - hours) * 60f);
            return $"{hours:D2}:{minutes:D2}";
        }

        /// <summary>
        /// 获取昼夜亮度系数（0=全黑, 1=全亮）
        /// </summary>
        public float GetDaylightFactor()
        {
            if (_dayNightMode == DayNightMode.EternalDay) return 1f;
            if (_dayNightMode == DayNightMode.EternalNight) return 0.15f;

            // 交替模式：根据小时计算亮度
            if (GameHour >= Cfg.World.DayStartHour && GameHour < Cfg.World.NightStartHour)
                return 1f; // 白天
            if (GameHour >= Cfg.World.NightStartHour || GameHour < Cfg.World.DayStartHour)
                return 0.2f; // 夜晚

            return 0.6f; // 默认
        }

        /// <summary>
        /// 更新时段枚举
        /// </summary>
        private void UpdateTimeOfDay()
        {
            if (_dayNightMode == DayNightMode.EternalDay)
            {
                CurrentTimeOfDay = TimeOfDay.Day;
                return;
            }
            if (_dayNightMode == DayNightMode.EternalNight)
            {
                CurrentTimeOfDay = TimeOfDay.Night;
                return;
            }

            if (GameHour >= 5 && GameHour < 7) CurrentTimeOfDay = TimeOfDay.Dawn;
            else if (GameHour >= 7 && GameHour < 18) CurrentTimeOfDay = TimeOfDay.Day;
            else if (GameHour >= 18 && GameHour < 20) CurrentTimeOfDay = TimeOfDay.Dusk;
            else CurrentTimeOfDay = TimeOfDay.Night;
        }

        /// <summary>
        /// 序列化时间数据（用于存档）
        /// </summary>
        public float GameTimeSeconds => _gameTimeSeconds;

        /// <summary>
        /// 从存档恢复时间
        /// </summary>
        public void LoadFromSave(int day, float gameTimeSeconds, float realPlayTime)
        {
            GameDay = day;
            _gameTimeSeconds = gameTimeSeconds;
            _realPlayTime = realPlayTime;
            GameHour = (_gameTimeSeconds % 86400f) / 3600f;
            UpdateTimeOfDay();
        }
    }
}
