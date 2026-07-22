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

        private UpgradeManager _manager;
        private double _currentGains;

        private void Start()
        {
            _manager = FindAnyObjectByType<UpgradeManager>();
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Subscribe<UpgradePurchasedEvent>(HandleUpgradePurchased);
            EventBus.Subscribe<StatsChangedEvent>(HandleStatsChanged);

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

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            if (_manager != null && _upgrade != null)
            {
                _manager.TryBuy(_upgrade.Id);
            }
        }

        private void Refresh()
        {
            if (_upgrade == null)
            {
                return;
            }

            int level = _manager != null ? _manager.GetLevel(_upgrade.Id) : 0;
            double cost = _manager != null ? _manager.GetCost(_upgrade.Id) : 0d;

            if (_label != null)
            {
                _label.text = $"{_upgrade.DisplayName}  Lv.{level}\n{NumberFormatter.Format(cost)}";
            }

            if (_button != null)
            {
                _button.interactable = _currentGains >= cost;
            }
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
