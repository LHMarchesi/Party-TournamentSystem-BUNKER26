using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Bunker26.Party.Persistence;

namespace Bunker26.Party
{
    /// <summary>
    /// Game integration helper that pairs a gameplay PlayerInput with the devices assigned in Party.
    /// The game still defines its own actions and control schemes; Party supplies ownership metadata.
    /// </summary>
    public sealed class PartyPlayerInputBinder : MonoBehaviour
    {
        [SerializeField] private PartyInputBindingSettings settings;

        public bool TryBind(PlayerInput playerInput, PartyParticipant participant, out string error)
        {
            error = null;
            if (playerInput == null) { error = "PlayerInput reference is missing."; return false; }
            if (participant == null) { error = "Party participant is missing."; return false; }
            if (settings == null) { error = "PartyInputBindingSettings is not assigned."; return false; }
            if (participant.inputAssignment == null || !participant.inputAssignment.IsAssigned)
            { error = "The participant has no input assigned."; return false; }

            InputAssignment assignment = participant.inputAssignment;
            try
            {
                if (assignment.deviceKind == InputDeviceKind.Keyboard)
                {
                    Keyboard keyboard = Keyboard.current;
                    if (keyboard == null) { error = "No keyboard is currently available."; return false; }
                    string scheme = assignment.keyboardSlot == 0 ? settings.keyboardSlot1ControlScheme : settings.keyboardSlot2ControlScheme;
                    if (string.IsNullOrWhiteSpace(scheme)) { error = "The keyboard control scheme is not configured."; return false; }
                    playerInput.SwitchCurrentControlScheme(scheme, keyboard);
                    return true;
                }

                if (assignment.deviceKind == InputDeviceKind.Gamepad)
                {
                    if (!assignment.isConnected) { error = "The assigned gamepad is disconnected."; return false; }
                    Gamepad gamepad = FindGamepad(assignment.deviceId);
                    if (gamepad == null) { error = "The assigned gamepad could not be found."; return false; }
                    if (string.IsNullOrWhiteSpace(settings.gamepadControlScheme)) { error = "The gamepad control scheme is not configured."; return false; }
                    playerInput.SwitchCurrentControlScheme(settings.gamepadControlScheme, gamepad);
                    return true;
                }

                error = "This input assignment type is not supported by the default binder.";
                return false;
            }
            catch (Exception exception)
            {
                error = "Could not bind PlayerInput. Check that the game's InputActionAsset contains the configured control scheme. " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Binds a gameplay PlayerInput to the participant's assigned device and applies the stored
        /// overrides for that participant's profile, game and control scheme.
        /// </summary>
        public bool TryBind(PlayerInput playerInput, PartyParticipant participant, GameInputDefinition definition,
            IInputBindingOverridesRepository overridesRepository, out string error)
        {
            error = null;
            if (playerInput == null) { error = "PlayerInput reference is missing."; return false; }
            if (participant == null) { error = "Party participant is missing."; return false; }
            if (definition == null) { error = "GameInputDefinition is not assigned."; return false; }
            if (overridesRepository == null) { error = "Input binding overrides repository is missing."; return false; }
            if (participant.inputAssignment == null || !participant.inputAssignment.IsAssigned)
            { error = "The participant has no input assigned."; return false; }

            if (!TryBind(playerInput, participant, out error)) return false;
            if (string.IsNullOrWhiteSpace(participant.profileId)) return true; // Unknown participants use game defaults.

            if (!definition.TryResolveScheme(participant.inputAssignment, out string schemeName, out _, out _, out error))
                return false;

            try
            {
                string overridesJson = overridesRepository.Load(participant.profileId, definition.gameId, schemeName);
                if (!string.IsNullOrWhiteSpace(overridesJson))
                {
                    if (playerInput.actions == null)
                    { error = "PlayerInput has no actions asset to apply overrides to."; return false; }
                    playerInput.actions.LoadBindingOverridesFromJson(overridesJson);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = "PlayerInput was paired, but saved control overrides could not be applied. " + exception.Message;
                return false;
            }
        }

        public void Unbind(PlayerInput playerInput)
        {
            if (playerInput == null) return;
            playerInput.DeactivateInput();
            playerInput.user.UnpairDevices();
        }

        private static Gamepad FindGamepad(string deviceId)
        {
            if (!int.TryParse(deviceId, out int parsedId)) return null;
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                if (Gamepad.all[i].deviceId == parsedId && Gamepad.all[i].added) return Gamepad.all[i];
            }
            return null;
        }
    }
}
