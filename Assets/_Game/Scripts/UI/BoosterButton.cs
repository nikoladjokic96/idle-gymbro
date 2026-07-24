using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;
using IdleGymBro.Data;

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

        private void Awake()
        {
            // Fallback label set in Awake, NOT Start: BoosterManager.Start() publishes the
            // initial ready-state event (with the "Nx" suffix) before this object's Start
            // would run, and that richer label must not be stomped afterwards.
            if (_label != null && _booster != null)
            {
                _label.text = _booster.DisplayName;
            }
        }

        private void Start()
        {
            _manager = FindAnyObjectByType<BoosterManager>();
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
            if (_booster != null)
            {
                _manager?.TryActivate(_booster.Id);
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
                    _label.text = $"{_booster.DisplayName}\n{_booster.Multiplier}x";
                }

                if (_button != null)
                {
                    _button.interactable = true;
                }
            }
        }
    }
}
