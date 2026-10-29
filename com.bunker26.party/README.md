# Bunker 26 Party

Reusable Unity package for local Party management, persistent player profiles and Unity Input System device assignments.

## Installation

1. Extract the ZIP archive.
2. In Unity, open **Window > Package Management > Package Manager**.
3. Press **+ > Add package from disk...** and select `com.bunker26.party/package.json` from the extracted folder.
4. Import **Party Setup Demo** and **BA Runner Gameplay Integration** only as needed from the package's **Samples** section. The BA Runner integration sample is game-specific and should be imported only into BA Runner.

If you imported an earlier copy of the samples into `Assets/Samples/Bunker 26 Party/0.4.0`, update or reimport those copied sample files. UPM package files do not automatically overwrite sample scripts already copied into the project's `Assets` folder.

## Requirements

- Unity 2022.3 or newer.
- Unity Input System 1.7.0.
- Unity UI (uGUI) 1.0.0.

## Features

- Local Party and persistent profiles.
- First local player initialization and mutable Party leadership.
- Persistent Party session across Unity scene changes (`PartyRuntime`).
- Leader transfer with device/control-scheme rebinding and leader-only Story pause via the optional BA Runner integration sample.
- Keyboard logical slots and gamepad ownership.
- Gamepad connect/disconnect and button-to-join detection.
- Profile-specific, game-specific and control-scheme-specific binding overrides.
- Interactive rebinding sessions, per-binding reset and reset to defaults.
- Inspector-driven sample UI.

## Input customization architecture

Each game defines a `GameInputDefinition` ScriptableObject with:

- a stable `gameId` used by persistence;
- the game's `InputActionAsset`;
- a `PartyInputBindingSettings` reference;
- a list of editable bindings identified by stable Input System binding IDs.

Binding defaults remain in the game's InputActionAsset. The package persists only overrides to:

`Application.persistentDataPath/Bunker26/party_input_binding_overrides.json`

The persistence key is `(profileId, gameId, controlSchemeName)`, so a profile can use different controls in different games and different schemes without overwriting one another.

Select a `GameInputDefinition` asset in the Inspector and press **Refresh Editable Bindings From Input Actions** to populate the editable binding list. Bindings must have groups matching each control scheme's `bindingGroup`.

For generic gameplay integration, use `PartyPlayerInputBinder.TryBind(PlayerInput, PartyParticipant, GameInputDefinition, IInputBindingOverridesRepository, out error)` to pair a participant's device and apply saved overrides.

`PartyRuntime` owns the active `PartyService`, profile service and rebinding service across scene loads. Scene-specific UI controllers should retrieve it with `PartyRuntime.GetOrCreate(...)` rather than creating a new service on every scene.

Use `PartyService.TrySetLeader(participantId, out error)` to transfer leadership. A leader must be a local human with an assigned input; a disconnected gamepad cannot become leader. `PartyChanged` is raised after a successful transfer.

For BA Runner, import the optional **BA Runner Gameplay Integration** sample. Its controller-specific source is separate from the runtime assembly so the reusable package never references the game's `PlayerController` type. Follow that sample's README and remove duplicate `PlayerInput` Unity Event hookups for movement and pause.

## Sample

1. Import **Party Setup Demo** via Package Manager > Samples.
2. Run **Bunker 26 > Party > Generate Inspector-Driven Party Demo**.
3. Open `Assets/PartySetupDemoGenerated/Scenes/PartySetupDemo.unity`.
4. Select a player > **Asignar controles** > **Personalizar controles**.

See `Samples~/PartySetupDemo/README.md` for complete sample instructions.


### Updating to 0.4.1
If you already imported samples from version 0.4.0, remove the old copied folders under `Assets/Samples/Bunker 26 Party/0.4.0` before importing the 0.4.1 samples. Unity does not automatically overwrite sample files already copied into `Assets`.
