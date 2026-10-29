using System;
using System.Collections.Generic;

namespace Bunker26.Party
{
    public interface IInputAssignmentService
    {
        bool TryAssign(PartyParticipant participant, IReadOnlyList<PartyParticipant> party, InputAssignment assignment, out string error);
        void Release(PartyParticipant participant);
        bool IsAssignmentAvailable(PartyParticipant participant, IReadOnlyList<PartyParticipant> party, InputAssignment assignment, out string error);
    }

    /// <summary>Validates the Party's logical ownership of keyboard slots and physical gamepads.</summary>
    public sealed class InputAssignmentService : IInputAssignmentService
    {
        public bool TryAssign(PartyParticipant participant, IReadOnlyList<PartyParticipant> party, InputAssignment assignment, out string error)
        {
            if (!IsAssignmentAvailable(participant, party, assignment, out error)) return false;
            participant.inputAssignment = assignment.Clone();
            return true;
        }

        public bool IsAssignmentAvailable(PartyParticipant participant, IReadOnlyList<PartyParticipant> party, InputAssignment assignment, out string error)
        {
            error = null;
            if (participant == null) { error = "Participant was not found."; return false; }
            if (party == null) throw new ArgumentNullException(nameof(party));
            if (assignment == null) { error = "Input assignment cannot be null."; return false; }

            // A participant cannot re-select the exact slot/device it already owns. The UI marks
            // this choice as the current assignment, and the service protects the same invariant
            // for callers that bypass the UI.
            if (IsSameAssignment(participant.inputAssignment, assignment))
            {
                error = "This input is already assigned to this player.";
                return false;
            }

            if (assignment.deviceKind == InputDeviceKind.Keyboard)
            {
                if (assignment.keyboardSlot < 0 || assignment.keyboardSlot > 1)
                { error = "Keyboard slot must be 0 or 1."; return false; }
                PartyParticipant occupied = FindOther(party, participant.id, p => p.inputAssignment != null &&
                    p.inputAssignment.deviceKind == InputDeviceKind.Keyboard && p.inputAssignment.keyboardSlot == assignment.keyboardSlot);
                if (occupied != null) { error = "That keyboard slot is already assigned."; return false; }
            }
            else if (assignment.deviceKind == InputDeviceKind.Gamepad)
            {
                if (string.IsNullOrWhiteSpace(assignment.deviceId)) { error = "A gamepad device ID is required."; return false; }
                PartyParticipant occupied = FindOther(party, participant.id, p => p.inputAssignment != null &&
                    p.inputAssignment.deviceKind == InputDeviceKind.Gamepad && p.inputAssignment.deviceId == assignment.deviceId);
                if (occupied != null) { error = "That gamepad is already assigned to another local player."; return false; }
            }

            return true;
        }

        public void Release(PartyParticipant participant)
        {
            if (participant == null) return;
            participant.inputAssignment = InputAssignment.Unassigned();
        }

        private static bool IsSameAssignment(InputAssignment current, InputAssignment requested)
        {
            if (current == null || requested == null || current.deviceKind != requested.deviceKind)
                return false;

            switch (requested.deviceKind)
            {
                case InputDeviceKind.Keyboard:
                    return current.keyboardSlot == requested.keyboardSlot;
                case InputDeviceKind.Gamepad:
                    return !string.IsNullOrWhiteSpace(current.deviceId) && current.deviceId == requested.deviceId;
                default:
                    return false;
            }
        }

        private static PartyParticipant FindOther(IReadOnlyList<PartyParticipant> party, string participantId, Predicate<PartyParticipant> predicate)
        {
            for (int i = 0; i < party.Count; i++)
            {
                PartyParticipant candidate = party[i];
                if (candidate != null && candidate.id != participantId && predicate(candidate)) return candidate;
            }
            return null;
        }
    }
}
