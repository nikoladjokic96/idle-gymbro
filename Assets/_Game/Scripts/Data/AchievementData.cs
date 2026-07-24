using UnityEngine;

namespace IdleGymBro.Data
{
    public enum AchievementType
    {
        TotalGainsEarned,
        RepsPerformed,
        UpgradesBought,
        LocationReached,
    }

    [CreateAssetMenu(fileName = "Achievement", menuName = "IdleGymBro/Achievement")]
    public class AchievementData : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private AchievementType _type;

        [SerializeField]
        private double _threshold;

        [SerializeField]
        private double _rewardGains;

        public string Id => _id;
        public string DisplayName => _displayName;
        public AchievementType Type => _type;
        public double Threshold => _threshold;
        public double RewardGains => _rewardGains;
    }
}
