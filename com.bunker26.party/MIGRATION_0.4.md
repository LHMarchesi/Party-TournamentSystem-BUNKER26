# Migration notes — 0.4.x

## Leadership API

`PartyParticipant.IsLeader` is now backed by the mutable session field `isLeader`; do not assign this field from game code. Transfer leadership through `PartyService.TrySetLeader(participantId, out error)`. `localSlotIndex` remains the participant's local input slot and no longer defines leadership after initialization.

A transfer is rejected when the target is not a local human, has no assigned input, or has a disconnected gamepad. The current leader cannot be removed until leadership is transferred to another participant.

## Scene persistence

Use `PartyRuntime.GetOrCreate(maxLocalPlayers, createLocalPlayerOne)` from each scene-specific Party composition/UI controller. Do not instantiate fresh `PartyService`, `PlayerProfileService`, or `InputRebindingService` objects in each scene.

## BA Runner

Import the optional **BA Runner Gameplay Integration** sample and follow `Samples~/BARunnerIntegration/README.md`. Keep its `PartyGameplayInputAdapter.cs` in BA Runner's normal gameplay assembly, where `PlayerController` is available. It must not be moved into `Bunker26.Party.Runtime.asmdef`.

On the gameplay `PlayerInput`, remove duplicate Unity Event hooks from `Move_Player1` / `Move_Player2` to `PlayerController.OnMove` and from `Pause` to `PlayerController.OnPause`. The adapter owns those callbacks and filters them to the active leader.

The project InputActionAsset's control scheme names and binding groups must match the assigned `GameInputDefinition` / `PartyInputBindingSettings`. The sample's default fallback names are `Keyboard1`, `Keyboard2`, and `Gamepad`.


## 0.4.1 hotfix
- The BA Runner integration now resolves binding groups from the action's binding collection using the triggering control, compatible with Unity Input System 1.7.
- If you previously imported samples into `Assets/Samples/Bunker 26 Party/0.4.0`, remove the old imported `0.4.0` sample folders before importing the `0.4.1` samples. Imported sample copies are not automatically overwritten by UPM.
