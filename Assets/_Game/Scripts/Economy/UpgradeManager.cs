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

        // Permanent prestige multiplier, driven by PrestigeMultiplierChangedEvent.
        private double _prestigeMultiplier = 1d;

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
            EventBus.Subscribe<PrestigeMultiplierChangedEvent>(HandlePrestigeMultiplierChanged);
            EventBus.Subscribe<PrestigeEvent>(HandlePrestige);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationMultiplierChangedEvent>(HandleLocationMultiplierChanged);
            EventBus.Unsubscribe<PrestigeMultiplierChangedEvent>(HandlePrestigeMultiplierChanged);
            EventBus.Unsubscribe<PrestigeEvent>(HandlePrestige);
        }

        private void HandleLocationMultiplierChanged(LocationMultiplierChangedEvent e)
        {
            _locationMultiplier = e.Multiplier;
            RecomputeAndPublish();
        }

        private void HandlePrestigeMultiplierChanged(PrestigeMultiplierChangedEvent e)
        {
            _prestigeMultiplier = e.Multiplier;
            RecomputeAndPublish();
        }

        // Prestige wipes all purchased upgrade levels (the run resets); stats recompute to base.
        private void HandlePrestige(PrestigeEvent e)
        {
            _levels.Clear();
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

        // Cost of buying `count` levels back to back. Each level costs more than the last, so this
        // is the sum of the geometric run, NOT count x the current price — quoting the cheap price
        // and then charging the real one is how bulk-buy buttons lie to players.
        public double GetCost(string id, int count)
        {
            var u = GetUpgrade(id);

            if (u == null || count <= 0)
            {
                return double.PositiveInfinity;
            }

            int level = GetLevel(id);
            int affordableSteps = u.MaxLevel > 0 ? System.Math.Min(count, u.MaxLevel - level) : count;

            if (affordableSteps <= 0)
            {
                return double.PositiveInfinity;
            }

            double total = 0d;

            for (int i = 0; i < affordableSteps; i++)
            {
                total += u.BaseCost * System.Math.Pow(u.GrowthRate, level + i);
            }

            return total;
        }

        // How many of the requested levels the player can actually afford right now (0..count).
        // Bulk buy is all-or-nothing per press would be worse: at x10 a player one coin short would
        // get nothing, with no hint why.
        public int AffordableLevels(string id, int count)
        {
            var u = GetUpgrade(id);

            if (u == null || _currency == null || count <= 0)
            {
                return 0;
            }

            int level = GetLevel(id);
            double budget = _currency.TotalGains;
            int bought = 0;

            for (int i = 0; i < count; i++)
            {
                if (u.MaxLevel > 0 && level + i >= u.MaxLevel)
                {
                    break;
                }

                double price = u.BaseCost * System.Math.Pow(u.GrowthRate, level + i);

                if (budget < price)
                {
                    break;
                }

                budget -= price;
                bought++;
            }

            return bought;
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

        // Buys up to `count` levels, spending once for the whole run and publishing one event.
        // Returns how many levels were actually bought.
        public int TryBuy(string id, int count)
        {
            var u = GetUpgrade(id);

            if (u == null || _currency == null || count <= 0)
            {
                return 0;
            }

            int levels = AffordableLevels(id, count);

            if (levels <= 0)
            {
                return 0;
            }

            double cost = GetCost(id, levels);

            if (!_currency.TrySpend(cost))
            {
                return 0;
            }

            int newLevel = GetLevel(id) + levels;
            _levels[id] = newLevel;

            RecomputeAndPublish();
            EventBus.Publish(new UpgradePurchasedEvent(id, newLevel));
            return levels;
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

            gpr *= _locationMultiplier * _prestigeMultiplier;
            pps *= _locationMultiplier * _prestigeMultiplier;

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
