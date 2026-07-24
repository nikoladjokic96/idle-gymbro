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

        // Multiplicative on top of the additive upgrade stat, cached from BoosterManager.
        private double _boosterMultiplier = 1d;

        public double GainsPerSecond => _gainsPerSecond;

        private double EffectiveRate => _gainsPerSecond * _boosterMultiplier;

        private void Awake()
        {
            _gainsPerSecond = _gameConfig != null ? _gameConfig.BasePassiveGainsPerSecond : 0d;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TickEvent>(HandleTick);
            EventBus.Subscribe<StatsChangedEvent>(HandleStatsChanged);
            EventBus.Subscribe<BoosterMultipliersChangedEvent>(HandleBoosterMultipliersChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TickEvent>(HandleTick);
            EventBus.Unsubscribe<StatsChangedEvent>(HandleStatsChanged);
            EventBus.Unsubscribe<BoosterMultipliersChangedEvent>(HandleBoosterMultipliersChanged);
        }

        private void Start()
        {
            if (!ValidateConfig())
            {
                return;
            }

            // Published in Start so HUD (subscribed in OnEnable) is ready to receive it.
            EventBus.Publish(new PassiveIncomeChangedEvent(EffectiveRate));
        }

        private void HandleTick(TickEvent e)
        {
            double effectiveRate = EffectiveRate;

            if (effectiveRate <= 0d)
            {
                return;
            }

            EventBus.Publish(new GainsEarnedEvent(effectiveRate * e.DeltaTime));
        }

        private void HandleStatsChanged(StatsChangedEvent e)
        {
            _gainsPerSecond = e.PassiveGainsPerSecond;
            EventBus.Publish(new PassiveIncomeChangedEvent(EffectiveRate));
        }

        private void HandleBoosterMultipliersChanged(BoosterMultipliersChangedEvent e)
        {
            _boosterMultiplier = e.PassiveMultiplier;
            EventBus.Publish(new PassiveIncomeChangedEvent(EffectiveRate));
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
