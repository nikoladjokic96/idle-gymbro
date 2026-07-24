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

        [SerializeField]
        private double _cost; // 0 = free/default

        [SerializeField]
        private bool _unlockedByDefault = true; // wardrobe/shop logic is post-MVP

        public string Id => _id;
        public string DisplayName => _displayName;
        public CharacterLayer Layer => _layer;
        public Sprite Sprite => _sprite;
        public double Cost => _cost;
        public bool UnlockedByDefault => _unlockedByDefault;
    }
}
