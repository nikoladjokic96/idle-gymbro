using System;
using System.Collections.Generic;

namespace IdleGymBro.Core
{
    public interface IGameEvent { }

    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            Type eventType = typeof(T);

            if (_handlers.TryGetValue(eventType, out Delegate existing))
            {
                _handlers[eventType] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[eventType] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            Type eventType = typeof(T);

            if (!_handlers.TryGetValue(eventType, out Delegate existing))
            {
                return;
            }

            Delegate remaining = Delegate.Remove(existing, handler);

            if (remaining == null)
            {
                _handlers.Remove(eventType);
            }
            else
            {
                _handlers[eventType] = remaining;
            }
        }

        public static void Publish<T>(T gameEvent) where T : IGameEvent
        {
            Type eventType = typeof(T);

            if (!_handlers.TryGetValue(eventType, out Delegate existing))
            {
                return;
            }

            ((Action<T>)existing).Invoke(gameEvent);
        }

        public static void Clear()
        {
            _handlers.Clear();
        }
    }
}
