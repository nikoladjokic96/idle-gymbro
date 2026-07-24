using System;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Economy;

namespace IdleGymBro.Progression
{
    // Prestige ("New Bulk", §6): reset the current run for permanent respect that boosts all income.
    //   respect gained  = factor x sqrt(TotalEarned this run)
    //   globalMultiplier = 1 + totalRespect x multiplierPerRespect
    // Only totalRespect is persisted; the reset itself is event-driven (PrestigeEvent).
    public class PrestigeManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private double _totalRespect;
        private double _totalEarned;
        private bool _missingConfigLogged;

        private double PendingRespect => _gameConfig != null ? Math.Floor(_gameConfig.PrestigeRespectFactor * Math.Sqrt(Math.Max(0d, _totalEarned))) : 0d;
        private double Multiplier => _gameConfig != null ? 1d + _totalRespect * _gameConfig.PrestigeMultiplierPerRespect : 1d;
        private bool CanPrestige => _gameConfig != null && PendingRespect >= _gameConfig.PrestigeMinRespect;

        private void OnEnable()
        {
            EventBus.Subscribe<GainsChangedEvent>(HandleGainsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GainsChangedEvent>(HandleGainsChanged);
        }

        private void Start()
        {
            EventBus.Publish(new PrestigeMultiplierChangedEvent(Multiplier));
            PublishState();
        }

        public bool DoPrestige()
        {
            if (!ValidateConfig() || !CanPrestige)
            {
                return false;
            }

            _totalRespect += PendingRespect;

            // Reset the run: other systems zero themselves; CurrencyManager's reset republishes
            // GainsChangedEvent(0,0), which sets _totalEarned back to 0 here.
            EventBus.Publish(new PrestigeEvent());
            EventBus.Publish(new PrestigeMultiplierChangedEvent(Multiplier));
            PublishState();
            return true;
        }

        private void HandleGainsChanged(GainsChangedEvent e)
        {
            _totalEarned = e.TotalEarned;
            PublishState();
        }

        private void PublishState()
        {
            EventBus.Publish(new PrestigeStateChangedEvent(CanPrestige, PendingRespect, _totalRespect, Multiplier));
        }

        public void CaptureState(SaveData data)
        {
            data.TotalRespect = _totalRespect;
        }

        public void RestoreState(SaveData data)
        {
            _totalRespect = data.TotalRespect;
            EventBus.Publish(new PrestigeMultiplierChangedEvent(Multiplier));
            PublishState();
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("PrestigeManager: GameConfig is not assigned. Prestige is disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }
}
