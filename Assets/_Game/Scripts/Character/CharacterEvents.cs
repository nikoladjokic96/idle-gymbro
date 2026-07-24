using IdleGymBro.Core;

namespace IdleGymBro.Character
{
    public readonly struct MuscleTierChangedEvent : IGameEvent
    {
        public int Tier { get; }
        public string DisplayName { get; }

        public MuscleTierChangedEvent(int tier, string displayName)
        {
            Tier = tier;
            DisplayName = displayName;
        }
    }
}
