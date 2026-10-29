namespace Bunker26.Party.Persistence
{
    /// <summary>Stores the preferred profile for a stable local-player slot between sessions.</summary>
    public interface IPlayerPreferencesRepository
    {
        string GetPreferredProfileId(int localSlotIndex);
        void SetPreferredProfileId(int localSlotIndex, string profileId);
        void ClearPreferredProfileId(int localSlotIndex);
    }
}
