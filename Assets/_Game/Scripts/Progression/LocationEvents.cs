using IdleGymBro.Core;

namespace IdleGymBro.Progression
{
    public readonly struct LocationProgressChangedEvent : IGameEvent
    {
        public string DisplayName { get; }
        public float Progress01 { get; }
        public bool CanAdvance { get; }

        public LocationProgressChangedEvent(string displayName, float progress01, bool canAdvance)
        {
            DisplayName = displayName;
            Progress01 = progress01;
            CanAdvance = canAdvance;
        }
    }

    public readonly struct LocationChangedEvent : IGameEvent
    {
        public string LocationId { get; }
        public string DisplayName { get; }
        public int Index { get; }

        public LocationChangedEvent(string locationId, string displayName, int index)
        {
            LocationId = locationId;
            DisplayName = displayName;
            Index = index;
        }
    }

    public readonly struct LocationMultiplierChangedEvent : IGameEvent
    {
        public double Multiplier { get; }

        public LocationMultiplierChangedEvent(double multiplier)
        {
            Multiplier = multiplier;
        }
    }
}
