using IdleGymBro.Core;

namespace IdleGymBro.Economy
{
    // How many levels one press of an upgrade button buys. Lives outside the buttons because every
    // upgrade row has to agree on it — the toggle sits once at the top of the modal, and each row
    // re-prices itself from the same number.
    //
    // Deliberately NOT saved: it is a view preference for the open panel, not progression, and a
    // player returning after a week should not find x10 silently armed against a smaller balance.
    public static class BuyMultiplier
    {
        public const int Single = 1;
        public const int Bulk = 10;

        private static int _current = Single;

        public static int Current => _current;

        public static void Toggle()
        {
            Set(_current == Single ? Bulk : Single);
        }

        public static void Set(int value)
        {
            int clamped = value == Bulk ? Bulk : Single;

            if (clamped == _current)
            {
                return;
            }

            _current = clamped;
            EventBus.Publish(new BuyMultiplierChangedEvent(_current));
        }

        // Reset on a fresh run so a new game never starts armed at x10 from a previous session.
        public static void Reset()
        {
            _current = Single;
        }
    }

    public readonly struct BuyMultiplierChangedEvent : IGameEvent
    {
        public int Multiplier { get; }

        public BuyMultiplierChangedEvent(int multiplier)
        {
            Multiplier = multiplier;
        }
    }
}
