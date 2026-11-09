using LostPetNetwork.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    public static partial class AppUIBuilder
    {
        static VetRowView _vetRowPrefab;

        static VetRowView BuildVetRowPrefab()
        {
            var root = new GameObject("VetRow", typeof(RectTransform));
            var rootImg = root.AddComponent<Image>();
            rootImg.color = Theme.Border;
            rootImg.sprite = _round16;
            rootImg.type = Image.Type.Sliced;
            VLayout(root, 0, new RectOffset(1, 1, 1, 1));

            var face = Img(root.transform, "Face", Theme.Card, _round16);
            VLayout(face.gameObject, 8, new RectOffset(16, 16, 14, 14));

            var top = NewRect("Top", face.transform);
            HLayout(top.gameObject, 8, null, TextAnchor.MiddleLeft);
            var (pill, _) = Pill(top, "OpenAllDay", Theme.Forest100, new RectOffset(10, 10, 4, 4));
            var pillLabel = Txt(pill, "Label", "24/7", 11, Theme.Forest900, F.Bold, TextAlignmentOptions.Center);
            pillLabel.characterSpacing = 3;
            var flex = NewRect("Flex", top);
            Le(flex.gameObject, flexW: 1);
            var distance = Txt(top, "Distance", "", 12, Theme.Forest700, F.Bold, TextAlignmentOptions.Right);
            var demo = DemoTag(top);

            var name = Txt(face.transform, "Name", "Clinique vétérinaire", 17, Theme.TextPrimary, F.Bold);
            var place = Txt(face.transform, "Place", "", 13, Theme.TextSecondary);
            var hours = Txt(face.transform, "Hours", "", 12, Theme.TextSecondary);

            var actions = NewRect("Actions", face.transform);
            HLayout(actions.gameObject, 10, null, TextAnchor.MiddleCenter, expandWidth: true);
            var call = PillButton(actions, "CallButton", "Call", "bell");
            var directions = PillButton(actions, "DirectionsButton", "Directions", "map");

            var view = root.AddComponent<VetRowView>();
            Wire(view, "nameLabel", name, "placeLabel", place, "hoursLabel", hours, "distanceLabel", distance,
                "openAllDayTag", pill.gameObject, "demoTag", demo, "callButton", call, "directionsButton", directions);
            return SavePrefab<VetRowView>(root, "VetRow");
        }

        static GameObject BuildVets(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "Vets", 40);
            var head = NewRect("Header", content);
            HLayout(head.gameObject, 12, null, TextAnchor.MiddleLeft);
            var back = CircleIconButton(head, "BackButton", "back");
            var title = Txt(head, "Title", "Vets near you", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);

            Txt(content, "Intro", "Tap Call to phone a clinic, or Directions to open it in Maps. In an emergency, call first.", 14, Theme.TextSecondary);
            var chips = ChipRow(content, "Filters", "All", "Open 24/7");
            var count = Txt(content, "Count", "0 clinics", 12, Theme.TextSecondary, F.Medium);
            var note = Txt(content, "LocationNote", "Waiting for your location. Clinics are listed A to Z until it arrives.", 12, Theme.TerracottaInk);
            note.gameObject.SetActive(false);
            var banner = Banner(content);
            var list = NewRect("List", content);
            VLayout(list.gameObject, 12);
            var loading = LoadingBlock(content);
            var empty = EmptyState(content, "No vets are listed yet.");

            var screen = root.AddComponent<VetsScreen>();
            Wire(screen, "manager", manager, "backButton", back, "chips", chips, "countLabel", count, "locationNote", note,
                "listRoot", list, "rowPrefab", _vetRowPrefab, "loading", loading, "emptyState", empty.root,
                "emptyLabel", empty.label, "banner", banner);
            return root;
        }
    }
}
