using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleGymBro.Core;
using IdleGymBro.Gameplay;
using IdleGymBro.Economy;

namespace IdleGymBro.UI
{
    public class HudController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _gainsText;

        [SerializeField]
        private Image _energyFill;

        [SerializeField]
        private TMP_Text _energyText;

        private void OnEnable()
        {
            EventBus.Subscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Subscribe<EnergyChangedEvent>(HandleEnergyChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GainsChangedEvent>(HandleGainsChanged);
            EventBus.Unsubscribe<EnergyChangedEvent>(HandleEnergyChanged);
        }

        private void HandleGainsChanged(GainsChangedEvent e)
        {
            if (_gainsText != null)
            {
                _gainsText.text = NumberFormatter.Format(e.Total);
            }
        }

        private void HandleEnergyChanged(EnergyChangedEvent e)
        {
            if (_energyFill != null)
            {
                _energyFill.fillAmount = e.Max > 0f ? Mathf.Clamp01(e.Current / e.Max) : 0f;
            }

            if (_energyText != null)
            {
                _energyText.text = $"{Mathf.CeilToInt(e.Current)}/{Mathf.CeilToInt(e.Max)}";
            }
        }
    }
}
