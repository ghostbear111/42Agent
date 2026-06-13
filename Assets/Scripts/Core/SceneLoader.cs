/// <summary>
/// 场景切换管理器
/// 封装异步场景加载逻辑，提供加载状态回调
/// </summary>
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GalaxyAgent.Core
{
    public class SceneLoader : MonoBehaviour
    {
        /// <summary>
        /// 异步加载指定场景
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        /// <param name="onComplete">加载完成回调</param>
        public static void LoadScene(string sceneName, System.Action onComplete = null)
        {
            // 查找或创建SceneLoader实例
            var loader = FindFirstObjectByType<SceneLoader>();
            if (loader == null)
            {
                var go = new GameObject("[SceneLoader]");
                loader = go.AddComponent<SceneLoader>();
            }
            loader.StartCoroutine(loader.LoadSceneAsync(sceneName, onComplete));
        }

        /// <summary>
        /// 协程：异步加载场景
        /// </summary>
        private IEnumerator LoadSceneAsync(string sceneName, System.Action onComplete)
        {
            var asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                Debug.LogError($"[SceneLoader] 场景 '{sceneName}' 不存在！");
                yield break;
            }

            // 允许场景加载完成后自动激活
            asyncOp.allowSceneActivation = true;

            // 等待加载完成
            while (!asyncOp.isDone)
            {
                // 可在此处更新加载进度条
                // Debug.Log($"加载进度: {asyncOp.progress * 100:F0}%");
                yield return null;
            }

            onComplete?.Invoke();
        }

        /// <summary>
        /// 重新加载当前场景
        /// </summary>
        public static void ReloadCurrentScene()
        {
            var currentScene = SceneManager.GetActiveScene().name;
            LoadScene(currentScene);
        }

        /// <summary>
        /// 获取当前场景名称
        /// </summary>
        public static string GetCurrentSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }
    }
}
