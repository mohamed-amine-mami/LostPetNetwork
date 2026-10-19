using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>A row of single-select filter chips (Figma "Filter Chip").</summary>
    public class ChipGroup : MonoBehaviour
    {
        [Serializable]
        public class Chip
        {
            public Button button;
            public Image background;
            public TMP_Text label;
            public Outline outline;
        }

        [SerializeField] List<Chip> chips = new List<Chip>();

        public int SelectedIndex { get; private set; } = -1;
        public int Count => chips.Count;
        public string LabelAt(int index) => chips[index].label.text;

        /// <summary>Raised when the user (or code with notify=true) changes the selection.</summary>
        public event Action<int> Changed;

        void Awake()
        {
            for (int i = 0; i < chips.Count; i++)
            {
                int index = i;
                chips[i].button.onClick.AddListener(() => Select(index));
            }
            if (SelectedIndex < 0) Select(0, false);
        }

        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= chips.Count) return;
            SelectedIndex = index;
            for (int i = 0; i < chips.Count; i++)
            {
                bool on = i == index;
                var chip = chips[i];
                chip.background.color = on ? Theme.Forest700 : Theme.Card;
                chip.label.color = on ? Theme.OnBrand : Theme.TextPrimary;
                if (chip.outline != null) chip.outline.enabled = !on;
            }
            if (notify) Changed?.Invoke(index);
        }
    }
}
