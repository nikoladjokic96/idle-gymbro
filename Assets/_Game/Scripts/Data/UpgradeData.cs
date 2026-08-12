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

        // White silhouette icon; the UI tints it, so one sprite covers every colour variant.
        [SerializeField]
        private Sprite _icon;

        [SerializeField]
        private UpgradeCategory _category;

        // Equipment only: the location this gear belongs to. Empty means "always available", which
        // is what Body and Macros use. Gear from a location you have not reached yet is hidden
        // rather than shown greyed out — a list of things you cannot buy is not information.
        [SerializeField]
        private string _locationId;

        public string Id => _id;
        public Sprite Icon => _icon;
        public string DisplayName => _displayName;
        public StatType StatType => _statType;
        public double EffectPerLevel => _effectPerLevel;
        public double BaseCost => _baseCost;
        public float GrowthRate => _growthRate;
        public int MaxLevel => _maxLevel;
        public UpgradeCategory Category => _category;
        public string LocationId => _locationId;
    }
}
