using System;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Economy
{
    public class OfflineEarningsSystem : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private bool _missingConfigLogged;

        // Effective passive rate (upgrades x location x prestige), cached from PassiveIncomeSystem.
        // Defaults to the config base so a load that somehow precedes the first stats publish still
        // grants something rather than nothing.
        private double _gainsPerSecond;

        private void Awake()
        {
            _gainsPerSecond = _gameConfig != null ? _gameConfig.BasePassiveGainsPerSecond : 0d;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded);
            EventBus.Subscribe<PassiveIncomeChangedEvent>(HandlePassiveIncomeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameLoadedEvent>(HandleGameLoaded);
            EventBus.Unsubscribe<PassiveIncomeChangedEvent>(HandlePassiveIncomeChanged);
        }

        private void HandlePassiveIncomeChanged(PassiveIncomeChangedEvent e)
        {
            _gainsPerSecond = e.GainsPerSecond;
        }

        private void HandleGameLoaded(GameLoadedEvent e)
        {
            if (!e.HadSave)
            {
                return; // fresh game -> no offline
            }

            if (!ValidateConfig())
            {
                return;
            }

            double secondsAway = (DateTime.UtcNow.Ticks - e.LastSaveTimeTicks) / (double)TimeSpan.TicksPerSecond;

            if (secondsAway <= 0)
            {
                return; // clock skew guard
            }

            // §5: offlineGains = min(timeAway, cap) x gainsPerSecond x efficiency. gainsPerSecond is
            // the player's CURRENT effective rate — SaveSystem restores every ISaveable (which
            // republishes stats -> PassiveIncomeChangedEvent) before it publishes GameLoadedEvent,
            // so the cached rate is already post-upgrade/location/prestige by the time we run.
            double capped = Math.Min(secondsAway, _gameConfig.OfflineCapSeconds);
            double gains = capped * _gainsPerSecond * _gameConfig.OfflineEfficiency;

            if (gains <= 0)
            {
                return;
            }

            EventBus.Publish(new GainsEarnedEvent(gains)); // grant
            EventBus.Publish(new OfflineProgressEvent(gains, secondsAway)); // notify popup
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("OfflineEarningsSystem: GameConfig is not assigned. Offline earnings are disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }
}
