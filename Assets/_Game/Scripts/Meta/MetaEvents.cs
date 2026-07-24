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

    // Published when the number of claimable achievements changes (for the HUD badge / panel).
    public readonly struct AchievementsChangedEvent : IGameEvent
    {
        public int ClaimableCount { get; }

        public AchievementsChangedEvent(int claimableCount)
        {
            ClaimableCount = claimableCount;
        }
    }
}
