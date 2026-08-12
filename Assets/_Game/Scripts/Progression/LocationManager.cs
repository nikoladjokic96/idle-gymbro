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
            EventBus.Subscribe<PrestigeEvent>(HandlePrestige);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(RecomputeProgress);
            EventBus.Unsubscribe<PrestigeEvent>(HandlePrestige);
        }

        private void RecomputeProgress(UpgradePurchasedEvent e)
        {
            PublishProgress();
        }

        // Prestige resets the run back to the first location (and its 1x multiplier).
        private void HandlePrestige(PrestigeEvent e)
        {
            _currentIndex = 0;
            PublishAll();
        }

        private int TotalUpgradeLevels => _upgrades != null ? _upgrades.TotalLevels : 0;

        public LocationData GetLocation(int index)
        {
            return _locations != null && index >= 0 && index < _locations.Length ? _locations[index] : null;
        }

        // Travel to a location that has already been unlocked. The Locations list used to be a
        // read-only wall of text with a single MOVE UP button, so tapping a row did nothing — which
        // reads as a broken button, not as "this is just a label".
        //
        // Unlocked means "at or below the furthest one reached": the gate is clearing a location,
        // not standing in it, so going back to Home never costs the player their progress.
        public bool TrySelect(int index)
        {
            if (_locations == null || index < 0 || index >= _locations.Length || _locations[index] == null)
            {
                return false;
            }

            if (index > _currentIndex)
            {
                return false; // forward travel goes through TryAdvance and its completion checks
            }

            if (index == _currentIndex)
            {
                return false;
            }

            _currentIndex = index;
            PublishAll();
            return true;
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

        // Three conditions, weighted equally, so the bar reflects what is actually left to do:
        //   Body   — total levels across the muscle groups vs this location's target
        //   Gear   — every piece of Equipment tied to this location at its MaxLevel
        //   Macros — the LOWEST of protein/carbs/fats vs the target, so pouring everything into
        //            one macro cannot carry the other two
        // The old single cumulative total let the player brute-force the next location with
        // whichever upgrade happened to be cheapest.
        private float ComputeProgress()
        {
            if (!HasLocations)
            {
                LogMissingLocationsOnce();
                return 0f;
            }

            var cur = Current;

            if (cur == null)
            {
                return 1f;
            }

            var upgrades = FindAnyObjectByType<UpgradeManager>();

            if (upgrades == null)
            {
                return 0f;
            }

            float body = cur.BodyLevelTarget <= 0
                ? 1f
                : Mathf.Clamp01(upgrades.TotalLevelsIn(UpgradeCategory.Body) / (float)cur.BodyLevelTarget);

            float macros = cur.MacroLevelTarget <= 0
                ? 1f
                : Mathf.Clamp01(upgrades.LowestMacroLevel() / (float)cur.MacroLevelTarget);

            float gear = upgrades.IsEquipmentComplete(cur.Id) ? 1f : 0f;

            return (body + gear + macros) / 3f;
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
