using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Inline error / info message shown above a form's primary button.</summary>
    public class MessageBanner : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;

        public string CurrentMessage => label.text;
        public bool IsVisible => gameObject.activeSelf;

        public void ShowError(string message) => Show(message, Theme.TerracottaSoft, Theme.TerracottaInk);

        public void ShowInfo(string message) => Show(message, Theme.Forest50, Theme.Forest900);

        public void Hide() => gameObject.SetActive(false);

        void Show(string message, Color back, Color text)
        {
            background.color = back;
            label.color = text;
            label.text = message;
            gameObject.SetActive(true);
        }
    }
}
