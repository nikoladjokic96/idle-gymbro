using IdleGymBro.Data;

namespace IdleGymBro.Core
{
    public interface ISaveable
    {
        void CaptureState(SaveData data);
        void RestoreState(SaveData data);
    }
}
