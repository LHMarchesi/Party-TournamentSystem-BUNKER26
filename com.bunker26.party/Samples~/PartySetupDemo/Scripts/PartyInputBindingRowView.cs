using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker26.Party.Samples
{
    /// <summary>One Inspector-authored row in the sample's editable controls panel.</summary>
    public sealed class PartyInputBindingRowView : MonoBehaviour
    {
        public Text actionNameText;
        public Text currentBindingText;
        public Button rebindButton;
        public Text rebindButtonText;

        private string _bindingId;
        private Action<string> _onRebind;

        public void Configure(InputBindingDisplayInfo bindingInfo, Action<string> onRebind)
        {
            _bindingId = bindingInfo != null ? bindingInfo.bindingId : string.Empty;
            _onRebind = onRebind;
            if (actionNameText != null) actionNameText.text = bindingInfo != null ? bindingInfo.displayName : "Acción";
            if (currentBindingText != null) currentBindingText.text = bindingInfo != null ? bindingInfo.currentDisplay : "—";
            if (rebindButtonText != null) rebindButtonText.text = "Cambiar";
            if (rebindButton != null)
            {
                rebindButton.onClick.RemoveAllListeners();
                rebindButton.onClick.AddListener(() => _onRebind?.Invoke(_bindingId));
                rebindButton.interactable = !string.IsNullOrEmpty(_bindingId);
            }
        }

        public void SetListening(bool listening)
        {
            if (rebindButtonText != null) rebindButtonText.text = listening ? "Escuchando…" : "Cambiar";
            if (rebindButton != null) rebindButton.interactable = !listening;
        }

        public string BindingId { get { return _bindingId; } }
    }
}
