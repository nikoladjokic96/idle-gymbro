namespace IdleGymBro.Core
{
    // Published by SaveSystem after every load attempt (successful or not) so other
    // systems (e.g. offline earnings) can react once load state is known.
    public readonly struct GameLoadedEvent : IGameEvent
    {
        public bool HadSave { get; }
        public long LastSaveTimeTicks { get; }

        public GameLoadedEvent(bool hadSave, long lastSaveTimeTicks)
        {
            HadSave = hadSave;
            LastSaveTimeTicks = lastSaveTimeTicks;
        }
    }
}
