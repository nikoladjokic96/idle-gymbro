using IdleGymBro.Core;

namespace IdleGymBro.Economy
{
    public readonly struct BoosterMultipliersChangedEvent : IGameEvent
    {
        public double TapMultiplier { get; }
        public double PassiveMultiplier { get; }

        public BoosterMultipliersChangedEvent(double tapMultiplier, double passiveMultiplier)
        {
            TapMultiplier = tapMultiplier;
            PassiveMultiplier = passiveMultiplier;
        }
    }

    public readonly struct BoosterStateChangedEvent : IGameEvent
    {
        public string BoosterId { get; }
        public bool IsActive { get; }
        public float RemainingSeconds { get; }
        public float CooldownRemainingSeconds { get; }

        public BoosterStateChangedEvent(string boosterId, bool isActive, float remainingSeconds, float cooldownRemainingSeconds)
        {
            BoosterId = boosterId;
            IsActive = isActive;
            RemainingSeconds = remainingSeconds;
            CooldownRemainingSeconds = cooldownRemainingSeconds;
        }
    }
}
