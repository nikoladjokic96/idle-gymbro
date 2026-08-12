using System;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Progression;

namespace IdleGymBro.Economy
{
    // A rest timer that arms every N upgrade levels and gets longer each time.
    //
    // It gates PURCHASES ONLY — tapping, passive income, boosters, wardrobe and everything else keep
    // running. That line matters: §10 rule 3 says an ad may never stand between the player and
    // playing the game. Here the game continues and the ad only shortens a wait the player could
    // equally well just sit out, which keeps it on the "opt-in boost" side of the rule rather than
    // "pay to continue".
    //
    // The deadline is stored as an absolute UTC time, so the wait also elapses while the app is
    // closed. Storing seconds-remaining would turn force-quitting into a free skip.
    public class UpgradeCooldownManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private long _endTicks;
        private int _served;
        private int _lastMilestoneCrossed;
        private bool _missingConfigLogged;

        public static UpgradeCooldownManager Instance { get; private set; }

        public bool IsActive => RemainingSeconds > 0d;

        public double RemainingSeconds
        {
            get
            {
                if (_endTicks <= 0L)
                {
                    return 0d;
                }

                double seconds = new TimeSpan(_endTicks - DateTime.UtcNow.Ticks).TotalSeconds;
                return seconds > 0d ? seconds : 0d;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Subscribe<TickEvent>(HandleTick);
            EventBus.Subscribe<PrestigeEvent>(HandlePrestige);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Unsubscribe<TickEvent>(HandleTick);
            EventBus.Unsubscribe<PrestigeEvent>(HandlePrestige);
        }

        private void Start()
        {
            Publish();
        }

        // A new run starts fresh: the escalating wait is part of one run's pacing, not a permanent
        // tax carried across prestige.
        private void HandlePrestige(PrestigeEvent e)
        {
            _endTicks = 0L;
            _served = 0;
            _lastMilestoneCrossed = 0;
            Publish();
        }

        private void HandleUpgradePurchased(UpgradePurchasedEvent e)
        {
            if (!ValidateConfig())
            {
                return;
            }

            var manager = FindAnyObjectByType<UpgradeManager>();

            if (manager == null)
            {
                return;
            }

            int every = Mathf.Max(1, _gameConfig.UpgradeCooldownEveryLevels);
            int milestone = manager.TotalLevels / every;

            // Bulk buying can cross several milestones at once; only the newest one arms a wait,
            // otherwise a single x10 press could stack half an hour.
            if (milestone <= _lastMilestoneCrossed)
            {
                return;
            }

            _lastMilestoneCrossed = milestone;
            _served++;
            _endTicks = DateTime.UtcNow.AddSeconds(DurationFor(_served)).Ticks;
            Publish();
        }

        // 1st wait = base, 2nd = 3x base, 3rd = 5x base. With the default 300s base that is
        // 5 / 15 / 25 minutes, matching the pacing asked for.
        public double DurationFor(int served)
        {
            double baseSeconds = _gameConfig != null ? _gameConfig.UpgradeCooldownBaseSeconds : 300d;
            return baseSeconds * Math.Max(1, 2 * served - 1);
        }

        // Called after a rewarded ad completes. Cuts the remaining wait rather than clearing it, so
        // the ad is a boost and not a switch that removes the mechanic.
        public void ShortenByAd()
        {
            if (!IsActive || !ValidateConfig())
            {
                return;
            }

            double cut = Math.Max(1d, _gameConfig.UpgradeCooldownAdCutSeconds);
            long newEnd = DateTime.UtcNow.AddSeconds(Math.Max(0d, RemainingSeconds - cut)).Ticks;
            _endTicks = newEnd;
            Publish();
        }

        private void HandleTick(TickEvent e)
        {
            if (_endTicks <= 0L)
            {
                return;
            }

            if (RemainingSeconds <= 0d)
            {
                _endTicks = 0L;
            }

            Publish();
        }

        private void Publish()
        {
            EventBus.Publish(new UpgradeCooldownChangedEvent(RemainingSeconds, DurationFor(Math.Max(1, _served))));
        }

        public void CaptureState(SaveData data)
        {
            data.UpgradeCooldownEndTicks = _endTicks;
            data.UpgradeCooldownsServed = _served;
        }

        public void RestoreState(SaveData data)
        {
            _endTicks = data.UpgradeCooldownEndTicks;
            _served = data.UpgradeCooldownsServed;

            // Re-derive the milestone from the levels that were restored, so a reloaded save does
            // not immediately re-arm a wait for a milestone it already served.
            var manager = FindAnyObjectByType<UpgradeManager>();

            if (manager != null && _gameConfig != null)
            {
                int every = Mathf.Max(1, _gameConfig.UpgradeCooldownEveryLevels);
                _lastMilestoneCrossed = manager.TotalLevels / every;
            }

            Publish();
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("UpgradeCooldownManager: GameConfig is not assigned. Upgrade cooldowns are disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }

    public readonly struct UpgradeCooldownChangedEvent : IGameEvent
    {
        public double RemainingSeconds { get; }
        public double TotalSeconds { get; }

        public UpgradeCooldownChangedEvent(double remainingSeconds, double totalSeconds)
        {
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
        }
    }
}
