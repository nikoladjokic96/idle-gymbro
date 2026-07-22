using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "IdleGymBro/Config/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField]
        [Min(0.0001f)]
        private float _tickIntervalSeconds = 0.1f;

        public float TickIntervalSeconds => _tickIntervalSeconds;
    }
}
