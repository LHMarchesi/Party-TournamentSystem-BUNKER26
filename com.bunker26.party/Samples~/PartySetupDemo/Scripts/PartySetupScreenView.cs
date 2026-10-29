using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker26.Party.Samples
{
    /// <summary>
    /// Inspector-driven sample presentation. The Canvas and panels are authored in the Editor;
    /// this class only binds data and spawns reusable, Inspector-assigned card prefabs.
    /// </summary>
    public sealed class PartySetupScreenView : MonoBehaviour
    {
        [Header("Main Screen")]
        public Text partySummaryText;
        public Text connectionHintText;
        public Text statusText;
        public Transform playerCardsRoot;
        public PartyPlayerCardView playerCardPrefab;
        public Button addLocalPlayerButton;
        public Button openProfileLibraryButton;
        public Image profileLibraryHeaderIcon;

        [Header("Player Context Menu")]
        public GameObject playerContextPanel;
        public RectTransform playerContextPanelRect;
        public Image contextProfileIcon;
        public Text contextPlayerNameText;
        public Text contextPlayerInputText;
        public Button contextEditProfileButton;
        public Button contextChangeProfileButton;
        public Button contextInputButton;
        public Button contextStatisticsButton;
        public Button contextMakeLeaderButton;
        public Button closePlayerContextButton;

        [Header("Profile Library")]
        public GameObject profileLibraryPanel;
        public Transform profileCardsRoot;
        public PartyProfileCardView profileCardPrefab;
        public Text profileLibrarySummaryText;
        public Button createProfileButton;
        public Button closeProfileLibraryButton;

        [Header("Profile Editor")]
        public GameObject profileEditorPanel;
        public Text profileEditorTitleText;
        public InputField profileNameInput;
        public Image profileIconPreview;
        public Button previousProfileIconButton;
        public Button nextProfileIconButton;
        public Button saveProfileButton;
        public Button cancelProfileButton;

        [Header("Profile Details")]
        public GameObject profileDetailsPanel;
        public Image detailsProfileIcon;
        public Text detailsProfileNameText;
        public Text detailsProfileStatsText;
        public Button detailsAssignButton;
        public Button detailsEditButton;
        public Button closeProfileDetailsButton;

        [Header("Input Selector")]
        public GameObject inputSelectorPanel;
        public Text inputTargetText;
        public Text inputDeviceSummaryText;
        public Button keyboardSlot1Button;
        public Button keyboardSlot2Button;
        public Button releaseInputButton;
        public Button closeInputSelectorButton;

        [Header("Input Binding Editor")]
        public Button customizeInputBindingsButton;
        public GameObject inputBindingsPanel;
        public Text inputBindingsHeadingText;
        public Text inputBindingsStatusText;
        public Transform inputBindingRowsRoot;
        public PartyInputBindingRowView inputBindingRowPrefab;
        public Button resetInputBindingsButton;
        public Button closeInputBindingsButton;

        private PartySetupController _controller;
        private PartyIconCatalog _icons;
        private readonly List<PartyPlayerCardView> _playerCards = new List<PartyPlayerCardView>();
        private readonly List<PartyProfileCardView> _profileCards = new List<PartyProfileCardView>();
        private readonly List<PartyInputBindingRowView> _inputBindingRows = new List<PartyInputBindingRowView>();
        private InputRebindingSession _activeBindingSession;
        private string _bindingTargetParticipantId = string.Empty;
        private string _targetParticipantId = string.Empty;
        private string _pendingInputTargetId = string.Empty;
        private string _editingProfileId = string.Empty;
        private string _detailsProfileId = string.Empty;
        private int _selectedIconIndex;
        private PartyPlayerCardView _contextCard;
        private bool _openProfileSelectionAfterInput;
        private bool _removeParticipantIfInputCancelled;
        private readonly Dictionary<GameObject, Coroutine> _panelTransitions = new Dictionary<GameObject, Coroutine>();
        private readonly Dictionary<GameObject, Vector3> _panelBaseScales = new Dictionary<GameObject, Vector3>();

        public string PendingInputTargetId { get { return _pendingInputTargetId; } }
        public bool IsModalPanelOpen
        {
            get
            {
                return IsActive(playerContextPanel) || IsActive(profileLibraryPanel) || IsActive(profileEditorPanel) ||
                       IsActive(profileDetailsPanel) || IsActive(inputSelectorPanel) || IsActive(inputBindingsPanel);
            }
        }

        public void Initialize(PartySetupController controller, PartyIconCatalog icons)
        {
            _controller = controller;
            _icons = icons;
            WireButton(addLocalPlayerButton, AddLocalPlayer);
            WireButton(openProfileLibraryButton, () => ShowProfileLibrary(string.Empty));
            WireButton(contextEditProfileButton, EditContextProfile);
            WireButton(contextChangeProfileButton, () => ShowProfileLibrary(_targetParticipantId));
            WireButton(contextInputButton, () => ShowInputSelector(_targetParticipantId, false));
            WireButton(contextStatisticsButton, ShowContextStatistics);
            WireButton(contextMakeLeaderButton, MakeContextLeader);
            WireButton(closePlayerContextButton, ClosePlayerContextMenu);
            WireButton(createProfileButton, BeginCreateProfile);
            WireButton(closeProfileLibraryButton, () => SetPanelActive(profileLibraryPanel, false));
            WireButton(previousProfileIconButton, () => CycleProfileIcon(-1));
            WireButton(nextProfileIconButton, () => CycleProfileIcon(1));
            WireButton(saveProfileButton, SaveProfileEditor);
            WireButton(cancelProfileButton, CancelProfileEditor);
            WireButton(detailsAssignButton, AssignDetailsProfile);
            WireButton(detailsEditButton, EditDetailsProfile);
            WireButton(closeProfileDetailsButton, () => SetPanelActive(profileDetailsPanel, false));
            WireButton(keyboardSlot1Button, () => AssignKeyboardSlot(0));
            WireButton(keyboardSlot2Button, () => AssignKeyboardSlot(1));
            WireButton(releaseInputButton, ReleaseInput);
            WireButton(closeInputSelectorButton, CloseInputSelector);
            WireButton(customizeInputBindingsButton, OpenInputBindingsEditor);
            WireButton(resetInputBindingsButton, ResetInputBindings);
            WireButton(closeInputBindingsButton, CloseInputBindingsEditor);

            HidePanelImmediately(playerContextPanel);
            HidePanelImmediately(profileLibraryPanel);
            HidePanelImmediately(profileEditorPanel);
            HidePanelImmediately(profileDetailsPanel);
            HidePanelImmediately(inputSelectorPanel);
            HidePanelImmediately(inputBindingsPanel);
            if (profileLibraryHeaderIcon != null && _icons != null) profileLibraryHeaderIcon.sprite = _icons.profiles;
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_activeBindingSession != null)
            {
                _activeBindingSession.Dispose();
                _activeBindingSession = null;
            }
        }

        public void RefreshAll()
        {
            if (_controller == null || _controller.Party == null) return;
            RefreshPlayerCards();
            if (IsActive(profileLibraryPanel)) RefreshProfileCards();
            RefreshDeviceStatus();
            if (partySummaryText != null)
            {
                int localCount = 0;
                var participants = _controller.Party.Participants;
                for (int i = 0; i < participants.Count; i++)
                    if (participants[i].type == PartyParticipantType.LocalHuman) localCount++;
                partySummaryText.text = localCount + " jugador(es) local(es) · " + _controller.Party.MaxLocalPlayers + " slots máximos";
            }
        }

        public void RefreshDeviceStatus()
        {
            string summary = _controller != null ? _controller.GetConnectedGamepadSummary() : string.Empty;
            if (connectionHintText != null)
            {
                if (string.IsNullOrEmpty(summary) || summary.StartsWith("No hay mandos", StringComparison.Ordinal))
                    connectionHintText.text = "Conectá un mando y presioná cualquier botón para unirte";
                else if (summary.IndexOf("libre", StringComparison.OrdinalIgnoreCase) >= 0)
                    connectionHintText.text = "Hay un mando disponible · presioná un botón para unirte";
                else
                    connectionHintText.text = "Todos los mandos conectados están asignados";
            }
            if (inputDeviceSummaryText != null) inputDeviceSummaryText.text = summary;
            RefreshKeyboardButtons();
        }

        public void SetStatus(string message, bool isError)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.color = isError ? new Color32(255, 143, 143, 255) : new Color32(173, 224, 196, 255);
        }

        public void CloseInputSelector()
        {
            string participantId = _pendingInputTargetId;
            bool cancelNewParticipant = _removeParticipantIfInputCancelled;
            _pendingInputTargetId = string.Empty;
            _openProfileSelectionAfterInput = false;
            _removeParticipantIfInputCancelled = false;
            SetPanelActive(inputSelectorPanel, false);

            if (cancelNewParticipant && !string.IsNullOrEmpty(participantId))
            {
                _controller.RemoveParticipant(participantId);
                _controller.SetStatus("Incorporación cancelada.", false);
            }
            RefreshAll();
        }

        public void CompleteInputAssignment(string participantId)
        {
            bool openProfileSelection = _openProfileSelectionAfterInput;
            _openProfileSelectionAfterInput = false;
            _removeParticipantIfInputCancelled = false;
            _pendingInputTargetId = string.Empty;
            SetPanelActive(inputSelectorPanel, false);
            RefreshAll();
            if (openProfileSelection) OpenProfileSelection(participantId);
        }

        private void RefreshPlayerCards()
        {
            ClearPlayerCards();
            if (playerCardsRoot == null || playerCardPrefab == null) return;
            var participants = _controller.Party.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                PartyParticipant participant = participants[i];
                PlayerProfile profile = string.IsNullOrEmpty(participant.profileId) ? null : _controller.Profiles.Get(participant.profileId);
                PartyPlayerCardView card = Instantiate(playerCardPrefab, playerCardsRoot);
                card.gameObject.SetActive(true);
                string participantId = participant.id;
                card.Bind(participant, profile, _icons,
                    () => ShowPlayerContextMenu(participantId, card),
                    null,
                    null,
                    () => RemoveParticipant(participantId));
                _playerCards.Add(card);
            }
        }

        private void RefreshProfileCards()
        {
            ClearProfileCards();
            if (profileCardsRoot == null || profileCardPrefab == null || _controller == null) return;
            IReadOnlyList<PlayerProfile> profiles = _controller.Profiles.GetAll();
            if (profileLibrarySummaryText != null) profileLibrarySummaryText.text = profiles.Count + " perfil(es) guardado(s)";

            for (int i = 0; i < profiles.Count; i++)
            {
                PlayerProfile profile = profiles[i];
                if (profile == null) continue;
                PartyProfileCardView card = Instantiate(profileCardPrefab, profileCardsRoot);
                card.gameObject.SetActive(true);
                string profileId = profile.id;
                bool assignedElsewhere = _controller.IsProfileAssignedToOtherParticipant(profileId, _targetParticipantId);
                card.Bind(profile, _icons, assignedElsewhere,
                    () => AssignProfileFromLibrary(profileId),
                    () => ShowProfileDetails(profileId),
                    () => BeginEditProfile(profileId));
                if (card.assignButton != null && string.IsNullOrEmpty(_targetParticipantId)) card.assignButton.interactable = false;
                _profileCards.Add(card);
            }
        }

        private void ClearPlayerCards()
        {
            for (int i = 0; i < _playerCards.Count; i++)
                if (_playerCards[i] != null) Destroy(_playerCards[i].gameObject);
            _playerCards.Clear();
        }

        private void ClearProfileCards()
        {
            for (int i = 0; i < _profileCards.Count; i++)
                if (_profileCards[i] != null) Destroy(_profileCards[i].gameObject);
            _profileCards.Clear();
        }

        private void ShowPlayerContextMenu(string participantId, PartyPlayerCardView card)
        {
            if (_controller == null || string.IsNullOrEmpty(participantId)) return;
            PartyParticipant participant = _controller.Party.GetParticipant(participantId);
            if (participant == null) return;

            if (_contextCard != null && _contextCard != card) _contextCard.SetContextSelected(false);
            _contextCard = card;
            if (_contextCard != null) _contextCard.SetContextSelected(true);
            _targetParticipantId = participantId;
            _pendingInputTargetId = string.Empty;

            PlayerProfile profile = string.IsNullOrEmpty(participant.profileId) ? null : _controller.Profiles.Get(participant.profileId);
            if (contextProfileIcon != null)
            {
                contextProfileIcon.sprite = _icons != null
                    ? (profile != null ? _icons.GetProfileIcon(profile.iconId) : _icons.unknownProfile)
                    : null;
                contextProfileIcon.preserveAspect = true;
            }
            if (contextPlayerNameText != null) contextPlayerNameText.text = profile != null ? profile.displayName : "Unknown";
            if (contextPlayerInputText != null) contextPlayerInputText.text = GetParticipantInputLabel(participant);
            if (contextStatisticsButton != null) contextStatisticsButton.interactable = profile != null;
            if (contextMakeLeaderButton != null)
            {
                contextMakeLeaderButton.gameObject.SetActive(!participant.IsLeader);
                bool hasUsableInput = participant.inputAssignment != null &&
                    (participant.inputAssignment.deviceKind == InputDeviceKind.Keyboard ||
                     (participant.inputAssignment.deviceKind == InputDeviceKind.Gamepad && participant.inputAssignment.isConnected));
                contextMakeLeaderButton.interactable = hasUsableInput;
            }
            PositionContextPanel(card);
            SetPanelActive(playerContextPanel, true);
        }

        private void MakeContextLeader()
        {
            if (_controller == null || string.IsNullOrEmpty(_targetParticipantId)) return;

            string participantId = _targetParticipantId;
            ClosePlayerContextMenu();

            string error;
            if (!_controller.TrySetLeader(participantId, out error))
            {
                SetStatus(error, true);
                return;
            }

            RefreshAll();
        }

        private static string GetParticipantInputLabel(PartyParticipant participant)
        {
            InputAssignment assignment = participant != null ? participant.inputAssignment : null;
            if (assignment == null || !assignment.IsAssigned) return "Sin controles asignados";
            if (assignment.deviceKind == InputDeviceKind.Keyboard) return "Teclado " + (assignment.keyboardSlot + 1);
            if (assignment.deviceKind == InputDeviceKind.Gamepad)
            {
                if (!assignment.isConnected) return "Mando desconectado";
                return string.IsNullOrWhiteSpace(assignment.deviceDisplayName) ? "Gamepad conectado" : assignment.deviceDisplayName;
            }
            return assignment.deviceKind.ToString();
        }

        private void PositionContextPanel(PartyPlayerCardView card)
        {
            if (playerContextPanelRect == null || card == null) return;
            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, card.transform.position);
            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out localPosition)) return;

            playerContextPanelRect.pivot = new Vector2(1f, 1f);
            Vector2 desired = localPosition + new Vector2(-20f, -36f);
            Rect canvasBounds = canvasRect.rect;
            float panelWidth = playerContextPanelRect.rect.width;
            float panelHeight = playerContextPanelRect.rect.height;
            desired.x = Mathf.Clamp(desired.x, canvasBounds.xMin + panelWidth + 16f, canvasBounds.xMax - 16f);
            desired.y = Mathf.Clamp(desired.y, canvasBounds.yMin + panelHeight + 16f, canvasBounds.yMax - 16f);
            playerContextPanelRect.anchoredPosition = desired;
        }

        private void EditContextProfile()
        {
            string participantId = _targetParticipantId;
            PartyParticipant participant = _controller != null ? _controller.Party.GetParticipant(participantId) : null;
            PlayerProfile profile = participant != null && !string.IsNullOrEmpty(participant.profileId)
                ? _controller.Profiles.Get(participant.profileId)
                : null;
            ClosePlayerContextMenu();
            if (profile == null) BeginCreateProfile();
            else BeginEditProfile(profile.id);
        }

        private void ShowContextStatistics()
        {
            PartyParticipant participant = _controller != null ? _controller.Party.GetParticipant(_targetParticipantId) : null;
            if (participant == null || string.IsNullOrEmpty(participant.profileId))
            {
                SetStatus("Este jugador todavía no tiene un perfil con estadísticas.", false);
                return;
            }
            ShowProfileDetails(participant.profileId);
        }

        private void ClosePlayerContextMenu()
        {
            if (_contextCard != null) _contextCard.SetContextSelected(false);
            _contextCard = null;
            SetPanelActive(playerContextPanel, false);
        }

        private void AddLocalPlayer()
        {
            PartyParticipant participant;
            string error;
            if (!_controller.TryAddLocalPlayer(out participant, out error)) return;
            _targetParticipantId = string.Empty;
            ShowInputSelector(participant.id, true, true);
        }

        private void RemoveParticipant(string participantId)
        {
            if (_controller.RemoveParticipant(participantId)) RefreshAll();
        }

        public void OpenProfileSelection(string participantId)
        {
            if (string.IsNullOrEmpty(participantId)) return;
            _openProfileSelectionAfterInput = false;
            ShowProfileLibrary(participantId);
        }

        private void ShowProfileLibrary(string targetParticipantId)
        {
            if (IsActive(playerContextPanel)) ClosePlayerContextMenu();
            _pendingInputTargetId = string.Empty;
            _targetParticipantId = targetParticipantId ?? string.Empty;
            SetPanelActive(profileLibraryPanel, true);
            SetPanelActive(profileDetailsPanel, false);
            RefreshProfileCards();
            if (profileLibrarySummaryText != null && !string.IsNullOrEmpty(_targetParticipantId))
            {
                PartyParticipant target = _controller.Party.GetParticipant(_targetParticipantId);
                profileLibrarySummaryText.text = "Elegí un perfil para " + (target != null ? target.DisplayName : "el jugador") + ".";
            }
        }

        private void BeginCreateProfile()
        {
            if (IsActive(playerContextPanel)) ClosePlayerContextMenu();
            _editingProfileId = string.Empty;
            _selectedIconIndex = 0;
            if (profileEditorTitleText != null) profileEditorTitleText.text = "Crear perfil";
            if (profileNameInput != null) profileNameInput.text = string.Empty;
            UpdateProfileIconPreview();
            SetPanelActive(profileEditorPanel, true);
            if (profileNameInput != null) profileNameInput.ActivateInputField();
        }

        private void BeginEditProfile(string profileId)
        {
            if (IsActive(playerContextPanel)) ClosePlayerContextMenu();
            PlayerProfile profile = _controller.Profiles.Get(profileId);
            if (profile == null) { SetStatus("El perfil ya no existe.", true); RefreshAll(); return; }
            _editingProfileId = profile.id;
            _selectedIconIndex = _icons != null ? _icons.FindProfileIconIndex(profile.iconId) : 0;
            if (profileEditorTitleText != null) profileEditorTitleText.text = "Editar perfil";
            if (profileNameInput != null) profileNameInput.text = profile.displayName;
            UpdateProfileIconPreview();
            SetPanelActive(profileEditorPanel, true);
        }

        private void SaveProfileEditor()
        {
            string name = profileNameInput != null ? profileNameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(name)) { SetStatus("Ingresá un nombre para el perfil.", true); return; }
            if (_icons == null || _icons.ProfileIconCount == 0) { SetStatus("Asigná al menos un icono de perfil en PartyIconCatalog.", true); return; }

            string iconId = _icons.GetProfileIconId(_selectedIconIndex);
            if (string.IsNullOrEmpty(_editingProfileId))
            {
                PlayerProfile created;
                try { created = _controller.CreateProfile(name, iconId); }
                catch (Exception exception) { SetStatus("No se pudo crear el perfil: " + exception.Message, true); return; }
                if (!string.IsNullOrEmpty(_targetParticipantId))
                {
                    string error;
                    if (!_controller.TryAssignProfile(_targetParticipantId, created.id, out error))
                    {
                        SetStatus("El perfil se guardó, pero no se pudo asignar: " + error, true);
                        SetPanelActive(profileEditorPanel, false);
                        RefreshAll();
                        return;
                    }
                }
                SetStatus(string.IsNullOrEmpty(_targetParticipantId) ? "Perfil creado y guardado." : "Perfil creado y asignado.", false);
            }
            else
            {
                PlayerProfile profile = _controller.Profiles.Get(_editingProfileId);
                if (profile == null) { SetStatus("El perfil ya no existe.", true); SetPanelActive(profileEditorPanel, false); return; }
                profile.displayName = name;
                profile.iconId = iconId;
                string error;
                if (!_controller.SaveProfile(profile, out error)) return;
            }

            SetPanelActive(profileEditorPanel, false);
            RefreshAll();
        }

        private void CancelProfileEditor()
        {
            _editingProfileId = string.Empty;
            SetPanelActive(profileEditorPanel, false);
        }

        private void CycleProfileIcon(int direction)
        {
            if (_icons == null || _icons.ProfileIconCount == 0) return;
            _selectedIconIndex = (_selectedIconIndex + direction + _icons.ProfileIconCount) % _icons.ProfileIconCount;
            UpdateProfileIconPreview();
        }

        private void UpdateProfileIconPreview()
        {
            if (profileIconPreview == null || _icons == null || _icons.ProfileIconCount == 0) return;
            profileIconPreview.sprite = _icons.GetProfileIcon(_icons.GetProfileIconId(_selectedIconIndex));
            profileIconPreview.preserveAspect = true;
        }

        private void AssignProfileFromLibrary(string profileId)
        {
            if (string.IsNullOrEmpty(_targetParticipantId))
            {
                SetStatus("Seleccioná primero un jugador para asignarle un perfil.", true);
                return;
            }
            string error;
            if (_controller.TryAssignProfile(_targetParticipantId, profileId, out error))
            {
                SetPanelActive(profileLibraryPanel, false);
                SetPanelActive(profileDetailsPanel, false);
                RefreshAll();
            }
        }

        private void ShowProfileDetails(string profileId)
        {
            if (IsActive(playerContextPanel)) ClosePlayerContextMenu();
            PlayerProfile profile = _controller.Profiles.Get(profileId);
            if (profile == null) return;
            _detailsProfileId = profileId;
            if (detailsProfileIcon != null)
            {
                detailsProfileIcon.sprite = _icons != null ? _icons.GetProfileIcon(profile.iconId) : null;
                detailsProfileIcon.preserveAspect = true;
            }
            if (detailsProfileNameText != null) detailsProfileNameText.text = profile.displayName;
            if (detailsProfileStatsText != null)
            {
                PlayerStatistics stats = profile.statistics ?? new PlayerStatistics();
                detailsProfileStatsText.text =
                    "Torneos jugados: " + stats.tournamentsPlayed + "\n" +
                    "Torneos ganados: " + stats.tournamentsWon + "\n" +
                    "Matches jugados: " + stats.matchesPlayed + "\n" +
                    "Matches ganados: " + stats.matchesWon + "\n" +
                    "Partidas jugadas: " + stats.gamesPlayed + "\n" +
                    "Partidas ganadas: " + stats.gamesWon;
            }
            if (detailsAssignButton != null) detailsAssignButton.interactable = !string.IsNullOrEmpty(_targetParticipantId);
            SetPanelActive(profileDetailsPanel, true);
        }

        private void AssignDetailsProfile()
        {
            if (string.IsNullOrEmpty(_targetParticipantId))
            {
                SetStatus("Para asignar el perfil, abrí el selector desde la tarjeta del jugador.", true);
                return;
            }
            string error;
            if (_controller.TryAssignProfile(_targetParticipantId, _detailsProfileId, out error))
            {
                SetPanelActive(profileDetailsPanel, false);
                SetPanelActive(profileLibraryPanel, false);
                RefreshAll();
            }
        }

        private void EditDetailsProfile()
        {
            SetPanelActive(profileDetailsPanel, false);
            BeginEditProfile(_detailsProfileId);
        }

        private void ShowInputSelector(string participantId, bool openProfileSelectionAfterInput, bool removeParticipantIfCancelled = false)
        {
            if (IsActive(playerContextPanel)) ClosePlayerContextMenu();
            _pendingInputTargetId = participantId;
            _openProfileSelectionAfterInput = openProfileSelectionAfterInput;
            _removeParticipantIfInputCancelled = removeParticipantIfCancelled;
            PartyParticipant participant = _controller.Party.GetParticipant(participantId);
            if (inputTargetText != null)
                inputTargetText.text = "Controles de " + (participant != null ? participant.DisplayName : "jugador") + " · elegí un slot o presioná un botón del mando que quieras asignar.";
            SetPanelActive(inputSelectorPanel, true);
            RefreshDeviceStatus();
            RefreshCustomizeBindingsButton();
        }

        private void RefreshCustomizeBindingsButton()
        {
            if (customizeInputBindingsButton == null) return;
            PartyParticipant participant = _controller != null && !string.IsNullOrEmpty(_pendingInputTargetId)
                ? _controller.Party.GetParticipant(_pendingInputTargetId) : null;
            bool canCustomize = participant != null && !string.IsNullOrWhiteSpace(participant.profileId) &&
                                participant.inputAssignment != null && participant.inputAssignment.IsAssigned &&
                                (participant.inputAssignment.deviceKind != InputDeviceKind.Gamepad || participant.inputAssignment.isConnected) &&
                                _controller.GameInputDefinition != null;
            customizeInputBindingsButton.interactable = canCustomize;
        }

        private void OpenInputBindingsEditor()
        {
            if (_controller == null || string.IsNullOrEmpty(_pendingInputTargetId)) return;
            PartyParticipant participant = _controller.Party.GetParticipant(_pendingInputTargetId);
            if (participant == null) { _controller.SetStatus("No se encontró el jugador seleccionado.", true); return; }

            if (_activeBindingSession != null) _activeBindingSession.Dispose();
            _activeBindingSession = null;
            string error;
            if (!_controller.TryCreateInputRebindingSession(participant.id, out _activeBindingSession, out error))
            {
                SetInputBindingEditorStatus(error, true);
                _controller.SetStatus(error, true);
                return;
            }

            _bindingTargetParticipantId = participant.id;
            PlayerProfile profile = _controller.Profiles.Get(participant.profileId);
            string gameName = _controller.GameInputDefinition != null && !string.IsNullOrWhiteSpace(_controller.GameInputDefinition.displayName)
                ? _controller.GameInputDefinition.displayName : "Controles";
            if (inputBindingsHeadingText != null)
                inputBindingsHeadingText.text = (profile != null ? profile.displayName : participant.DisplayName) + " · " + gameName;
            SetInputBindingEditorStatus("Seleccioná Cambiar y presioná la tecla o botón deseado. Escape cancela.", false);
            RefreshInputBindingRows();
            SetPanelActive(inputBindingsPanel, true);
        }

        private void RefreshInputBindingRows()
        {
            if (inputBindingRowsRoot == null || inputBindingRowPrefab == null || _activeBindingSession == null) return;
            for (int i = _inputBindingRows.Count - 1; i >= 0; i--)
            {
                PartyInputBindingRowView row = _inputBindingRows[i];
                if (row == null) continue;
                if (Application.isPlaying) Destroy(row.gameObject); else DestroyImmediate(row.gameObject);
            }
            _inputBindingRows.Clear();

            IReadOnlyList<InputBindingDisplayInfo> bindings = _activeBindingSession.GetBindings();
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBindingDisplayInfo binding = bindings[i];
                PartyInputBindingRowView row = Instantiate(inputBindingRowPrefab, inputBindingRowsRoot);
                row.name = "Binding - " + binding.actionName + " - " + binding.bindingId;
                row.Configure(binding, BeginRebindBinding);
                _inputBindingRows.Add(row);
            }

            if (bindings.Count == 0)
                SetInputBindingEditorStatus("No hay bindings editables para este esquema. Revisá los grupos del InputActionAsset.", true);
        }

        private void BeginRebindBinding(string bindingId)
        {
            if (_activeBindingSession == null) return;
            string error;
            bool started = _activeBindingSession.TryBeginRebind(bindingId, result =>
            {
                if (result == null || !IsActive(inputBindingsPanel)) return;
                SetInputBindingEditorStatus(result.message, !result.succeeded && !result.cancelled);
                RefreshInputBindingRows();
            }, out error);

            if (!started)
            {
                SetInputBindingEditorStatus(error, true);
                return;
            }

            SetInputBindingEditorStatus("Presioná una tecla o botón… · Escape cancela", false);
            for (int i = 0; i < _inputBindingRows.Count; i++)
                if (_inputBindingRows[i] != null && _inputBindingRows[i].BindingId == bindingId)
                    _inputBindingRows[i].SetListening(true);
        }

        private void ResetInputBindings()
        {
            if (_activeBindingSession == null) return;
            string error;
            bool success = _activeBindingSession.ResetAll(out error);
            SetInputBindingEditorStatus(success ? "Se restauraron los controles predeterminados para este perfil y esquema." : error, !success);
            RefreshInputBindingRows();
        }

        private void CloseInputBindingsEditor()
        {
            // Disposing the session stops listening and releases the temporary InputActionAsset clone.
            if (_activeBindingSession != null)
            {
                _activeBindingSession.Dispose();
                _activeBindingSession = null;
            }
            _bindingTargetParticipantId = string.Empty;
            SetPanelActive(inputBindingsPanel, false);
        }

        private void SetInputBindingEditorStatus(string message, bool isError)
        {
            if (inputBindingsStatusText == null) return;
            inputBindingsStatusText.text = message ?? string.Empty;
            inputBindingsStatusText.color = isError ? new Color32(255, 143, 143, 255) : new Color32(173, 224, 196, 255);
        }

        private void AssignKeyboardSlot(int slot)
        {
            if (string.IsNullOrEmpty(_pendingInputTargetId)) return;
            string error;
            if (_controller.AssignKeyboardSlot(_pendingInputTargetId, slot, out error))
            {
                string participantId = _pendingInputTargetId;
                bool openProfileSelection = _openProfileSelectionAfterInput;
                SetPanelActive(inputSelectorPanel, false);
                _pendingInputTargetId = string.Empty;
                _openProfileSelectionAfterInput = false;
                _removeParticipantIfInputCancelled = false;
                RefreshAll();
                if (openProfileSelection) ShowProfileLibrary(participantId);
            }
            else RefreshKeyboardButtons();
        }

        private void ReleaseInput()
        {
            if (string.IsNullOrEmpty(_pendingInputTargetId)) return;

            // During the + flow the participant has not completed setup yet. Releasing an input
            // here should cancel that flow rather than leave an empty, orphaned Party slot.
            if (_removeParticipantIfInputCancelled)
            {
                CloseInputSelector();
                return;
            }

            _controller.ReleaseInput(_pendingInputTargetId);
            SetPanelActive(inputSelectorPanel, false);
            _pendingInputTargetId = string.Empty;
            _openProfileSelectionAfterInput = false;
            _removeParticipantIfInputCancelled = false;
            RefreshAll();
        }

        private void RefreshKeyboardButtons()
        {
            if (_controller == null) return;

            PartyParticipant target = string.IsNullOrEmpty(_pendingInputTargetId)
                ? null
                : _controller.Party.GetParticipant(_pendingInputTargetId);
            InputAssignment current = target != null ? target.inputAssignment : null;

            RefreshKeyboardButton(keyboardSlot1Button, 0, "Teclado 1", current);
            RefreshKeyboardButton(keyboardSlot2Button, 1, "Teclado 2", current);
        }

        private void RefreshKeyboardButton(Button button, int slot, string defaultLabel, InputAssignment currentAssignment)
        {
            if (button == null) return;

            bool isCurrent = currentAssignment != null &&
                currentAssignment.deviceKind == InputDeviceKind.Keyboard &&
                currentAssignment.keyboardSlot == slot;
            bool isAvailable = _controller.IsKeyboardSlotAvailable(slot, _pendingInputTargetId);

            // The current slot is shown as selected, not as an available action. Slots owned by
            // another participant are disabled as well. This is mirrored by InputAssignmentService.
            button.interactable = !isCurrent && isAvailable;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null) return;

            if (isCurrent)
            {
                label.text = defaultLabel + "  ✓ Actual";
                label.color = new Color(0.62f, 0.88f, 0.69f, 1f);
            }
            else if (!isAvailable)
            {
                label.text = defaultLabel + "  · En uso";
                label.color = new Color(0.62f, 0.66f, 0.73f, 1f);
            }
            else
            {
                label.text = defaultLabel;
                label.color = new Color(0.91f, 0.93f, 0.97f, 1f);
            }
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel == null) return;
            Coroutine running;
            if (_panelTransitions.TryGetValue(panel, out running) && running != null)
            {
                StopCoroutine(running);
                _panelTransitions.Remove(panel);
            }

            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            Vector3 baseScale;
            if (!_panelBaseScales.TryGetValue(panel, out baseScale))
            {
                baseScale = panel.transform.localScale;
                _panelBaseScales[panel] = baseScale;
            }

            if (active)
            {
                if (!panel.activeSelf)
                {
                    panel.transform.localScale = baseScale * 0.94f;
                    group.alpha = 0f;
                    panel.SetActive(true);
                }
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            else
            {
                if (!panel.activeInHierarchy) return;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            _panelTransitions[panel] = StartCoroutine(AnimatePanel(panel, group, active, baseScale));
        }

        private IEnumerator AnimatePanel(GameObject panel, CanvasGroup group, bool show, Vector3 baseScale)
        {
            float duration = show ? 0.18f : 0.12f;
            float elapsed = 0f;
            float startAlpha = group.alpha;
            Vector3 startScale = panel.transform.localScale;
            Vector3 endScale = show ? baseScale : baseScale * 0.96f;
            float endAlpha = show ? 1f : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = show ? 1f - Mathf.Pow(1f - t, 3f) : t * t;
                group.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                panel.transform.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
                yield return null;
            }

            group.alpha = endAlpha;
            panel.transform.localScale = show ? baseScale : baseScale;
            if (!show) panel.SetActive(false);
            _panelTransitions.Remove(panel);
        }

        private static void HidePanelImmediately(GameObject panel)
        {
            if (panel == null) return;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            panel.SetActive(false);
        }

        private static bool IsActive(GameObject panel)
        {
            return panel != null && panel.activeInHierarchy;
        }

        private static void WireButton(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }
    }
}
