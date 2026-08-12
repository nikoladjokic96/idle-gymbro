namespace IdleGymBro.Data
{
    public enum StatType
    {
        GainsPerRep,
        PassiveGainsPerSecond,

        // Added for the Macros tab: carbs raise the energy ceiling rather than income, so a bigger
        // pool is spent per training burst instead of every rep paying more.
        MaxEnergy
    }
}
