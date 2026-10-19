using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>A labelled text field with an inline error message, matching the Figma "Text Field".</summary>
    public class TextFieldView : MonoBehaviour
    {
        [SerializeField] TMP_InputField input;
        [SerializeField] TMP_Text errorLabel;
        [SerializeField] Image border;

        public string Text => input.text;
        public TMP_InputField Input => input;

        public void SetError(string message)
        {
            errorLabel.text = message;
            errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
            border.color = string.IsNullOrEmpty(message) ? Theme.Border : Theme.Error;
        }

        public void ClearError() => SetError(null);

        public void Clear() => input.text = string.Empty;

        public void SetInteractable(bool value) => input.interactable = value;
    }
}
