# BA Runner Gameplay Input Integration

This is an **optional, game-specific** integration sample for BA Runner. It is intentionally not compiled into `Bunker26.Party.Runtime`: the script references BA Runner's `PlayerController`, so it must live in the game's default gameplay assembly.

## Import

1. In Unity, open **Window > Package Management > Package Manager** and select **Bunker 26 Party**.
2. Expand **Samples** and import **BA Runner Gameplay Integration**. Import this sample only into BA Runner; it references BA Runner's `PlayerController` type.
3. The imported script should compile in the game's default assembly because this optional sample has no package `.asmdef`. If your `PlayerController` lives in a custom assembly, move/copy the adapter into an assembly that references the same gameplay assembly and `Bunker26.Party.Runtime`.
4. Attach it to the same GameObject as the Story-mode `PlayerInput` and `PlayerController`.
5. Assign both references. If you use Party's per-profile input rebinding, assign the same `GameInputDefinition` asset used by Party Setup.

## Input Actions / Inspector

- The PlayerInput asset must have a `Player` map and schemes named to match the `GameInputDefinition`'s `PartyInputBindingSettings`. Without a `GameInputDefinition`, the defaults are `Keyboard1`, `Keyboard2`, and `Gamepad`.
- Default movement actions in the Inspector are `Move_Player1` for Keyboard 1 and Gamepad, and `Move_Player2` for Keyboard 2. Change these names if BA Runner's asset differs.
- The pause action is `Pause`.
- Keep the binding `groups` aligned with their control schemes (or their `bindingGroup` values when using `GameInputDefinition`).
- On the gameplay `PlayerInput` Inspector, remove only the existing Unity Event entries that connect `Move_Player1` or `Move_Player2` to `PlayerController.OnMove`, and `Pause` to `PlayerController.OnPause`. The adapter subscribes to these action callbacks itself. Keep unrelated action events wired as before.
- `PlayerInput` should remain on the `Player` map. The adapter disables input if there is no leader with a connected, assigned device, then reactivates it after valid leadership/input returns.

## Behavior

- The current Party leader's input assignment determines the `PlayerInput` control scheme and device pairing.
- Changing the leader through Party Setup triggers an immediate rebind without restarting Story or recreating the gameplay character.
- Movement and pause callbacks are accepted only from the active leader's binding group/device. Other gameplay actions remain filtered by the active `PlayerInput` control scheme.
- If enabled and `GameInputDefinition` is assigned, the adapter loads stored overrides by `(profileId, gameId, control scheme)` after clearing the previous profile's overrides from this PlayerInput instance.
- This adapter targets the BA Runner `PlayerController` API used by this sample: `SetHorizontalInput(float)`, `OnMove(InputAction.CallbackContext)`, and `OnPause(InputAction.CallbackContext)`. If those signatures change, update this integration sample, not Party core.

Do not use this integration in a different game without adapting its target controller; the core Party package remains game-agnostic.
