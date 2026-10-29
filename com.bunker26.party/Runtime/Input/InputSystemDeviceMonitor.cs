using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker26.Party
{
    /// <summary>
    /// Monitors real Input System device changes and gamepad button activity. It does not create
    /// players itself; the Party coordinator decides whether activity joins or reassigns a slot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputSystemDeviceMonitor : MonoBehaviour, IObserver<InputControl>
    {
        public event Action<Gamepad> GamepadButtonPressed;
        public event Action<InputDevice, InputDeviceChange> DeviceChanged;

        private IDisposable _buttonSubscription;
        private readonly List<Gamepad> _connectedGamepads = new List<Gamepad>();
        private IReadOnlyList<Gamepad> _readOnlyGamepads;

        public IReadOnlyList<Gamepad> ConnectedGamepads { get { return _readOnlyGamepads ?? _connectedGamepads.AsReadOnly(); } }

        private void Awake()
        {
            _readOnlyGamepads = _connectedGamepads.AsReadOnly();
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
            _buttonSubscription = InputSystem.onAnyButtonPress.Subscribe(this);
            RefreshGamepads();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
            if (_buttonSubscription != null)
            {
                _buttonSubscription.Dispose();
                _buttonSubscription = null;
            }
        }

        public void OnNext(InputControl control)
        {
            if (!isActiveAndEnabled || control == null) return;
            Gamepad gamepad = control.device as Gamepad;
            if (gamepad == null || !gamepad.added) return;

            Action<Gamepad> handler = GamepadButtonPressed;
            if (handler != null) handler(gamepad);
        }

        public void OnError(Exception error)
        {
            Debug.LogError("[Bunker26 Party] Input System device monitor failed: " + error);
        }

        public void OnCompleted() { }

        public bool IsGamepadConnected(int deviceId)
        {
            for (int i = 0; i < _connectedGamepads.Count; i++)
            {
                if (_connectedGamepads[i] != null && _connectedGamepads[i].deviceId == deviceId) return true;
            }
            return false;
        }

        public string GetConnectedGamepadsSummary()
        {
            if (_connectedGamepads.Count == 0) return "No hay mandos conectados.";
            string summary = "Mandos conectados: ";
            for (int i = 0; i < _connectedGamepads.Count; i++)
            {
                if (i > 0) summary += ", ";
                summary += string.IsNullOrWhiteSpace(_connectedGamepads[i].displayName) ? "Gamepad" : _connectedGamepads[i].displayName;
            }
            return summary + ".";
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            Gamepad gamepad = device as Gamepad;
            if (gamepad == null) return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.ConfigurationChanged:
                    RefreshGamepads();
                    break;
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disabled:
                    RefreshGamepads();
                    break;
            }

            Action<InputDevice, InputDeviceChange> handler = DeviceChanged;
            if (handler != null) handler(device, change);
        }

        private void RefreshGamepads()
        {
            _connectedGamepads.Clear();
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                Gamepad gamepad = Gamepad.all[i];
                if (gamepad != null && gamepad.added) _connectedGamepads.Add(gamepad);
            }
            // DeviceChanged also refreshes the display after hot-plug operations.
            Action<InputDevice, InputDeviceChange> handler = DeviceChanged;
            if (handler != null) handler(null, InputDeviceChange.ConfigurationChanged);
        }
    }
}
