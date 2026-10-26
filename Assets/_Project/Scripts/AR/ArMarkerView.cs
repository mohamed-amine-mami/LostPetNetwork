using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.AR
{
    /// <summary>A floating label in the compass view: a pin for something nearby with its name and distance.</summary>
    public class ArMarkerView : MonoBehaviour
    {
        [SerializeField] Image dot;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text distanceLabel;
        [SerializeField] Button button;

        public RectTransform Rect => (RectTransform)transform;
        public object Tag { get; private set; }

        public void Bind(object tag, string title, Color color, Action onClick)
        {
            Tag = tag;
            nameLabel.text = title;
            dot.color = color;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
            button.interactable = onClick != null;
        }

        public void SetDistance(string text, bool offScreenLeft, bool offScreenRight)
        {
            distanceLabel.text = offScreenLeft ? "◀ " + text : offScreenRight ? text + " ▶" : text;
        }
    }
}
