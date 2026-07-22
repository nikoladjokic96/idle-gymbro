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

        private void OnEnable()
        {
            EventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameLoadedEvent>(HandleGameLoaded);
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

            double capped = Math.Min(secondsAway, _gameConfig.OfflineCapSeconds);
            double gains = capped * _gameConfig.BasePassiveGainsPerSecond * _gameConfig.OfflineEfficiency;

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
