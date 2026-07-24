namespace IdleGymBro.Data
{
    public class SaveData
    {
        public int Version = 1;
        public double TotalGains;
        public double TotalEarned;
        public float CurrentEnergy;
        public long LastSaveTimeTicks;
        public int CurrentLocationIndex;
        public System.Collections.Generic.Dictionary<string, int> UpgradeLevels = new System.Collections.Generic.Dictionary<string, int>();
    }
}
