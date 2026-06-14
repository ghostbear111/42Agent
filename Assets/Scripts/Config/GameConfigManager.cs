/// <summary>
/// 游戏配置运行时管理器（全局单例）
/// 持有当前生效的 GameConfig，供各游戏系统在运行时读取（替代 Constants 中的可调常量）。
///
/// 生命周期：
/// - 首次访问 Instance 时自动创建并从磁盘加载配置（Awake 中调用 GameConfigStore.Load）
/// - 跨场景 DontDestroyOnLoad
/// - 运行时面板/编辑器修改后调用 Save() 持久化；ResetToDefaults() 恢复默认
///
/// 注意：编辑器非播放模式下不存在 MonoBehaviour 实例，编辑器窗口请直接用 GameConfigStore 读写文件。
/// </summary>
using GalaxyAgent.Core;
using UnityEngine;

namespace GalaxyAgent.Config
{
    public class GameConfigManager : Singleton<GameConfigManager>
    {
        /// <summary>当前生效的配置（运行时可读写）</summary>
        public GameConfig Config { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            // 加载磁盘配置，不存在则使用默认（并会在首次保存时落盘）
            Config = GameConfigStore.Load();
            Debug.Log("[GameConfigManager] 初始化完成，配置已加载");
        }

        /// <summary>从磁盘重新加载配置（覆盖当前未保存的修改）</summary>
        public void Reload()
        {
            Config = GameConfigStore.Load();
            Debug.Log("[GameConfigManager] 配置已从磁盘重新加载");
        }

        /// <summary>把当前配置写入磁盘</summary>
        public void Save()
        {
            GameConfigStore.Save(Config);
        }

        /// <summary>恢复为默认配置并落盘</summary>
        public void ResetToDefaults()
        {
            Config = GameConfigStore.CreateDefault();
            GameConfigStore.Save(Config);
            Debug.Log("[GameConfigManager] 配置已重置为默认");
        }
    }
}
