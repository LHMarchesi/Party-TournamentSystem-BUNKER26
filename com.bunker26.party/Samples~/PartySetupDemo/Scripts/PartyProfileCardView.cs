using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker26.Party.Samples
{
    /// <summary>Reusable saved-profile card. Images and buttons are inspector-assigned.</summary>
    public sealed class PartyProfileCardView : MonoBehaviour
    {
        public Image profileImage;
        public Text profileNameLabel;
        public Text statisticsLabel;
        public Text assignmentStateLabel;
        public Button assignButton;
        public Button detailsButton;
        public Button editButton;

        public void Bind(PlayerProfile profile, PartyIconCatalog icons, bool assignedToAnotherPlayer,
            Action onAssign, Action onDetails, Action onEdit)
        {
            if (profile == null) return;
            if (profileImage != null)
            {
                profileImage.sprite = icons != null ? icons.GetProfileIcon(profile.iconId) : null;
                profileImage.preserveAspect = true;
            }
            if (profileNameLabel != null) profileNameLabel.text = profile.displayName;
            if (statisticsLabel != null)
            {
                PlayerStatistics stats = profile.statistics ?? new PlayerStatistics();
                statisticsLabel.text = stats.tournamentsPlayed + " torneos  ·  " + stats.tournamentsWon + " victorias  ·  " + stats.matchesWon + " matches ganados";
            }
            if (assignmentStateLabel != null) assignmentStateLabel.text = assignedToAnotherPlayer ? "EN USO" : "DISPONIBLE";
            Configure(assignButton, onAssign);
            Configure(detailsButton, onDetails);
            Configure(editButton, onEdit);
            if (assignButton != null) assignButton.interactable = !assignedToAnotherPlayer;
        }

        private static void Configure(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }
    }
}
