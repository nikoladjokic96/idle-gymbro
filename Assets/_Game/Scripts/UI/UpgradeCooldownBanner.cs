using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;
using IdleGymBro.Monetization;

namespace IdleGymBro.UI
{
    // Shows the remaining rest before upgrades can be bought again, plus the opt-in ad that
    // shortens it. Hidden entirely while no cooldown is running — an always-present timer that
    // usually reads "ready" is just noise in the panel.
    public class UpgradeCooldownBanner : MonoBehaviour
    {
        [SerializeField]
        private GameObject _root;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private Button _adButton;

        private void OnEnable()
        {
            EventBus.Subscribe<UpgradeCooldownChangedEvent>(HandleChanged);

            if (_adButton != null)
            {
                _adButton.onClick.AddListener(OnAdClicked);
            }

            // The modal is disabled while closed, so the current state is re-read on every open
            // rather than relying on having caught the last event.
            UpgradeCooldownManager manager = UpgradeCooldownManager.Instance;
            Apply(manager != null ? manager.RemainingSeconds : 0d);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UpgradeCooldownChangedEvent>(HandleChanged);

            if (_adButton != null)
            {
                _adButton.onClick.RemoveListener(OnAdClicked);
            }
        }

        private void HandleChanged(UpgradeCooldownChangedEvent e)
        {
            Apply(e.RemainingSeconds);
        }

        private void OnAdClicked()
        {
            AdManager ads = FindAnyObjectByType<AdManager>();
            UpgradeCooldownManager manager = UpgradeCooldownManager.Instance;

            if (manager == null)
            {
                return;
            }

            // Shorten only AFTER the ad reports back, never optimistically — the reward has to be
            // the thing the player was offered, not something granted for pressing a button.
            if (ads != null)
            {
                ads.ShowRewarded("upgrade_cooldown", () => manager.ShortenByAd());
            }
            else
            {
                manager.ShortenByAd();
            }
        }

        private void Apply(double remainingSeconds)
        {
            bool active = remainingSeconds > 0d;

            if (_root != null)
            {
                _root.SetActive(active);
            }

            if (!active || _label == null)
            {
                return;
            }

            int total = Mathf.CeilToInt((float)remainingSeconds);
            int minutes = total / 60;
            int seconds = total % 60;
            _label.text = $"RESTING {minutes}:{seconds:00}";
        }
    }
}
