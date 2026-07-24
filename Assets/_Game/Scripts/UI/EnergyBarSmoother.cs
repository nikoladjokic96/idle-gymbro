using UnityEngine;
using UnityEngine.UI;
using IdleGymBro.Core;
using IdleGymBro.Gameplay;

namespace IdleGymBro.UI
{
    // Lerps the energy bar fill toward the latest EnergyChangedEvent instead of snapping,
    // so regen/spend reads as smooth motion. Sole writer of the fill's fillAmount — HudController
    // no longer holds this reference (see CoreLoopSceneBootstrap), so there is no double-writer.
    public class EnergyBarSmoother : MonoBehaviour
    {
        [SerializeField]
        private Image _fill;

        [SerializeField]
        private float _fillLerpSpeed = 4f;

        private float _targetFill = 1f;

        private void OnEnable()
        {
            EventBus.Subscribe<EnergyChangedEvent>(HandleEnergyChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnergyChangedEvent>(HandleEnergyChanged);
        }

        private void Update()
        {
            if (_fill != null && !Mathf.Approximately(_fill.fillAmount, _targetFill))
            {
                _fill.fillAmount = Mathf.MoveTowards(_fill.fillAmount, _targetFill, _fillLerpSpeed * Time.deltaTime);
            }
        }

        private void HandleEnergyChanged(EnergyChangedEvent e)
        {
            _targetFill = e.Max > 0f ? Mathf.Clamp01(e.Current / e.Max) : 0f;
        }
    }
}
