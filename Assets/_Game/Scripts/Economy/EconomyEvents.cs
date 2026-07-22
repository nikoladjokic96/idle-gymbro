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
}
