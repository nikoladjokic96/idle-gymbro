using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Gameplay;

namespace IdleGymBro.Economy
{
    public readonly struct GainsChangedEvent : IGameEvent
    {
        public double Total { get; }

        public GainsChangedEvent(double total)
        {
            Total = total;
        }
    }

    public class CurrencyManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private bool _missingConfigLogged;

        // Effective gains-per-rep, defaults to config base and is overridden by upgrades
        // once UpgradeManager publishes its first StatsChangedEvent.
        private double _gainsPerRep;

        public double TotalGains { get; private set; }

        private void Awake()
        {
            _gainsPerRep = _gameConfig != null ? _gameConfig.GainsPerRep : 0d;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Subscribe<GainsEarnedEvent>(HandleGainsEarned);
            EventBus.Subscribe<StatsChangedEvent>(HandleStatsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Unsubscribe<GainsEarnedEvent>(HandleGainsEarned);
            EventBus.Unsubscribe<StatsChangedEvent>(HandleStatsChanged);
        }

        private void Start()
        {
            EventBus.Publish(new GainsChangedEvent(TotalGains));
        }

        private void HandleRepPerformed(RepPerformedEvent e)
        {
            if (!ValidateConfig())
            {
                return;
            }

            TotalGains += _gainsPerRep;
            EventBus.Publish(new GainsChangedEvent(TotalGains));
        }

        private void HandleGainsEarned(GainsEarnedEvent e)
        {
            TotalGains += e.Amount;
            EventBus.Publish(new GainsChangedEvent(TotalGains));
        }

        private void HandleStatsChanged(StatsChangedEvent e)
        {
            _gainsPerRep = e.GainsPerRep;
        }

        public bool TrySpend(double amount)
        {
            if (amount <= TotalGains)
            {
                TotalGains -= amount;
                EventBus.Publish(new GainsChangedEvent(TotalGains));
                return true;
            }

            return false;
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("CurrencyManager: GameConfig is not assigned. Gains are disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }

        public void CaptureState(SaveData data)
        {
            data.TotalGains = TotalGains;
        }

        public void RestoreState(SaveData data)
        {
            TotalGains = data.TotalGains;
            EventBus.Publish(new GainsChangedEvent(TotalGains));
        }
    }
}
