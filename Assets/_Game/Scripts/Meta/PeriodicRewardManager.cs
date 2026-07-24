using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Economy;

namespace IdleGymBro.Meta
{
    // A periodic "time chest": every PeriodicRewardIntervalSeconds a reward becomes claimable,
    // worth PeriodicRewardSeconds of the player's current passive income (scales with progress,
    // no snowball). State is NOT persisted — the timer starts fresh each session (offline income
    // already covers being away); this is an in-session "check back" nudge.
    public class PeriodicRewardManager : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private float _timer;
        private double _passiveRate;
        private bool _ready;
        private int _lastPublishedSeconds = -1;
        private bool _missingConfigLogged;

        private double RewardAmount => _gameConfig != null ? _passiveRate * _gameConfig.PeriodicRewardSeconds : 0d;

        private void OnEnable()
        {
            EventBus.Subscribe<TickEvent>(HandleTick);
            EventBus.Subscribe<PassiveIncomeChangedEvent>(HandlePassiveIncomeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TickEvent>(HandleTick);
            EventBus.Unsubscribe<PassiveIncomeChangedEvent>(HandlePassiveIncomeChanged);
        }

        private void Start()
        {
            PublishState();
        }

        public bool TryClaim()
        {
            if (!_ready)
            {
                return false;
            }

            EventBus.Publish(new GainsEarnedEvent(RewardAmount));
            _ready = false;
            _timer = 0f;
            _lastPublishedSeconds = -1;
            PublishState();
            return true;
        }

        private void HandlePassiveIncomeChanged(PassiveIncomeChangedEvent e)
        {
            _passiveRate = e.GainsPerSecond;
        }

        private void HandleTick(TickEvent e)
        {
            if (!ValidateConfig() || _ready)
            {
                return;
            }

            _timer += e.DeltaTime;

            if (_timer >= _gameConfig.PeriodicRewardIntervalSeconds)
            {
                _timer = _gameConfig.PeriodicRewardIntervalSeconds;
                _ready = true;
                PublishState();
                return;
            }

            // Throttle: only republish when the whole-second countdown the UI shows changes.
            int secondsRemaining = Mathf.CeilToInt(_gameConfig.PeriodicRewardIntervalSeconds - _timer);
            if (secondsRemaining != _lastPublishedSeconds)
            {
                _lastPublishedSeconds = secondsRemaining;
                PublishState();
            }
        }

        private void PublishState()
        {
            float remaining = _gameConfig != null && !_ready
                ? Mathf.Max(0f, _gameConfig.PeriodicRewardIntervalSeconds - _timer)
                : 0f;

            EventBus.Publish(new PeriodicRewardStateChangedEvent(_ready, remaining, RewardAmount));
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("PeriodicRewardManager: GameConfig is not assigned. Periodic reward is disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }
}
