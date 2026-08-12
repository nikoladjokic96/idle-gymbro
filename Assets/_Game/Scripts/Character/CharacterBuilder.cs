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

        // What is currently worn on each layer, so a tier change can re-cut the garment.
        private readonly Dictionary<CharacterLayer, CosmeticData> _equipped = new Dictionary<CharacterLayer, CosmeticData>();

        private int _currentTierIndex = -1;
        private bool _missingTiersLogged;

        // The Body layer's renderer and the active tier's idle clip, exposed so CharacterAnimator
        // can play frames without reaching into the layer stack itself. Frame 0 is always the
        // tier's static BodySprite, so a tier with no clip authored simply never animates.
        public SpriteRenderer BodyRenderer => _renderers.TryGetValue(CharacterLayer.Body, out SpriteRenderer r) ? r : null;

        // The Head layer carries the blink patch: above the body, below beard and hair, and
        // otherwise unused now that the tier art includes the head.
        public SpriteRenderer BlinkRenderer => _renderers.TryGetValue(CharacterLayer.Head, out SpriteRenderer r) ? r : null;

        // Layers that sit ON the skull. The animator slides these by the current frame's head
        // offset so they ride along with a head that bobs and turns; Shorts and the body itself
        // deliberately stay put.
        public SpriteRenderer HairRenderer => _renderers.TryGetValue(CharacterLayer.Hair, out SpriteRenderer r) ? r : null;

        public SpriteRenderer BeardRenderer => _renderers.TryGetValue(CharacterLayer.Beard, out SpriteRenderer r) ? r : null;

        public Sprite BlinkSprite => _blinkSprite;

        // The active tier's static pose, so the animator can fall back to it instead of leaving the
        // body stuck on whatever frame it stopped on.
        public Sprite CurrentBodySprite { get; private set; }

        public Sprite[] CurrentIdleFrames { get; private set; }

        public Sprite[] CurrentWorkoutFrames { get; private set; }

        // Head offsets in WORLD UNITS, index-aligned with the clips above (entry 0 is the static
        // pose, hence always zero). Null when the tier has no baked anchors.
        public Vector2[] CurrentIdleHeadOffsets { get; private set; }

        public Vector2[] CurrentWorkoutHeadOffsets { get; private set; }

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

        // Sets the sprite for one layer whenever the wardrobe equips a cosmetic there, and remembers
        // WHICH cosmetic it was so the layer can be re-cut when the muscle tier changes.
        private void HandleCosmeticEquipped(CosmeticEquippedEvent e)
        {
            if (e.Cosmetic != null)
            {
                _equipped[e.Layer] = e.Cosmetic;
            }

            if (_renderers.TryGetValue(e.Layer, out SpriteRenderer renderer))
            {
                renderer.sprite = e.Cosmetic != null ? e.Cosmetic.SpriteForTier(CurrentTier) : e.Sprite;
            }
        }

        // Re-resolves every equipped layer against the new tier. Shorts are cut to fit each tier's
        // hips, so growing a tier while wearing them would otherwise leave the old, narrower (or
        // wider) pair on the new body — which is exactly how they ended up floating off the skinny
        // character.
        private void ReapplyCosmeticsForTier()
        {
            foreach (KeyValuePair<CharacterLayer, CosmeticData> pair in _equipped)
            {
                if (pair.Value != null && _renderers.TryGetValue(pair.Key, out SpriteRenderer renderer))
                {
                    renderer.sprite = pair.Value.SpriteForTier(CurrentTier);
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

            // Frame 0 is the static pose; any authored frames follow it, so the clip always starts
            // from exactly what a non-animating tier would show.
            CurrentBodySprite = tier.BodySprite;
            CurrentIdleFrames = BuildClip(tier, tier.IdleFrames);
            CurrentWorkoutFrames = BuildClip(tier, tier.WorkoutFrames);
            CurrentIdleHeadOffsets = BuildOffsets(tier, tier.IdleFrames, tier.IdleHeadOffsets);
            CurrentWorkoutHeadOffsets = BuildOffsets(tier, tier.WorkoutFrames, tier.WorkoutHeadOffsets);

            if (_renderers.TryGetValue(CharacterLayer.Body, out SpriteRenderer bodyRenderer))
            {
                bodyRenderer.sprite = tier.BodySprite;
            }

            if (tier.HeadSprite != null && _renderers.TryGetValue(CharacterLayer.Head, out SpriteRenderer headRenderer))
            {
                headRenderer.sprite = tier.HeadSprite;
            }

            ReapplyCosmeticsForTier();

            EventBus.Publish(new MuscleTierChangedEvent(tier.Tier, tier.DisplayName));
        }

        public int CurrentTier => _currentTierIndex >= 0 && _tiers != null && _currentTierIndex < _tiers.Length && _tiers[_currentTierIndex] != null ? _tiers[_currentTierIndex].Tier : 0;

        // Frame 0 is always the tier's static pose, so every clip starts from exactly what a
        // non-animating tier shows — and both clips share that frame, which makes the transition
        // between breathing and working out land on a common pose.
        private static Sprite[] BuildClip(MuscleTierData tier, Sprite[] extraFrames)
        {
            if (tier == null || tier.BodySprite == null)
            {
                return null;
            }

            var clip = new List<Sprite> { tier.BodySprite };

            if (extraFrames != null)
            {
                foreach (Sprite frame in extraFrames)
                {
                    if (frame != null)
                    {
                        clip.Add(frame);
                    }
                }
            }

            return clip.Count > 1 ? clip.ToArray() : null;
        }

        // Mirrors BuildClip so the offsets line up index-for-index with the clip it describes:
        // a leading zero for the static pose, then one entry per surviving frame. Nulls are skipped
        // in BuildClip, so they must be skipped here too or every later frame reads a stale offset.
        // Pixels become world units via the sprite's own PPU, which is derived from texture height
        // (art-brief §2) — so this stays correct if the art is ever redrawn at another resolution.
        private static Vector2[] BuildOffsets(MuscleTierData tier, Sprite[] frames, Vector2[] pixelOffsets)
        {
            if (tier == null || tier.BodySprite == null || frames == null || pixelOffsets == null)
            {
                return null;
            }

            // A mismatch means the bake is stale (art changed, anchors not re-baked). Compensating
            // with wrong numbers is worse than not compensating: skip rather than guess.
            if (pixelOffsets.Length != frames.Length)
            {
                return null;
            }

            float ppu = tier.BodySprite.pixelsPerUnit;

            if (ppu <= 0f)
            {
                return null;
            }

            var offsets = new List<Vector2> { Vector2.zero };

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                {
                    offsets.Add(pixelOffsets[i] / ppu);
                }
            }

            return offsets.Count > 1 ? offsets.ToArray() : null;
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
