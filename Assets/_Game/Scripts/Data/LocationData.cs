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

        [SerializeField]
        private Sprite _backgroundSprite;

        // Completion is now three conditions, not one running total: reach the Body target, max out
        // every piece of Equipment tied to this location, and bring all three Macros to the target.
        // A single cumulative number let a player brute-force the next location with whichever
        // upgrade happened to be cheapest, which is exactly what the tabs exist to stop.
        [SerializeField]
        private int _bodyLevelTarget = 100;

        [SerializeField]
        private int _macroLevelTarget = 5;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int TotalLevelsToComplete => _totalLevelsToComplete;
        public float GlobalMultiplier => _globalMultiplier;
        public Sprite BackgroundSprite => _backgroundSprite;
        public int BodyLevelTarget => _bodyLevelTarget;
        public int MacroLevelTarget => _macroLevelTarget;
    }
}
