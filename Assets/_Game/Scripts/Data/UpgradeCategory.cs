namespace IdleGymBro.Data
{
    // Which tab of the Upgrades modal an upgrade belongs to. Also drives location completion:
    // a location is cleared by reaching its Body target, maxing its Equipment and reaching its
    // Macro target (see LocationManager).
    public enum UpgradeCategory
    {
        // Muscle groups. Always available, no location tie — the spine of the progression.
        Body,

        // Gear tied to ONE location (towel and yoga mat at home, chalk and straps in the hardcore
        // gym). Costs far more than body work and has a finite MaxLevel, so "buy it all" is a
        // reachable goal rather than an infinite sink.
        Equipment,

        // Diet. Protein feeds gains per rep, carbs raise max energy, fats feed passive income.
        Macros
    }
}
