using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Economy;

namespace IdleGymBro.UI
{
    // The x1 / x10 switch at the top of the Upgrades modal. Flips the shared BuyMultiplier; every
    // upgrade row re-prices itself off the event, so the button owns no per-row state.
    public class BuyMultiplierToggle : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private TMP_Text _label;

        [SerializeField]
        private Image _surface;

        // Lit when x10 is armed, so the state is readable without parsing the text.
        [SerializeField]
        private Color _onColor = new Color(0.16f, 0.51f, 0.96f, 1f);

        [SerializeField]
        private Color _offColor = Color.white;

        private void OnEnable()
        {
            EventBus.Subscribe<BuyMultiplierChangedEvent>(HandleChanged);

            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }

            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BuyMultiplierChangedEvent>(HandleChanged);

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            BuyMultiplier.Toggle();
        }

        private void HandleChanged(BuyMultiplierChangedEvent e)
        {
            Refresh();
        }

        private void Refresh()
        {
            bool bulk = BuyMultiplier.Current == BuyMultiplier.Bulk;

            if (_label != null)
            {
                _label.text = bulk ? "BUY x10" : "BUY x1";
            }

            if (_surface != null)
            {
                _surface.color = bulk ? _onColor : _offColor;
            }
        }
    }
}
