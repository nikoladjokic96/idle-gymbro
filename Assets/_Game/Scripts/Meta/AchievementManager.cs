using System.Collections.Generic;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Economy;
using IdleGymBro.Progression;

namespace IdleGymBro.Meta
{
    // Tracks lifetime counters and awards one-time gains rewards when milestones are hit.
    // Counters that aren't derivable from other systems' saves (reps, upgrades bought, furthest
    // location) are persisted here; TotalEarned comes from CurrencyManager via GainsChangedEvent.
    public class AchievementManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private AchievementData[] _achievements;

        // Lifetime earned ACROSS prestiges. CurrencyManager.TotalEarned is per-run (prestige zeroes
        // it), but achievements are permanent records (§12) — a completed goal must not un-complete
        // itself when the player starts a New Bulk. Accumulated from deltas so a reset just
        // re-baselines instead of subtracting.
        private double _lifetimeEarned;
        private double _lastSeenRunEarned;
        private long _reps;
        private int _upgradesBought;
        private int _maxLocationIndex;

        private readonly HashSet<string> _claimed = new HashSet<string>();
        private int _lastClaimableCount = -1;

        public int Count => _achievements != null ? _achievements.Length : 0;

        public AchievementData GetData(int index)
        {
            return _achievements != null && index >= 0 && index < _achievements.Length ? _achievements[index] : null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Subscribe<TapGainsEvent>(HandleTapGains);
            EventBus.Subscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Subscribe<LocationChangedEvent>(HandleLocationChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Unsubscribe<TapGainsEvent>(HandleTapGains);
            EventBus.Unsubscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Unsubscribe<LocationChangedEvent>(HandleLocationChanged);
        }

        private void Start()
        {
            PublishState();
        }

        public double GetProgress(AchievementData achievement)
        {
            if (achievement == null)
            {
                return 0d;
            }

            switch (achievement.Type)
            {
                case AchievementType.TotalGainsEarned: return _lifetimeEarned;
                case AchievementType.RepsPerformed: return _reps;
                case AchievementType.UpgradesBought: return _upgradesBought;
                case AchievementType.LocationReached: return _maxLocationIndex;
                default: return 0d;
            }
        }

        public bool IsClaimed(string id) => _claimed.Contains(id);

        public bool IsClaimable(AchievementData achievement)
        {
            return achievement != null && !_claimed.Contains(achievement.Id) && GetProgress(achievement) >= achievement.Threshold;
        }

        // Claims every currently-claimable achievement at once; returns how many were claimed.
        public int ClaimAll()
        {
            if (_achievements == null)
            {
                return 0;
            }

            double totalReward = 0d;
            int claimedNow = 0;

            foreach (AchievementData achievement in _achievements)
            {
                if (IsClaimable(achievement))
                {
                    _claimed.Add(achievement.Id);
                    totalReward += achievement.RewardGains;
                    claimedNow++;
                }
            }

            if (claimedNow > 0)
            {
                EventBus.Publish(new GainsEarnedEvent(totalReward));
                PublishState();
            }

            return claimedNow;
        }

        private void HandleGainsChanged(GainsChangedEvent e)
        {
            // A drop means the run was reset (prestige): re-baseline, never subtract.
            if (e.TotalEarned >= _lastSeenRunEarned)
            {
                _lifetimeEarned += e.TotalEarned - _lastSeenRunEarned;
            }

            _lastSeenRunEarned = e.TotalEarned;
            PublishState();
        }

        private void HandleTapGains(TapGainsEvent e)
        {
            _reps++;
            PublishState();
        }

        private void HandleUpgradePurchased(UpgradePurchasedEvent e)
        {
            _upgradesBought++;
            PublishState();
        }

        private void HandleLocationChanged(LocationChangedEvent e)
        {
            if (e.Index > _maxLocationIndex)
            {
                _maxLocationIndex = e.Index;
                PublishState();
            }
        }

        private void PublishState()
        {
            int claimable = 0;

            if (_achievements != null)
            {
                foreach (AchievementData achievement in _achievements)
                {
                    if (IsClaimable(achievement))
                    {
                        claimable++;
                    }
                }
            }

            // Only publish when the badge count actually changes (HandleGainsChanged fires ~10x/s).
            if (claimable != _lastClaimableCount)
            {
                _lastClaimableCount = claimable;
                EventBus.Publish(new AchievementsChangedEvent(claimable));
            }
        }

        public void CaptureState(SaveData data)
        {
            data.AchievementReps = _reps;
            data.AchievementUpgradesBought = _upgradesBought;
            data.MaxLocationIndex = _maxLocationIndex;
            data.AchievementLifetimeEarned = _lifetimeEarned;
            data.ClaimedAchievements = new List<string>(_claimed);
        }

        public void RestoreState(SaveData data)
        {
            _reps = data.AchievementReps;
            _upgradesBought = data.AchievementUpgradesBought;
            _maxLocationIndex = data.MaxLocationIndex;

            // Migration: saves written before lifetime tracking existed have 0 here, so seed from
            // the run total rather than wiping a player's achievement progress.
            _lifetimeEarned = data.AchievementLifetimeEarned > 0d ? data.AchievementLifetimeEarned : data.TotalEarned;

            // Baseline against the value CurrencyManager restores, so its republished
            // GainsChangedEvent contributes a zero delta no matter which of us restores first.
            _lastSeenRunEarned = data.TotalEarned;

            _claimed.Clear();
            if (data.ClaimedAchievements != null)
            {
                foreach (string id in data.ClaimedAchievements)
                {
                    _claimed.Add(id);
                }
            }

            _lastClaimableCount = -1; // force a fresh publish
            PublishState();
        }
    }
}
