using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "Location", menuName = "IdleGymBro/Location")]
    public class LocationData : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private int _totalLevelsToComplete;

        [SerializeField]
        [Min(1f)]
        private float _globalMultiplier = 1f;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int TotalLevelsToComplete => _totalLevelsToComplete;
        public float GlobalMultiplier => _globalMultiplier;
    }
}
