using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker26.Party
{
    public enum RebindDeviceFamily
    {
        KeyboardMouse = 0,
        Gamepad = 1,
        Any = 2
    }

    [Serializable]
    public sealed class RebindableInputBindingDefinition
    {
        [Tooltip("Stable Input System binding GUID. Refresh this list from the action asset using the Inspector button.")]
        public string bindingId;

        [Tooltip("Label shown in the controls UI. If empty, a label is derived from the action and binding.")]
        public string displayName;

        [HideInInspector] public string actionMapName;
        [HideInInspector] public string actionName;
    }

    /// <summary>
    /// Game-owned Input Actions and control schemes that Party exposes to its generic rebinding UI.
    /// A separate asset should be created for each game. Default bindings remain in the InputActionAsset;
    /// Party persists only user overrides.
    /// </summary>
    [CreateAssetMenu(menuName = "Bunker 26/Party/Game Input Definition", fileName = "GameInputDefinition")]
    public sealed class GameInputDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string gameId = "my-game";
        public string displayName = "My Game";

        [Header("Input Actions")]
        public InputActionAsset actionsAsset;
        public PartyInputBindingSettings bindingSettings;

        [Tooltip("When disabled, the rebinding service rejects a binding if another editable binding in the same control scheme already uses that control.")]
        public bool allowDuplicateBindings;

        [Header("Bindings exposed in the controls UI")]
        public List<RebindableInputBindingDefinition> editableBindings = new List<RebindableInputBindingDefinition>();

        public bool IsConfigured
        {
            get
            {
                return !string.IsNullOrWhiteSpace(gameId) && actionsAsset != null && bindingSettings != null;
            }
        }

        public bool TryResolveScheme(InputAssignment assignment, out string schemeName, out string bindingGroup,
            out RebindDeviceFamily deviceFamily, out string error)
        {
            schemeName = string.Empty;
            bindingGroup = string.Empty;
            deviceFamily = RebindDeviceFamily.Any;
            error = null;

            if (assignment == null || !assignment.IsAssigned)
            {
                error = "El jugador no tiene un input asignado.";
                return false;
            }
            if (actionsAsset == null)
            {
                error = "Asigná un InputActionAsset en GameInputDefinition.";
                return false;
            }
            if (bindingSettings == null)
            {
                error = "Asigná PartyInputBindingSettings en GameInputDefinition.";
                return false;
            }

            switch (assignment.deviceKind)
            {
                case InputDeviceKind.Keyboard:
                    if (assignment.keyboardSlot < 0 || assignment.keyboardSlot > 1)
                    {
                        error = "El slot de teclado debe ser 1 o 2.";
                        return false;
                    }
                    schemeName = assignment.keyboardSlot == 0
                        ? bindingSettings.keyboardSlot1ControlScheme
                        : bindingSettings.keyboardSlot2ControlScheme;
                    deviceFamily = RebindDeviceFamily.KeyboardMouse;
                    break;
                case InputDeviceKind.Gamepad:
                    schemeName = bindingSettings.gamepadControlScheme;
                    deviceFamily = RebindDeviceFamily.Gamepad;
                    break;
                default:
                    error = "Este tipo de input todavía no admite personalización de bindings.";
                    return false;
            }

            if (string.IsNullOrWhiteSpace(schemeName))
            {
                error = "El esquema de control no está configurado.";
                return false;
            }

            for (int i = 0; i < actionsAsset.controlSchemes.Count; i++)
            {
                InputControlScheme scheme = actionsAsset.controlSchemes[i];
                if (!string.Equals(scheme.name, schemeName, StringComparison.OrdinalIgnoreCase)) continue;
                bindingGroup = string.IsNullOrWhiteSpace(scheme.bindingGroup) ? scheme.name : scheme.bindingGroup;
                return true;
            }

            error = "El InputActionAsset no contiene el control scheme '" + schemeName + "'. Revisá PartyInputBindingSettings.";
            return false;
        }

        /// <summary>Rebuilds the Inspector list from all concrete, grouped bindings in the assigned action asset.</summary>
        public void RefreshEditableBindingsFromAsset()
        {
            if (editableBindings == null) editableBindings = new List<RebindableInputBindingDefinition>();
            else editableBindings.Clear();
            if (actionsAsset == null) return;

            for (int mapIndex = 0; mapIndex < actionsAsset.actionMaps.Count; mapIndex++)
            {
                InputActionMap map = actionsAsset.actionMaps[mapIndex];
                foreach (InputAction action in map.actions)
                {
                    for (int bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                    {
                        InputBinding binding = action.bindings[bindingIndex];
                        if (binding.isComposite || string.IsNullOrWhiteSpace(binding.groups) || binding.id == Guid.Empty)
                            continue;

                        string label = string.IsNullOrWhiteSpace(binding.name)
                            ? action.name
                            : action.name + " · " + HumanizePartName(binding.name);

                        editableBindings.Add(new RebindableInputBindingDefinition
                        {
                            bindingId = binding.id.ToString(),
                            displayName = label,
                            actionMapName = map.name,
                            actionName = action.name
                        });
                    }
                }
            }
        }

        public bool HasBindingForGroup(string bindingGroup)
        {
            if (actionsAsset == null || editableBindings == null || string.IsNullOrWhiteSpace(bindingGroup)) return false;
            for (int i = 0; i < editableBindings.Count; i++)
            {
                if (TryFindBinding(actionsAsset, editableBindings[i].bindingId, out _, out _, out InputBinding binding) &&
                    BindingHasGroup(binding.groups, bindingGroup)) return true;
            }
            return false;
        }

        internal static bool BindingHasGroup(string groups, string targetGroup)
        {
            if (string.IsNullOrWhiteSpace(groups) || string.IsNullOrWhiteSpace(targetGroup)) return false;
            string[] split = groups.Split(';');
            for (int i = 0; i < split.Length; i++)
                if (string.Equals(split[i].Trim(), targetGroup, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal static bool TryFindBinding(InputActionAsset asset, string bindingId, out InputAction action,
            out int bindingIndex, out InputBinding binding)
        {
            action = null;
            bindingIndex = -1;
            binding = default;
            if (asset == null || string.IsNullOrWhiteSpace(bindingId)) return false;

            string normalizedId = bindingId.Trim().Trim('{', '}');
            for (int mapIndex = 0; mapIndex < asset.actionMaps.Count; mapIndex++)
            {
                InputActionMap map = asset.actionMaps[mapIndex];
                foreach (InputAction candidate in map.actions)
                {
                    for (int i = 0; i < candidate.bindings.Count; i++)
                    {
                        InputBinding candidateBinding = candidate.bindings[i];
                        if (!string.Equals(candidateBinding.id.ToString(), normalizedId, StringComparison.OrdinalIgnoreCase)) continue;
                        action = candidate;
                        bindingIndex = i;
                        binding = candidateBinding;
                        return true;
                    }
                }
            }
            return false;
        }

        private static string HumanizePartName(string name)
        {
            if (string.Equals(name, "up", StringComparison.OrdinalIgnoreCase)) return "Arriba";
            if (string.Equals(name, "down", StringComparison.OrdinalIgnoreCase)) return "Abajo";
            if (string.Equals(name, "left", StringComparison.OrdinalIgnoreCase)) return "Izquierda";
            if (string.Equals(name, "right", StringComparison.OrdinalIgnoreCase)) return "Derecha";
            return name;
        }
    }

    public sealed class InputBindingDisplayInfo
    {
        public string bindingId;
        public string displayName;
        public string actionMapName;
        public string actionName;
        public string currentPath;
        public string currentDisplay;
    }

    public sealed class InputBindingRebindResult
    {
        public bool succeeded;
        public bool cancelled;
        public string message;
        public string bindingId;
    }
}
