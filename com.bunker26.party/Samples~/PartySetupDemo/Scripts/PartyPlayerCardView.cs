using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bunker26.Party.Samples
{
    /// <summary>
    /// Compact player icon used in the party HUD. The root Button is assigned in the Inspector.
    /// Pointer hover, EventSystem selection and contextual selection share the same gentle scale effect.
    /// </summary>
    public sealed class PartyPlayerCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Main Icon")]
        public Button selectButton;
        public Image profileImage;
        public Image inputImage;
        public Image leaderBadgeImage;
        public Image connectionStatusImage;

        [Header("Optional Labels")]
        public Text playerSlotLabel;
        public Text profileNameLabel;
        public Text inputLabel;
        public Text connectionLabel;

        [Header("Optional Direct Actions")]
        public Button changeProfileButton;
        public Button changeInputButton;
        public Button removePlayerButton;

        [Header("Interaction Feel")]
        [Min(1f)] public float hoverScale = 1.12f;
        [Min(1f)] public float selectedScale = 1.07f;
        [Min(1f)] public float scaleSmoothing = 16f;

        private Vector3 _baseScale = Vector3.one;
        private bool _pointerInside;
        private bool _selectedByNavigation;
        private bool _selectedByContext;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            bool highlighted = _pointerInside || _selectedByNavigation || _selectedByContext;
            float multiplier = _pointerInside ? hoverScale : highlighted ? selectedScale : 1f;
            Vector3 target = _baseScale * multiplier;
            float blend = 1f - Mathf.Exp(-Mathf.Max(1f, scaleSmoothing) * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, target, blend);
        }

        public void OnPointerEnter(PointerEventData eventData) { _pointerInside = true; }
        public void OnPointerExit(PointerEventData eventData) { _pointerInside = false; }
        public void OnSelect(BaseEventData eventData) { _selectedByNavigation = true; }
        public void OnDeselect(BaseEventData eventData) { _selectedByNavigation = false; }

        public void SetContextSelected(bool selected)
        {
            _selectedByContext = selected;
        }

        public void Bind(PartyParticipant participant, PlayerProfile profile, PartyIconCatalog icons,
            Action onSelect, Action onChangeProfile, Action onChangeInput, Action onRemove)
        {
            if (participant == null) return;

            if (profileImage != null)
            {
                profileImage.sprite = icons == null
                    ? null
                    : profile != null ? icons.GetProfileIcon(profile.iconId) : icons.unknownProfile;
                profileImage.preserveAspect = true;
            }

            if (inputImage != null)
            {
                inputImage.sprite = icons != null ? icons.GetInputIcon(participant.inputAssignment) : null;
                inputImage.preserveAspect = true;
                inputImage.enabled = inputImage.sprite != null;
            }

            if (leaderBadgeImage != null)
            {
                leaderBadgeImage.sprite = icons != null ? icons.leader : null;
                leaderBadgeImage.preserveAspect = true;
                leaderBadgeImage.gameObject.SetActive(participant.IsLeader && leaderBadgeImage.sprite != null);
            }

            if (playerSlotLabel != null)
                playerSlotLabel.text = participant.type == PartyParticipantType.LocalHuman
                    ? "JUGADOR " + participant.localSlotIndex.ToString("00")
                    : participant.type.ToString().ToUpperInvariant();
            if (profileNameLabel != null) profileNameLabel.text = profile != null ? profile.displayName : participant.DisplayName;
            if (inputLabel != null) inputLabel.text = GetInputLabel(participant.inputAssignment);

            bool isGamepad = participant.inputAssignment != null && participant.inputAssignment.deviceKind == InputDeviceKind.Gamepad;
            bool connected = participant.inputAssignment != null && participant.inputAssignment.isConnected;
            if (connectionStatusImage != null)
            {
                connectionStatusImage.sprite = icons != null ? (connected ? icons.connected : icons.disconnected) : null;
                bool hasAssignedDevice = participant.inputAssignment != null &&
                    (participant.inputAssignment.deviceKind == InputDeviceKind.Gamepad || participant.inputAssignment.deviceKind == InputDeviceKind.Keyboard);
                connectionStatusImage.gameObject.SetActive(hasAssignedDevice && connectionStatusImage.sprite != null);
            }
            if (connectionLabel != null)
            {
                bool inputAssigned = participant.inputAssignment != null && participant.inputAssignment.IsAssigned;
                connectionLabel.text = !inputAssigned ? "Sin input" :
                    (isGamepad && !connected ? "Mando desconectado" : "Conectado");
            }

            ConfigureButton(selectButton, onSelect);
            ConfigureButton(changeProfileButton, onChangeProfile);
            ConfigureButton(changeInputButton, onChangeInput);
            ConfigureButton(removePlayerButton, onRemove);
            if (removePlayerButton != null) removePlayerButton.gameObject.SetActive(!participant.IsLeader);
        }

        private static string GetInputLabel(InputAssignment assignment)
        {
            if (assignment == null || !assignment.IsAssigned) return "Sin input";
            if (assignment.deviceKind == InputDeviceKind.Keyboard) return "Teclado " + (assignment.keyboardSlot + 1);
            if (assignment.deviceKind == InputDeviceKind.Gamepad)
                return string.IsNullOrWhiteSpace(assignment.deviceDisplayName) ? "Gamepad" : assignment.deviceDisplayName;
            return assignment.deviceKind.ToString();
        }

        private static void ConfigureButton(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }
    }
}
