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
        private float _workoutBlend;      // 0 = arms down, 1 = fully raised and held
        private float _holdPhase;         // drives the tremor at the top of the hold
        private float _cycleVariation = 1f;
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

            if (renderer == null)
            {
                return;
            }

            Sprite[] workout = _characterBuilder != null ? _characterBuilder.CurrentWorkoutFrames : null;
            Sprite[] idle = _characterBuilder != null ? _characterBuilder.CurrentIdleFrames : null;

            bool training = _timeSinceRep < _gameConfig.IdleTrainingWindowSeconds;
            bool canWork = workout != null && workout.Length >= 2;

            // Holding the screen RAISES the arms and keeps them there; it does not rep up and down.
            // A repeating curl loop read as a machine, so the hold is the pose and the only motion
            // on top of it is a small tremor, the way a real held contraction shakes.
            float target = training && canWork ? 1f : 0f;
            float raiseSpeed = 1f / Mathf.Max(0.05f, _gameConfig.WorkoutRaiseSeconds);
            _workoutBlend = Mathf.MoveTowards(_workoutBlend, target, dt * raiseSpeed);

            if (_workoutBlend > 0f && canWork)
            {
                PlayHeldWorkout(workout, renderer, blend, dt);
                return;
            }

            if (idle == null || idle.Length < 2)
            {
                return; // tier without an authored clip simply holds its static pose
            }

            PlayIdleLoop(idle, renderer, blend, dt);
        }

        // Position along the workout frames is the raise amount itself: 0 = arms down (frame 0),
        // 1 = fully raised (last frame).
        //
        // Frames SNAP here — deliberately. The arms travel a long way between poses, so
        // cross-fading them draws the lower frame's arms at full opacity with the next frame's
        // arms appearing on top: the character visibly grows a second pair of arms. Blending only
        // works when consecutive frames barely differ, which is true of the breathing clip and
        // false of this one. Snapping through 4 frames in WorkoutRaiseSeconds (~10 fps) is how 2D
        // animation actually reads.
        private void PlayHeldWorkout(Sprite[] frames, SpriteRenderer renderer, SpriteRenderer blend, float dt)
        {
            int top = frames.Length - 1;
            int index = Mathf.Clamp(Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, _workoutBlend) * top), 0, top);

            // Once fully raised he holds the pose, easing back one frame for a short beat each
            // cycle — a held contraction that pulses instead of freezing. Also a snap, for the
            // same reason as above.
            if (_workoutBlend >= 1f && top >= 1)
            {
                _holdPhase += dt * _gameConfig.WorkoutHoldPulseSpeed;
                _holdPhase -= Mathf.Floor(_holdPhase);

                index = _holdPhase > 1f - _gameConfig.WorkoutHoldPulseDuty ? top - 1 : top;
            }

            renderer.sprite = frames[index];

            if (blend != null)
            {
                SetAlpha(blend, 0f); // nothing may linger on top of a snapped frame
            }
        }

        private void PlayIdleLoop(Sprite[] frames, SpriteRenderer renderer, SpriteRenderer blend, float dt)
        {
            float rate = _gameConfig.IdleBreathCyclesPerSecond * _cycleVariation;

            if (_tired)
            {
                rate *= _gameConfig.IdleTiredRateMultiplier;
            }

            _phase += dt * rate;

            // Re-roll the tempo once per breath so the loop never settles into a metronome.
            if (_phase >= 1f)
            {
                _phase -= Mathf.Floor(_phase);
                _cycleVariation = Random.Range(1f - _gameConfig.IdleCycleVariation, 1f + _gameConfig.IdleCycleVariation);
            }

            // Ping-pong: N frames produce 2N-2 steps (3 frames -> 0,1,2,1).
            int steps = frames.Length * 2 - 2;
            float position = _phase * steps;
            int step = Mathf.Clamp(Mathf.FloorToInt(position), 0, steps - 1);
            float t = position - step;

            int a = PingPongIndex(step, frames.Length, steps);
            int b = PingPongIndex(step + 1, frames.Length, steps);
            ApplyPair(frames[a], frames[b], t, renderer, blend);
        }

        // Cross-fade: the lower frame stays fully opaque and the next one fades in over it.
        // Fading BOTH would make the body translucent for half of every step.
        //
        // ONLY safe for the breathing clip, where consecutive frames differ by a few thousand
        // pixels of torso outline. Applied to a clip whose limbs travel (the workout curl) it
        // renders the old arms and the new arms at once — see PlayHeldWorkout.
        private static void ApplyPair(Sprite a, Sprite b, float t, SpriteRenderer renderer, SpriteRenderer blend)
        {
            renderer.sprite = a;

            if (blend == null)
            {
                return;
            }

            blend.sprite = b;
            SetAlpha(blend, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
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
