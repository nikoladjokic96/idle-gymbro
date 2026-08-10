using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Gameplay;

namespace IdleGymBro.Character
{
    // Plays the character's idle clip as real sprite frames.
    //
    // An earlier version faked breathing by scaling the root transform; it read as the character
    // inflating and deflating, not breathing, because a uniform scale moves the head and legs too.
    // Real frames only redraw the torso, so the motion is anatomically correct.
    //
    // Playback is PING-PONG (0,1,2,1,…): the authored frames cover the inhale, and replaying them
    // backwards gives the exhale for free — half the art for a full breath cycle.
    //
    // The clip lives on MuscleTierData, so each muscle tier breathes with its own silhouette, and
    // CharacterBuilder swaps the clip when the tier changes.
    [DefaultExecutionOrder(50)]
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        [SerializeField]
        private CharacterBuilder _characterBuilder;

        private Vector3 _baseScale;
        private float _phase;
        private float _punch;              // 1 -> 0 after each player rep
        private float _timeSinceRep = 999f;
        private bool _tired;
        private bool _missingConfigLogged;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Subscribe<EnergyChangedEvent>(HandleEnergyChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Unsubscribe<EnergyChangedEvent>(HandleEnergyChanged);

            // Never leave a punch baked into the transform.
            transform.localScale = _baseScale;
        }

        private void Start()
        {
            if (_characterBuilder == null)
            {
                _characterBuilder = GetComponent<CharacterBuilder>();
            }
        }

        private void HandleRepPerformed(RepPerformedEvent e)
        {
            _punch = 1f;
            _timeSinceRep = 0f;
        }

        // "Tired" = not enough energy left for another rep, i.e. exactly when the game stops
        // accepting training input (§5). Breathing slows to sell the exhaustion.
        private void HandleEnergyChanged(EnergyChangedEvent e)
        {
            if (!ValidateConfig())
            {
                return;
            }

            _tired = e.Current < _gameConfig.EnergyPerRep;
        }

        private void Update()
        {
            if (!ValidateConfig())
            {
                return;
            }

            float dt = Time.deltaTime;
            _timeSinceRep += dt;

            AdvanceIdleClip(dt);
            ApplyRepPunch(dt);
        }

        private void AdvanceIdleClip(float dt)
        {
            Sprite[] frames = _characterBuilder != null ? _characterBuilder.CurrentIdleFrames : null;
            SpriteRenderer renderer = _characterBuilder != null ? _characterBuilder.BodyRenderer : null;

            if (frames == null || frames.Length < 2 || renderer == null)
            {
                return; // tier without an authored clip simply holds its static pose
            }

            bool training = _timeSinceRep < _gameConfig.IdleTrainingWindowSeconds;

            float rate = _tired
                ? _gameConfig.IdleTiredRateMultiplier
                : training ? _gameConfig.IdleTrainingRateMultiplier : 1f;

            _phase += dt * _gameConfig.IdleBreathCyclesPerSecond * rate;
            _phase -= Mathf.Floor(_phase); // keep in 0..1 without losing precision over a session

            // Ping-pong: N frames produce 2N-2 steps (3 frames -> 0,1,2,1).
            int steps = frames.Length * 2 - 2;
            int step = Mathf.Clamp(Mathf.FloorToInt(_phase * steps), 0, steps - 1);
            int index = step < frames.Length ? step : steps - step;

            renderer.sprite = frames[index];
        }

        // Kept as a deliberate hit-feedback on the player's own reps — not idle motion.
        private void ApplyRepPunch(float dt)
        {
            if (_punch <= 0f)
            {
                if (transform.localScale != _baseScale)
                {
                    transform.localScale = _baseScale;
                }

                return;
            }

            _punch = Mathf.Max(0f, _punch - dt / Mathf.Max(0.0001f, _gameConfig.RepPunchDuration));

            float curve = _punch * _punch; // ease-out, snappier than linear
            float stretch = 1f + (_gameConfig.RepPunchScale - 1f) * curve;
            float squash = 1f - (stretch - 1f) * 0.5f;

            transform.localScale = new Vector3(_baseScale.x * squash, _baseScale.y * stretch, _baseScale.z);
        }

        private bool ValidateConfig()
        {
            if (_gameConfig != null)
            {
                return true;
            }

            if (!_missingConfigLogged)
            {
                Debug.LogError("CharacterAnimator: GameConfig is not assigned. Character animation is disabled.");
                _missingConfigLogged = true;
            }

            return false;
        }
    }
}
