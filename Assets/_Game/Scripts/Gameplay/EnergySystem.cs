using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Progression;
using IdleGymBro.Economy;

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

        // Effective ceiling, raised by Carbs (Macros tab) on top of the config base. Cached from
        // StatsChangedEvent per the house pattern: the config value stands in until UpgradeManager
        // publishes for the first time, so a cold boot never reads zero.
        private float _maxEnergyOverride = -1f;

        public float CurrentEnergy => _currentEnergy;

        public float MaxEnergy => _maxEnergyOverride > 0f
            ? _maxEnergyOverride
            : (_gameConfig != null ? _gameConfig.MaxEnergy : 0f);

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
            EventBus.Subscribe<PrestigeEvent>(HandlePrestige);
            EventBus.Subscribe<StatsChangedEvent>(HandleStatsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TapEvent>(HandleTap);
            EventBus.Unsubscribe<TickEvent>(HandleTick);
            EventBus.Unsubscribe<PrestigeEvent>(HandlePrestige);
            EventBus.Unsubscribe<StatsChangedEvent>(HandleStatsChanged);
        }

        private void HandleStatsChanged(StatsChangedEvent e)
        {
            if (e.MaxEnergy <= 0d)
            {
                return;
            }

            _maxEnergyOverride = (float)e.MaxEnergy;

            // Buying carbs should FEEL like more room, not silently raise a number the player only
            // notices later; the bar redraws immediately at the new ceiling.
            EventBus.Publish(new EnergyChangedEvent(_currentEnergy, MaxEnergy));
        }

        private void HandlePrestige(PrestigeEvent e)
        {
            _currentEnergy = MaxEnergy;
            EventBus.Publish(new EnergyChangedEvent(_currentEnergy, MaxEnergy));
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
