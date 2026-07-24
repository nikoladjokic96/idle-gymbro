using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Meta;

namespace IdleGymBro.UI
{
    // Shows a claim popup on launch when today's daily reward is available. Lives on an
    // always-active object; toggles a child panel (same pattern as OfflineClaimPopup).
    public class DailyRewardPopup : MonoBehaviour
    {
        [SerializeField]
        private GameObject _panel;

        [SerializeField]
        private TMP_Text _messageText;

        [SerializeField]
        private Button _claimButton;

        private DailyRewardManager _manager;

        private void Awake()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void Start()
        {
            _manager = FindAnyObjectByType<DailyRewardManager>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DailyRewardAvailableEvent>(HandleDailyRewardAvailable);

            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(OnClaim);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DailyRewardAvailableEvent>(HandleDailyRewardAvailable);

            if (_claimButton != null)
            {
                _claimButton.onClick.RemoveListener(OnClaim);
            }
        }

        private void HandleDailyRewardAvailable(DailyRewardAvailableEvent e)
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
            }

            if (_messageText != null)
            {
                _messageText.text = $"DAILY REWARD\nDay {e.StreakDay}\n+{NumberFormatter.Format(e.RewardAmount)} Gains";
            }
        }

        private void OnClaim()
        {
            _manager?.TryClaim();

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }
    }
}
