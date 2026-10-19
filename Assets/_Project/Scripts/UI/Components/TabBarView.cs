using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.UI
{
    /// <summary>Bottom navigation bar (Figma "Tab Bar"): Home, Explore, My reports, Profile.</summary>
    public class TabBarView : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public ScreenId id;
            public Button button;
            public Image icon;
            public TMP_Text label;
        }

        [SerializeField] ScreenManager manager;
        [SerializeField] List<Tab> tabs = new List<Tab>();

        public ScreenId Highlighted { get; private set; }

        void Awake()
        {
            foreach (var tab in tabs)
            {
                var id = tab.id;
                tab.button.onClick.AddListener(() => manager.Show(id));
            }
        }

        /// <summary>Highlights the tab for the given screen (the Map belongs to the Explore tab).</summary>
        public void Highlight(ScreenId screen)
        {
            Highlighted = screen == ScreenId.Map ? ScreenId.Explore : screen;
            foreach (var tab in tabs)
            {
                bool on = tab.id == Highlighted;
                tab.icon.color = on ? Theme.Forest700 : Theme.Placeholder;
                tab.label.color = on ? Theme.Forest700 : Theme.Placeholder;
            }
        }
    }
}
