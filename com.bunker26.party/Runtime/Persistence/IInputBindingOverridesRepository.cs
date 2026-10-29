namespace Bunker26.Party.Persistence
{
    /// <summary>Persists user binding overrides independently by profile, game and control scheme.</summary>
    public interface IInputBindingOverridesRepository
    {
        string Load(string profileId, string gameId, string controlSchemeName);
        void Save(string profileId, string gameId, string controlSchemeName, string overridesJson);
        void Delete(string profileId, string gameId, string controlSchemeName);
    }
}
