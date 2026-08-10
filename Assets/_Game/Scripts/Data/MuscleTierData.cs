using UnityEngine;

namespace IdleGymBro.Data
{
    [CreateAssetMenu(fileName = "MuscleTier", menuName = "IdleGymBro/Muscle Tier")]
    public class MuscleTierData : ScriptableObject
    {
        [SerializeField]
        private int _tier;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private double _totalEarnedThreshold;

        [SerializeField]
        private Sprite _bodySprite;

        [SerializeField]
        private Sprite _headSprite; // may be null for MVP (head shared)

        // Idle breathing clip for this tier, played ping-pong (0,1,2,1,…) so N frames cover a full
        // inhale AND exhale. Authored so ONLY the torso changes between frames — head, hips and
        // legs are pixel-identical — which is what lets the hair/beard/shorts layers stay static
        // and still line up on every frame.
        [SerializeField]
        private Sprite[] _idleFrames;

        // Workout clip, played while the player holds the screen. A bicep curl: ONLY the arms move,
        // so — exactly like the breathing clip — the head, hips and legs stay pixel-identical and
        // the static hair/beard/shorts layers keep lining up on every frame.
        [SerializeField]
        private Sprite[] _workoutFrames;

        public int Tier => _tier;
        public string DisplayName => _displayName;
        public double TotalEarnedThreshold => _totalEarnedThreshold;
        public Sprite BodySprite => _bodySprite;
        public Sprite HeadSprite => _headSprite;
        public Sprite[] IdleFrames => _idleFrames;
        public Sprite[] WorkoutFrames => _workoutFrames;
    }
}
