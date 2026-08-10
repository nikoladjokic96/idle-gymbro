using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Gameplay;

namespace IdleGymBro.Character
{
    // All character motion, driven procedurally on the character ROOT:
    //   * breathing      — always, speed/depth react to training vs. exhausted
    //   * idle glance    — occasional lean + look to one side, so he is never statue-still
    //   * idle workout   — periodic squat rep while the player is not tapping
    //   * rep punch      — snap on every player-driven rep
    //
    // Why procedural and not frame-by-frame: the character is a stack of independent sprite layers
    // (body, hair, beard, shorts) combined at runtime by the wardrobe. Frame animation would need
    // every layer redrawn for every frame of every tier, and each new cosmetic would multiply that
    // cost again. Transforming the root moves all layers as one, so registration can never drift.
    // The trade-off is honest: motion that the whole body shares (breathe, lean, squat, bob) looks
    // right, but a true head turn or an arm curl needs real frame art per layer (§7).
    //
    // Single owner of the root's localScale / localPosition / localRotation — nothing else may
    // write them, or the two writers fight and the character jitters.
    [DefaultExecutionOrder(50)]
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        private Vector3 _baseScale;
        private Vector3 _basePosition;

        private float _breathPhase;
        private float _punch;              // 1 -> 0 after each player rep
        private float _timeSinceRep = 999f;

        private float _glanceTimer;
        private float _glanceCooldown;
        private float _glanceDirection = 1f;
        private float _glanceAmount;       // signed -1..1, eased

        private float _workoutTimer;
        private float _squatProgress = -1f; // <0 = not squatting, else 0..1 through the rep

        private bool _tired;
        private bool _missingConfigLogged;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Subscribe<EnergyChangedEvent>(HandleEnergyChanged);
            ResetGlanceCooldown();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Unsubscribe<EnergyChangedEvent>(HandleEnergyChanged);

            // Leave the character exactly as the scene authored it, so being disabled mid-motion
            // cannot bake a squashed scale or a tilt into the object.
            transform.localScale = _baseScale;
            transform.localPosition = _basePosition;
            transform.localRotation = Quaternion.identity;
        }

        private void HandleRepPerformed(RepPerformedEvent e)
        {
            _punch = 1f;
            _timeSinceRep = 0f;
            _squatProgress = -1f; // the player is training — drop the autonomous workout
        }

        // "Tired" = not enough energy left for another rep, i.e. exactly when the game stops
        // accepting training input (§5). Breathing slows and deepens to sell it.
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

            bool training = _timeSinceRep < _gameConfig.IdleTrainingWindowSeconds;

            float breath = UpdateBreath(dt, training);
            float glance = UpdateGlance(dt, training);
            float squat = UpdateWorkout(dt, training);

            if (_punch > 0f)
            {
                _punch = Mathf.Max(0f, _punch - dt / Mathf.Max(0.0001f, _gameConfig.RepPunchDuration));
            }

            Apply(breath, glance, squat);
        }

        private float UpdateBreath(float dt, bool training)
        {
            float rate = _tired
                ? _gameConfig.IdleTiredRateMultiplier
                : training ? _gameConfig.IdleTrainingRateMultiplier : 1f;

            _breathPhase += dt * _gameConfig.IdleBreathCyclesPerSecond * rate * Mathf.PI * 2f;

            if (_breathPhase > Mathf.PI * 2f)
            {
                _breathPhase -= Mathf.PI * 2f; // keep float precision stable over a long session
            }

            return Mathf.Sin(_breathPhase);
        }

        // Every few seconds he leans and looks to one side, then returns. Alternating sides with a
        // randomised gap keeps it from reading as a metronome.
        private float UpdateGlance(float dt, bool training)
        {
            if (training)
            {
                _glanceAmount = Mathf.MoveTowards(_glanceAmount, 0f, dt * 3f);
                return _glanceAmount;
            }

            _glanceTimer += dt;

            float hold = _gameConfig.GlanceHoldSeconds;
            float target = 0f;

            if (_glanceTimer >= _glanceCooldown && _glanceTimer < _glanceCooldown + hold)
            {
                target = _glanceDirection;
            }
            else if (_glanceTimer >= _glanceCooldown + hold)
            {
                _glanceDirection = -_glanceDirection; // next glance goes the other way
                ResetGlanceCooldown();
            }

            float speed = hold > 0f ? 1f / Mathf.Max(0.05f, hold * 0.35f) : 4f;
            _glanceAmount = Mathf.MoveTowards(_glanceAmount, target, dt * speed);
            return _glanceAmount;
        }

        private void ResetGlanceCooldown()
        {
            _glanceTimer = 0f;
            _glanceCooldown = _gameConfig != null
                ? Random.Range(_gameConfig.GlanceIntervalMinSeconds, _gameConfig.GlanceIntervalMaxSeconds)
                : 4f;
        }

        // While the player is idle the gymbro keeps working out on his own: a squat rep. The whole
        // body drops and springs back, which every layer shares — so it stays correct with any
        // wardrobe combination and needs no art.
        private float UpdateWorkout(float dt, bool training)
        {
            if (training || _tired)
            {
                _squatProgress = -1f;
                _workoutTimer = 0f;
                return 0f;
            }

            if (_squatProgress < 0f)
            {
                _workoutTimer += dt;

                if (_workoutTimer >= _gameConfig.WorkoutRepIntervalSeconds)
                {
                    _workoutTimer = 0f;
                    _squatProgress = 0f;
                }

                return 0f;
            }

            _squatProgress += dt / Mathf.Max(0.05f, _gameConfig.WorkoutRepDurationSeconds);

            if (_squatProgress >= 1f)
            {
                _squatProgress = -1f;
                return 0f;
            }

            // Down fast, hold briefly at the bottom, drive back up — a rep, not a sine wave.
            return Mathf.Sin(_squatProgress * Mathf.PI);
        }

        private void Apply(float breath, float glance, float squat)
        {
            float amplitude = _gameConfig.IdleBreathAmplitude * (_tired ? _gameConfig.IdleTiredAmplitudeMultiplier : 1f);
            float punchCurve = _punch * _punch; // ease-out, snappier than linear

            // Squash-and-stretch: widening as it shortens keeps the silhouette's volume readable
            // instead of looking like a plain scale-up.
            float stretch = 1f
                + amplitude * breath
                + (_gameConfig.RepPunchScale - 1f) * punchCurve
                - _gameConfig.WorkoutSquatSquash * squat;

            float squash = 1f - (stretch - 1f) * 0.5f;

            transform.localScale = new Vector3(_baseScale.x * squash, _baseScale.y * stretch, _baseScale.z);

            // Pivot is bottom-center, so vertical scaling already keeps the feet planted; the bob
            // and the squat dip are extra vertical travel on top of that.
            float y = _gameConfig.IdleBobAmplitude * breath - _gameConfig.WorkoutSquatDepth * squat;
            float x = _gameConfig.GlanceLeanOffset * glance;

            transform.localPosition = _basePosition + new Vector3(x, y, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, -_gameConfig.GlanceLeanAngle * glance);
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
