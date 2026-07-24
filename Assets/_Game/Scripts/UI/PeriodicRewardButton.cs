using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Meta;

namespace IdleGymBro.UI
{
    // Bottom-right HUD button: shows a countdown, then "COLLECT +X" when the periodic reward
    // is ready. Clicking while ready claims it.
    public class PeriodicRewardButton : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _label;

        private PeriodicRewardManager _manager;

        private void Start()
        {
            _manager = FindAnyObjectByType<PeriodicRewardManager>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PeriodicRewardStateChangedEvent>(HandleStateChanged);

            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PeriodicRewardStateChangedEvent>(HandleStateChanged);

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            _manager?.TryClaim();
        }

        private void HandleStateChanged(PeriodicRewardStateChangedEvent e)
        {
            if (_label != null)
            {
                _label.text = e.IsReady
                    ? $"COLLECT\n+{NumberFormatter.Format(e.RewardAmount)}"
                    : $"REWARD\n{FormatTime(e.SecondsRemaining)}";
            }

            if (_button != null)
            {
                _button.interactable = e.IsReady;
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            int minutes = total / 60;
            int secs = total % 60;
            return $"{minutes:0}:{secs:00}";
        }
    }
}
