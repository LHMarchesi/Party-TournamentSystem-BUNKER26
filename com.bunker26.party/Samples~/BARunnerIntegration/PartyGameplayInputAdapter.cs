using System;
using System.Globalization;
using Bunker26.Party;
using Bunker26.Party.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BA Runner integration sample. Keep this file in the BA Runner project's default gameplay
/// assembly (for example Assets/Scripts/Integrations), not inside Bunker26.Party's runtime asmdef.
/// It transfers the Party leader's device/scheme to the single Story-mode PlayerInput.
/// </summary>
[DisallowMultipleComponent]
public sealed class PartyGameplayInputAdapter : MonoBehaviour
{
    [Header("BA Runner References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerController playerController;

    [Header("Input Actions")]
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string keyboard1MoveAction = "Move_Player1";
    [SerializeField] private string keyboard2MoveAction = "Move_Player2";
    [SerializeField] private string gamepadMoveAction = "Move_Player1";
    [SerializeField] private string pauseAction = "Pause";

    [Header("Fallback Control Schemes")]
    [SerializeField] private string keyboard1Scheme = "Keyboard1";
    [SerializeField] private string keyboard2Scheme = "Keyboard2";
    [SerializeField] private string gamepadScheme = "Gamepad";

    [Header("Optional Profile Rebinding")]
    [Tooltip("Assign the same GameInputDefinition used by Party Setup to apply the leader's saved profile overrides.")]
    [SerializeField] private GameInputDefinition gameInputDefinition;
    [SerializeField] private bool applySavedProfileOverrides = true;

    private PartyService _party;
    private InputActionMap _playerMap;
    private InputAction _pause;
    private readonly System.Collections.Generic.List<InputAction> _moveActions =
        new System.Collections.Generic.List<InputAction>();

    private string _activeLeaderId;
    private string _activeSignature;
    private string _activeScheme;
    private string _activeBindingGroup;
    private bool _disabledByAdapter;

    private void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        if (playerInput == null || playerController == null)
        {
            Debug.LogError("[PartyGameplayInputAdapter] Assign PlayerInput and PlayerController.", this);
            enabled = false;
            return;
        }

        PartyRuntime runtime = PartyRuntime.GetOrCreate(8, true);
        _party = runtime.Party;
        if (_party == null)
        {
            Debug.LogError("[PartyGameplayInputAdapter] PartyRuntime has no PartyService.", this);
            enabled = false;
            return;
        }

        _playerMap = playerInput.actions != null
            ? playerInput.actions.FindActionMap(playerActionMapName, false)
            : null;
        if (_playerMap == null)
        {
            Debug.LogError("[PartyGameplayInputAdapter] Action Map not found: " + playerActionMapName, this);
            enabled = false;
            return;
        }

        SubscribeMovement(keyboard1MoveAction);
        SubscribeMovement(keyboard2MoveAction);
        SubscribeMovement(gamepadMoveAction);
        _pause = _playerMap.FindAction(pauseAction, false);
        if (_pause != null) _pause.started += HandlePause;
        else Debug.LogError("[PartyGameplayInputAdapter] Pause action not found: " + pauseAction, this);

        // Device choice is controlled by Party, not by whichever device was used most recently.
        playerInput.neverAutoSwitchControlSchemes = true;
        _party.PartyChanged += HandlePartyChanged;
        ApplyLeaderInput();
    }

    private void OnDestroy()
    {
        if (_party != null) _party.PartyChanged -= HandlePartyChanged;

        for (int i = 0; i < _moveActions.Count; i++)
        {
            InputAction action = _moveActions[i];
            if (action == null) continue;
            action.started -= HandleMove;
            action.performed -= HandleMove;
            action.canceled -= HandleMove;
        }

        if (_pause != null) _pause.started -= HandlePause;
    }

    private void SubscribeMovement(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName) || _playerMap == null) return;
        InputAction action = _playerMap.FindAction(actionName, false);
        if (action == null)
        {
            Debug.LogWarning("[PartyGameplayInputAdapter] Movement action not found: " + actionName, this);
            return;
        }
        if (_moveActions.Contains(action)) return;

        _moveActions.Add(action);
        action.started += HandleMove;
        action.performed += HandleMove;
        action.canceled += HandleMove;
    }

    private void HandlePartyChanged()
    {
        ApplyLeaderInput();
    }

    private void ApplyLeaderInput()
    {
        PartyParticipant leader = _party != null ? _party.Leader : null;
        if (leader == null)
        {
            DisableGameplayInput("There is no local Party leader.");
            return;
        }

        InputAssignment assignment = leader.inputAssignment;
        if (assignment == null || !assignment.IsAssigned)
        {
            DisableGameplayInput("The Party leader has no input assigned.");
            return;
        }

        string schemeName;
        string bindingGroup;
        string error;
        if (!TryResolveScheme(assignment, out schemeName, out bindingGroup, out error))
        {
            DisableGameplayInput(error);
            return;
        }

        InputDevice device;
        if (assignment.deviceKind == InputDeviceKind.Keyboard)
        {
            device = Keyboard.current;
            if (device == null && Keyboard.all.Count > 0) device = Keyboard.all[0];
        }
        else if (assignment.deviceKind == InputDeviceKind.Gamepad)
        {
            if (!assignment.isConnected)
            {
                DisableGameplayInput("The Party leader's assigned gamepad is disconnected.");
                return;
            }
            device = FindGamepad(assignment.deviceId);
        }
        else
        {
            DisableGameplayInput("The Party leader's input type is not supported.");
            return;
        }

        if (device == null)
        {
            DisableGameplayInput("The physical device assigned to the Party leader could not be found.");
            return;
        }

        string signature = leader.id + "|" + leader.profileId + "|" + schemeName + "|" +
                           assignment.deviceKind + "|" + assignment.deviceId + "|" + assignment.keyboardSlot;
        if (_activeLeaderId == leader.id && _activeSignature == signature &&
            playerInput.currentControlScheme == schemeName)
            return;

        try
        {
            // Stop horizontal momentum from stale input before changing ownership.
            playerController.SetHorizontalInput(0f);
            if (assignment.deviceKind == InputDeviceKind.Keyboard && Mouse.current != null)
                playerInput.SwitchCurrentControlScheme(schemeName, device, Mouse.current);
            else
                playerInput.SwitchCurrentControlScheme(schemeName, device);

            if (applySavedProfileOverrides)
                ApplyLeaderOverrides(leader, schemeName);

            _activeLeaderId = leader.id;
            _activeSignature = signature;
            _activeScheme = schemeName;
            _activeBindingGroup = bindingGroup;

            if (_disabledByAdapter)
            {
                playerInput.ActivateInput();
                _disabledByAdapter = false;
            }

            Debug.Log("[PartyGameplayInputAdapter] Active leader: " + leader.DisplayName +
                      " | Scheme: " + schemeName + " | Group: " + bindingGroup, this);
        }
        catch (Exception exception)
        {
            _activeLeaderId = null;
            _activeSignature = null;
            DisableGameplayInput("Could not activate leader input scheme '" + schemeName + "'. " + exception.Message);
        }
    }

    private bool TryResolveScheme(InputAssignment assignment, out string schemeName,
        out string bindingGroup, out string error)
    {
        schemeName = string.Empty;
        bindingGroup = string.Empty;
        error = null;

        if (gameInputDefinition != null)
        {
            if (!gameInputDefinition.TryResolveScheme(assignment, out schemeName,
                    out bindingGroup, out _, out error))
                return false;
        }
        else
        {
            switch (assignment.deviceKind)
            {
                case InputDeviceKind.Keyboard:
                    schemeName = assignment.keyboardSlot == 0 ? keyboard1Scheme : keyboard2Scheme;
                    bindingGroup = schemeName;
                    break;
                case InputDeviceKind.Gamepad:
                    schemeName = gamepadScheme;
                    bindingGroup = schemeName;
                    break;
                default:
                    error = "Unsupported input assignment type.";
                    return false;
            }
        }

        if (playerInput.actions == null)
        {
            error = "PlayerInput has no InputActionAsset.";
            return false;
        }

        bool schemeExists = false;
        for (int i = 0; i < playerInput.actions.controlSchemes.Count; i++)
        {
            if (string.Equals(playerInput.actions.controlSchemes[i].name, schemeName,
                    StringComparison.OrdinalIgnoreCase))
            {
                schemeExists = true;
                break;
            }
        }
        if (!schemeExists)
        {
            error = "PlayerInput's InputActionAsset does not contain control scheme '" + schemeName + "'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(bindingGroup)) bindingGroup = schemeName;
        return true;
    }

    private void ApplyLeaderOverrides(PartyParticipant leader, string schemeName)
    {
        if (playerInput.actions == null || gameInputDefinition == null) return;

        // Avoid carrying the previous profile's overrides into the new leader's control scheme.
        playerInput.actions.RemoveAllBindingOverrides();
        if (string.IsNullOrWhiteSpace(leader.profileId)) return;

        IInputBindingOverridesRepository repository = new JsonInputBindingOverridesRepository();
        string json = repository.Load(leader.profileId, gameInputDefinition.gameId, schemeName);
        if (!string.IsNullOrWhiteSpace(json))
            playerInput.actions.LoadBindingOverridesFromJson(json);
    }

    private static Gamepad FindGamepad(string deviceId)
    {
        if (!int.TryParse(deviceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            return null;

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad gamepad = Gamepad.all[i];
            if (gamepad != null && gamepad.added && gamepad.deviceId == id)
                return gamepad;
        }
        return null;
    }

    private void DisableGameplayInput(string reason)
    {
        _activeLeaderId = null;
        _activeSignature = null;
        _activeScheme = null;
        _activeBindingGroup = null;

        if (playerController != null) playerController.SetHorizontalInput(0f);
        if (playerInput != null && !_disabledByAdapter)
        {
            playerInput.DeactivateInput();
            _disabledByAdapter = true;
        }

        Debug.LogWarning("[PartyGameplayInputAdapter] " + reason, this);
    }

    private void HandleMove(InputAction.CallbackContext context)
    {
        PartyParticipant leader = _party != null ? _party.Leader : null;
        if (leader == null || leader.id != _activeLeaderId || !ContextMatchesLeader(context, leader))
            return;

        playerController.OnMove(context);
    }

    private void HandlePause(InputAction.CallbackContext context)
    {
        if (!context.started) return;
        PartyParticipant leader = _party != null ? _party.Leader : null;
        if (leader == null || leader.id != _activeLeaderId || !ContextMatchesLeader(context, leader))
            return;

        playerController.OnPause(context);
    }

    private bool ContextMatchesLeader(InputAction.CallbackContext context, PartyParticipant leader)
    {
        if (context.control == null || leader == null || leader.inputAssignment == null)
            return false;

        InputAssignment assignment = leader.inputAssignment;
        if (playerInput.currentControlScheme != _activeScheme)
            return false;

        if (assignment.deviceKind == InputDeviceKind.Keyboard)
            return context.control.device is Keyboard && BindingHasGroup(context, _activeBindingGroup);

        if (assignment.deviceKind == InputDeviceKind.Gamepad)
        {
            return context.control.device is Gamepad &&
                   context.control.device.deviceId.ToString(CultureInfo.InvariantCulture) == assignment.deviceId &&
                   BindingHasGroup(context, _activeBindingGroup);
        }

        return false;
    }

    private static bool BindingHasGroup(InputAction.CallbackContext context, string expectedGroup)
    {
        if (string.IsNullOrWhiteSpace(expectedGroup) || context.action == null || context.control == null)
            return false;

        // CallbackContext doesn't expose the triggering binding directly in Input System 1.7.
        // Resolve the binding from the control that triggered this action instead.
        int bindingIndex = context.action.GetBindingIndexForControl(context.control);
        if (bindingIndex < 0 || bindingIndex >= context.action.bindings.Count)
            return false;

        string groups = context.action.bindings[bindingIndex].groups;
        if (string.IsNullOrWhiteSpace(groups)) return false;

        string[] values = groups.Split(';');
        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i].Trim(), expectedGroup, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
