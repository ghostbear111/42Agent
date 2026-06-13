/// <summary>
/// 泛型单例基类
/// 继承此类的MonoBehaviour在场景中只会存在一个实例，并在场景切换时不销毁
/// 用法：public class GameManager : Singleton<GameManager>
/// </summary>
using UnityEngine;

namespace GalaxyAgent.Core
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        // 加锁对象，保证线程安全
        private static readonly object _lock = new object();
        
        // 单例实例
        private static T _instance;
        
        // 应用程序是否正在退出标志
        private static bool _applicationIsQuitting = false;

        /// <summary>
        /// 获取单例实例，如果不存在则自动创建
        /// </summary>
        public static T Instance
        {
            get
            {
                // 如果应用正在退出，不再创建实例
                if (_applicationIsQuitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 在场景中查找已有实例
                _instance = FindFirstObjectByType<T>();

                        if (_instance == null)
                        {
                            // 创建新的GameObject并挂载组件
                            var singleton = new GameObject($"[Singleton] {typeof(T).Name}");
                            _instance = singleton.AddComponent<T>();
                            DontDestroyOnLoad(singleton);
                        }
                    }

                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 已存在实例，销毁重复的
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _applicationIsQuitting = true;
            }
        }
    }
}
