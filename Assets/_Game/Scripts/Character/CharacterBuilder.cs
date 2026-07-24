using System;
using System.Collections.Generic;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Economy;

namespace IdleGymBro.Character
{
    // Builds the world-space character as a stack of SpriteRenderer child layers, applies the
    // muscle tier driven by lifetime earned gains (never shrinks on spend), and equips the
    // default cosmetics. Renderers are created at runtime so no scene wiring is required.
    public class CharacterBuilder : MonoBehaviour
    {
        [SerializeField]
        private MuscleTierData[] _tiers; // sorted by threshold ascending

        [SerializeField]
        private CosmeticData[] _defaultCosmetics;

        private readonly Dictionary<CharacterLayer, SpriteRenderer> _renderers = new Dictionary<CharacterLayer, SpriteRenderer>();

        private int _currentTierIndex = -1;
        private bool _missingTiersLogged;

        private void Awake()
        {
            foreach (CharacterLayer layer in Enum.GetValues(typeof(CharacterLayer)))
            {
                var layerGo = new GameObject("Layer_" + layer);
                layerGo.transform.SetParent(transform, false);

                var renderer = layerGo.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = (int)layer;

                _renderers[layer] = renderer;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GainsChangedEvent>(HandleGainsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GainsChangedEvent>(HandleGainsChanged);
        }

        private void Start()
        {
            // Applies the lowest tier available at zero lifetime earned. SaveSystem restore
            // (execution order +1000) republishes GainsChangedEvent after Start, so a loaded
            // TotalEarned re-applies the correct tier automatically.
            HandleGainsChanged(new GainsChangedEvent(0d, 0d));

            if (_defaultCosmetics == null)
            {
                return;
            }

            foreach (CosmeticData cosmetic in _defaultCosmetics)
            {
                if (cosmetic != null && cosmetic.UnlockedByDefault && _renderers.TryGetValue(cosmetic.Layer, out SpriteRenderer renderer))
                {
                    renderer.sprite = cosmetic.Sprite;
                }
            }
        }

        private void HandleGainsChanged(GainsChangedEvent e)
        {
            if (!ValidateTiers())
            {
                return;
            }

            // Pick the tier with the HIGHEST threshold <= TotalEarned, independent of array order
            // (inspector reordering must never silently select a lower tier).
            int idx = -1;
            double bestThreshold = double.NegativeInfinity;
            for (int i = 0; i < _tiers.Length; i++)
            {
                if (_tiers[i] != null && e.TotalEarned >= _tiers[i].TotalEarnedThreshold && _tiers[i].TotalEarnedThreshold > bestThreshold)
                {
                    idx = i;
                    bestThreshold = _tiers[i].TotalEarnedThreshold;
                }
            }

            if (idx < 0 || idx == _currentTierIndex)
            {
                return;
            }

            MuscleTierData tier = _tiers[idx];
            _currentTierIndex = idx;

            if (_renderers.TryGetValue(CharacterLayer.Body, out SpriteRenderer bodyRenderer))
            {
                bodyRenderer.sprite = tier.BodySprite;
            }

            if (tier.HeadSprite != null && _renderers.TryGetValue(CharacterLayer.Head, out SpriteRenderer headRenderer))
            {
                headRenderer.sprite = tier.HeadSprite;
            }

            EventBus.Publish(new MuscleTierChangedEvent(tier.Tier, tier.DisplayName));
        }

        public int CurrentTier => _currentTierIndex >= 0 && _tiers != null && _currentTierIndex < _tiers.Length && _tiers[_currentTierIndex] != null ? _tiers[_currentTierIndex].Tier : 0;

        private bool ValidateTiers()
        {
            if (_tiers != null && _tiers.Length > 0)
            {
                return true;
            }

            if (!_missingTiersLogged)
            {
                Debug.LogError("CharacterBuilder: no MuscleTierData assigned. Muscle tier progression is disabled.");
                _missingTiersLogged = true;
            }

            return false;
        }
    }
}
