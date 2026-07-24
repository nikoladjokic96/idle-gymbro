using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "MuscleTier", menuName = "IdleGymBro/Muscle Tier")]
    public class MuscleTierData : ScriptableObject
    {
        [SerializeField]
        private int _tier;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private double _totalEarnedThreshold;

        [SerializeField]
        private Sprite _bodySprite;

        [SerializeField]
        private Sprite _headSprite; // may be null for MVP (head shared)

        public int Tier => _tier;
        public string DisplayName => _displayName;
        public double TotalEarnedThreshold => _totalEarnedThreshold;
        public Sprite BodySprite => _bodySprite;
        public Sprite HeadSprite => _headSprite;
    }
}
