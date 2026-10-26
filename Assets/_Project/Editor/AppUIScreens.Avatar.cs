using LostPetNetwork.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    public static partial class AppUIBuilder
    {
        // ------------------------------------------------------------------ Avatar creator

        static GameObject BuildAvatarCreator(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "AvatarCreator", 40);

            var header = NewRect("Header", content);
            HLayout(header.gameObject, 12, null, TextAnchor.MiddleLeft);
            var close = CircleIconButton(header, "CloseButton", "x");
            var title = Txt(header, "Title", "Your 3D avatar", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);

            var banner = Banner(content);

            var stage = Img(content, "Stage", Theme.Forest50, _round24);
            Le(stage.gameObject, prefH: 340);
            var viewRt = NewRect("Preview", stage.transform);
            Stretch(viewRt, 8, 8, 8, 8);
            viewRt.gameObject.AddComponent<RawImage>();
            var preview = viewRt.gameObject.AddComponent<Preview3D>();
            var hint = Txt(stage.transform, "Hint", "Drag to turn · scroll or pinch to zoom", 12, Theme.TextSecondary, F.Medium, TextAlignmentOptions.Center);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(300, 20));

            var actions = NewRect("Actions", content);
            HLayout(actions.gameObject, 10, null, TextAnchor.MiddleCenter, expandWidth: true);
            var random = PillButton(actions, "RandomButton", "Surprise me", "zap");
            var resetView = PillButton(actions, "ResetViewButton", "Reset view", "locate");

            var categories = ChipRow(content, "Categories", "Skin", "Hair", "Eyes", "Outfit", "Extras");

            var panels = new GameObject[5];
            for (int i = 0; i < panels.Length; i++)
            {
                var panel = NewRect("Panel-" + new[] { "Skin", "Hair", "Eyes", "Outfit", "Extras" }[i], content);
                VLayout(panel.gameObject, 16);
                panels[i] = panel.gameObject;
            }

            SwatchRow(panels[0].transform, "Skin tone", AvatarProperty.Skin, AvatarPalette.Skin);
            ChipOptionRow(panels[1].transform, "Hair style", AvatarProperty.HairStyle, AvatarPalette.HairStyleNames);
            SwatchRow(panels[1].transform, "Hair colour", AvatarProperty.HairColor, AvatarPalette.Hair);
            SwatchRow(panels[2].transform, "Eye colour", AvatarProperty.Eyes, AvatarPalette.Eyes);
            ChipOptionRow(panels[3].transform, "Outfit", AvatarProperty.OutfitStyle, AvatarPalette.OutfitStyleNames);
            SwatchRow(panels[3].transform, "Outfit colour", AvatarProperty.OutfitColor, AvatarPalette.Outfit);
            ChipOptionRow(panels[4].transform, "Accessory", AvatarProperty.Accessory, AvatarPalette.AccessoryNames);

            var save = PrimaryButton(content, "SaveButton", "Save avatar", Theme.Forest700, Theme.OnBrand);
            var cancel = LinkButton(content, "CancelButton", "Cancel", TextAlignmentOptions.Center);

            var screen = root.AddComponent<AvatarCreatorScreen>();
            Wire(screen, "manager", manager, "preview", preview, "closeButton", close, "randomButton", random,
                "resetViewButton", resetView, "categories", categories, "saveButton", save, "cancelButton", cancel,
                "banner", banner);
            var so = new SerializedObject(screen);
            var list = so.FindProperty("categoryPanels");
            list.arraySize = panels.Length;
            for (int i = 0; i < panels.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static Button PillButton(Transform parent, string name, string text, string icon)
        {
            var img = Img(parent, name, Theme.Forest100, _round24);
            HLayout(img.gameObject, 6, new RectOffset(14, 14, 0, 0), TextAnchor.MiddleCenter);
            Le(img.gameObject, prefH: 40, flexW: 1);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            IconImg(img.transform, icon, Theme.Forest700, 18);
            var label = Txt(img.transform, "Label", text, 13, Theme.Forest700, F.Bold, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        static RectTransform OptionRowShell(Transform parent, string caption, AvatarProperty property, out AvatarOptionRow row)
        {
            var box = NewRect("Option-" + property, parent);
            VLayout(box.gameObject, 10);
            Txt(box, "Caption", caption, 14, Theme.TextSecondary, F.Medium);
            row = box.gameObject.AddComponent<AvatarOptionRow>();
            return box;
        }

        static void SwatchRow(Transform parent, string caption, AvatarProperty property, Color[] colors, float size = 38f, float spacing = 10f)
        {
            var box = OptionRowShell(parent, caption, property, out var row);
            var line = NewRect("Swatches", box);
            HLayout(line.gameObject, spacing, new RectOffset(3, 3, 3, 3), TextAnchor.MiddleLeft);

            var so = new SerializedObject(row);
            so.FindProperty("property").enumValueIndex = (int)property;
            var list = so.FindProperty("options");
            list.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++)
            {
                var swatch = Img(line, "Swatch-" + i, colors[i], _circle);
                Le(swatch.gameObject, prefH: size, prefW: size);
                var outline = swatch.gameObject.AddComponent<Outline>();
                outline.effectColor = Theme.Border;
                outline.effectDistance = new Vector2(1f, 1f);
                var button = swatch.gameObject.AddComponent<Button>();
                button.targetGraphic = swatch;
                button.transition = Selectable.Transition.None;
                var mark = Img(swatch.transform, "Mark", Theme.Forest900, _ring24);
                mark.raycastTarget = false;
                Stretch(mark.rectTransform, -3, -3, -3, -3);
                mark.gameObject.SetActive(false);

                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = button;
                el.FindPropertyRelative("background").objectReferenceValue = swatch;
                el.FindPropertyRelative("mark").objectReferenceValue = mark.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject ChipOptionRow(Transform parent, string caption, AvatarProperty property, string[] labels)
        {
            var box = OptionRowShell(parent, caption, property, out var row);
            var grid = NewRect("Chips", box);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(106, 40);
            layout.spacing = new Vector2(8, 8);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.UpperLeft;

            var so = new SerializedObject(row);
            so.FindProperty("property").enumValueIndex = (int)property;
            var list = so.FindProperty("options");
            list.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                var chip = Img(grid, "Chip-" + labels[i], Theme.Card, _round24);
                var outline = chip.gameObject.AddComponent<Outline>();
                outline.effectColor = Theme.Border;
                outline.effectDistance = new Vector2(1.2f, 1.2f);
                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = chip;
                button.transition = Selectable.Transition.None;
                var label = Txt(chip.transform, "Label", labels[i], 13, Theme.TextPrimary, F.Bold, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(label.rectTransform);

                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = button;
                el.FindPropertyRelative("background").objectReferenceValue = chip;
                el.FindPropertyRelative("label").objectReferenceValue = label;
                el.FindPropertyRelative("outline").objectReferenceValue = outline;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return box.gameObject;
        }

        // ------------------------------------------------------------------ Pet creator

        static GameObject BuildPetCreator(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "PetCreator", 40);

            var header = NewRect("Header", content);
            HLayout(header.gameObject, 12, null, TextAnchor.MiddleLeft);
            var close = CircleIconButton(header, "CloseButton", "x");
            var title = Txt(header, "Title", "Your pet in 3D", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);

            var stage = Img(content, "Stage", Theme.Forest50, _round24);
            Le(stage.gameObject, prefH: 300);
            var viewRt = NewRect("Preview", stage.transform);
            Stretch(viewRt, 8, 8, 8, 8);
            viewRt.gameObject.AddComponent<RawImage>();
            var preview = viewRt.gameObject.AddComponent<Preview3D>();
            var hint = Txt(stage.transform, "Hint", "Drag to turn · tilt your phone · pinch to zoom", 12, Theme.TextSecondary, F.Medium, TextAlignmentOptions.Center);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(320, 20));

            var actions = NewRect("Actions", content);
            HLayout(actions.gameObject, 10, null, TextAnchor.MiddleCenter, expandWidth: true);
            var random = PillButton(actions, "RandomButton", "Surprise me", "zap");
            var resetView = PillButton(actions, "ResetViewButton", "Reset view", "locate");

            var categories = ChipRow(content, "Categories", "Pet", "Coat", "Extras");

            var panels = new GameObject[3];
            for (int i = 0; i < panels.Length; i++)
            {
                var panel = NewRect("Panel-" + new[] { "Pet", "Coat", "Extras" }[i], content);
                VLayout(panel.gameObject, 16);
                panels[i] = panel.gameObject;
            }

            ChipOptionRow(panels[0].transform, "Kind of pet", AvatarProperty.PetSpecies, PetPalette.SpeciesNames);
            ChipOptionRow(panels[0].transform, "Size", AvatarProperty.PetSize, PetPalette.SizeNames);
            SwatchRow(panels[1].transform, "Coat colour", AvatarProperty.PetCoat, PetPalette.Coat, 34f, 8f);
            ChipOptionRow(panels[1].transform, "Markings", AvatarProperty.PetPattern, PetPalette.PatternNames);
            var earsRow = ChipOptionRow(panels[2].transform, "Ears", AvatarProperty.PetEars, PetPalette.EarNames);
            ChipOptionRow(panels[2].transform, "Collar", AvatarProperty.PetCollar, new[] { "None", "Red", "Blue", "Green", "Yellow", "Purple" });

            var save = PrimaryButton(content, "SaveButton", "Use this 3D look", Theme.Forest700, Theme.OnBrand);
            var remove = LinkButton(content, "RemoveButton", "Remove 3D look", TextAlignmentOptions.Center);

            var screen = root.AddComponent<PetCreatorScreen>();
            Wire(screen, "manager", manager, "preview", preview, "closeButton", close, "randomButton", random,
                "resetViewButton", resetView, "categories", categories, "saveButton", save, "removeButton", remove,
                "earsRow", earsRow);
            var so = new SerializedObject(screen);
            var list = so.FindProperty("categoryPanels");
            list.arraySize = panels.Length;
            for (int i = 0; i < panels.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }
    }
}
