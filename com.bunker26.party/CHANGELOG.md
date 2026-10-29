# Changelog

## 0.4.1 - Input System Callback Compatibility Fix
- Fixed the BA Runner integration sample to retrieve triggering binding groups through `InputAction.GetBindingIndexForControl` and `InputAction.bindings`, rather than the unavailable `CallbackContext.binding` property.

## 0.4.0 - Party Runtime, Leadership & Gameplay Input
- Added mutable leadership independent of `localSlotIndex`; `PartyService.TrySetLeader` validates local-human eligibility and usable input, then raises `PartyChanged`.
- Added `PartyRuntime`, a persistent owner for the active Party, profile and rebinding services so participants are preserved across scene loads.
- Updated Party Setup Demo to reuse `PartyRuntime` and added a contextual **Hacer líder** action to the generated demo UI.
- Added the optional BA Runner Gameplay Integration sample, isolated from the package assembly, for PlayerInput scheme/device switching, profile override loading, leader-filtered movement and leader-only pause.
- Added leadership transfer and invalid target tests.
- Added `GameInputDefinition`, which associates a stable game ID, the game's InputActionAsset, control-scheme settings and editable binding definitions.
- Added an Inspector action to populate editable bindings from the assigned InputActionAsset; composite headers are excluded and composite parts remain editable.
- Added interactive rebinding sessions using Unity Input System's rebinding operation, with keyboard/gamepad family filters, Escape cancellation and native operation cleanup.
- Added JSON persistence isolated by profile ID, game ID and control-scheme name.
- Added per-binding reset and reset-all-for-current-scheme operations.
- Added duplicate binding validation, configurable through `GameInputDefinition.allowDuplicateBindings`.
- Added a gameplay binder overload to load persisted overrides into a PlayerInput action instance.
- Extended the Party Setup Demo with a configurable controls editor panel, editable binding rows, reset-defaults action and sample InputActionAsset.
- Added persistence tests for profile/game/scheme isolation.

## 0.3.3 - 2026-10-28
- Input selector now marks the keyboard slot already assigned to the selected player as `Actual` and disables it.
- Keyboard slots owned by another participant are displayed as `En uso` and disabled.
- InputAssignmentService rejects reassigning the exact keyboard slot or gamepad already owned by the same participant, keeping the invariant outside the UI as well.
- Added tests for current-slot and other-player slot availability.

## 0.3.2 - 2026-10-28
- Player 1 now explicitly receives the first connected gamepad on startup, or Keyboard Slot 1 when no gamepad is connected.
- Gamepad joins report whether profile selection should open after input assignment.
- Newly created or previously unconfigured participants are routed to profile selection after a gamepad join.
- Existing participants who replace a disconnected gamepad keep their profile without being forced through the profile picker (unless still Unknown).
- The manual `+` flow remains: create local participant -> choose input -> choose/confirm profile.
- Cancelling input selection in the manual-add flow removes the incomplete participant instead of leaving an unconfigured slot behind.
- Preserved the previous `TryJoinWithGamepad` API overload for compatibility.
- Added unit tests for profile-selection decisions during gamepad join/reassignment.

## 0.3.1 - 2026-10-28
- Redesigned the Party demo HUD as a compact top-right row of player icons and an add button.
- Added a four-action player context panel for editing/changing profiles, assigning input, and viewing statistics.
- Added hover/selection scaling and contextual fade/scale transitions.
- Kept primary visual references Inspector-assigned in the generated scene and prefabs.

## 0.3.0
- Added Unity Input System device monitoring for gamepad connection changes and button-join behavior.
- Local Player 1 is initialized automatically and is the persistent leader slot.
- Added per-local-slot profile preferences.
- Added keyboard slot assignment and gamepad ownership validation.
- Added an Inspector-driven sample scene generator and profile UI.

## 0.2.0
- Added initial local Party and profile management sample.

## 0.1.0
- Initial Party core, profile repository and input assignment bookkeeping.
