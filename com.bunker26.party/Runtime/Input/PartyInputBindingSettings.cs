using UnityEngine;

namespace Bunker26.Party
{
    /// <summary>Control-scheme names used when pairing a game's PlayerInput to a Party participant.</summary>
    [CreateAssetMenu(menuName = "Bunker 26/Party/Input Binding Settings", fileName = "PartyInputBindingSettings")]
    public sealed class PartyInputBindingSettings : ScriptableObject
    {
        [Tooltip("Must match a control scheme in the game's InputActionAsset.")]
        public string gamepadControlScheme = "Gamepad";

        [Tooltip("Must match the first logical keyboard slot's control scheme in the game's InputActionAsset.")]
        public string keyboardSlot1ControlScheme = "KeyboardLeft";

        [Tooltip("Must match the second logical keyboard slot's control scheme in the game's InputActionAsset.")]
        public string keyboardSlot2ControlScheme = "KeyboardRight";
    }
}
