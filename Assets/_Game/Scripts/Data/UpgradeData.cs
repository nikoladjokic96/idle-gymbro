using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "Upgrade", menuName = "IdleGymBro/Upgrade")]
    public class UpgradeData : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private StatType _statType;

        [SerializeField]
        private double _effectPerLevel;

        [SerializeField]
        private double _baseCost;

        [SerializeField]
        private float _growthRate = 1.1f;

        [SerializeField]
        private int _maxLevel = 0; // 0 = unlimited

        public string Id => _id;
        public string DisplayName => _displayName;
        public StatType StatType => _statType;
        public double EffectPerLevel => _effectPerLevel;
        public double BaseCost => _baseCost;
        public float GrowthRate => _growthRate;
        public int MaxLevel => _maxLevel;
    }
}
