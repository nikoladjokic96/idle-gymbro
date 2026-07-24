using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Progression;

namespace IdleGymBro.Economy
{
    public class UpgradeManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private GameConfig _gameConfig;

        [SerializeField]
        private UpgradeData[] _upgrades;

        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        private CurrencyManager _currency;

        // Applied on top of upgrade aggregation in RecomputeAndPublish; driven purely by
        // LocationMultiplierChangedEvent so this class never references LocationManager directly.
        private double _locationMultiplier = 1d;

        public int TotalLevels
        {
            get
            {
                int sum = 0;

                foreach (var kv in _levels)
                {
                    sum += kv.Value;
                }

                return sum;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LocationMultiplierChangedEvent>(HandleLocationMultiplierChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationMultiplierChangedEvent>(HandleLocationMultiplierChanged);
        }

        private void HandleLocationMultiplierChanged(LocationMultiplierChangedEvent e)
        {
            _locationMultiplier = e.Multiplier;
            RecomputeAndPublish();
        }

        private void Start()
        {
            _currency = FindAnyObjectByType<CurrencyManager>();
            RecomputeAndPublish();
        }

        public int GetLevel(string id)
        {
            return _levels.TryGetValue(id, out int l) ? l : 0;
        }

        public UpgradeData GetUpgrade(string id)
        {
            return _upgrades?.FirstOrDefault(u => u != null && u.Id == id);
        }

        public double GetCost(string id)
        {
            var u = GetUpgrade(id);

            if (u == null)
            {
                return double.PositiveInfinity;
            }

            return u.BaseCost * System.Math.Pow(u.GrowthRate, GetLevel(id));
        }

        // Read-only affordability check; does not spend.
        public bool CanBuy(string id)
        {
            var u = GetUpgrade(id);

            if (u == null)
            {
                return false;
            }

            if (u.MaxLevel > 0 && GetLevel(id) >= u.MaxLevel)
            {
                return false;
            }

            return _currency != null && _currency.TotalGains >= GetCost(id);
        }

        public bool TryBuy(string id)
        {
            var u = GetUpgrade(id);

            if (u == null || _currency == null)
            {
                return false;
            }

            if (u.MaxLevel > 0 && GetLevel(id) >= u.MaxLevel)
            {
                return false;
            }

            double cost = GetCost(id);

            if (!_currency.TrySpend(cost))
            {
                return false;
            }

            int newLevel = GetLevel(id) + 1;
            _levels[id] = newLevel;

            RecomputeAndPublish();
            EventBus.Publish(new UpgradePurchasedEvent(id, newLevel));
            return true;
        }

        private void RecomputeAndPublish()
        {
            double gpr = _gameConfig != null ? _gameConfig.GainsPerRep : 0d;
            double pps = _gameConfig != null ? _gameConfig.BasePassiveGainsPerSecond : 0d;

            if (_upgrades != null)
            {
                foreach (var u in _upgrades)
                {
                    if (u == null)
                    {
                        continue;
                    }

                    double contrib = u.EffectPerLevel * GetLevel(u.Id);

                    if (u.StatType == StatType.GainsPerRep)
                    {
                        gpr += contrib;
                    }
                    else if (u.StatType == StatType.PassiveGainsPerSecond)
                    {
                        pps += contrib;
                    }
                }
            }

            gpr *= _locationMultiplier;
            pps *= _locationMultiplier;

            EventBus.Publish(new StatsChangedEvent(gpr, pps));
        }

        public void CaptureState(SaveData data)
        {
            data.UpgradeLevels = new Dictionary<string, int>(_levels);
        }

        public void RestoreState(SaveData data)
        {
            _levels.Clear();

            if (data.UpgradeLevels != null)
            {
                foreach (var kv in data.UpgradeLevels)
                {
                    _levels[kv.Key] = kv.Value;
                }
            }

            RecomputeAndPublish();
        }
    }
}
