using UnityEngine;
using IdleGymBro.Core;
using IdleGymBro.Data;
using IdleGymBro.Gameplay;

namespace IdleGymBro.Character
{
    // Plays the character's clips as real sprite frames:
    //   * idle    — breathing, when the player is not holding the screen
    //   * workout — an alternating dumbbell curl, looping while the player holds it
    //
    // An earlier version faked breathing by scaling the root transform; it read as the character
    // inflating and deflating, because a uniform scale moves the head and legs too. It also punched
    // the scale on every rep, which at 4 reps/second just twitched. Both are gone: what the player
    // sees now is drawn art, not a transform trick.
    //
    // FRAMES SNAP. Nothing cross-fades. An earlier version faded the next frame in over the current
    // one, which suited the painted 848x1264 art, where consecutive frames differed by a thin band
    // of torso outline. The pixel-art clips differ across the whole body, so the fade drew two
    // bodies at once — the "ghosting" this replaced. Pixel art snaps; that is how 2D animation
    // reads, and it is why the blend renderer is gone entirely.
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

        private float _phase;         // idle breath cycle, 0..1
        private float _workoutPhase;  // curl cycle, 0..1
        private float _timeSinceRep = 999f;
        private float _blinkTimer;
        private float _nextBlinkAt = 3f;
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

            AdvanceBody(dt);
            UpdateBlink(dt);
        }

        private void AdvanceBody(float dt)
        {
            SpriteRenderer renderer = _characterBuilder != null ? _characterBuilder.BodyRenderer : null;

            if (renderer == null)
            {
                return;
            }

            Sprite[] workout = _characterBuilder.CurrentWorkoutFrames;
            Sprite[] idle = _characterBuilder.CurrentIdleFrames;

            bool training = _timeSinceRep < _gameConfig.IdleTrainingWindowSeconds;

            // The curl LOOPS while training rather than easing into a held pose. The previous
            // raise-and-hold read as lag: the arms took WorkoutRaiseSeconds to arrive and the same
            // again to drop, so the character was always catching up to the input instead of
            // working. A loop starts on the first rep and is obviously "doing reps".
            if (training && workout != null && workout.Length >= 2)
            {
                _workoutPhase += dt * _gameConfig.WorkoutCyclesPerSecond;
                _workoutPhase -= Mathf.Floor(_workoutPhase);

                // Skips index 0 — that is the tier's static, dumbbell-less pose, which belongs to
                // the transition into the clip and would drop the weights for one frame per loop.
                int span = workout.Length - 1;
                int index = 1 + Mathf.Clamp(Mathf.FloorToInt(_workoutPhase * span), 0, span - 1);
                Show(renderer, workout, index, _characterBuilder.CurrentWorkoutHeadOffsets);
                return;
            }

            _workoutPhase = 0f;

            // A tier with no idle clip holds its static pose — explicitly, because the body may
            // have stopped on a raised-dumbbell workout frame and would otherwise stay frozen there.
            if (idle == null || idle.Length < 2)
            {
                if (_characterBuilder.CurrentBodySprite != null)
                {
                    renderer.sprite = _characterBuilder.CurrentBodySprite;
                    ApplyHeadOffset(_characterBuilder.HairRenderer, Vector2.zero);
                    ApplyHeadOffset(_characterBuilder.BeardRenderer, Vector2.zero);
                    ApplyHeadOffset(_characterBuilder.BlinkRenderer, Vector2.zero);
                }

                return;
            }

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

            // Ping-pong: N frames produce 2N-2 steps (3 frames -> 0,1,2,1), so the authored half of
            // the breath covers the whole cycle.
            int steps = idle.Length * 2 - 2;
            int step = Mathf.Clamp(Mathf.FloorToInt(_phase * steps), 0, steps - 1);
            Show(renderer, idle, PingPongIndex(step, idle.Length, steps), _characterBuilder.CurrentIdleHeadOffsets);
        }

        // Sets the body frame and slides the layers that sit on the skull to match it. Without this
        // the hair stays where the static pose put it while the head bobs and turns underneath.
        private void Show(SpriteRenderer renderer, Sprite[] frames, int index, Vector2[] headOffsets)
        {
            if (frames == null || index < 0 || index >= frames.Length)
            {
                return;
            }

            renderer.sprite = frames[index];

            Vector2 offset = headOffsets != null && index < headOffsets.Length ? headOffsets[index] : Vector2.zero;

            ApplyHeadOffset(_characterBuilder.HairRenderer, offset);
            ApplyHeadOffset(_characterBuilder.BeardRenderer, offset);
            ApplyHeadOffset(_characterBuilder.BlinkRenderer, offset);
        }

        private static void ApplyHeadOffset(SpriteRenderer renderer, Vector2 offset)
        {
            if (renderer == null)
            {
                return;
            }

            Transform t = renderer.transform;
            Vector3 p = t.localPosition;

            if (Mathf.Approximately(p.x, offset.x) && Mathf.Approximately(p.y, offset.y))
            {
                return;
            }

            t.localPosition = new Vector3(offset.x, offset.y, p.z);
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
