using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Gameplay;

namespace IdleGymBro.Economy
{
    public readonly struct GainsChangedEvent : IGameEvent
    {
        public double Total { get; }
        public double TotalEarned { get; }

        public GainsChangedEvent(double total, double totalEarned)
        {
            Total = total;
            TotalEarned = totalEarned;
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

        // Multiplicative on top of the additive upgrade stat, cached from BoosterManager.
        private double _tapBoosterMultiplier = 1d;

        public double TotalGains { get; private set; }

        // Lifetime gains ever earned; never decreases on spend. Drives muscle-tier progression
        // so buying upgrades can never shrink the character.
        public double TotalEarned { get; private set; }

        private void Awake()
        {
            _gainsPerRep = _gameConfig != null ? _gameConfig.GainsPerRep : 0d;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Subscribe<GainsEarnedEvent>(HandleGainsEarned);
            EventBus.Subscribe<StatsChangedEvent>(HandleStatsChanged);
            EventBus.Subscribe<BoosterMultipliersChangedEvent>(HandleBoosterMultipliersChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Unsubscribe<GainsEarnedEvent>(HandleGainsEarned);
            EventBus.Unsubscribe<StatsChangedEvent>(HandleStatsChanged);
            EventBus.Unsubscribe<BoosterMultipliersChangedEvent>(HandleBoosterMultipliersChanged);
        }

        private void Start()
        {
            EventBus.Publish(new GainsChangedEvent(TotalGains, TotalEarned));
        }

        private void HandleRepPerformed(RepPerformedEvent e)
        {
            if (!ValidateConfig())
            {
                return;
            }

            double amount = _gainsPerRep * _tapBoosterMultiplier;
            TotalGains += amount;
            TotalEarned += amount;
            EventBus.Publish(new GainsChangedEvent(TotalGains, TotalEarned));
        }

        private void HandleGainsEarned(GainsEarnedEvent e)
        {
            TotalGains += e.Amount;
            TotalEarned += e.Amount;
            EventBus.Publish(new GainsChangedEvent(TotalGains, TotalEarned));
        }

        private void HandleStatsChanged(StatsChangedEvent e)
        {
            _gainsPerRep = e.GainsPerRep;
        }

        private void HandleBoosterMultipliersChanged(BoosterMultipliersChangedEvent e)
        {
            _tapBoosterMultiplier = e.TapMultiplier;
        }

        public bool TrySpend(double amount)
        {
            if (amount <= TotalGains)
            {
                TotalGains -= amount;
                EventBus.Publish(new GainsChangedEvent(TotalGains, TotalEarned));
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
            data.TotalEarned = TotalEarned;
        }

        public void RestoreState(SaveData data)
        {
            TotalGains = data.TotalGains;
            // Migration guard: saves written before TotalEarned existed have it at 0, so floor
            // it at TotalGains to avoid an impossible earned-less-than-balance state.
            TotalEarned = System.Math.Max(data.TotalEarned, data.TotalGains);
            EventBus.Publish(new GainsChangedEvent(TotalGains, TotalEarned));
        }
    }
}
