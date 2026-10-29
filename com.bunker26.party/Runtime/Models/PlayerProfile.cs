using System;

namespace Bunker26.Party
{
    /// <summary>Persistent identity for a player. The ID must not depend on the display name.</summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public string id;
        public string displayName;
        public string iconId;
        public PlayerStatistics statistics;
        public string controlOverridesJson;
        public int schemaVersion;

        public PlayerProfile()
        {
            id = Guid.NewGuid().ToString("N");
            displayName = "Player";
            iconId = "default";
            statistics = new PlayerStatistics();
            controlOverridesJson = string.Empty;
            schemaVersion = 1;
        }

        public PlayerProfile(string displayName, string iconId = "default") : this()
        {
            this.displayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            this.iconId = string.IsNullOrWhiteSpace(iconId) ? "default" : iconId.Trim();
        }

        public void EnsureValidData()
        {
            if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(displayName)) displayName = "Player";
            if (string.IsNullOrWhiteSpace(iconId)) iconId = "default";
            if (statistics == null) statistics = new PlayerStatistics();
            if (schemaVersion <= 0) schemaVersion = 1;
        }
    }
}
