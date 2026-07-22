using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Gameplay
{
    public readonly struct EnergyChangedEvent : IGameEvent
    {
        public float Current { get; }
        public float Max { get; }

        public EnergyChangedEvent(float current, float max)
        {
            Current = current;
            Max = max;
        }
    }

    public readonly struct RepPerformedEvent : IGameEvent { }

    public class EnergySystem : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private float _currentEnergy;
        private bool _missingConfigLogged;

        public float CurrentEnergy => _currentEnergy;
        public float MaxEnergy => _gameConfig != null ? _gameConfig.MaxEnergy : 0f;

        private void Awake()
        {
            if (!ValidateConfig())
            {
                return;
            }

            _currentEnergy = _gameConfig.MaxEnergy;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TapEvent>(HandleTap);
            EventBus.Subscribe<TickEvent>(HandleTick);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TapEvent>(HandleTap);
            EventBus.Unsubscribe<TickEvent>(HandleTick);
        }

        private void Start()
        {
            // Published in Start, not OnEnable, so all subscribers (e.g. UI) are ready to receive it.
            EventBus.Publish(new EnergyChangedEvent(_currentEnergy, MaxEnergy));
        }

        private void HandleTap(TapEvent e)
        {
            if (!ValidateConfig())
            {
                return;
            }

            if (_currentEnergy < _gameConfig.EnergyPerRep)
            {
                return;
            }

            _currentEnergy -= _gameConfig.EnergyPerRep;
            EventBus.Publish(new EnergyChangedEvent(_currentEnergy, MaxEnergy));
            EventBus.Publish(new RepPerformedEvent());
        }

        private void HandleTick(TickEvent e)
        {
            if (!ValidateConfig())
            {
                return;
            }

            if (_currentEnergy >= MaxEnergy)
            {
                return;
            }

            _currentEnergy = Mathf.Min(MaxEnergy, _currentEnergy + _gameConfig.EnergyRegenPerSecond * e.DeltaTime);
            EventBus.Publish(new EnergyChangedEvent(_currentEnergy, MaxEnergy));
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("EnergySystem: GameConfig is not assigned. Energy is disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }

        public void CaptureState(SaveData data)
        {
            data.CurrentEnergy = _currentEnergy;
        }

        public void RestoreState(SaveData data)
        {
            _currentEnergy = Mathf.Clamp(data.CurrentEnergy, 0f, MaxEnergy);
            EventBus.Publish(new EnergyChangedEvent(_currentEnergy, MaxEnergy));
        }
    }
}
