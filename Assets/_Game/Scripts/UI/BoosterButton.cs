using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;
using IdleGymBro.Data;
using IdleGymBro.Monetization;

namespace IdleGymBro.UI
{
    // Binds one on-screen button to one BoosterData, showing ready/active/cooldown state.
    public class BoosterButton : MonoBehaviour
    {
        [SerializeField]
        private BoosterData _booster;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _label;

        private BoosterManager _manager;
        private AdManager _adManager;

        private void Awake()
        {
            // Fallback label set in Awake, NOT Start: BoosterManager.Start() publishes the
            // initial ready-state event (with the "Nx" suffix) before this object's Start
            // would run, and that richer label must not be stomped afterwards.
            if (_label != null && _booster != null)
            {
                _label.text = (_booster.RequiresAd ? "▶ " : string.Empty) + _booster.DisplayName;
            }
        }

        private void Start()
        {
            _manager = FindAnyObjectByType<BoosterManager>();
            _adManager = FindAnyObjectByType<AdManager>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<BoosterStateChangedEvent>(HandleBoosterStateChanged);

            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BoosterStateChangedEvent>(HandleBoosterStateChanged);

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            if (_booster == null || _manager == null)
            {
                return;
            }

            if (_booster.RequiresAd && _adManager != null)
            {
                // Capture the id: by the time the mock ad completes, _booster is still the
                // same reference, but the local copy keeps the closure's intent explicit.
                string id = _booster.Id;
                _adManager.ShowRewarded("booster_" + id, () => _manager.TryActivate(id));
            }
            else
            {
                _manager.TryActivate(_booster.Id);
            }
        }

        private void HandleBoosterStateChanged(BoosterStateChangedEvent e)
        {
            if (_booster == null || e.BoosterId != _booster.Id)
            {
                return;
            }

            if (e.IsActive)
            {
                if (_label != null)
                {
                    _label.text = $"{_booster.DisplayName}\n{Mathf.CeilToInt(e.RemainingSeconds)}s";
                }

                if (_button != null)
                {
                    _button.interactable = false;
                }
            }
            else if (e.CooldownRemainingSeconds > 0f)
            {
                if (_label != null)
                {
                    _label.text = $"{_booster.DisplayName}\nCD {Mathf.CeilToInt(e.CooldownRemainingSeconds)}s";
                }

                if (_button != null)
                {
                    _button.interactable = false;
                }
            }
            else
            {
                if (_label != null)
                {
                    string prefix = _booster.RequiresAd ? "▶ " : string.Empty;
                    _label.text = $"{prefix}{_booster.DisplayName}\n{_booster.Multiplier}x";
                }

                if (_button != null)
                {
                    _button.interactable = true;
                }
            }
        }
    }
}
