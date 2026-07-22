using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Gameplay
{
    public readonly struct TapEvent : IGameEvent { }

    public class TapController : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private float _repTimer;
        private bool _missingConfigLogged;

        private void Update()
        {
            if (_gameConfig == null)
            {
                if (!_missingConfigLogged)
                {
                    Debug.LogError("TapController: GameConfig is not assigned. Tapping is disabled.");
                    _missingConfigLogged = true;
                }

                return;
            }

            // Taps that land on UI (a button, or the modal's full-screen dimmer) must not
            // train — otherwise pressing "Upgrades" or buying would also drain energy.
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool held = !overUi && Pointer.current != null && Pointer.current.press.isPressed;

            if (held)
            {
                _repTimer += Time.deltaTime;

                while (_repTimer >= _gameConfig.RepIntervalSeconds)
                {
                    _repTimer -= _gameConfig.RepIntervalSeconds;
                    EventBus.Publish(new TapEvent());
                }
            }
            else
            {
                // Next press should fire a rep immediately instead of waiting out a stale timer.
                _repTimer = _gameConfig.RepIntervalSeconds;
            }
        }
    }
}
