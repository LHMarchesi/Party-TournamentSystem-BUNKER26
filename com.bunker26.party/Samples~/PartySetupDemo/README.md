# Party Setup Demo (0.4.1)

This sample demonstrates Inspector-driven local Party UI, profile persistence, real Input System device ownership, per-game input definitions and interactive rebinding.

## Requirements

- Unity 2022.3 or newer.
- Input System 1.7.0 and uGUI 1.0.0 (declared package dependencies).
- Active Input Handling set to `Input System Package (New)` or `Both`.

## Setup

1. Import this sample from **Window > Package Manager > Bunker 26 Party > Samples**.
2. Run **Bunker 26 > Party > Generate Inspector-Driven Party Demo**.
3. Open `Assets/PartySetupDemoGenerated/Scenes/PartySetupDemo.unity` and press Play.
4. Select a local player, open **Asignar controles**, then choose **Personalizar controles**.
5. Open a participant's context panel and choose **Hacer líder** to transfer leadership to a local participant with a usable input assignment.

The demo session is owned by `PartyRuntime` and is reused when scene-specific Party views are recreated. For BA Runner's Story character and leader-only pause, import the separate **BA Runner Gameplay Integration** sample and follow its README.

The generator copies `PartyDemoInputActions.inputactions` into `Assets/PartySetupDemoGenerated/Settings`, creates `PartyInputBindingSettings` and `PartyDemoGameInputDefinition`, and assigns the definition to the sample controller.

## Rebinding workflow

- The sample action asset contains `KeyboardLeft`, `KeyboardRight` and `Gamepad` control schemes.
- Bindings are grouped by control scheme in the InputActionAsset.
- `PartyDemoGameInputDefinition` lists the bindings shown by the runtime rebinding UI.
- Select **Refresh Editable Bindings From Input Actions** in the GameInputDefinition Inspector after changing the asset's actions/bindings.
- A profile's overrides are saved separately by profile ID, game ID and control scheme at `Application.persistentDataPath/Bunker26/party_input_binding_overrides.json`.
- **Restaurar predeterminados** clears only the current profile/game/control-scheme overrides.
- The sample UI uses the profile's selected input assignment to decide which scheme's bindings to display.

## Integrating rebinding into a game

1. Create a `GameInputDefinition` asset for the game.
2. Assign the game's `InputActionAsset` and `PartyInputBindingSettings`.
3. Ensure that each editable binding has the correct binding group matching the control scheme's `bindingGroup`.
4. Use **Refresh Editable Bindings From Input Actions** to populate the list automatically; unneeded entries can be removed from the list.
5. Use `InputRebindingService` to open a session for a profile, game definition, control-scheme name, binding group and device family.
6. When binding gameplay `PlayerInput`, call the `PartyPlayerInputBinder.TryBind` overload that accepts `GameInputDefinition` and `IInputBindingOverridesRepository`. This applies the saved overrides to the gameplay player's private action instance.

Defaults remain authored in the game's InputActionAsset. Party stores only non-destructive binding overrides and does not hardcode game actions such as jump, attack or interact.

## Expected Party flow

- Player 1 is created at startup and receives the first connected gamepad, or Keyboard Slot 1 when no gamepad is connected.
- Select `+` to add another local player, choose input, then select/create a profile.
- A free gamepad can join by pressing a button.
- The four-action player panel allows editing/changing a profile, assigning input, and viewing statistics.

## Persistence

- Profiles: `Application.persistentDataPath/Bunker26/party_profiles.json`
- Preferred profile per local slot: `Application.persistentDataPath/Bunker26/party_player_preferences.json`
- Binding overrides: `Application.persistentDataPath/Bunker26/party_input_binding_overrides.json`

## Limitations

The sample's demo `InputActionAsset` is for proving rebinding/persistence. A real game must assign its own InputActionAsset, action maps, control schemes and binding groups. The package cannot infer gameplay actions from a game that has not declared them.
