using IdleGymBro.Core;

namespace IdleGymBro.Economy
{
    public readonly struct StatsChangedEvent : IGameEvent
    {
        public double GainsPerRep { get; }
        public double PassiveGainsPerSecond { get; }

        public StatsChangedEvent(double gainsPerRep, double passiveGainsPerSecond)
        {
            GainsPerRep = gainsPerRep;
            PassiveGainsPerSecond = passiveGainsPerSecond;
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
