using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Economy;

namespace IdleGymBro.Progression
{
    // Location progress = total upgrade levels owned (summed across ALL upgrades, order-
    // independent) vs each location's cumulative TotalLevelsToComplete — same pattern as
    // muscle-tier thresholds. No per-location save state is needed beyond the current index.
    public class LocationManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private LocationData[] _locations; // ordered by TotalLevelsToComplete ascending

        private int _currentIndex;
        private UpgradeManager _upgrades;
        private bool _missingLocationsLogged;

        public int CurrentIndex => _currentIndex;

        public LocationData Current => _locations != null && _currentIndex >= 0 && _currentIndex < _locations.Length
            ? _locations[_currentIndex]
            : null;

        public int Count => _locations?.Length ?? 0;

        private void Start()
        {
            _upgrades = FindAnyObjectByType<UpgradeManager>();
            PublishAll();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UpgradePurchasedEvent>(RecomputeProgress);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(RecomputeProgress);
        }

        private void RecomputeProgress(UpgradePurchasedEvent e)
        {
            PublishProgress();
        }

        private int TotalUpgradeLevels => _upgrades != null ? _upgrades.TotalLevels : 0;

        public LocationData GetLocation(int index)
        {
            return _locations != null && index >= 0 && index < _locations.Length ? _locations[index] : null;
        }

        public bool TryAdvance()
        {
            if (!CanAdvance)
            {
                return false;
            }

            _currentIndex++;
            PublishAll();
            return true;
        }

        private bool CanAdvance => ComputeProgress() >= 1f && _currentIndex < (_locations?.Length ?? 0) - 1 && _locations[_currentIndex + 1] != null;

        private float ComputeProgress()
        {
            if (!HasLocations)
            {
                LogMissingLocationsOnce();
                return 0f;
            }

            int prevTarget = _currentIndex > 0 && _locations[_currentIndex - 1] != null
                ? _locations[_currentIndex - 1].TotalLevelsToComplete
                : 0;

            var cur = Current;

            if (cur == null || cur.TotalLevelsToComplete <= prevTarget)
            {
                return 1f;
            }

            return Mathf.Clamp01((float)(TotalUpgradeLevels - prevTarget) / (cur.TotalLevelsToComplete - prevTarget));
        }

        private bool HasLocations => _locations != null && _locations.Length > 0;

        private void LogMissingLocationsOnce()
        {
            if (_missingLocationsLogged)
            {
                return;
            }

            _missingLocationsLogged = true;
            Debug.LogError("[LocationManager] No locations configured.");
        }

        private void PublishAll()
        {
            PublishProgress();

            if (!HasLocations)
            {
                LogMissingLocationsOnce();
                return;
            }

            var cur = Current;

            if (cur == null)
            {
                return;
            }

            EventBus.Publish(new LocationChangedEvent(cur.Id, cur.DisplayName, _currentIndex));
            EventBus.Publish(new LocationMultiplierChangedEvent(cur.GlobalMultiplier));
        }

        private void PublishProgress()
        {
            EventBus.Publish(new LocationProgressChangedEvent(Current?.DisplayName ?? string.Empty, ComputeProgress(), CanAdvance));
        }

        public void CaptureState(SaveData data)
        {
            data.CurrentLocationIndex = _currentIndex;
        }

        public void RestoreState(SaveData data)
        {
            _currentIndex = Mathf.Clamp(data.CurrentLocationIndex, 0, (_locations?.Length ?? 1) - 1);
            PublishAll();
        }
    }
}
