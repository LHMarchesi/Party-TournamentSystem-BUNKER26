using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bunker26.Party.Persistence
{
    /// <summary>JSON-backed, versioned preferences for local player slots.</summary>
    public sealed class JsonPlayerPreferencesRepository : IPlayerPreferencesRepository
    {
        [Serializable]
        private sealed class PreferenceCollection
        {
            public int schemaVersion = 1;
            public List<SlotPreference> slots = new List<SlotPreference>();
        }

        [Serializable]
        private sealed class SlotPreference
        {
            public int slotIndex;
            public string preferredProfileId;
        }

        private readonly string _filePath;
        private readonly PreferenceCollection _data = new PreferenceCollection();
        private bool _loaded;
        private bool _loadFailed;

        public JsonPlayerPreferencesRepository(string filePath = null)
        {
            _filePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(Application.persistentDataPath, "Bunker26", "party_player_preferences.json")
                : filePath;
        }

        public string GetPreferredProfileId(int localSlotIndex)
        {
            EnsureLoaded();
            SlotPreference entry = _data.slots.Find(item => item.slotIndex == localSlotIndex);
            return entry == null ? string.Empty : entry.preferredProfileId ?? string.Empty;
        }

        public void SetPreferredProfileId(int localSlotIndex, string profileId)
        {
            if (localSlotIndex < 1) throw new ArgumentOutOfRangeException(nameof(localSlotIndex));
            EnsureLoaded();
            SlotPreference entry = _data.slots.Find(item => item.slotIndex == localSlotIndex);
            if (entry == null)
            {
                entry = new SlotPreference { slotIndex = localSlotIndex };
                _data.slots.Add(entry);
            }
            entry.preferredProfileId = profileId ?? string.Empty;
            Flush();
        }

        public void ClearPreferredProfileId(int localSlotIndex)
        {
            SetPreferredProfileId(localSlotIndex, string.Empty);
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (!File.Exists(_filePath)) return;

            try
            {
                PreferenceCollection loaded = JsonUtility.FromJson<PreferenceCollection>(File.ReadAllText(_filePath));
                if (loaded != null && loaded.slots != null)
                {
                    _data.schemaVersion = loaded.schemaVersion <= 0 ? 1 : loaded.schemaVersion;
                    _data.slots = loaded.slots;
                }
            }
            catch (Exception exception)
            {
                _loadFailed = true;
                Debug.LogError("[Bunker26 Party] Could not read local player preferences; writes are blocked to protect the existing file. " + exception.Message);
            }
        }

        private void Flush()
        {
            if (_loadFailed)
                throw new InvalidOperationException("Local player preferences could not be loaded. Writes are blocked to avoid overwriting the existing file.");

            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(_data, true));
            if (!File.Exists(_filePath))
            {
                File.Move(temporaryPath, _filePath);
                return;
            }

            string backupPath = _filePath + ".bak";
            try
            {
                File.Replace(temporaryPath, _filePath, backupPath, true);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithBackup(temporaryPath, backupPath);
            }
            catch (IOException)
            {
                ReplaceWithBackup(temporaryPath, backupPath);
            }
        }

        private void ReplaceWithBackup(string temporaryPath, string backupPath)
        {
            if (File.Exists(_filePath)) File.Copy(_filePath, backupPath, true);
            try
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
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
