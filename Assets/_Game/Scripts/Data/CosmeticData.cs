using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "Cosmetic", menuName = "IdleGymBro/Cosmetic")]
    public class CosmeticData : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private CharacterLayer _layer;

        [SerializeField]
        private Sprite _sprite;

        // Per-tier variants, indexed by tier-1. A garment that wraps the body cannot be one sprite
        // for every muscle tier: the hips are far wider on tier 6 than on tier 1, so a single pair
        // of shorts either floats off the skinny body or cuts into the huge one. Empty means "this
        // cosmetic looks the same at every tier" (hair, beard — the skull barely changes).
        [SerializeField]
        private Sprite[] _tierSprites;

        [SerializeField]
        private double _cost; // 0 = free/default

        [SerializeField]
        private bool _unlockedByDefault = true; // wardrobe/shop logic is post-MVP

        public string Id => _id;
        public string DisplayName => _displayName;
        public CharacterLayer Layer => _layer;
        public Sprite Sprite => _sprite;
        public Sprite[] TierSprites => _tierSprites;
        public double Cost => _cost;
        public bool UnlockedByDefault => _unlockedByDefault;

        // Falls back to the shared sprite whenever this tier has no variant, so a half-filled array
        // degrades to the old behaviour instead of rendering nothing.
        public Sprite SpriteForTier(int tier)
        {
            int index = tier - 1;

            if (_tierSprites != null && index >= 0 && index < _tierSprites.Length && _tierSprites[index] != null)
            {
                return _tierSprites[index];
            }

            return _sprite;
        }
    }
}
