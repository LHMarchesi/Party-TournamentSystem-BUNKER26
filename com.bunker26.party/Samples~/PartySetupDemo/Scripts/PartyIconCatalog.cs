using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bunker26.Party.Samples
{
    [Serializable]
    public sealed class PartyProfileIconEntry
    {
        public string id;
        public Sprite sprite;
    }

    /// <summary>Inspector-assigned sprites used by the sample views. Replace these with project artwork.</summary>
    [CreateAssetMenu(menuName = "Bunker 26/Party/Sample Icon Catalog", fileName = "PartyIconCatalog")]
    public sealed class PartyIconCatalog : ScriptableObject
    {
        [Header("System Icons")]
        public Sprite unknownProfile;
        public Sprite keyboard;
        public Sprite gamepad;
        public Sprite leader;
        public Sprite connected;
        public Sprite disconnected;
        public Sprite add;
        public Sprite edit;
        public Sprite remove;
        public Sprite close;
        public Sprite profiles;
        public Sprite statistics;
        public Sprite arrowLeft;
        public Sprite arrowRight;
        public Sprite fallbackPanel;

        [Header("Profile Avatars")]
        public List<PartyProfileIconEntry> profileIcons = new List<PartyProfileIconEntry>();

        public Sprite GetProfileIcon(string iconId)
        {
            if (profileIcons != null)
            {
                for (int i = 0; i < profileIcons.Count; i++)
                {
                    PartyProfileIconEntry entry = profileIcons[i];
                    if (entry != null && entry.id == iconId && entry.sprite != null) return entry.sprite;
                }
            }
            return unknownProfile;
        }

        public string GetProfileIconId(int index)
        {
            if (profileIcons == null || profileIcons.Count == 0) return "default";
            index = Mathf.Clamp(index, 0, profileIcons.Count - 1);
            PartyProfileIconEntry entry = profileIcons[index];
            return entry == null || string.IsNullOrWhiteSpace(entry.id) ? "default" : entry.id;
        }

        public int FindProfileIconIndex(string iconId)
        {
            if (profileIcons == null) return 0;
            for (int i = 0; i < profileIcons.Count; i++)
                if (profileIcons[i] != null && profileIcons[i].id == iconId) return i;
            return 0;
        }

        public int ProfileIconCount { get { return profileIcons == null ? 0 : profileIcons.Count; } }

        public Sprite GetInputIcon(InputAssignment assignment)
        {
            if (assignment == null || !assignment.IsAssigned) return disconnected != null ? disconnected : unknownProfile;
            if (assignment.deviceKind == InputDeviceKind.Keyboard) return keyboard;
            if (assignment.deviceKind == InputDeviceKind.Gamepad) return gamepad;
            return unknownProfile;
        }
    }
}
