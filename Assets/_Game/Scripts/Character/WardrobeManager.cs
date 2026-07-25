using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Character
{
    // Owns the player's equipped cosmetics (one per layer) and drives the character's look via
    // CosmeticEquippedEvent. Data-driven: all options come from CosmeticData assets. MVP: every
    // cosmetic is free/available; purchase-gating (§8) is a later addition.
    public class WardrobeManager : MonoBehaviour, ISaveable
    {
        [SerializeField]
        private CosmeticData[] _cosmetics;

        private readonly Dictionary<CharacterLayer, string> _equipped = new Dictionary<CharacterLayer, string>();

        private void Start()
        {
            EnsureDefaults();
            PublishAll();
        }

        public List<CosmeticData> GetCosmeticsForLayer(CharacterLayer layer)
        {
            if (_cosmetics == null)
            {
                return new List<CosmeticData>();
            }

            return _cosmetics.Where(c => c != null && c.Layer == layer).ToList();
        }

        public string GetEquippedId(CharacterLayer layer)
        {
            return _equipped.TryGetValue(layer, out string id) ? id : null;
        }

        public void Equip(string id)
        {
            CosmeticData cosmetic = FindById(id);
            if (cosmetic == null)
            {
                return;
            }

            _equipped[cosmetic.Layer] = cosmetic.Id;
            EventBus.Publish(new CosmeticEquippedEvent(cosmetic.Layer, cosmetic.Sprite));
        }

        // Cycles to the next cosmetic in a layer (wraps) — drives the wardrobe UI's NEXT button.
        public void CycleLayer(CharacterLayer layer)
        {
            List<CosmeticData> options = GetCosmeticsForLayer(layer);
            if (options.Count == 0)
            {
                return;
            }

            string current = GetEquippedId(layer);
            int index = options.FindIndex(c => c.Id == current);
            int next = (index + 1) % options.Count;
            Equip(options[next].Id);
        }

        private CosmeticData FindById(string id)
        {
            return _cosmetics != null ? _cosmetics.FirstOrDefault(c => c != null && c.Id == id) : null;
        }

        // Any layer that has cosmetics but no equipped selection defaults to its first option.
        private void EnsureDefaults()
        {
            if (_cosmetics == null)
            {
                return;
            }

            foreach (CosmeticData cosmetic in _cosmetics)
            {
                if (cosmetic != null && !_equipped.ContainsKey(cosmetic.Layer))
                {
                    _equipped[cosmetic.Layer] = cosmetic.Id;
                }
            }
        }

        private void PublishAll()
        {
            foreach (KeyValuePair<CharacterLayer, string> pair in _equipped)
            {
                CosmeticData cosmetic = FindById(pair.Value);
                if (cosmetic != null)
                {
                    EventBus.Publish(new CosmeticEquippedEvent(cosmetic.Layer, cosmetic.Sprite));
                }
            }
        }

        public void CaptureState(SaveData data)
        {
            data.EquippedCosmetics = new Dictionary<string, string>();
            foreach (KeyValuePair<CharacterLayer, string> pair in _equipped)
            {
                data.EquippedCosmetics[pair.Key.ToString()] = pair.Value;
            }
        }

        public void RestoreState(SaveData data)
        {
            _equipped.Clear();

            if (data.EquippedCosmetics != null)
            {
                foreach (KeyValuePair<string, string> pair in data.EquippedCosmetics)
                {
                    if (Enum.TryParse(pair.Key, out CharacterLayer layer))
                    {
                        _equipped[layer] = pair.Value;
                    }
                }
            }

            EnsureDefaults(); // fill in any layer added since the save was written
            PublishAll();
        }
    }
}
