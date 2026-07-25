using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Character
{
    // Published by WardrobeManager when a cosmetic is equipped on a layer; CharacterBuilder
    // swaps that layer's sprite.
    public readonly struct CosmeticEquippedEvent : IGameEvent
    {
        public CharacterLayer Layer { get; }
        public Sprite Sprite { get; }

        public CosmeticEquippedEvent(CharacterLayer layer, Sprite sprite)
        {
            Layer = layer;
            Sprite = sprite;
        }
    }
}
