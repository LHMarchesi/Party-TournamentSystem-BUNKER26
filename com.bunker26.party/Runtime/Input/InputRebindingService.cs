using System;
using System.Collections.Generic;
using Bunker26.Party.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bunker26.Party
{
    /// <summary>Creates isolated runtime rebinding sessions for a profile/game/control-scheme combination.</summary>
    public sealed class InputRebindingService
    {
        private readonly IInputBindingOverridesRepository _repository;

        public InputRebindingService(IInputBindingOverridesRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public InputRebindingSession CreateSession(string profileId, GameInputDefinition definition,
            string controlSchemeName, string bindingGroup, RebindDeviceFamily deviceFamily, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(profileId)) { error = "Seleccioná un perfil antes de personalizar los controles."; return null; }
            if (definition == null || !definition.IsConfigured) { error = "GameInputDefinition no está configurado."; return null; }
            if (string.IsNullOrWhiteSpace(controlSchemeName) || string.IsNullOrWhiteSpace(bindingGroup))
            { error = "No se pudo resolver el esquema de control del jugador."; return null; }
            if (!definition.HasBindingForGroup(bindingGroup))
            { error = "No hay bindings editables para el esquema '" + controlSchemeName + "'. Revisá los grupos del InputActionAsset."; return null; }

            try
            {
                string overridesJson = _repository.Load(profileId, definition.gameId, controlSchemeName);
                return new InputRebindingSession(profileId, definition, controlSchemeName, bindingGroup, deviceFamily,
                    _repository, overridesJson);
            }
            catch (Exception exception)
            {
                error = "No se pudieron cargar las personalizaciones de controles. " + exception.Message;
                return null;
            }
        }

        public string GetOverridesJson(string profileId, GameInputDefinition definition, string controlSchemeName)
        {
            if (definition == null || string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(controlSchemeName))
                return string.Empty;
            return _repository.Load(profileId, definition.gameId, controlSchemeName);
        }
    }

    /// <summary>
    /// Owns an isolated clone of a game's action asset while one profile edits one control scheme.
    /// Interactive rebinding operations are always disposed when completed, cancelled or the session closes.
    /// </summary>
    public sealed class InputRebindingSession : IDisposable
    {
        private readonly string _profileId;
        private readonly GameInputDefinition _definition;
        private readonly string _controlSchemeName;
        private readonly string _bindingGroup;
        private readonly RebindDeviceFamily _deviceFamily;
        private readonly IInputBindingOverridesRepository _repository;
        private InputActionRebindingExtensions.RebindingOperation _operation;
        private Action<InputBindingRebindResult> _completionCallback;
        private InputAction _pendingAction;
        private int _pendingBindingIndex = -1;
        private InputBinding _originalBinding;
        private string _pendingBindingId;
        private bool _disposed;

        public InputActionAsset RuntimeActions { get { return _runtimeActions; } }
        private readonly InputActionAsset _runtimeActions;
        public string ProfileId { get { return _profileId; } }
        public string GameId { get { return _definition.gameId; } }
        public string ControlSchemeName { get { return _controlSchemeName; } }
        public string BindingGroup { get { return _bindingGroup; } }
        public bool IsRebinding { get { return _operation != null; } }

        internal InputRebindingSession(string profileId, GameInputDefinition definition, string controlSchemeName,
            string bindingGroup, RebindDeviceFamily deviceFamily, IInputBindingOverridesRepository repository, string overridesJson)
        {
            _profileId = profileId;
            _definition = definition;
            _controlSchemeName = controlSchemeName;
            _bindingGroup = bindingGroup;
            _deviceFamily = deviceFamily;
            _repository = repository;
            _runtimeActions = UnityEngine.Object.Instantiate(definition.actionsAsset);
            _runtimeActions.name = definition.actionsAsset.name + " (Party Rebind Session)";
            _runtimeActions.hideFlags = HideFlags.HideAndDontSave;

            if (!string.IsNullOrWhiteSpace(overridesJson))
                _runtimeActions.LoadBindingOverridesFromJson(overridesJson);
        }

        public IReadOnlyList<InputBindingDisplayInfo> GetBindings()
        {
            ThrowIfDisposed();
            List<InputBindingDisplayInfo> result = new List<InputBindingDisplayInfo>();
            if (_definition.editableBindings == null) return result;
            for (int i = 0; i < _definition.editableBindings.Count; i++)
            {
                RebindableInputBindingDefinition definition = _definition.editableBindings[i];
                if (!GameInputDefinition.TryFindBinding(_runtimeActions, definition.bindingId,
                        out InputAction action, out int bindingIndex, out InputBinding binding)) continue;
                if (!GameInputDefinition.BindingHasGroup(binding.groups, _bindingGroup)) continue;

                string displayPath = ToDisplayString(binding.effectivePath);
                result.Add(new InputBindingDisplayInfo
                {
                    bindingId = definition.bindingId,
                    displayName = string.IsNullOrWhiteSpace(definition.displayName) ? action.name : definition.displayName,
                    actionMapName = action.actionMap != null ? action.actionMap.name : definition.actionMapName,
                    actionName = action.name,
                    currentPath = binding.effectivePath,
                    currentDisplay = displayPath
                });
            }
            return result;
        }

        public bool TryBeginRebind(string bindingId, Action<InputBindingRebindResult> onCompleted, out string error)
        {
            error = null;
            ThrowIfDisposed();
            if (_operation != null) { error = "Ya hay una operación de reasignación en curso."; return false; }
            if (!GameInputDefinition.TryFindBinding(_runtimeActions, bindingId,
                    out InputAction action, out int bindingIndex, out InputBinding binding))
            { error = "No se encontró el binding solicitado. Actualizá la lista editable desde el Inspector."; return false; }
            if (binding.isComposite)
            { error = "Seleccioná una parte del binding compuesto, no el encabezado del compuesto."; return false; }
            if (!GameInputDefinition.BindingHasGroup(binding.groups, _bindingGroup))
            { error = "El binding no pertenece al esquema de control de este jugador."; return false; }

            _pendingAction = action;
            _pendingBindingIndex = bindingIndex;
            _originalBinding = binding;
            _pendingBindingId = bindingId;
            _completionCallback = onCompleted;

            InputActionRebindingExtensions.RebindingOperation operation = null;
            try
            {
                operation = action.PerformInteractiveRebinding(bindingIndex)
                    .OnMatchWaitForAnother(0.12f)
                    .WithMatchingEventsBeingSuppressed(true)
                    .WithCancelingThrough("<Keyboard>/escape")
                    .OnComplete(FinishCompleted)
                    .OnCancel(FinishCancelled);

                ApplyDeviceFamilyFilter(operation);
                _operation = operation;
                operation.Start();
                return true;
            }
            catch (Exception exception)
            {
                if (ReferenceEquals(_operation, operation)) _operation = null;
                if (operation != null) operation.Dispose();
                _pendingAction = null;
                _pendingBindingIndex = -1;
                _pendingBindingId = null;
                _completionCallback = null;
                error = "No se pudo iniciar el rebinding. " + exception.Message;
                return false;
            }
        }

        public void CancelRebind()
        {
            if (_operation == null) return;
            _operation.Cancel();
        }

        public bool ResetBinding(string bindingId, out string error)
        {
            error = null;
            ThrowIfDisposed();
            if (_operation != null) { error = "Cancelá la reasignación actual antes de restaurar un control."; return false; }
            if (!GameInputDefinition.TryFindBinding(_runtimeActions, bindingId,
                    out InputAction action, out int bindingIndex, out InputBinding binding) ||
                !GameInputDefinition.BindingHasGroup(binding.groups, _bindingGroup))
            { error = "No se encontró el binding en el esquema actual."; return false; }

            InputBinding originalBinding = binding;
            action.RemoveBindingOverride(bindingIndex);
            if (SaveCurrentOverrides(out error)) return true;

            RestoreBinding(action, bindingIndex, originalBinding);
            return false;
        }

        public bool ResetAll(out string error)
        {
            error = null;
            ThrowIfDisposed();
            if (_operation != null) { error = "Cancelá la reasignación actual antes de restaurar los controles."; return false; }

            List<BindingOverrideSnapshot> snapshots = new List<BindingOverrideSnapshot>();
            for (int mapIndex = 0; mapIndex < _runtimeActions.actionMaps.Count; mapIndex++)
            {
                InputActionMap map = _runtimeActions.actionMaps[mapIndex];
                foreach (InputAction action in map.actions)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding binding = action.bindings[i];
                        if (!binding.hasOverrides || !GameInputDefinition.BindingHasGroup(binding.groups, _bindingGroup)) continue;
                        snapshots.Add(new BindingOverrideSnapshot(action, i, binding));
                        action.RemoveBindingOverride(i);
                    }
                }
            }

            try
            {
                _repository.Delete(_profileId, _definition.gameId, _controlSchemeName);
                return true;
            }
            catch (Exception exception)
            {
                for (int i = 0; i < snapshots.Count; i++)
                    RestoreBinding(snapshots[i].action, snapshots[i].bindingIndex, snapshots[i].binding);
                error = "No se pudieron guardar los valores predeterminados; se restauró la configuración anterior. " + exception.Message;
                return false;
            }
        }

        private void ApplyDeviceFamilyFilter(InputActionRebindingExtensions.RebindingOperation operation)
        {
            switch (_deviceFamily)
            {
                case RebindDeviceFamily.KeyboardMouse:
                    operation.WithControlsExcluding("<Gamepad>");
                    operation.WithControlsExcluding("<Joystick>");
                    operation.WithControlsExcluding("<XRController>");
                    operation.WithControlsExcluding("<Touchscreen>");
                    break;
                case RebindDeviceFamily.Gamepad:
                    operation.WithControlsExcluding("<Keyboard>");
                    operation.WithControlsExcluding("<Mouse>");
                    operation.WithControlsExcluding("<Joystick>");
                    operation.WithControlsExcluding("<XRController>");
                    operation.WithControlsExcluding("<Touchscreen>");
                    operation.WithControlsExcluding("<Pen>");
                    break;
            }
        }

        private void FinishCompleted(InputActionRebindingExtensions.RebindingOperation operation)
        {
            bool conflict = !_definition.allowDuplicateBindings && HasDuplicateBinding(_pendingBindingId, _pendingAction.bindings[_pendingBindingIndex].effectivePath);
            string message;
            bool succeeded = !conflict;

            if (conflict)
            {
                RestoreOriginalBinding();
                message = "Ese control ya está asignado a otra acción de este esquema.";
            }
            else
            {
                string saveError;
                succeeded = SaveCurrentOverrides(out saveError);
                if (!succeeded) RestoreOriginalBinding();
                message = succeeded ? "Control actualizado y guardado." : saveError;
            }

            FinishOperation(operation, new InputBindingRebindResult
            {
                succeeded = succeeded,
                cancelled = false,
                bindingId = _pendingBindingId,
                message = message
            });
        }

        private void FinishCancelled(InputActionRebindingExtensions.RebindingOperation operation)
        {
            FinishOperation(operation, new InputBindingRebindResult
            {
                succeeded = false,
                cancelled = true,
                bindingId = _pendingBindingId,
                message = "Reasignación cancelada."
            });
        }

        private void FinishOperation(InputActionRebindingExtensions.RebindingOperation operation, InputBindingRebindResult result)
        {
            if (_operation != null && !ReferenceEquals(_operation, operation)) return;
            _operation = null;
            Action<InputBindingRebindResult> callback = _completionCallback;
            _completionCallback = null;
            _pendingAction = null;
            _pendingBindingIndex = -1;
            _pendingBindingId = null;
            operation.Dispose();
            if (!_disposed && callback != null) callback(result);
        }

        private bool SaveCurrentOverrides(out string error)
        {
            error = null;
            try
            {
                if (HasAnyOverridesForGroup())
                    _repository.Save(_profileId, _definition.gameId, _controlSchemeName, _runtimeActions.SaveBindingOverridesAsJson());
                else
                    _repository.Delete(_profileId, _definition.gameId, _controlSchemeName);
                return true;
            }
            catch (Exception exception)
            {
                error = "No se pudieron guardar los controles. " + exception.Message;
                return false;
            }
        }

        private bool HasAnyOverridesForGroup()
        {
            for (int mapIndex = 0; mapIndex < _runtimeActions.actionMaps.Count; mapIndex++)
            {
                foreach (InputAction action in _runtimeActions.actionMaps[mapIndex].actions)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding binding = action.bindings[i];
                        if (binding.hasOverrides && GameInputDefinition.BindingHasGroup(binding.groups, _bindingGroup)) return true;
                    }
                }
            }
            return false;
        }

        private bool HasDuplicateBinding(string bindingId, string effectivePath)
        {
            if (string.IsNullOrWhiteSpace(effectivePath)) return false;
            for (int mapIndex = 0; mapIndex < _runtimeActions.actionMaps.Count; mapIndex++)
            {
                foreach (InputAction action in _runtimeActions.actionMaps[mapIndex].actions)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding candidate = action.bindings[i];
                        if (candidate.isComposite || string.Equals(candidate.id.ToString(), bindingId, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!GameInputDefinition.BindingHasGroup(candidate.groups, _bindingGroup)) continue;
                        if (string.Equals(candidate.effectivePath, effectivePath, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }
            return false;
        }

        private void RestoreOriginalBinding()
        {
            if (_pendingAction == null || _pendingBindingIndex < 0 || _pendingBindingIndex >= _pendingAction.bindings.Count) return;
            RestoreBinding(_pendingAction, _pendingBindingIndex, _originalBinding);
        }

        private static void RestoreBinding(InputAction action, int bindingIndex, InputBinding originalBinding)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;
            if (string.IsNullOrEmpty(originalBinding.overridePath) &&
                string.IsNullOrEmpty(originalBinding.overrideProcessors) &&
                string.IsNullOrEmpty(originalBinding.overrideInteractions))
                action.RemoveBindingOverride(bindingIndex);
            else
                action.ApplyBindingOverride(bindingIndex, originalBinding);
        }

        private struct BindingOverrideSnapshot
        {
            public readonly InputAction action;
            public readonly int bindingIndex;
            public readonly InputBinding binding;

            public BindingOverrideSnapshot(InputAction action, int bindingIndex, InputBinding binding)
            {
                this.action = action;
                this.bindingIndex = bindingIndex;
                this.binding = binding;
            }
        }

        private static string ToDisplayString(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Sin asignar";
            try
            {
                string readable = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                return string.IsNullOrWhiteSpace(readable) ? path : readable;
            }
            catch { return path; }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InputRebindingSession));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_operation != null)
            {
                InputActionRebindingExtensions.RebindingOperation operation = _operation;
                _completionCallback = null;
                // Cancel invokes the registered cleanup callback, which disposes the native operation once.
                operation.Cancel();
                if (ReferenceEquals(_operation, operation))
                {
                    _operation = null;
                    operation.Dispose();
                }
            }
            _completionCallback = null;
            _pendingAction = null;
            if (_runtimeActions != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_runtimeActions);
                else UnityEngine.Object.DestroyImmediate(_runtimeActions);
            }
        }
    }
}
