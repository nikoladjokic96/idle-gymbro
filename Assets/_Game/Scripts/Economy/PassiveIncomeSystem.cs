using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;

namespace IdleGymBro.Economy
{
    public class PassiveIncomeSystem : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private bool _missingConfigLogged;

        // Upgrades will add to this later; for now it is just the base config rate.
        public double GainsPerSecond => _gameConfig != null ? _gameConfig.BasePassiveGainsPerSecond : 0d;

        private void OnEnable()
        {
            EventBus.Subscribe<TickEvent>(HandleTick);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TickEvent>(HandleTick);
        }

        private void Start()
        {
            if (!ValidateConfig())
            {
                return;
            }

            // Published in Start so HUD (subscribed in OnEnable) is ready to receive it.
            EventBus.Publish(new PassiveIncomeChangedEvent(GainsPerSecond));
        }

        private void HandleTick(TickEvent e)
        {
            double gainsPerSecond = GainsPerSecond;

            if (gainsPerSecond <= 0d)
            {
                return;
            }

            EventBus.Publish(new GainsEarnedEvent(gainsPerSecond * e.DeltaTime));
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("PassiveIncomeSystem: GameConfig is not assigned. Passive income is disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }
}
