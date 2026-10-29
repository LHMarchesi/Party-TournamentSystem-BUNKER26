using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bunker26.Party.Persistence
{
    /// <summary>Local JSON repository suitable for the initial single-player/local-party implementation.</summary>
    public sealed class JsonProfileRepository : IProfileRepository
    {
        [Serializable]
        private sealed class ProfileCollection
        {
            public List<PlayerProfile> profiles = new List<PlayerProfile>();
        }

        private readonly string _filePath;
        private readonly List<PlayerProfile> _profiles;
        private bool _loaded;
        private bool _loadFailed;

        public JsonProfileRepository(string filePath = null)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(Application.persistentDataPath, "Bunker26", "party_profiles.json")
                : filePath;
            _profiles = new List<PlayerProfile>();
        }

        public IReadOnlyList<PlayerProfile> LoadAll()
        {
            EnsureLoaded();
            return _profiles.AsReadOnly();
        }

        public PlayerProfile Load(string profileId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(profileId)) return null;
            return _profiles.Find(p => p != null && p.id == profileId);
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            profile.EnsureValidData();
            EnsureLoaded();

            int index = _profiles.FindIndex(p => p != null && p.id == profile.id);
            if (index >= 0) _profiles[index] = profile;
            else _profiles.Add(profile);
            Flush();
        }

        public bool Delete(string profileId)
        {
            EnsureLoaded();
            int index = _profiles.FindIndex(p => p != null && p.id == profileId);
            if (index < 0) return false;
            _profiles.RemoveAt(index);
            Flush();
            return true;
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _profiles.Clear();
            if (!File.Exists(_filePath)) return;

            try
            {
                string json = File.ReadAllText(_filePath);
                ProfileCollection collection = JsonUtility.FromJson<ProfileCollection>(json);
                if (collection != null && collection.profiles != null)
                {
                    foreach (PlayerProfile profile in collection.profiles)
                    {
                        if (profile == null) continue;
                        profile.EnsureValidData();
                        _profiles.Add(profile);
                    }
                }
            }
            catch (Exception exception)
            {
                _loadFailed = true;
                Debug.LogError("[Bunker26 Party] Could not read profile data. The source file was left untouched and writes are blocked for this repository instance. " + exception.Message);
            }
        }

        private void Flush()
        {
            if (_loadFailed)
                throw new InvalidOperationException("Profile data could not be loaded. Writes are blocked to avoid overwriting the existing file.");

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temporaryPath = _filePath + ".tmp";
            var collection = new ProfileCollection { profiles = new List<PlayerProfile>(_profiles) };
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(collection, true));

            if (File.Exists(_filePath))
            {
                string backupPath = _filePath + ".bak";
                try
                {
                    File.Replace(temporaryPath, _filePath, backupPath, true);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Fallback for platforms without File.Replace support.
                }
                catch (IOException)
                {
                    // Fallback to a replace sequence; the backup preserves the prior file if the move fails.
                }

                string backup = _filePath + ".bak";
                File.Copy(_filePath, backup, true);
                File.Delete(_filePath);
                File.Move(temporaryPath, _filePath);
                if (File.Exists(backup)) File.Delete(backup);
            }
            else
            {
                File.Move(temporaryPath, _filePath);
            }
        }
    }
}
