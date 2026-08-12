namespace IdleGymBro.Data
{
    public class SaveData
    {
        public int Version = 1;
        public double TotalGains;
        public double TotalEarned;
        public float CurrentEnergy;
        public long LastSaveTimeTicks;
        public int CurrentLocationIndex;
        public long AchievementReps;
        public int AchievementUpgradesBought;
        public int MaxLocationIndex;
        public double AchievementLifetimeEarned; // survives prestige, unlike TotalEarned
        public System.Collections.Generic.List<string> ClaimedAchievements = new System.Collections.Generic.List<string>();
        public long LastDailyClaimDay;
        public int DailyStreak;
        public double TotalRespect;
        public System.Collections.Generic.Dictionary<string, string> EquippedCosmetics = new System.Collections.Generic.Dictionary<string, string>();
        public System.Collections.Generic.Dictionary<string, int> UpgradeLevels = new System.Collections.Generic.Dictionary<string, int>();

        // When the current upgrade cooldown expires, in UTC ticks; 0 = none running. Stored as an
        // absolute deadline rather than "seconds remaining" so the wait keeps running while the app
        // is closed — a rest timer you can skip by force-quitting is not a rest timer.
        public long UpgradeCooldownEndTicks;

        // How many cooldowns have already been served this run, so the next one is longer even if
        // the player quits between them.
        public int UpgradeCooldownsServed;
    }
}
