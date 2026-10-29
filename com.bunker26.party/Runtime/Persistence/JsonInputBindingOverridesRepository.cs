using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bunker26.Party.Persistence
{
    /// <summary>JSON storage for overrides, isolated by profile ID, game ID and control scheme.</summary>
    public sealed class JsonInputBindingOverridesRepository : IInputBindingOverridesRepository
    {
        [Serializable]
        private sealed class DataFile
        {
            public int schemaVersion = 1;
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            public string profileId;
            public string gameId;
            public string controlSchemeName;
            public string overridesJson;
        }

        private readonly string _filePath;
        private readonly DataFile _data = new DataFile();
        private bool _loaded;
        private bool _loadFailed;

        public JsonInputBindingOverridesRepository(string filePath = null)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(Application.persistentDataPath, "Bunker26", "party_input_binding_overrides.json")
                : filePath;
        }

        public string Load(string profileId, string gameId, string controlSchemeName)
        {
            EnsureLoaded();
            Entry entry = Find(profileId, gameId, controlSchemeName);
            return entry == null ? string.Empty : entry.overridesJson ?? string.Empty;
        }

        public void Save(string profileId, string gameId, string controlSchemeName, string overridesJson)
        {
            ValidateKey(profileId, gameId, controlSchemeName);
            EnsureLoaded();
            EnsureWritable();

            Entry entry = Find(profileId, gameId, controlSchemeName);
            if (entry == null)
            {
                entry = new Entry { profileId = profileId, gameId = gameId, controlSchemeName = controlSchemeName };
                _data.entries.Add(entry);
            }
            entry.overridesJson = overridesJson ?? string.Empty;
            Flush();
        }

        public void Delete(string profileId, string gameId, string controlSchemeName)
        {
            ValidateKey(profileId, gameId, controlSchemeName);
            EnsureLoaded();
            EnsureWritable();
            _data.entries.RemoveAll(entry => Matches(entry, profileId, gameId, controlSchemeName));
            Flush();
        }

        private Entry Find(string profileId, string gameId, string controlSchemeName)
        {
            return _data.entries.Find(entry => Matches(entry, profileId, gameId, controlSchemeName));
        }

        private static bool Matches(Entry entry, string profileId, string gameId, string controlSchemeName)
        {
            return entry != null &&
                   string.Equals(entry.profileId, profileId, StringComparison.Ordinal) &&
                   string.Equals(entry.gameId, gameId, StringComparison.Ordinal) &&
                   string.Equals(entry.controlSchemeName, controlSchemeName, StringComparison.Ordinal);
        }

        private static void ValidateKey(string profileId, string gameId, string controlSchemeName)
        {
            if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("A profile ID is required.", nameof(profileId));
            if (string.IsNullOrWhiteSpace(gameId)) throw new ArgumentException("A game ID is required.", nameof(gameId));
            if (string.IsNullOrWhiteSpace(controlSchemeName)) throw new ArgumentException("A control scheme is required.", nameof(controlSchemeName));
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (!File.Exists(_filePath)) return;

            try
            {
                DataFile loaded = JsonUtility.FromJson<DataFile>(File.ReadAllText(_filePath));
                if (loaded == null || loaded.entries == null) throw new InvalidDataException("The binding overrides file is empty or invalid.");
                _data.schemaVersion = loaded.schemaVersion <= 0 ? 1 : loaded.schemaVersion;
                _data.entries = loaded.entries;
            }
            catch (Exception exception)
            {
                _loadFailed = true;
                Debug.LogError("[Bunker26 Party] Could not read input binding overrides. Writes are blocked to protect the existing file. " + exception.Message);
            }
        }

        private void EnsureWritable()
        {
            if (_loadFailed)
                throw new InvalidOperationException("Input binding overrides could not be loaded. Writes are blocked to protect existing data.");
        }

        private void Flush()
        {
            EnsureWritable();
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temporaryPath = _filePath + ".tmp";
            string backupPath = _filePath + ".bak";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(_data, true));
            if (!File.Exists(_filePath))
            {
                File.Move(temporaryPath, _filePath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, _filePath, backupPath, true);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            catch (PlatformNotSupportedException) { ReplaceWithBackup(temporaryPath, backupPath); }
            catch (IOException) { ReplaceWithBackup(temporaryPath, backupPath); }
        }

        private void ReplaceWithBackup(string temporaryPath, string backupPath)
        {
            File.Copy(_filePath, backupPath, true);
            try
            {
                File.Delete(_filePath);
                File.Move(temporaryPath, _filePath);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            catch
            {
                if (!File.Exists(_filePath) && File.Exists(backupPath)) File.Copy(backupPath, _filePath, true);
                throw;
            }
        }
    }
}
