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

        // Total levels across the BODY upgrades needed for this tier.
        //
        // Replaces lifetime gains as the driver. Gains grow exponentially, so tiers arrived in a
        // rush early on and then never again — the physique raced ahead of the training. Body
        // levels are bought one at a time and rise roughly linearly, so the silhouette now grows in
        // step with the work the player is actually doing.
        //
        // Still monotonic, which is what mattered about TotalEarned: levels are never refunded, so
        // the character cannot shrink (prestige resets the run wholesale, tier included).
        [SerializeField]
        private int _bodyLevelThreshold;

        [SerializeField]
        private Sprite _bodySprite;

        [SerializeField]
        private Sprite _headSprite; // may be null for MVP (head shared)

        // Idle breathing clip for this tier, played ping-pong (0,1,2,1,…) so N frames cover a full
        // inhale AND exhale.
        [SerializeField]
        private Sprite[] _idleFrames;

        // Workout clip, played on a loop while the player holds the screen: an alternating dumbbell
        // curl, one arm at a time, head turning to watch the lifting hand.
        [SerializeField]
        private Sprite[] _workoutFrames;

        // Where this frame's head sits relative to the head in BodySprite, IN PIXELS (+y = up).
        //
        // The generated clips do not keep the head pixel-identical — it bobs with the breath and
        // turns to watch the dumbbell — so the hair/beard/blink layers, which are single static
        // sprites, would float off the skull. These offsets let the animator carry those layers
        // along with the head instead of authoring a cosmetic sprite per frame (which the layer
        // system cannot produce: PixelLab has no way to isolate "just the hair" for a given pose).
        //
        // Index-aligned with _idleFrames / _workoutFrames. Baked by Editor/FrameAnchorBaker; an
        // empty or mismatched array simply means "no compensation" and the layers stay put.
        [SerializeField]
        private Vector2[] _idleHeadOffsets;

        [SerializeField]
        private Vector2[] _workoutHeadOffsets;

        // The dumbbells, lifted out of each workout frame so they can be drawn ABOVE the shorts.
        // They are painted into the body art, and the shorts are a layer on top of the body, so the
        // garment was covering the iron. Index-aligned with _workoutFrames; baked by
        // Editor/HeldItemExtractor.
        [SerializeField]
        private Sprite[] _workoutHeldFrames;

        public int Tier => _tier;
        public string DisplayName => _displayName;
        public double TotalEarnedThreshold => _totalEarnedThreshold;
        public int BodyLevelThreshold => _bodyLevelThreshold;
        public Sprite BodySprite => _bodySprite;
        public Sprite HeadSprite => _headSprite;
        public Sprite[] IdleFrames => _idleFrames;
        public Sprite[] WorkoutFrames => _workoutFrames;
        public Vector2[] IdleHeadOffsets => _idleHeadOffsets;
        public Vector2[] WorkoutHeadOffsets => _workoutHeadOffsets;
        public Sprite[] WorkoutHeldFrames => _workoutHeldFrames;
    }
}
