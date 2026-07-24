using IdleGymBro.Core;

namespace IdleGymBro.Meta
{
    // Periodic "time chest" state for the HUD button: ready to collect, or counting down.
    public readonly struct PeriodicRewardStateChangedEvent : IGameEvent
    {
        public bool IsReady { get; }
        public float SecondsRemaining { get; }
        public double RewardAmount { get; }

        public PeriodicRewardStateChangedEvent(bool isReady, float secondsRemaining, double rewardAmount)
        {
            IsReady = isReady;
            SecondsRemaining = secondsRemaining;
            RewardAmount = rewardAmount;
        }
    }
}
