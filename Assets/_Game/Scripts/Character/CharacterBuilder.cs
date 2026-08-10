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

        // Closed-eyelids patch drawn over the face during a blink. One sprite serves every tier:
        // the tier art keeps the head pixel-identical, so the eyes are always in the same place.
        [SerializeField]
        private Sprite _blinkSprite;

        private readonly Dictionary<CharacterLayer, SpriteRenderer> _renderers = new Dictionary<CharacterLayer, SpriteRenderer>();

        private int _currentTierIndex = -1;
        private bool _missingTiersLogged;

        // The Body layer's renderer and the active tier's idle clip, exposed so CharacterAnimator
        // can play frames without reaching into the layer stack itself. Frame 0 is always the
        // tier's static BodySprite, so a tier with no clip authored simply never animates.
        public SpriteRenderer BodyRenderer => _renderers.TryGetValue(CharacterLayer.Body, out SpriteRenderer r) ? r : null;

        // Second body renderer sitting one sorting step above the first (still under Shorts at 10).
        // The animator fades the NEXT frame in over the current one, so a 3-frame clip reads as
        // continuous motion instead of three discrete pops.
        public SpriteRenderer BodyBlendRenderer { get; private set; }

        // The Head layer carries the blink patch: above the body, below beard and hair, and
        // otherwise unused now that the tier art includes the head.
        public SpriteRenderer BlinkRenderer => _renderers.TryGetValue(CharacterLayer.Head, out SpriteRenderer r) ? r : null;

        public Sprite BlinkSprite => _blinkSprite;

        public Sprite[] CurrentIdleFrames { get; private set; }

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

            var blendGo = new GameObject("Layer_BodyBlend");
            blendGo.transform.SetParent(transform, false);
            BodyBlendRenderer = blendGo.AddComponent<SpriteRenderer>();
            BodyBlendRenderer.sortingOrder = (int)CharacterLayer.Body + 1;
            SetAlpha(BodyBlendRenderer, 0f);
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Subscribe<CosmeticEquippedEvent>(HandleCosmeticEquipped);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Unsubscribe<CosmeticEquippedEvent>(HandleCosmeticEquipped);
        }

        private void Start()
        {
            // Applies the lowest tier available at zero lifetime earned. SaveSystem restore
            // (execution order +1000) republishes GainsChangedEvent after Start, so a loaded
            // TotalEarned re-applies the correct tier automatically. Cosmetics are driven by
            // WardrobeManager via CosmeticEquippedEvent (published in its Start/restore).
            HandleGainsChanged(new GainsChangedEvent(0d, 0d));
        }

        // Sets the sprite for one layer whenever the wardrobe equips a cosmetic there.
        private void HandleCosmeticEquipped(CosmeticEquippedEvent e)
        {
            if (_renderers.TryGetValue(e.Layer, out SpriteRenderer renderer))
            {
                renderer.sprite = e.Sprite;
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

            // Frame 0 is the static pose; any authored frames follow it, so the clip always starts
            // from exactly what a non-animating tier would show.
            CurrentIdleFrames = BuildIdleClip(tier);

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

        private static Sprite[] BuildIdleClip(MuscleTierData tier)
        {
            if (tier == null || tier.BodySprite == null)
            {
                return null;
            }

            var clip = new List<Sprite> { tier.BodySprite };

            if (tier.IdleFrames != null)
            {
                foreach (Sprite frame in tier.IdleFrames)
                {
                    if (frame != null)
                    {
                        clip.Add(frame);
                    }
                }
            }

            return clip.Count > 1 ? clip.ToArray() : null;
        }

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
