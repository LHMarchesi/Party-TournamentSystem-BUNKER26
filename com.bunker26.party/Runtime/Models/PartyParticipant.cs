using System;

namespace Bunker26.Party
{
    public enum PartyParticipantType
    {
        LocalHuman = 0,
        AI = 1,
        Online = 2
    }

    /// <summary>A participant currently registered in a Party session.</summary>
    [Serializable]
    public sealed class PartyParticipant
    {
        public string id;
        public string profileId;
        public string displayNameOverride;
        public PartyParticipantType type;
        public InputAssignment inputAssignment;
        public int localSlotIndex;

        // Mutable session state. PartyService is the authority that transfers leadership.
        public bool isLeader;

        public bool IsLeader
        {
            get { return type == PartyParticipantType.LocalHuman && isLeader; }
        }

        public string DisplayName
        {
            get { return !string.IsNullOrWhiteSpace(displayNameOverride) ? displayNameOverride : "Unknown"; }
        }

        public PartyParticipant(PartyParticipantType type = PartyParticipantType.LocalHuman, int localSlotIndex = 0)
        {
            id = Guid.NewGuid().ToString("N");
            profileId = string.Empty;
            displayNameOverride = type == PartyParticipantType.AI ? "CPU" : "Unknown";
            this.type = type;
            this.localSlotIndex = localSlotIndex;
            inputAssignment = InputAssignment.Unassigned();
            isLeader = type == PartyParticipantType.LocalHuman && localSlotIndex == 1;
        }
    }
}
