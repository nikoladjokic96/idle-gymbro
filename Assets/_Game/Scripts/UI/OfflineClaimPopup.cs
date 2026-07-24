using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;
using IdleGymBro.Monetization;

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

        [SerializeField]
        private Button _doubleButton;

        private AdManager _adManager;
        private double _pendingDoubleAmount;

        private void Awake()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void Start()
        {
            _adManager = FindAnyObjectByType<AdManager>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OfflineProgressEvent>(HandleOfflineProgress);

            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(Hide);
            }

            if (_doubleButton != null)
            {
                _doubleButton.onClick.AddListener(OnDoubleClicked);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OfflineProgressEvent>(HandleOfflineProgress);

            if (_claimButton != null)
            {
                _claimButton.onClick.RemoveListener(Hide);
            }

            if (_doubleButton != null)
            {
                _doubleButton.onClick.RemoveListener(OnDoubleClicked);
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

            _pendingDoubleAmount = e.GainsEarned;

            if (_doubleButton != null)
            {
                _doubleButton.gameObject.SetActive(true);
            }
        }

        private void OnDoubleClicked()
        {
            if (_pendingDoubleAmount <= 0d || _adManager == null)
            {
                return;
            }

            double amount = _pendingDoubleAmount;
            // Zero BEFORE the ad plays so a rapid second click can't queue a second reward
            // while the mock ad is already running for the first.
            _pendingDoubleAmount = 0d;

            if (_doubleButton != null)
            {
                _doubleButton.gameObject.SetActive(false);
            }

            _adManager.ShowRewarded("offline_double", () =>
            {
                EventBus.Publish(new GainsEarnedEvent(amount));
                Hide();
            });
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
