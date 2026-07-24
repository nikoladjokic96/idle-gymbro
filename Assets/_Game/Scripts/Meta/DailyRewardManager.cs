using System;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Economy;

namespace IdleGymBro.Meta
{
    // Once-per-UTC-day escalating reward. Consecutive days build a streak (bigger reward);
    // missing a day resets it to 1. Reward scales with the player's passive income so it never
    // becomes trivial late-game: reward = passiveRate x DailyRewardSeconds x streakDay(clamped).
    public class DailyRewardManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private long _lastClaimDay;
        private int _streak;
        private double _passiveRate;
        private bool _missingConfigLogged;

        private static long TodayIndex => DateTime.UtcNow.Ticks / TimeSpan.TicksPerDay;

        private void OnEnable()
        {
            EventBus.Subscribe<PassiveIncomeChangedEvent>(HandlePassiveIncomeChanged);
            EventBus.Subscribe<GameLoadedEvent>(HandleGameLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PassiveIncomeChangedEvent>(HandlePassiveIncomeChanged);
            EventBus.Unsubscribe<GameLoadedEvent>(HandleGameLoaded);
        }

        public bool IsClaimable()
        {
            return _lastClaimDay != TodayIndex;
        }

        // The streak day that a claim right now would count as (continues if yesterday, else resets).
        private int PendingStreakDay()
        {
            return _lastClaimDay == TodayIndex - 1 ? _streak + 1 : 1;
        }

        private double RewardFor(int streakDay)
        {
            if (_gameConfig == null)
            {
                return 0d;
            }

            int effectiveDay = Mathf.Clamp(streakDay, 1, _gameConfig.DailyStreakCycle);
            return _passiveRate * _gameConfig.DailyRewardSeconds * effectiveDay;
        }

        public bool TryClaim()
        {
            if (!ValidateConfig() || !IsClaimable())
            {
                return false;
            }

            int streakDay = PendingStreakDay();
            EventBus.Publish(new GainsEarnedEvent(RewardFor(streakDay)));

            _lastClaimDay = TodayIndex;
            _streak = streakDay;
            return true;
        }

        private void HandlePassiveIncomeChanged(PassiveIncomeChangedEvent e)
        {
            _passiveRate = e.GainsPerSecond;
        }

        // Fired by SaveSystem after all state is restored, so _lastClaimDay/_streak are current.
        private void HandleGameLoaded(GameLoadedEvent e)
        {
            if (!ValidateConfig() || !IsClaimable())
            {
                return;
            }

            int streakDay = PendingStreakDay();
            EventBus.Publish(new DailyRewardAvailableEvent(streakDay, RewardFor(streakDay)));
        }

        public void CaptureState(SaveData data)
        {
            data.LastDailyClaimDay = _lastClaimDay;
            data.DailyStreak = _streak;
        }

        public void RestoreState(SaveData data)
        {
            _lastClaimDay = data.LastDailyClaimDay;
            _streak = data.DailyStreak;
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("DailyRewardManager: GameConfig is not assigned. Daily reward is disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }
}
