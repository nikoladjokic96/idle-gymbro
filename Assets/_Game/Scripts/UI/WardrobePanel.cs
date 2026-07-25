using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Character;

namespace IdleGymBro.UI
{
    // Wardrobe modal: one row per customizable layer with a NEXT button that cycles its variants.
    public class WardrobePanel : MonoBehaviour
    {
        [SerializeField]
        private Button _hairNext;

        [SerializeField]
        private TMP_Text _hairLabel;

        [SerializeField]
        private Button _beardNext;

        [SerializeField]
        private TMP_Text _beardLabel;

        [SerializeField]
        private Button _shortsNext;

        [SerializeField]
        private TMP_Text _shortsLabel;

        private WardrobeManager _manager;

        private void Start()
        {
            _manager = FindAnyObjectByType<WardrobeManager>();
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CosmeticEquippedEvent>(HandleCosmeticEquipped);

            if (_hairNext != null) { _hairNext.onClick.AddListener(OnHairNext); }
            if (_beardNext != null) { _beardNext.onClick.AddListener(OnBeardNext); }
            if (_shortsNext != null) { _shortsNext.onClick.AddListener(OnShortsNext); }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CosmeticEquippedEvent>(HandleCosmeticEquipped);

            if (_hairNext != null) { _hairNext.onClick.RemoveListener(OnHairNext); }
            if (_beardNext != null) { _beardNext.onClick.RemoveListener(OnBeardNext); }
            if (_shortsNext != null) { _shortsNext.onClick.RemoveListener(OnShortsNext); }
        }

        private void OnHairNext() { _manager?.CycleLayer(CharacterLayer.Hair); }
        private void OnBeardNext() { _manager?.CycleLayer(CharacterLayer.Beard); }
        private void OnShortsNext() { _manager?.CycleLayer(CharacterLayer.Shorts); }

        private void HandleCosmeticEquipped(CosmeticEquippedEvent e)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_manager == null)
            {
                return;
            }

            SetLabel(_hairLabel, "Hair", CharacterLayer.Hair);
            SetLabel(_beardLabel, "Beard", CharacterLayer.Beard);
            SetLabel(_shortsLabel, "Shorts", CharacterLayer.Shorts);
        }

        private void SetLabel(TMP_Text label, string title, CharacterLayer layer)
        {
            if (label != null)
            {
                string equipped = _manager.GetEquippedId(layer);
                label.text = $"{title}: {(string.IsNullOrEmpty(equipped) ? "-" : equipped)}";
            }
        }
    }
}
