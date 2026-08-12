using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;
using IdleGymBro.Data;

namespace IdleGymBro.UI
{
    // Binds one on-screen button to one UpgradeData.
    public class UpgradeButton : MonoBehaviour
    {
        [SerializeField]
        private UpgradeData _upgrade;

        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private Image _icon;

        private UpgradeManager _manager;
        private double _currentGains;

        private void Start()
        {
            _manager = FindAnyObjectByType<UpgradeManager>();

            // The icon comes from the data asset, so adding an upgrade never means touching the UI.
            if (_icon != null && _upgrade != null)
            {
                _icon.sprite = _upgrade.Icon;
                _icon.enabled = _upgrade.Icon != null;
            }

            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Subscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Subscribe<StatsChangedEvent>(HandleStatsChanged);
            EventBus.Subscribe<BuyMultiplierChangedEvent>(HandleMultiplierChanged);

            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Unsubscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Unsubscribe<StatsChangedEvent>(HandleStatsChanged);
            EventBus.Unsubscribe<BuyMultiplierChangedEvent>(HandleMultiplierChanged);

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            if (_manager != null && _upgrade != null)
            {
                _manager.TryBuy(_upgrade.Id, BuyMultiplier.Current);
            }
        }

        private void Refresh()
        {
            if (_upgrade == null)
            {
                return;
            }

            int level = _manager != null ? _manager.GetLevel(_upgrade.Id) : 0;
            int want = BuyMultiplier.Current;

            // Price what this press will ACTUALLY buy. Near a max level — or with only enough for
            // three of the ten — quoting the full x10 run would show a price the button never
            // charges and would sit greyed out with no explanation.
            int available = _manager != null ? _manager.AffordableLevels(_upgrade.Id, want) : 0;
            int quoted = Mathf.Max(1, available);
            double cost = _manager != null ? _manager.GetCost(_upgrade.Id, quoted) : 0d;

            if (_label != null)
            {
                string batch = want > 1 ? $" x{quoted}" : string.Empty;
                _label.text = $"{_upgrade.DisplayName}  Lv.{level}\n{NumberFormatter.Format(cost)}{batch}";
            }

            if (_button != null)
            {
                _button.interactable = available > 0;
            }
        }

        private void HandleMultiplierChanged(BuyMultiplierChangedEvent e)
        {
            Refresh();
        }

        private void HandleGainsChanged(GainsChangedEvent e)
        {
            _currentGains = e.Total;
            Refresh();
        }

        private void HandleUpgradePurchased(UpgradePurchasedEvent e)
        {
            Refresh();
        }

        private void HandleStatsChanged(StatsChangedEvent e)
        {
            Refresh();
        }
    }
}
