using IdleGymBro.Core;

namespace IdleGymBro.Progression
{
    // Broadcast when the player prestiges ("New Bulk"). Run-specific systems (gains, upgrades,
    // energy, location) reset themselves on this; nothing references PrestigeManager directly.
    public readonly struct PrestigeEvent : IGameEvent { }

    // Permanent global multiplier from accumulated respect; UpgradeManager applies it to income
    // (same event-driven pattern as the location multiplier).
    public readonly struct PrestigeMultiplierChangedEvent : IGameEvent
    {
        public double Multiplier { get; }

        public PrestigeMultiplierChangedEvent(double multiplier)
        {
            Multiplier = multiplier;
        }
    }

    // Drives the prestige panel: whether prestige is available, respect gained now, total respect, multiplier.
    public readonly struct PrestigeStateChangedEvent : IGameEvent
    {
        public bool CanPrestige { get; }
        public double PendingRespect { get; }
        public double TotalRespect { get; }
        public double Multiplier { get; }

        public PrestigeStateChangedEvent(bool canPrestige, double pendingRespect, double totalRespect, double multiplier)
        {
            CanPrestige = canPrestige;
            PendingRespect = pendingRespect;
            TotalRespect = totalRespect;
            Multiplier = multiplier;
        }
    }
}
