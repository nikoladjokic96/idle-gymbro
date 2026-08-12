using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Character
{
    // Published by WardrobeManager when a cosmetic is equipped on a layer; CharacterBuilder
    // swaps that layer's sprite.
    //
    // Carries the CosmeticData, not just a Sprite, because the right sprite depends on the muscle
    // tier the character is currently at (shorts are cut per tier). CharacterBuilder keeps the data
    // so it can re-resolve the layer when the tier changes, without the wardrobe re-publishing.
    public readonly struct CosmeticEquippedEvent : IGameEvent
    {
        public CharacterLayer Layer { get; }
        public Sprite Sprite { get; }
        public CosmeticData Cosmetic { get; }

        public CosmeticEquippedEvent(CharacterLayer layer, Sprite sprite, CosmeticData cosmetic = null)
        {
            Layer = layer;
            Sprite = sprite;
            Cosmetic = cosmetic;
        }
    }
}
