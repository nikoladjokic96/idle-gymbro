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
    }
}
