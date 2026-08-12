using IdleGymBro.Core;

namespace IdleGymBro.Economy
{
    public readonly struct StatsChangedEvent : IGameEvent
    {
        public double GainsPerRep { get; }
        public double PassiveGainsPerSecond { get; }

        // Carbs (Macros tab) raise the energy ceiling. Defaulted so the two-argument form still
        // compiles for callers that only care about income.
        public double MaxEnergy { get; }

        public StatsChangedEvent(double gainsPerRep, double passiveGainsPerSecond, double maxEnergy = 0d)
        {
            GainsPerRep = gainsPerRep;
            PassiveGainsPerSecond = passiveGainsPerSecond;
            MaxEnergy = maxEnergy;
        }
    }

    public readonly struct UpgradePurchasedEvent : IGameEvent
    {
        public string UpgradeId { get; }
        public int NewLevel { get; }

        public UpgradePurchasedEvent(string upgradeId, int newLevel)
        {
            UpgradeId = upgradeId;
            NewLevel = newLevel;
        }
    }
}
