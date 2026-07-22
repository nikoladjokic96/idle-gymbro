using UnityEngine;
using IdleGymBro.Data;

namespace IdleGymBro.Core
{
    // Runs first so EventBus.Clear() in Awake happens before any system subscribes
    // (in OnEnable), otherwise a stale-state wipe could delete live subscriptions.
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField]
        private GameConfig _gameConfig;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Domain reload may be disabled in editor settings, so static EventBus
            // state from a previous play session must be wiped on fresh boot.
            EventBus.Clear();
        }
    }
}
