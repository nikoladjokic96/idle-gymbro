using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;

namespace IdleGymBro.UI
{
    public class OfflineClaimPopup : MonoBehaviour
    {
        [SerializeField]
        private GameObject _panel;

        [SerializeField]
        private TMP_Text _messageText;

        [SerializeField]
        private Button _claimButton;

        private void Awake()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OfflineProgressEvent>(HandleOfflineProgress);

            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(Hide);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OfflineProgressEvent>(HandleOfflineProgress);

            if (_claimButton != null)
            {
                _claimButton.onClick.RemoveListener(Hide);
            }
        }

        private void HandleOfflineProgress(OfflineProgressEvent e)
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            if (_messageText != null)
            {
                // Gains are already granted by OfflineEarningsSystem; this is informational only.
                _messageText.text = $"Dok si trenirao van igre:\n+{NumberFormatter.Format(e.GainsEarned)} Gains";
            }
        }

        private void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }
    }
}
