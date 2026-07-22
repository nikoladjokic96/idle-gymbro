using UnityEngine;
using IdleGymBro.Data;

namespace IdleGymBro.Core
{
    public readonly struct TickEvent : IGameEvent
    {
        public float DeltaTime { get; }

        public TickEvent(float deltaTime)
        {
            DeltaTime = deltaTime;
        }
    }

    public class TickSystem : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private float _accumulatedTime;
        private bool _missingConfigLogged;

        private void Update()
        {
            if (_gameConfig == null)
            {
                if (!_missingConfigLogged)
                {
                    Debug.LogError("TickSystem: GameConfig is not assigned. Ticking is disabled.");
                    _missingConfigLogged = true;
                }

                return;
            }

            float tickInterval = _gameConfig.TickIntervalSeconds;
            _accumulatedTime += Time.deltaTime;

            while (_accumulatedTime >= tickInterval)
            {
                _accumulatedTime -= tickInterval;
                EventBus.Publish(new TickEvent(tickInterval));
            }
        }
    }
}
