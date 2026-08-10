using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Gameplay;

namespace IdleGymBro.Character
{
    // Plays the character's clips as real sprite frames:
    //   * idle    — breathing, when the player is not holding the screen
    //   * workout — bicep curls, while the player holds it
    //
    // An earlier version faked breathing by scaling the root transform; it read as the character
    // inflating and deflating, because a uniform scale moves the head and legs too. It also punched
    // the scale on every rep, which at 4 reps/second just twitched. Both are gone: what the player
    // sees now is drawn art, not a transform trick.
    //
    // Playback is PING-PONG (0,1,2,1,…): the authored frames cover half the motion (the inhale, the
    // way up), and replaying them backwards gives the other half free — half the art per cycle.
    //
    // Clips live on MuscleTierData, so every muscle tier breathes and curls with its own silhouette,
    // and CharacterBuilder swaps both clips when the tier changes.
    [DefaultExecutionOrder(50)]
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField]
        private GameConfig _gameConfig;

        [SerializeField]
        private CharacterBuilder _characterBuilder;

        private float _phase;
        private float _timeSinceRep = 999f;
        private float _blinkTimer;
        private float _nextBlinkAt = 3f;
        private bool _wasWorking;
        private bool _tired;
        private bool _missingConfigLogged;

        private void OnEnable()
        {
            EventBus.Subscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Subscribe<EnergyChangedEvent>(HandleEnergyChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RepPerformedEvent>(HandleRepPerformed);
            EventBus.Unsubscribe<EnergyChangedEvent>(HandleEnergyChanged);
        }

        private void Start()
        {
            if (_characterBuilder == null)
            {
                _characterBuilder = GetComponent<CharacterBuilder>();
            }

            ScheduleNextBlink();
        }

        // Each rep only refreshes the "is training" window; the reps themselves are far too fast
        // (RepIntervalSeconds) to drive one curl each, so the workout clip runs at its own tempo.
        private void HandleRepPerformed(RepPerformedEvent e)
        {
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
            UpdateBlink(dt);
        }

        private void AdvanceIdleClip(float dt)
        {
            SpriteRenderer renderer = _characterBuilder != null ? _characterBuilder.BodyRenderer : null;
            SpriteRenderer blend = _characterBuilder != null ? _characterBuilder.BodyBlendRenderer : null;

            // Holding the screen puts him through curls; letting go drops back to breathing.
            bool training = _timeSinceRep < _gameConfig.IdleTrainingWindowSeconds;

            Sprite[] workout = _characterBuilder != null ? _characterBuilder.CurrentWorkoutFrames : null;
            Sprite[] idle = _characterBuilder != null ? _characterBuilder.CurrentIdleFrames : null;

            bool working = training && workout != null && workout.Length >= 2;
            Sprite[] frames = working ? workout : idle;

            if (frames == null || frames.Length < 2 || renderer == null)
            {
                return; // tier without an authored clip simply holds its static pose
            }

            // Restart the cycle when switching clips so a curl always begins from arms-down
            // instead of snapping in halfway through the motion.
            if (working != _wasWorking)
            {
                _wasWorking = working;
                _phase = 0f;
            }

            float rate = working
                ? _gameConfig.WorkoutCyclesPerSecond
                : _tired
                    ? _gameConfig.IdleBreathCyclesPerSecond * _gameConfig.IdleTiredRateMultiplier
                    : _gameConfig.IdleBreathCyclesPerSecond;

            _phase += dt * rate;
            _phase -= Mathf.Floor(_phase); // keep in 0..1 without losing precision over a session

            // Ping-pong: N frames produce 2N-2 steps (3 frames -> 0,1,2,1).
            int steps = frames.Length * 2 - 2;
            float position = _phase * steps;
            int step = Mathf.Clamp(Mathf.FloorToInt(position), 0, steps - 1);
            float blendAmount = position - step;

            renderer.sprite = frames[PingPongIndex(step, frames.Length, steps)];

            if (blend == null)
            {
                return;
            }

            // Cross-fade the next frame in on top. Holding the current frame fully opaque
            // underneath keeps the silhouette solid — fading BOTH would make the body go
            // translucent for half of every step.
            blend.sprite = frames[PingPongIndex(step + 1, frames.Length, steps)];
            SetAlpha(blend, Mathf.SmoothStep(0f, 1f, blendAmount));
        }

        private static int PingPongIndex(int step, int frameCount, int steps)
        {
            int s = step % steps;
            return s < frameCount ? s : steps - s;
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }

        // Occasional blink: the closed-eyelids patch is shown for a fraction of a second, with a
        // randomised gap so it never reads as a metronome.
        private void UpdateBlink(float dt)
        {
            SpriteRenderer blink = _characterBuilder != null ? _characterBuilder.BlinkRenderer : null;
            Sprite sprite = _characterBuilder != null ? _characterBuilder.BlinkSprite : null;

            if (blink == null || sprite == null)
            {
                return;
            }

            _blinkTimer += dt;

            if (_blinkTimer >= _nextBlinkAt + _gameConfig.BlinkDurationSeconds)
            {
                _blinkTimer = 0f;
                ScheduleNextBlink();
            }

            bool closed = _blinkTimer >= _nextBlinkAt;

            if (closed && blink.sprite != sprite)
            {
                blink.sprite = sprite;
            }

            SetAlpha(blink, closed ? 1f : 0f);
        }

        private void ScheduleNextBlink()
        {
            _nextBlinkAt = _gameConfig != null
                ? Random.Range(_gameConfig.BlinkIntervalMinSeconds, _gameConfig.BlinkIntervalMaxSeconds)
                : 4f;
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
