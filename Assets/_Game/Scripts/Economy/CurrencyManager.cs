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

        public double TotalGains { get; private set; }

        private void OnEnable()
        {
            EventBus.Subscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Subscribe<GainsEarnedEvent>(HandleGainsEarned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Unsubscribe<GainsEarnedEvent>(HandleGainsEarned);
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

            TotalGains += _gameConfig.GainsPerRep;
            EventBus.Publish(new GainsChangedEvent(TotalGains));
        }

        private void HandleGainsEarned(GainsEarnedEvent e)
        {
            TotalGains += e.Amount;
            EventBus.Publish(new GainsChangedEvent(TotalGains));
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
