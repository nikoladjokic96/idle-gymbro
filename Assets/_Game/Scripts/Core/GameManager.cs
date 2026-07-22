using UnityEngine;
using IdleGymBro.Data;

namespace IdleGymBro.Core
{
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
