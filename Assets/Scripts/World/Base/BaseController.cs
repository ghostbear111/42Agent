/// <summary>
/// 基地控制器
/// 管理基地实体：仓库存储、生命值、Agent出生/返回点
/// Agent收集的资源存放于此，玩家可点击查看
/// </summary>
using System.Collections.Generic;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using UnityEngine;

namespace GalaxyAgent.World.Base
{
    public class BaseController : MonoBehaviour
    {
        // 基地属性
        /// <summary>基地生命值</summary>
        public float Health = 100f;
        /// <summary>基地最大生命值</summary>
        public float MaxHealth = 100f;
        /// <summary>仓库资源存储</summary>
        public Dictionary<ResourceType, float> Storage = new Dictionary<ResourceType, float>();

        // 点击检测
        private SpriteRenderer _renderer;
        private Collider2D _collider;
        private bool _initialized = false;

        /// <summary>
        /// 初始化基地
        /// </summary>
        public void Initialize(Vector2 position)
        {
            transform.position = position;
            name = "Base";

            // 确保有渲染组件
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<SpriteRenderer>();

            // 创建白色方块Sprite表示基地
            _renderer.sprite = CreateColorSprite(Constants.COLOR_BASE);
            _renderer.color = Constants.COLOR_BASE;
            _renderer.sortingOrder = 5; // 在地形之上

            // 确保有碰撞体（用于点击检测）
            _collider = GetComponent<BoxCollider2D>();
            if (_collider == null)
                _collider = gameObject.AddComponent<BoxCollider2D>();

            // 设置缩放让基地显眼一点
            transform.localScale = new Vector3(2f, 2f, 1f);

            _initialized = true;
            Debug.Log($"[BaseController] 基地初始化于 ({position.x}, {position.y})");
        }

        /// <summary>
        /// 存入资源到仓库
        /// </summary>
        public void DepositResource(ResourceType type, float amount)
        {
            if (!Storage.ContainsKey(type))
                Storage[type] = 0f;
            Storage[type] += amount;
        }

        /// <summary>
        /// 从仓库取出资源
        /// </summary>
        public float WithdrawResource(ResourceType type, float requestedAmount)
        {
            if (!Storage.ContainsKey(type) || Storage[type] <= 0) return 0f;

            float actual = Mathf.Min(requestedAmount, Storage[type]);
            Storage[type] -= actual;
            return actual;
        }

        /// <summary>
        /// 获取仓库中指定资源的数量
        /// </summary>
        public float GetResourceAmount(ResourceType type)
        {
            return Storage != null && Storage.ContainsKey(type) ? Storage[type] : 0f;
        }

        /// <summary>
        /// 鼠标点击检测
        /// </summary>
        private void OnMouseDown()
        {
            Debug.Log("[BaseController] 基地被点击");
            EventBus.Publish(new BaseClickedEvent());
        }

        /// <summary>
        /// 创建纯色Sprite
        /// </summary>
        private static Sprite CreateColorSprite(Color color)
        {
            int size = 32;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
