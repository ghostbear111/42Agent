/// <summary>
/// 全局事件总线
/// 使用发布/订阅模式实现系统间松耦合通信
/// 用法：EventBus.Subscribe<MyEvent>(OnMyEvent); EventBus.Publish(new MyEvent());
/// </summary>
using System;
using System.Collections.Generic;

namespace GalaxyAgent.Core
{
    /// <summary>
    /// 所有事件必须实现此空接口，用于类型约束
    /// </summary>
    public interface IEvent { }

    public static class EventBus
    {
        // 订阅者字典：事件类型 -> 回调列表
        private static readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        /// 订阅指定类型的事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理回调</param>
        public static void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            var eventType = typeof(T);
            if (!_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType] = new List<Delegate>();
            }
            _subscribers[eventType].Add(handler);
        }

        /// <summary>
        /// 取消订阅指定类型的事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">要移除的回调</param>
        public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            var eventType = typeof(T);
            if (_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType].Remove(handler);
            }
        }

        /// <summary>
        /// 发布事件，通知所有订阅者
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        public static void Publish<T>(T eventData) where T : IEvent
        {
            var eventType = typeof(T);
            if (_subscribers.ContainsKey(eventType))
            {
                // 复制列表避免遍历时修改
                var handlers = _subscribers[eventType].ToArray();
                foreach (var handler in handlers)
                {
                    try
                    {
                        ((Action<T>)handler).Invoke(eventData);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"[EventBus] 处理事件 {eventType.Name} 时出错: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 清除所有订阅（场景切换或重置时使用）
        /// </summary>
        public static void Clear()
        {
            _subscribers.Clear();
        }

        /// <summary>
        /// 清除指定类型事件的所有订阅
        /// </summary>
        public static void Clear<T>() where T : IEvent
        {
            var eventType = typeof(T);
            if (_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType].Clear();
            }
        }
    }
}
