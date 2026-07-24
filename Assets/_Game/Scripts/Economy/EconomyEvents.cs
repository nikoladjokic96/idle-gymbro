using IdleGymBro.Core;

namespace IdleGymBro.Economy
{
    // Grants gains from any source (passive income, offline earnings, future upgrades, etc.).
    public readonly struct GainsEarnedEvent : IGameEvent
    {
        public double Amount { get; }

        public GainsEarnedEvent(double amount)
        {
            Amount = amount;
        }
    }

    public readonly struct PassiveIncomeChangedEvent : IGameEvent
    {
        public double GainsPerSecond { get; }

        public PassiveIncomeChangedEvent(double gainsPerSecond)
        {
            GainsPerSecond = gainsPerSecond;
        }
    }

    public readonly struct OfflineProgressEvent : IGameEvent
    {
        public double GainsEarned { get; }
        public double SecondsAway { get; }

        public OfflineProgressEvent(double gainsEarned, double secondsAway)
        {
            GainsEarned = gainsEarned;
            SecondsAway = secondsAway;
        }
    }

    // Fired per successful tap rep with the actually-credited amount (after booster
    // multipliers), for juice systems (floating text, counter punch) that must react only
    // to active taps, not passive trickle.
    public readonly struct TapGainsEvent : IGameEvent
    {
        public double Amount { get; }

        public TapGainsEvent(double amount)
        {
            Amount = amount;
        }
    }
}
