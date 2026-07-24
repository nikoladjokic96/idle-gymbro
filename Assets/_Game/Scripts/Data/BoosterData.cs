using UnityEngine;

namespace IdleGymBro.Data
{
    public enum BoosterTarget
    {
        TapIncome,
        PassiveIncome
    }

    [CreateAssetMenu(fileName = "Booster", menuName = "IdleGymBro/Booster")]
    public class BoosterData : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private BoosterTarget _target;

        [SerializeField]
        [Min(1f)]
        private float _multiplier = 2f;

        [SerializeField]
        [Min(1f)]
        private float _durationSeconds = 60f;

        [SerializeField]
        [Min(0f)]
        private float _cooldownSeconds = 180f;

        [SerializeField]
        private bool _requiresAd = false;

        public string Id => _id;
        public string DisplayName => _displayName;
        public BoosterTarget Target => _target;
        public float Multiplier => _multiplier;
        public float DurationSeconds => _durationSeconds;
        public float CooldownSeconds => _cooldownSeconds;
        public bool RequiresAd => _requiresAd;
    }
}
