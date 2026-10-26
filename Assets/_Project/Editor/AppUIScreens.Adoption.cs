using LostPetNetwork.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    public static partial class AppUIBuilder
    {
        static AdoptionCardView _adoptionCardPrefab;

        // ------------------------------------------------------------------ prefab

        static AdoptionCardView BuildAdoptionCardPrefab()
        {
            var root = new GameObject("AdoptionCard", typeof(RectTransform));
            var rootImg = root.AddComponent<Image>();
            rootImg.color = Theme.Border;
            rootImg.sprite = _round24;
            rootImg.type = Image.Type.Sliced;
            VLayout(root, 0, new RectOffset(1, 1, 1, 1));
            var button = root.AddComponent<Button>();
            button.targetGraphic = rootImg;

            var face = Img(root.transform, "Face", Theme.Card, _round24);
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            VLayout(face.gameObject, 0);

            var stage = Img(face.transform, "Stage", Theme.Forest50);
            Le(stage.gameObject, prefH: 190);
            var portrait = Img(stage.transform, "Portrait", Color.white);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            Stretch(portrait.rectTransform, 8, 8, 8, 8);
            portrait.enabled = false;

            var (pill, _) = Pill(stage.transform, "MatchPill", Theme.GoldSoft, new RectOffset(10, 10, 4, 4));
            var matchLabel = Txt(pill, "Label", "0% match", 12, Theme.GoldInk, F.Bold, TextAlignmentOptions.Center);
            Anchor(pill, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), Vector2.zero);
            var demo = DemoTag(stage.transform);
            Anchor(demo.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -14), Vector2.zero);

            var body = NewRect("Body", face.transform);
            VLayout(body.gameObject, 6, new RectOffset(16, 16, 14, 16));
            var nameLabel = Txt(body, "Name", "Rocky", 20, Theme.TextPrimary, F.Heading);
            var details = Txt(body, "Details", "Dog · 2 years · Large", 15, Theme.TextSecondary);
            var locRow = NewRect("LocationRow", body);
            HLayout(locRow.gameObject, 6, new RectOffset(0, 0, 4, 0), TextAnchor.MiddleLeft);
            IconImg(locRow, "pin", Theme.Forest700, 16);
            var loc = Txt(locRow, "Location", "Ariana, Tunis", 13, Theme.Forest700, F.Bold);

            var view = root.AddComponent<AdoptionCardView>();
            Wire(view, "portrait", portrait, "nameLabel", nameLabel, "detailsLabel", details, "locationLabel", loc,
                "matchPill", pill.gameObject, "matchLabel", matchLabel, "demoTag", demo);
            return SavePrefab<AdoptionCardView>(root, "AdoptionCard");
        }

        // ------------------------------------------------------------------ list

        static GameObject BuildAdoptionList(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "Adoption", TabScreenBottomPadding);
            Header(content, "Adopt", out _);
            Txt(content, "Intro", "Pets looking for a loving home. Add your home details to see how well each one fits.", 14, Theme.TextSecondary);

            var actions = NewRect("Actions", content);
            HLayout(actions.gameObject, 10, null, TextAnchor.MiddleCenter, expandWidth: true);
            var home = PillButton(actions, "HomeButton", "My home", "home");
            var list = PillButton(actions, "ListButton", "List a pet", "plus");

            var chips = ChipRow(content, "Filters", "All", "Dogs", "Cats");
            var count = Txt(content, "Count", "0 pets", 12, Theme.TextSecondary, F.Medium);
            var banner = Banner(content);
            var listRoot = NewRect("List", content);
            VLayout(listRoot.gameObject, 16);
            var loading = LoadingBlock(content);
            var empty = EmptyState(content, "No pets are waiting for a home right now. Check back soon, or list one.");

            var screen = root.AddComponent<AdoptionListScreen>();
            Wire(screen, "manager", manager, "homeButton", home, "listButton", list, "chips", chips, "countLabel", count,
                "listRoot", listRoot, "cardPrefab", _adoptionCardPrefab, "loading", loading, "emptyState", empty.root,
                "banner", banner);
            return root;
        }

        // ------------------------------------------------------------------ details

        static GameObject BuildAdoptionDetails(Transform parent, ScreenManager manager)
        {
            var container = NewRect("AdoptionDetails", parent);
            Stretch(container);
            var (scrollRoot, content) = NewScreen(container, "Scroll", 24, viewportBottomInset: 190);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 72, 24);
            layout.spacing = 14;

            var stage = Img(content, "Stage", Theme.Forest50, _round24);
            Le(stage.gameObject, prefH: 280);
            var viewRt = NewRect("Preview", stage.transform);
            Stretch(viewRt, 8, 8, 8, 8);
            viewRt.gameObject.AddComponent<RawImage>();
            var preview = viewRt.gameObject.AddComponent<Preview3D>();
            var hintTxt = Txt(stage.transform, "Hint", "Drag or tilt your phone to turn", 12, Theme.TextSecondary, F.Medium, TextAlignmentOptions.Center);
            Anchor(hintTxt.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(300, 20));

            var nameLabel = Txt(content, "Name", "Rocky", 32, Theme.Forest900, F.Heading);
            var summary = Txt(content, "Summary", "Dog · 2 years · Large", 15, Theme.TextSecondary);

            var info = Img(content, "InfoCard", Theme.Card, _round16);
            VLayout(info.gameObject, 0, new RectOffset(0, 0, 4, 4));
            var infoOutline = info.gameObject.AddComponent<Outline>();
            infoOutline.effectColor = Theme.Border; infoOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var energy = InfoRow(info.transform, "zap", "Energy");
            var location = InfoRow(info.transform, "pin", "Where");
            var owner = InfoRow(info.transform, "user", "Listed by");

            var description = Txt(content, "Description", "", 15, Theme.TextPrimary);

            // Prompt shown until the user has described their home.
            var prompt = Img(content, "HomePrompt", Theme.GoldSoft, _round16);
            VLayout(prompt.gameObject, 10, new RectOffset(16, 16, 14, 14));
            Txt(prompt.transform, "Text", "Tell us about your home to see how well this pet would fit.", 14, Theme.GoldInk, F.Medium);
            var setup = PrimaryButton(prompt.transform, "SetupHomeButton", "Set up my home", Theme.Forest700, Theme.OnBrand);

            // Compatibility card.
            var match = Img(content, "MatchCard", Theme.Card, _round24);
            VLayout(match.gameObject, 10, new RectOffset(16, 16, 16, 16));
            var matchOutline = match.gameObject.AddComponent<Outline>();
            matchOutline.effectColor = Theme.Border; matchOutline.effectDistance = new Vector2(1.2f, 1.2f);
            Txt(match.transform, "Title", "How well it fits your home", 13, Theme.TextSecondary, F.Bold);
            var scoreRow = NewRect("ScoreRow", match.transform);
            HLayout(scoreRow.gameObject, 12, null, TextAnchor.MiddleLeft);
            var score = Txt(scoreRow, "Score", "0%", 40, Theme.Forest700, F.Heading);
            var verdict = Txt(scoreRow, "Verdict", "", 17, Theme.TextPrimary, F.Medium);
            Le(verdict.gameObject, flexW: 1);

            var titles = new TMP_Text[5];
            var points = new TMP_Text[5];
            var notes = new TMP_Text[5];
            for (int i = 0; i < 5; i++)
            {
                var row = NewRect("Factor-" + i, match.transform);
                VLayout(row.gameObject, 2, new RectOffset(0, 0, 6, 0));
                var top = NewRect("Top", row);
                HLayout(top.gameObject, 8, null, TextAnchor.MiddleLeft);
                titles[i] = Txt(top, "Title", "Factor", 15, Theme.TextPrimary, F.Bold);
                Le(titles[i].gameObject, flexW: 1);
                points[i] = Txt(top, "Points", "0/0", 14, Theme.Forest700, F.Bold, TextAlignmentOptions.Right);
                notes[i] = Txt(row, "Note", "", 13, Theme.TextSecondary);
            }
            Txt(match.transform, "Disclaimer", "A guide only: meet the pet and talk to the owner before deciding.", 12, Theme.Placeholder);

            var banner = Banner(content);

            var back = CircleIconButton(container, "BackButton", "back");
            Anchor((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(44, 44));
            back.GetComponent<LayoutElement>().ignoreLayout = true;

            var bar = Img(container, "ActionBar", Theme.Card);
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0.5f, 0); bar.rectTransform.offsetMin = Vector2.zero; bar.rectTransform.offsetMax = new Vector2(0, 190);
            VLayout(bar.gameObject, 8, new RectOffset(24, 24, 14, 24));
            var request = PrimaryButton(bar.transform, "RequestButton", "Request to adopt", Theme.Forest700, Theme.OnBrand);
            var arPrimary = PrimaryButton(bar.transform, "ArButton", "Try at home in AR", Theme.Forest50, Theme.Forest700);
            var hint = Txt(bar.transform, "Hint", "", 12, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);

            var screen = container.gameObject.AddComponent<AdoptionDetailsScreen>();
            Wire(screen, "manager", manager, "backButton", back, "preview", preview, "nameLabel", nameLabel,
                "summaryLabel", summary, "energyValue", energy, "locationValue", location, "ownerValue", owner,
                "descriptionLabel", description, "matchCard", match.gameObject, "scoreLabel", score, "verdictLabel", verdict,
                "homePrompt", prompt.gameObject, "setupHomeButton", setup.Button, "requestButton", request,
                "arButton", arPrimary.Button, "hint", hint, "banner", banner);
            var so = new SerializedObject(screen);
            AssignTexts(so, "factorTitles", titles);
            AssignTexts(so, "factorPoints", points);
            AssignTexts(so, "factorNotes", notes);
            so.ApplyModifiedPropertiesWithoutUndo();
            return container.gameObject;
        }

        static void AssignTexts(SerializedObject so, string property, TMP_Text[] values)
        {
            var list = so.FindProperty(property);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ------------------------------------------------------------------ home profile

        static void Question(Transform parent, string text) => Txt(parent, "Q-" + text, text, 13, Theme.TextPrimary, F.Bold);

        static GameObject BuildAdopterProfile(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "AdopterProfile", 40);
            var head = NewRect("Header", content);
            HLayout(head.gameObject, 12, null, TextAnchor.MiddleLeft);
            var back = CircleIconButton(head, "BackButton", "back");
            var title = Txt(head, "Title", "My home", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);
            Txt(content, "Intro", "Private to you. We only use this to estimate how well a pet fits your home.", 14, Theme.TextSecondary);
            var banner = Banner(content);
            var loading = LoadingBlock(content);

            Question(content, "What kind of home do you live in?");
            var homeType = ChipRow(content, "HomeType", "Apartment", "House");
            Question(content, "How big is it?");
            var size = ChipRow(content, "Size", "< 40 m²", "40–80", "80–150", "150+");
            Question(content, "Do you have a garden or yard?");
            var garden = ChipRow(content, "Garden", "Yes", "No");
            Question(content, "Are there children at home?");
            var kids = ChipRow(content, "Kids", "Yes", "No");
            Question(content, "Do you have other pets?");
            var pets = ChipRow(content, "Pets", "Yes", "No");
            Question(content, "How active is your household?");
            var activity = ChipRow(content, "Activity", "Calm", "Moderate", "Very active");
            var save = PrimaryButton(content, "SaveButton", "Save my home", Theme.Forest700, Theme.OnBrand);

            var screen = root.AddComponent<AdopterProfileScreen>();
            Wire(screen, "manager", manager, "backButton", back, "homeTypeChips", homeType, "sizeChips", size,
                "gardenChips", garden, "kidsChips", kids, "petsChips", pets, "activityChips", activity,
                "saveButton", save, "loading", loading, "banner", banner);
            return root;
        }

        // ------------------------------------------------------------------ list a pet

        static GameObject BuildAdoptionCreate(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "AdoptionCreate", 40);
            var head = NewRect("Header", content);
            HLayout(head.gameObject, 12, null, TextAnchor.MiddleLeft);
            var back = CircleIconButton(head, "BackButton", "back");
            var title = Txt(head, "Title", "List a pet", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);

            var name = TextField(content, "Name", "Pet's name", "e.g. Rocky", TMP_InputField.ContentType.Standard);
            Question(content, "What kind of pet?");
            var species = ChipRow(content, "Species", "Dog", "Cat");
            Question(content, "How old?");
            var age = ChipRow(content, "Age", "< 1 year", "1–3", "4–8", "9+");
            Question(content, "How big?");
            var size = ChipRow(content, "Size", "Small", "Medium", "Large");
            Question(content, "Energy level");
            var energy = ChipRow(content, "Energy", "Calm", "Moderate", "Very active");
            Question(content, "Good with children?");
            var kids = ChipRow(content, "Kids", "Yes", "No");
            Question(content, "Good with other pets?");
            var pets = ChipRow(content, "Pets", "Yes", "No");
            Question(content, "Needs a garden?");
            var garden = ChipRow(content, "Garden", "Yes", "No");

            var location = TextField(content, "Location", "Where is the pet?", "Street, park or neighbourhood", TMP_InputField.ContentType.Standard);
            var useLocation = NewRect("UseLocation", content);
            HLayout(useLocation.gameObject, 6, null, TextAnchor.MiddleLeft);
            IconImg(useLocation, "locate", Theme.Forest700, 16);
            var useLocationLink = LinkButton(useLocation, "Link", "Use my current location", TextAlignmentOptions.Left);
            Le(useLocationLink.gameObject, minH: 32);
            var description = TextField(content, "Description", "About the pet (optional)", "Character, habits, health, why it needs a home", TMP_InputField.ContentType.Standard, multiline: true);

            var pet3d = Img(content, "Pet3DBox", Theme.Card, _round16);
            HLayout(pet3d.gameObject, 12, new RectOffset(16, 16, 14, 14), TextAnchor.MiddleLeft, expandWidth: false);
            var pet3dOutline = pet3d.gameObject.AddComponent<Outline>();
            pet3dOutline.effectColor = Theme.Border; pet3dOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var pet3dButton = pet3d.gameObject.AddComponent<Button>();
            pet3dButton.targetGraphic = pet3d;
            pet3dButton.transition = Selectable.Transition.None;
            var pet3dText = NewRect("Text", pet3d.transform);
            VLayout(pet3dText.gameObject, 2);
            Le(pet3dText.gameObject, flexW: 1);
            Txt(pet3dText, "Title", "Design a 3D look", 15, Theme.TextPrimary, F.Medium);
            var pet3dCaption = Txt(pet3dText, "Caption", "", 12, Theme.TextSecondary);
            IconImg(pet3d.transform, "paw", Theme.Forest700, 22);

            var banner = Banner(content);
            var submit = PrimaryButton(content, "SubmitButton", "List for adoption", Theme.Forest700, Theme.OnBrand);

            var screen = root.AddComponent<AdoptionCreateScreen>();
            Wire(screen, "manager", manager, "backButton", back, "nameField", name, "speciesChips", species,
                "ageChips", age, "sizeChips", size, "energyChips", energy, "kidsChips", kids, "petsChips", pets,
                "gardenChips", garden, "locationField", location, "useLocationLink", useLocationLink,
                "descriptionField", description, "pet3dButton", pet3dButton, "pet3dCaption", pet3dCaption,
                "submitButton", submit, "banner", banner);
            return root;
        }
    }
}
