namespace IdleGymBro.Data
{
    public class SaveData
    {
        public int Version = 1;
        public double TotalGains;
        public float CurrentEnergy;
        public long LastSaveTimeTicks;
        public System.Collections.Generic.Dictionary<string, int> UpgradeLevels = new System.Collections.Generic.Dictionary<string, int>();
    }
}
