using System;
using System.Collections.Generic;
using Bunker26.Party.Persistence;

namespace Bunker26.Party
{
    /// <summary>Maintains one local Party session. Profiles and local-slot preferences are separate persisted concerns.</summary>
    public sealed class PartyService
    {
        private readonly PlayerProfileService _profiles;
        private readonly IInputAssignmentService _inputAssignments;
        private readonly IPlayerPreferencesRepository _preferences;
        private readonly List<PartyParticipant> _participants = new List<PartyParticipant>();
        private readonly IReadOnlyList<PartyParticipant> _readOnlyParticipants;
        private readonly int _maxLocalPlayers;

        public event Action PartyChanged;
        public int MaxLocalPlayers { get { return _maxLocalPlayers; } }
        public IReadOnlyList<PartyParticipant> Participants { get { return _readOnlyParticipants; } }
        public PartyParticipant Leader { get { return _participants.Find(p => p != null && p.IsLeader); } }

        public PartyService(
            PlayerProfileService profiles,
            IInputAssignmentService inputAssignments = null,
            IPlayerPreferencesRepository preferencesRepository = null,
            int maxLocalPlayers = 8)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _inputAssignments = inputAssignments ?? new InputAssignmentService();
            _preferences = preferencesRepository;
            _maxLocalPlayers = Math.Max(1, maxLocalPlayers);
            _readOnlyParticipants = _participants.AsReadOnly();
        }

        /// <summary>
        /// Guarantees that local slot 1 exists and restores its preferred profile when possible.
        /// Existing leadership is preserved across scene-level calls to initialization.
        /// </summary>
        public PartyParticipant InitializeLocalParty()
        {
            PartyParticipant playerOne = FindByLocalSlot(1);
            if (playerOne != null)
            {
                if (Leader == null)
                {
                    playerOne.isLeader = true;
                    RaiseChanged();
                }
                return playerOne;
            }

            playerOne = new PartyParticipant(PartyParticipantType.LocalHuman, 1);
            playerOne.isLeader = Leader == null;
            _participants.Insert(0, playerOne);
            RestorePreferredProfile(playerOne);
            RaiseChanged();
            return playerOne;
        }

        public PartyParticipant AddLocalPlayer()
        {
            PartyParticipant participant;
            string error;
            if (!TryAddLocalPlayer(out participant, out error))
                throw new InvalidOperationException(error);
            return participant;
        }

        public bool TryAddLocalPlayer(out PartyParticipant participant, out string error)
        {
            participant = null;
            error = null;
            int slot = FindNextFreeSlot();
            if (slot < 1)
            {
                error = "The local Party has reached its configured player limit.";
                return false;
            }

            participant = new PartyParticipant(PartyParticipantType.LocalHuman, slot);
            // A new slot becomes leader only if the session does not currently have one.
            participant.isLeader = Leader == null;
            _participants.Add(participant);
            RestorePreferredProfile(participant);
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Compatibility overload. Use the overload with shouldOpenProfileSelection when the caller
        /// needs to drive the join UI after the input has been assigned.
        /// </summary>
        public bool TryJoinWithGamepad(string deviceId, string displayName, out PartyParticipant participant, out string error)
        {
            bool ignoredProfilePrompt;
            return TryJoinWithGamepad(deviceId, displayName, out participant, out ignoredProfilePrompt, out error);
        }

        /// <summary>
        /// Assigns a gamepad after activity from an unowned physical device. Disconnected controller
        /// slots are reused first, then an unassigned local slot, then a new slot. New/unconfigured
        /// participants should be sent to profile selection after the device is assigned.
        /// </summary>
        public bool TryJoinWithGamepad(
            string deviceId,
            string displayName,
            out PartyParticipant participant,
            out bool shouldOpenProfileSelection,
            out string error)
        {
            participant = null;
            shouldOpenProfileSelection = false;
            error = null;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                error = "A valid gamepad device ID is required.";
                return false;
            }

            PartyParticipant existing = FindByGamepadDeviceId(deviceId);
            if (existing != null)
            {
                string normalizedName = string.IsNullOrWhiteSpace(displayName) ? "Gamepad" : displayName;
                bool changed = !existing.inputAssignment.isConnected || existing.inputAssignment.deviceDisplayName != normalizedName;
                existing.inputAssignment.isConnected = true;
                existing.inputAssignment.deviceDisplayName = normalizedName;
                participant = existing;
                if (changed) RaiseChanged();
                return true;
            }

            PartyParticipant target = FindDisconnectedGamepadParticipant();
            bool reusingDisconnectedSlot = target != null;
            if (target == null) target = FindFirstUnassignedLocalPlayer();
            bool targetHadNoInput = target != null &&
                (target.inputAssignment == null || !target.inputAssignment.IsAssigned);
            bool createdParticipant = false;

            if (target == null)
            {
                if (!TryAddLocalPlayer(out target, out error)) return false;
                createdParticipant = true;
                targetHadNoInput = true;
            }

            bool profileWasUnknown = string.IsNullOrWhiteSpace(target.profileId);
            if (!AssignInput(target.id, InputAssignment.Gamepad(deviceId, displayName, true), out error))
            {
                // Roll back a newly-created slot if the device cannot be assigned.
                if (createdParticipant) RemoveParticipant(target.id);
                return false;
            }

            participant = target;
            shouldOpenProfileSelection = createdParticipant || targetHadNoInput ||
                (reusingDisconnectedSlot && profileWasUnknown);
            return true;
        }

        public PartyParticipant AddAI(string displayName = "CPU")
        {
            var participant = new PartyParticipant(PartyParticipantType.AI) { displayNameOverride = displayName };
            _participants.Add(participant);
            RaiseChanged();
            return participant;
        }

        /// <summary>Removes a non-leader participant. The current leader must be transferred first.</summary>
        public bool RemoveParticipant(string participantId)
        {
            int index = _participants.FindIndex(p => p != null && p.id == participantId);
            if (index < 0 || _participants[index].IsLeader) return false;
            _participants.RemoveAt(index);
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Transfers leadership to an existing local human with a usable input assignment.
        /// Leadership is session state; local slot indexes and profile ownership do not change.
        /// </summary>
        public bool TrySetLeader(string participantId, out string error)
        {
            error = null;
            PartyParticipant target = FindParticipant(participantId);
            if (target == null)
            {
                error = "Participant was not found.";
                return false;
            }

            if (target.type != PartyParticipantType.LocalHuman)
            {
                error = "Only a local human participant can become Party leader.";
                return false;
            }

            InputAssignment assignment = target.inputAssignment;
            if (assignment == null || !assignment.IsAssigned ||
                (assignment.deviceKind != InputDeviceKind.Keyboard && assignment.deviceKind != InputDeviceKind.Gamepad))
            {
                error = "Assign a supported keyboard slot or gamepad before making this participant the leader.";
                return false;
            }

            if (assignment.deviceKind == InputDeviceKind.Gamepad && !assignment.isConnected)
            {
                error = "The assigned gamepad is disconnected. Reconnect it before transferring leadership.";
                return false;
            }

            if (target.IsLeader)
                return true;

            for (int i = 0; i < _participants.Count; i++)
            {
                PartyParticipant participant = _participants[i];
                if (participant == null) continue;
                participant.isLeader = participant.type == PartyParticipantType.LocalHuman &&
                                        participant.id == target.id;
            }

            RaiseChanged();
            return true;
        }

        public bool AssignProfile(string participantId, string profileId, out string error)
        {
            error = null;
            PartyParticipant participant = FindParticipant(participantId);
            if (participant == null) { error = "Participant was not found."; return false; }
            PlayerProfile profile = _profiles.Get(profileId);
            if (profile == null) { error = "Profile was not found."; return false; }

            PartyParticipant duplicate = _participants.Find(p => p.id != participantId && p.type == PartyParticipantType.LocalHuman && p.profileId == profileId);
            if (duplicate != null) { error = "That profile is already assigned to another local player."; return false; }

            participant.profileId = profile.id;
            participant.displayNameOverride = profile.displayName;
            if (_preferences != null && participant.localSlotIndex > 0)
                _preferences.SetPreferredProfileId(participant.localSlotIndex, profile.id);
            RaiseChanged();
            return true;
        }

        public bool ClearProfile(string participantId)
        {
            PartyParticipant participant = FindParticipant(participantId);
            if (participant == null) return false;
            participant.profileId = string.Empty;
            participant.displayNameOverride = "Unknown";
            if (_preferences != null && participant.localSlotIndex > 0)
                _preferences.ClearPreferredProfileId(participant.localSlotIndex);
            RaiseChanged();
            return true;
        }

        public bool AssignInput(string participantId, InputAssignment assignment, out string error)
        {
            PartyParticipant participant = FindParticipant(participantId);
            if (!_inputAssignments.TryAssign(participant, _participants, assignment, out error)) return false;
            RaiseChanged();
            return true;
        }

        public bool IsInputAssignmentAvailable(string participantId, InputAssignment assignment, out string error)
        {
            return _inputAssignments.IsAssignmentAvailable(FindParticipant(participantId), _participants, assignment, out error);
        }

        public bool IsKeyboardSlotAvailable(int keyboardSlot, string participantId = null)
        {
            PartyParticipant target = string.IsNullOrEmpty(participantId) ? new PartyParticipant() : FindParticipant(participantId);
            string ignoredError;
            return _inputAssignments.IsAssignmentAvailable(target, _participants, InputAssignment.Keyboard(keyboardSlot), out ignoredError);
        }

        public bool ReleaseInput(string participantId)
        {
            PartyParticipant participant = FindParticipant(participantId);
            if (participant == null) return false;
            _inputAssignments.Release(participant);
            RaiseChanged();
            return true;
        }

        public bool MarkGamepadDisconnected(string deviceId)
        {
            PartyParticipant participant = FindByGamepadDeviceId(deviceId);
            if (participant == null || participant.inputAssignment.isConnected == false) return false;
            participant.inputAssignment.isConnected = false;
            RaiseChanged();
            return true;
        }

        public bool MarkGamepadConnected(string deviceId, string displayName)
        {
            PartyParticipant participant = FindByGamepadDeviceId(deviceId);
            if (participant == null) return false;
            string normalizedName = string.IsNullOrWhiteSpace(displayName) ? participant.inputAssignment.deviceDisplayName : displayName;
            bool changed = !participant.inputAssignment.isConnected || participant.inputAssignment.deviceDisplayName != normalizedName;
            if (!changed) return false;
            participant.inputAssignment.isConnected = true;
            if (!string.IsNullOrWhiteSpace(normalizedName)) participant.inputAssignment.deviceDisplayName = normalizedName;
            RaiseChanged();
            return true;
        }

        public bool IsGamepadAssigned(string deviceId)
        {
            return FindByGamepadDeviceId(deviceId) != null;
        }

        public PartyParticipant GetParticipant(string participantId) { return FindParticipant(participantId); }
        public PartyParticipant GetParticipantByLocalSlot(int slotIndex) { return FindByLocalSlot(slotIndex); }
        public PlayerProfile GetProfile(string profileId) { return _profiles.Get(profileId); }
        public IReadOnlyList<PlayerProfile> GetProfiles() { return _profiles.GetAll(); }

        public bool RefreshProfile(string profileId)
        {
            PlayerProfile profile = _profiles.Get(profileId);
            if (profile == null) return false;
            bool changed = false;
            for (int i = 0; i < _participants.Count; i++)
            {
                if (_participants[i].profileId != profileId) continue;
                _participants[i].displayNameOverride = profile.displayName;
                changed = true;
            }
            if (changed) RaiseChanged();
            return changed;
        }

        private void RestorePreferredProfile(PartyParticipant participant)
        {
            if (_preferences == null || participant == null || participant.localSlotIndex < 1) return;
            string preferredId = _preferences.GetPreferredProfileId(participant.localSlotIndex);
            if (string.IsNullOrWhiteSpace(preferredId)) return;

            PlayerProfile profile = _profiles.Get(preferredId);
            if (profile == null)
            {
                _preferences.ClearPreferredProfileId(participant.localSlotIndex);
                return;
            }

            bool alreadyUsed = _participants.Exists(p => p.id != participant.id && p.type == PartyParticipantType.LocalHuman && p.profileId == profile.id);
            if (alreadyUsed) return;
            participant.profileId = profile.id;
            participant.displayNameOverride = profile.displayName;
        }

        private int FindNextFreeSlot()
        {
            for (int slot = 1; slot <= _maxLocalPlayers; slot++)
            {
                if (FindByLocalSlot(slot) == null) return slot;
            }
            return -1;
        }

        private PartyParticipant FindByLocalSlot(int slotIndex)
        {
            return _participants.Find(p => p.type == PartyParticipantType.LocalHuman && p.localSlotIndex == slotIndex);
        }

        private PartyParticipant FindByGamepadDeviceId(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return null;
            return _participants.Find(p => p.type == PartyParticipantType.LocalHuman && p.inputAssignment != null &&
                p.inputAssignment.deviceKind == InputDeviceKind.Gamepad && p.inputAssignment.deviceId == deviceId);
        }

        private PartyParticipant FindDisconnectedGamepadParticipant()
        {
            return _participants.Find(p => p.type == PartyParticipantType.LocalHuman && p.inputAssignment != null &&
                p.inputAssignment.deviceKind == InputDeviceKind.Gamepad && !p.inputAssignment.isConnected);
        }

        private PartyParticipant FindFirstUnassignedLocalPlayer()
        {
            return _participants.Find(p => p.type == PartyParticipantType.LocalHuman &&
                (p.inputAssignment == null || p.inputAssignment.deviceKind == InputDeviceKind.Unassigned));
        }

        private PartyParticipant FindParticipant(string participantId)
        {
            return string.IsNullOrWhiteSpace(participantId) ? null : _participants.Find(p => p.id == participantId);
        }

        private void RaiseChanged()
        {
            Action handler = PartyChanged;
            if (handler != null) handler();
        }
    }
}
