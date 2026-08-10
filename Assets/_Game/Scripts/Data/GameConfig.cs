using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "IdleGymBro/Config/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField]
        [Min(0.0001f)]
        private float _tickIntervalSeconds = 0.1f;

        public float TickIntervalSeconds => _tickIntervalSeconds;

        [Header("Core Loop")]
        [SerializeField]
        [Min(0.01f)]
        private float _maxEnergy = 100f;

        [SerializeField]
        [Min(0.01f)]
        private float _energyPerRep = 5f;

        [SerializeField]
        [Min(0f)]
        private float _energyRegenPerSecond = 10f;

        [SerializeField]
        [Min(0f)]
        private float _gainsPerRep = 1f;

        [SerializeField]
        [Min(0.01f)]
        private float _repIntervalSeconds = 0.25f;

        public float MaxEnergy => _maxEnergy;
        public float EnergyPerRep => _energyPerRep;
        public float EnergyRegenPerSecond => _energyRegenPerSecond;
        public float GainsPerRep => _gainsPerRep;
        public float RepIntervalSeconds => _repIntervalSeconds;

        [Header("Save System")]
        [SerializeField]
        [Min(1f)]
        private float _autoSaveIntervalSeconds = 30f;

        public float AutoSaveIntervalSeconds => _autoSaveIntervalSeconds;

        [Header("Economy")]
        [SerializeField]
        [Min(0f)]
        private float _basePassiveGainsPerSecond = 1f;

        [SerializeField]
        [Min(0f)]
        private float _offlineCapSeconds = 7200f; // 2h

        [SerializeField]
        [Range(0f, 1f)]
        private float _offlineEfficiency = 0.5f;

        public float BasePassiveGainsPerSecond => _basePassiveGainsPerSecond;
        public float OfflineCapSeconds => _offlineCapSeconds;
        public float OfflineEfficiency => _offlineEfficiency;

        [Header("Periodic Reward")]
        [SerializeField]
        [Min(1f)]
        private float _periodicRewardIntervalSeconds = 900f; // 15 min

        [SerializeField]
        [Min(1f)]
        private float _periodicRewardSeconds = 300f; // reward = passive rate x this (worth ~5 min)

        public float PeriodicRewardIntervalSeconds => _periodicRewardIntervalSeconds;
        public float PeriodicRewardSeconds => _periodicRewardSeconds;

        [Header("Daily Reward")]
        [SerializeField]
        [Min(1f)]
        private float _dailyRewardSeconds = 600f; // per streak day: reward = passive rate x this x streakDay

        [SerializeField]
        [Min(1)]
        private int _dailyStreakCycle = 7;

        public float DailyRewardSeconds => _dailyRewardSeconds;
        public int DailyStreakCycle => _dailyStreakCycle;

        [Header("Character Animation")]
        // Full breath cycles per second. The idle clip plays PING-PONG across this period, so one
        // cycle covers inhale + exhale. 0.28 ~= 17 breaths/min, a relaxed resting rate.
        [SerializeField]
        [Min(0.01f)]
        private float _idleBreathCyclesPerSecond = 0.28f;

        [SerializeField]
        [Min(0.05f)]
        private float _idleTrainingWindowSeconds = 1.2f; // "still training" grace after releasing

        [SerializeField]
        [Min(0f)]
        private float _idleTiredRateMultiplier = 0.55f; // out of energy -> slower breathing

        // Curls per second while the player holds the screen. Deliberately decoupled from
        // RepIntervalSeconds (4 reps/s) — one curl per rep would be an unreadable twitch.
        [SerializeField]
        [Min(0.05f)]
        private float _workoutCyclesPerSecond = 1.1f;

        public float WorkoutCyclesPerSecond => _workoutCyclesPerSecond;

        [SerializeField]
        [Min(0.02f)]
        private float _blinkDurationSeconds = 0.11f; // how long the eyes stay shut

        [SerializeField]
        [Min(0.1f)]
        private float _blinkIntervalMinSeconds = 2.5f;

        [SerializeField]
        [Min(0.1f)]
        private float _blinkIntervalMaxSeconds = 6.5f;

        public float BlinkDurationSeconds => _blinkDurationSeconds;
        public float BlinkIntervalMinSeconds => _blinkIntervalMinSeconds;
        public float BlinkIntervalMaxSeconds => _blinkIntervalMaxSeconds;

        public float IdleBreathCyclesPerSecond => _idleBreathCyclesPerSecond;
        public float IdleTrainingWindowSeconds => _idleTrainingWindowSeconds;
        public float IdleTiredRateMultiplier => _idleTiredRateMultiplier;

        [Header("Prestige")]
        [SerializeField]
        [Min(0f)]
        private float _prestigeRespectFactor = 1f; // respect = factor x sqrt(TotalEarned)

        [SerializeField]
        [Min(0f)]
        private float _prestigeMultiplierPerRespect = 0.02f; // multiplier = 1 + respect x this

        [SerializeField]
        [Min(1f)]
        private float _prestigeMinRespect = 10f; // can't prestige until pending respect >= this

        public float PrestigeRespectFactor => _prestigeRespectFactor;
        public float PrestigeMultiplierPerRespect => _prestigeMultiplierPerRespect;
        public float PrestigeMinRespect => _prestigeMinRespect;
    }
}
