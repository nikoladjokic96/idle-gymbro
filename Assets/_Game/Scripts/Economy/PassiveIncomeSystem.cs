using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Economy
{
    public class PassiveIncomeSystem : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private bool _missingConfigLogged;

        // Effective passive rate, defaults to config base and is overridden by upgrades
        // once UpgradeManager publishes its first StatsChangedEvent.
        private double _gainsPerSecond;

        public double GainsPerSecond => _gainsPerSecond;

        private void Awake()
        {
            _gainsPerSecond = _gameConfig != null ? _gameConfig.BasePassiveGainsPerSecond : 0d;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TickEvent>(HandleTick);
            EventBus.Subscribe<StatsChangedEvent>(HandleStatsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TickEvent>(HandleTick);
            EventBus.Unsubscribe<StatsChangedEvent>(HandleStatsChanged);
        }

        private void Start()
        {
            if (!ValidateConfig())
            {
                return;
            }

            // Published in Start so HUD (subscribed in OnEnable) is ready to receive it.
            EventBus.Publish(new PassiveIncomeChangedEvent(GainsPerSecond));
        }

        private void HandleTick(TickEvent e)
        {
            double gainsPerSecond = GainsPerSecond;

            if (gainsPerSecond <= 0d)
            {
                return;
            }

            EventBus.Publish(new GainsEarnedEvent(gainsPerSecond * e.DeltaTime));
        }

        private void HandleStatsChanged(StatsChangedEvent e)
        {
            _gainsPerSecond = e.PassiveGainsPerSecond;
            EventBus.Publish(new PassiveIncomeChangedEvent(_gainsPerSecond));
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("PassiveIncomeSystem: GameConfig is not assigned. Passive income is disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }
}
