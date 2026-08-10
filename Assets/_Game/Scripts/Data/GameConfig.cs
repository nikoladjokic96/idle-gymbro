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
        [SerializeField]
        [Min(0f)]
        private float _idleBreathAmplitude = 0.022f; // vertical scale swing, fraction of height

        [SerializeField]
        [Min(0f)]
        private float _idleBreathCyclesPerSecond = 0.28f; // ~17 breaths/min at rest

        [SerializeField]
        [Min(0f)]
        private float _idleBobAmplitude = 0.012f; // world units

        [SerializeField]
        [Min(0f)]
        private float _idleTrainingRateMultiplier = 2.6f; // breathing speeds up while repping

        [SerializeField]
        [Min(0.05f)]
        private float _idleTrainingWindowSeconds = 1.2f; // "still training" grace after a rep

        [SerializeField]
        [Min(0f)]
        private float _idleTiredRateMultiplier = 0.55f; // out of energy: slower...

        [SerializeField]
        [Min(0f)]
        private float _idleTiredAmplitudeMultiplier = 1.8f; // ...but deeper

        [SerializeField]
        [Min(1f)]
        private float _repPunchScale = 1.05f;

        [SerializeField]
        [Min(0.01f)]
        private float _repPunchDuration = 0.12f;

        [Header("Character Animation — idle glance (looks around)")]
        [SerializeField]
        [Min(0f)]
        private float _glanceLeanAngle = 3.5f; // degrees of Z tilt at full glance

        [SerializeField]
        [Min(0f)]
        private float _glanceLeanOffset = 0.06f; // world units of sideways shift

        [SerializeField]
        [Min(0.1f)]
        private float _glanceHoldSeconds = 1.1f;

        [SerializeField]
        [Min(0.1f)]
        private float _glanceIntervalMinSeconds = 3f;

        [SerializeField]
        [Min(0.1f)]
        private float _glanceIntervalMaxSeconds = 7f;

        [Header("Character Animation — autonomous workout (squat)")]
        [SerializeField]
        [Min(0.1f)]
        private float _workoutRepIntervalSeconds = 4f; // gap between self-driven reps when idle

        [SerializeField]
        [Min(0.1f)]
        private float _workoutRepDurationSeconds = 1.1f;

        [SerializeField]
        [Min(0f)]
        private float _workoutSquatDepth = 0.34f; // world units the body drops

        [SerializeField]
        [Min(0f)]
        private float _workoutSquatSquash = 0.10f; // vertical squash at the bottom of the rep

        public float GlanceLeanAngle => _glanceLeanAngle;
        public float GlanceLeanOffset => _glanceLeanOffset;
        public float GlanceHoldSeconds => _glanceHoldSeconds;
        public float GlanceIntervalMinSeconds => _glanceIntervalMinSeconds;
        public float GlanceIntervalMaxSeconds => _glanceIntervalMaxSeconds;
        public float WorkoutRepIntervalSeconds => _workoutRepIntervalSeconds;
        public float WorkoutRepDurationSeconds => _workoutRepDurationSeconds;
        public float WorkoutSquatDepth => _workoutSquatDepth;
        public float WorkoutSquatSquash => _workoutSquatSquash;

        public float IdleBreathAmplitude => _idleBreathAmplitude;
        public float IdleBreathCyclesPerSecond => _idleBreathCyclesPerSecond;
        public float IdleBobAmplitude => _idleBobAmplitude;
        public float IdleTrainingRateMultiplier => _idleTrainingRateMultiplier;
        public float IdleTrainingWindowSeconds => _idleTrainingWindowSeconds;
        public float IdleTiredRateMultiplier => _idleTiredRateMultiplier;
        public float IdleTiredAmplitudeMultiplier => _idleTiredAmplitudeMultiplier;
        public float RepPunchScale => _repPunchScale;
        public float RepPunchDuration => _repPunchDuration;

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
