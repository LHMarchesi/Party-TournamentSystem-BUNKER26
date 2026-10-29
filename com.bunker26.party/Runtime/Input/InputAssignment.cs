using System;

namespace Bunker26.Party
{
    public enum InputDeviceKind
    {
        Unassigned = 0,
        Keyboard = 1,
        Gamepad = 2,
        Other = 3
    }

    /// <summary>
    /// Logical input ownership for one local participant. Keyboard slots are logical schemes
    /// sharing the same physical keyboard; gamepad IDs are runtime-only Input System device IDs.
    /// </summary>
    [Serializable]
    public sealed class InputAssignment
    {
        public InputDeviceKind deviceKind;
        public string deviceId;
        public string deviceDisplayName;
        public int keyboardSlot;
        public bool isConnected;

        public bool IsAssigned { get { return deviceKind != InputDeviceKind.Unassigned; } }

        public static InputAssignment Unassigned()
        {
            return new InputAssignment
            {
                deviceKind = InputDeviceKind.Unassigned,
                deviceId = string.Empty,
                deviceDisplayName = string.Empty,
                keyboardSlot = -1,
                isConnected = false
            };
        }

        public static InputAssignment Keyboard(int slot)
        {
            return new InputAssignment
            {
                deviceKind = InputDeviceKind.Keyboard,
                deviceId = "keyboard",
                deviceDisplayName = slot == 0 ? "Keyboard Slot 1" : "Keyboard Slot 2",
                keyboardSlot = slot,
                isConnected = true
            };
        }

        public static InputAssignment Gamepad(string deviceId, string displayName = null, bool connected = true)
        {
            return new InputAssignment
            {
                deviceKind = InputDeviceKind.Gamepad,
                deviceId = deviceId ?? string.Empty,
                deviceDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Gamepad" : displayName,
                keyboardSlot = -1,
                isConnected = connected
            };
        }

        public InputAssignment Clone()
        {
            return new InputAssignment
            {
                deviceKind = deviceKind,
                deviceId = deviceId,
                deviceDisplayName = deviceDisplayName,
                keyboardSlot = keyboardSlot,
                isConnected = isConnected
            };
        }
    }
}
