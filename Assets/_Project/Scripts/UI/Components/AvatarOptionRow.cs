using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Which setting an option row edits (avatar settings first, then the 3D pet's).</summary>
    public enum AvatarProperty { Skin, HairStyle, HairColor, Eyes, OutfitStyle, OutfitColor, Accessory, PetSpecies, PetCoat, PetPattern, PetSize, PetEars, PetCollar }

    /// <summary>
    /// One row of choices in the avatar creator: colour swatches (marked with a ring) or text chips.
    /// Reports the chosen index; the creator screen applies it to the avatar.
    /// </summary>
    public class AvatarOptionRow : MonoBehaviour
    {
        [Serializable]
        public class Option
        {
            public Button button;
            public Image background;
            public TMP_Text label;
            public Outline outline;
            public GameObject mark;
        }

        [SerializeField] AvatarProperty property;
        [SerializeField] List<Option> options = new List<Option>();

        public AvatarProperty Property => property;
        public int Count => options.Count;
        public int Selected { get; private set; } = -1;

        /// <summary>Raised only when the user taps an option.</summary>
        public event Action<AvatarProperty, int> Chosen;

        void Awake()
        {
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                options[i].button.onClick.AddListener(() =>
                {
                    SetSelected(index);
                    Chosen?.Invoke(property, index);
                });
            }
        }

        public void SetSelected(int index)
        {
            Selected = index;
            for (int i = 0; i < options.Count; i++)
            {
                bool on = i == index;
                var o = options[i];
                if (o.mark != null) o.mark.SetActive(on);
                if (o.label != null)
                {
                    o.background.color = on ? Theme.Forest700 : Theme.Card;
                    o.label.color = on ? Theme.OnBrand : Theme.TextPrimary;
                    if (o.outline != null) o.outline.enabled = !on;
                }
            }
        }

        /// <summary>Test helper: the same as the user tapping an option.</summary>
        public void Choose(int index) => options[index].button.onClick.Invoke();
    }
}
