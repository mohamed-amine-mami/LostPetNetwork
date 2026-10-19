using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Button that shows a loading label and ignores clicks while a request is running.</summary>
    [RequireComponent(typeof(Button))]
    public class LoadingButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] string loadingText = "Please wait…";

        Button _button;
        string _idleText;

        public Button Button => _button != null ? _button : (_button = GetComponent<Button>());

        void Awake() => _idleText = label.text;

        public void SetLoading(bool loading)
        {
            if (_idleText == null) _idleText = label.text;
            Button.interactable = !loading;
            label.text = loading ? loadingText : _idleText;
        }

        public void SetIdleText(string text)
        {
            _idleText = text;
            label.text = text;
        }
    }
}
