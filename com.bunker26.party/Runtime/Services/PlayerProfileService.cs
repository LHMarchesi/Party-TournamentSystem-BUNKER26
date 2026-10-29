using System;
using System.Collections.Generic;
using Bunker26.Party.Persistence;

namespace Bunker26.Party
{
    public sealed class PlayerProfileService
    {
        private readonly IProfileRepository _repository;

        public PlayerProfileService(IProfileRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IReadOnlyList<PlayerProfile> GetAll() { return _repository.LoadAll(); }
        public PlayerProfile Get(string profileId) { return _repository.Load(profileId); }

        public PlayerProfile Create(string displayName, string iconId = "default")
        {
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A profile display name is required.", nameof(displayName));
            var profile = new PlayerProfile(displayName, iconId);
            _repository.Save(profile);
            return profile;
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.EnsureValidData();
            _repository.Save(profile);
        }

        public bool Delete(string profileId) { return _repository.Delete(profileId); }
    }
}
