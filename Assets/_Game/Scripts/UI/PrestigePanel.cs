using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Progression;

namespace IdleGymBro.UI
{
    // Modal content for prestige ("New Bulk"): shows the permanent multiplier, banked respect,
    // and what a prestige right now would grant; the button resets the run for that respect.
    public class PrestigePanel : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _infoText;

        [SerializeField]
        private Button _prestigeButton;

        [SerializeField]
        private TMP_Text _prestigeLabel;

        private PrestigeManager _manager;

        private void Start()
        {
            _manager = FindAnyObjectByType<PrestigeManager>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PrestigeStateChangedEvent>(HandleStateChanged);

            if (_prestigeButton != null)
            {
                _prestigeButton.onClick.AddListener(OnPrestige);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PrestigeStateChangedEvent>(HandleStateChanged);

            if (_prestigeButton != null)
            {
                _prestigeButton.onClick.RemoveListener(OnPrestige);
            }
        }

        private void OnPrestige()
        {
            _manager?.DoPrestige();
        }

        private void HandleStateChanged(PrestigeStateChangedEvent e)
        {
            if (_infoText != null)
            {
                _infoText.text =
                    $"Global multiplier: x{e.Multiplier:0.##}\n" +
                    $"Banked respect: {NumberFormatter.Format(e.TotalRespect)}\n\n" +
                    $"New Bulk resets this run\nfor +{NumberFormatter.Format(e.PendingRespect)} respect";
            }

            if (_prestigeLabel != null)
            {
                _prestigeLabel.text = "NEW BULK";
            }

            if (_prestigeButton != null)
            {
                _prestigeButton.interactable = e.CanPrestige;
            }
        }
    }
}
