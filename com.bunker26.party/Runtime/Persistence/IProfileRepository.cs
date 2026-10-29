using System.Collections.Generic;

namespace Bunker26.Party.Persistence
{
    public interface IProfileRepository
    {
        IReadOnlyList<PlayerProfile> LoadAll();
        PlayerProfile Load(string profileId);
        void Save(PlayerProfile profile);
        bool Delete(string profileId);
    }
}
