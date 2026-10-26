using LostPetNetwork.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    public static partial class AppUIBuilder
    {
        const int TabScreenBottomPadding = 120;

        // ------------------------------------------------------------------ shared pieces

        static void Logo(Transform parent, bool withPets = false)
        {
            var row = NewRect("Logo", parent);
            HLayout(row.gameObject, 12, null, TextAnchor.MiddleLeft, expandWidth: false);

            var circle = Img(row, "Mark", Theme.Forest700, _circle);
            Le(circle.gameObject, prefH: 48, prefW: 48);
            var paw = IconImg(circle.transform, "paw", Theme.OnBrand, 26);
            paw.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(paw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));

            Txt(row, "Wordmark", "LostPetNetwork", 20, Theme.Forest900, F.Heading);

            if (!withPets) return;
            var flex = NewRect("Flex", row);
            Le(flex.gameObject, flexW: 1);
            var group = NewRect("PetAvatars", row);
            HLayout(group.gameObject, -10, null, TextAnchor.MiddleRight);
            foreach (var key in new[] { "biscuit", "luna", "max" })
            {
                var ringImg = Img(group, key, Theme.Cream, _circle);
                Le(ringImg.gameObject, prefH: 40, prefW: 40);
                var photo = Img(ringImg.transform, "Photo", Theme.Forest100, _circle);
                photo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                Stretch(photo.rectTransform, 3, 3, 3, 3);
                CoverSlot(photo.rectTransform, out var cover);
                cover.Set(Photo(key));
            }
        }

        static GameObject PhotoBox(Transform parent, string name, float height, out CoverImage cover, Sprite round = null)
        {
            var box = Img(parent, name, Theme.Forest100, round ?? _round24);
            box.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Le(box.gameObject, prefH: height);
            var paw = IconImg(box.transform, "paw", new Color(Theme.Forest700.r, Theme.Forest700.g, Theme.Forest700.b, 0.35f), 40);
            paw.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(paw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            CoverSlot(box.rectTransform, out cover);
            return box.gameObject;
        }

        static TMP_Text Header(Transform parent, string title, out GameObject trailingHost)
        {
            var row = NewRect("Header", parent);
            HLayout(row.gameObject, 12, null, TextAnchor.MiddleLeft);
            var t = Txt(row, "Title", title, 26, Theme.Forest900, F.Heading);
            Le(t.gameObject, flexW: 1);
            trailingHost = row.gameObject;
            return t;
        }

        static void SectionTitle(Transform parent, string text) =>
            Txt(parent, "SectionTitle", text, 20, Theme.TextPrimary, F.Heading);

        // ------------------------------------------------------------------ auth screens

        static GameObject BuildSplash(Transform parent)
        {
            var root = NewRect("Splash", parent);
            Stretch(root);
            VLayout(root.gameObject, 16, null, TextAnchor.MiddleCenter, expandWidth: false);
            Logo(root);
            Txt(root, "Loading", "Loading…", 15, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);
            return root.gameObject;
        }

        static GameObject BuildSignIn(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "SignIn");
            Spacer(content, 8);
            Logo(content);
            Spacer(content, 4);
            Txt(content, "Title", "Welcome back", 32, Theme.Forest900, F.Heading);
            Txt(content, "Subtitle", "Sign in to track your reports and help lost pets find their way home.", 16, Theme.TextSecondary);
            PhotoBox(content, "HeroPhoto", 110, out var hero);
            hero.Set(Photo("auth"));
            var email = TextField(content, "Email", "Email", "you@example.com", TMP_InputField.ContentType.EmailAddress);
            var password = TextField(content, "Password", "Password", "Enter your password", TMP_InputField.ContentType.Password);
            var forgot = LinkButton(content, "ForgotPassword", "Forgot password?", TextAlignmentOptions.Right);
            var banner = Banner(content);
            var signIn = PrimaryButton(content, "SignInButton", "Sign in", Theme.Forest700, Theme.OnBrand);
            var create = FooterLink(content, "New here?", "Create an account");

            var screen = root.AddComponent<SignInScreen>();
            Wire(screen, "manager", manager, "email", email, "password", password, "signInButton", signIn,
                "banner", banner, "forgotPasswordLink", forgot, "createAccountLink", create);
            return root;
        }

        static GameObject BuildSignUp(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "SignUp");
            Spacer(content, 8);
            Logo(content, withPets: true);
            Spacer(content, 4);
            Txt(content, "Title", "Join your neighbourhood", 32, Theme.Forest900, F.Heading);
            Txt(content, "Subtitle", "Create a free account to report a lost pet or help reunite one.", 16, Theme.TextSecondary);
            var name = TextField(content, "Name", "Full name", "Your name", TMP_InputField.ContentType.Name);
            var email = TextField(content, "Email", "Email", "you@example.com", TMP_InputField.ContentType.EmailAddress);
            var password = TextField(content, "Password", "Password", "At least 8 characters, letters and numbers", TMP_InputField.ContentType.Password);
            var banner = Banner(content);
            var create = PrimaryButton(content, "CreateButton", "Create account", Theme.Forest700, Theme.OnBrand);
            Txt(content, "Legal", "By continuing you agree to our Terms and Privacy Policy. Your phone number is never shown publicly.", 12, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);
            var signIn = FooterLink(content, "Already have an account?", "Sign in");

            var screen = root.AddComponent<SignUpScreen>();
            Wire(screen, "manager", manager, "displayName", name, "email", email, "password", password,
                "createButton", create, "banner", banner, "signInLink", signIn);
            return root;
        }

        static GameObject BuildResetPassword(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "ResetPassword");
            Spacer(content, 8);
            Logo(content);
            Spacer(content, 4);
            Txt(content, "Title", "Reset your password", 32, Theme.Forest900, F.Heading);
            Txt(content, "Subtitle", "Enter your email and we'll send you a 6-digit code to choose a new password.", 16, Theme.TextSecondary);
            var email = TextField(content, "Email", "Email", "you@example.com", TMP_InputField.ContentType.EmailAddress);

            var step = NewRect("CodeStep", content);
            VLayout(step.gameObject, 16);
            var code = TextField(step, "Code", "6-digit code", "123456", TMP_InputField.ContentType.IntegerNumber);
            var newPassword = TextField(step, "NewPassword", "New password", "At least 8 characters, letters and numbers", TMP_InputField.ContentType.Password);
            var resend = LinkButton(step, "ResendCode", "Resend code", TextAlignmentOptions.Right);

            var banner = Banner(content);
            var primary = PrimaryButton(content, "PrimaryButton", "Send code", Theme.Forest700, Theme.OnBrand);
            var back = LinkButton(content, "BackToSignIn", "Back to sign in", TextAlignmentOptions.Center);

            var screen = root.AddComponent<ResetPasswordScreen>();
            Wire(screen, "manager", manager, "email", email, "codeStep", step.gameObject, "code", code,
                "newPassword", newPassword, "primaryButton", primary, "banner", banner,
                "resendLink", resend, "backLink", back);
            return root;
        }

        // ------------------------------------------------------------------ Home

        static GameObject BuildHome(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "Home", TabScreenBottomPadding);
            Spacer(content, 4);
            var greeting = Txt(content, "Greeting", "Good morning", 15, Theme.TextSecondary, F.Medium);
            Txt(content, "Headline", "Let’s bring pets home", 26, Theme.Forest900, F.Heading);

            var hero = Img(content, "Hero", Theme.Forest700, _round24);
            VLayout(hero.gameObject, 10, new RectOffset(20, 20, 20, 20));
            var eyebrow = Txt(hero.transform, "Eyebrow", "FOR NEIGHBOURS, BY NEIGHBOURS", 11, Theme.Forest100, F.Bold);
            eyebrow.characterSpacing = 4;
            Txt(hero.transform, "Title", "Missing a pet? Report it in 2 minutes.", 24, Theme.OnBrand, F.Heading);
            var actions = NewRect("Actions", hero.transform);
            HLayout(actions.gameObject, 12, new RectOffset(0, 0, 6, 0), TextAnchor.MiddleLeft, expandWidth: true);
            var lost = PrimaryButton(actions, "ReportLostButton", "Lost a pet", Theme.Terracotta, Theme.OnBrand);
            var found = PrimaryButton(actions, "ReportFoundButton", "Found a pet", Theme.Forest50, Theme.Forest700);
            Le(lost.gameObject, flexW: 1); Le(found.gameObject, flexW: 1);

            var sosCard = Img(content, "SosCard", Theme.TerracottaSoft, _round24);
            HLayout(sosCard.gameObject, 12, new RectOffset(20, 16, 16, 16), TextAnchor.MiddleLeft, expandWidth: false);
            var sosButton = sosCard.gameObject.AddComponent<Button>();
            sosButton.targetGraphic = sosCard;
            sosButton.transition = Selectable.Transition.None;
            var sosText = NewRect("Text", sosCard.transform);
            VLayout(sosText.gameObject, 2);
            Le(sosText.gameObject, flexW: 1);
            Txt(sosText, "Title", "Animal in danger?", 18, Theme.TerracottaInk, F.Heading);
            Txt(sosText, "Sub", "Send an SOS or help a neighbour", 13, Theme.TerracottaInk);
            IconImg(sosCard.transform, "chevron", Theme.TerracottaInk, 22);

            var header = NewRect("SectionHeader", content);
            HLayout(header.gameObject, 8, null, TextAnchor.MiddleLeft);
            var st = Txt(header, "Title", "Recently reported near you", 20, Theme.TextPrimary, F.Heading);
            Le(st.gameObject, flexW: 1);
            var seeAll = LinkButton(header, "SeeAll", "See all", TextAlignmentOptions.Right);

            var banner = Banner(content);
            var list = NewRect("List", content);
            VLayout(list.gameObject, 16);
            var loading = LoadingBlock(content);
            var empty = EmptyState(content, "No reports nearby yet. Be the first to report a lost or found pet.");

            var screen = root.AddComponent<HomeScreen>();
            Wire(screen, "manager", manager, "greeting", greeting, "reportLostButton", lost.Button,
                "reportFoundButton", found.Button, "sosButton", sosButton, "seeAllButton", seeAll, "listRoot", list,
                "cardPrefab", _cardPrefab, "loading", loading, "emptyState", empty.root, "banner", banner);
            return root;
        }

        // ------------------------------------------------------------------ Explore

        static GameObject BuildExplore(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "Explore", TabScreenBottomPadding);
            Header(content, "Explore", out var headerHost);
            var (mapPill, mapBg) = Pill(headerHost.transform, "MapButton", Theme.Card, new RectOffset(14, 16, 8, 8));
            mapBg.raycastTarget = true;
            var mapOutline = mapBg.gameObject.AddComponent<Outline>();
            mapOutline.effectColor = Theme.Border;
            mapOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var mapButton = mapPill.gameObject.AddComponent<Button>();
            mapButton.targetGraphic = mapBg;
            mapPill.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            IconImg(mapPill, "map", Theme.Forest700, 20);
            Txt(mapPill, "Label", "Map", 13, Theme.Forest700, F.Bold);
            var (arPill, arBg) = Pill(headerHost.transform, "RadarButton", Theme.Card, new RectOffset(14, 16, 8, 8));
            arBg.raycastTarget = true;
            var arOutline = arBg.gameObject.AddComponent<Outline>();
            arOutline.effectColor = Theme.Border;
            arOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var radarButton = arPill.gameObject.AddComponent<Button>();
            radarButton.targetGraphic = arBg;
            arPill.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            IconImg(arPill, "camera", Theme.Forest700, 20);
            Txt(arPill, "Label", "AR", 13, Theme.Forest700, F.Bold);
            arPill.gameObject.SetActive(false);

            var search = SearchField(content, "Search", "Breed, colour or area");
            var chips = ChipRow(content, "Filters", "All", "Lost", "Found", "Dogs", "Cats");
            var sortChips = ChipRow(content, "Sort", "Newest", "Nearest", "Within 5 km");
            var count = Txt(content, "Count", "0 reports · Demo data", 12, Theme.TextSecondary, F.Medium);
            var banner = Banner(content);
            var list = NewRect("List", content);
            VLayout(list.gameObject, 16);
            var loading = LoadingBlock(content);
            var empty = EmptyState(content, "No reports match your search. Try a different word or filter.");

            var screen = root.AddComponent<ExploreScreen>();
            Wire(screen, "manager", manager, "mapButton", mapButton, "radarButton", radarButton, "search", search, "chips", chips, "sortChips", sortChips,
                "countLabel", count, "listRoot", list, "cardPrefab", _cardPrefab, "loading", loading,
                "emptyState", empty.root, "banner", banner);
            return root;
        }

        // ------------------------------------------------------------------ Map

        static GameObject BuildMap(Transform parent, ScreenManager manager)
        {
            var root = NewRect("Map", parent);
            Stretch(root);
            var back = Img(root, "MapBackground", Theme.CreamDeep);
            Stretch(back.rectTransform);
            back.gameObject.AddComponent<RectMask2D>();
            var mapArt = NewRect("MapArt", back.transform);
            Stretch(mapArt);

            Color road = Theme.Card, park = new Color(0.81f, 0.88f, 0.81f), water = new Color(0.79f, 0.87f, 0.89f);
            Shape(mapArt, "Park", _round24, park, new Vector2(0.15f, 0.62f), new Vector2(0.55f, 0.2f), 0);
            Shape(mapArt, "Park", _round24, park, new Vector2(0.85f, 0.3f), new Vector2(0.4f, 0.14f), 0);
            Shape(mapArt, "Park", _round24, park, new Vector2(0.8f, 0.82f), new Vector2(0.3f, 0.1f), 0);
            Shape(mapArt, "Lake", _circle, water, new Vector2(0.72f, 0.55f), new Vector2(0.5f, 0.15f), 0);
            Shape(mapArt, "Road", null, road, new Vector2(0.5f, 0.68f), new Vector2(1.3f, 0.02f), -18);
            Shape(mapArt, "Road", null, road, new Vector2(0.5f, 0.42f), new Vector2(1.3f, 0.015f), 12);
            Shape(mapArt, "Road", null, road, new Vector2(0.5f, 0.25f), new Vector2(1.3f, 0.015f), -8);
            Shape(mapArt, "Road", null, road, new Vector2(0.22f, 0.5f), new Vector2(0.035f, 1.2f), 8);
            Shape(mapArt, "Road", null, road, new Vector2(0.64f, 0.5f), new Vector2(0.03f, 1.2f), -6);
            Shape(mapArt, "Road", null, road, new Vector2(0.86f, 0.5f), new Vector2(0.025f, 1.2f), 14);

            // Live map (Mapbox tiles). Shown instead of the stylised art when a map token is configured.
            var live = Img(back.transform, "MapLive", new Color(0.93f, 0.91f, 0.86f, 1f));
            Stretch(live.rectTransform);
            var tileLayer = NewRect("Tiles", live.transform);
            Stretch(tileLayer);
            var mapView = live.gameObject.AddComponent<MapView>();
            Wire(mapView, "tileLayer", tileLayer);
            live.gameObject.SetActive(false);

            var overlay = NewRect("Overlay", root);
            Stretch(overlay);
            var youDot = Img(overlay, "YouMarker", new Color(0.23f, 0.43f, 0.65f, 1f), _circle);
            youDot.raycastTarget = false;
            Anchor(youDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var youRing = Img(youDot.transform, "Ring", Color.white, _ring24);
            youRing.raycastTarget = false;
            Stretch(youRing.rectTransform);
            youDot.gameObject.SetActive(false);

            var pinLayer = NewRect("Pins", root);
            Stretch(pinLayer);

            var top = NewRect("Controls", root);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = Vector2.zero;
            VLayout(top.gameObject, 12, new RectOffset(24, 24, 12, 0));
            top.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var searchRow = NewRect("SearchRow", top);
            HLayout(searchRow.gameObject, 12, null, TextAnchor.MiddleLeft, expandWidth: false);
            var search = SearchField(searchRow, "Search", "Search this area");
            var close = CircleIconButton(searchRow, "CloseButton", "x", 52);
            var chips = ChipRow(top, "Filters", "All", "Lost", "Found");
            var noteHost = LeftAligned(top, "NoteRow");
            var (note, _) = Pill(noteHost.transform, "Note", new Color(1f, 0.99f, 0.97f, 0.92f), new RectOffset(12, 12, 5, 5));
            var noteLabel = Txt(note, "Label", "Stylised map preview · demo locations", 11, Theme.TextSecondary, F.Medium);

            var liveControls = NewRect("LiveControls", root);
            Stretch(liveControls);
            var zoomColumn = NewRect("Zoom", liveControls);
            Anchor(zoomColumn, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 20), new Vector2(48, 0));
            VLayout(zoomColumn.gameObject, 10, null, TextAnchor.MiddleCenter, expandWidth: false);
            zoomColumn.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var zoomIn = CircleIconButton(zoomColumn, "ZoomInButton", "plus", 48);
            var zoomOut = CircleTextButton(zoomColumn, "ZoomOutButton", "−", 48);
            var locate = CircleIconButton(zoomColumn, "LocateButton", "locate", 48);
            var attribution = Txt(liveControls, "Attribution", "© Mapbox © OpenStreetMap", 10, Theme.TextSecondary, F.Medium);
            attribution.raycastTarget = false;
            Anchor(attribution.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(10, 88), new Vector2(220, 14));
            liveControls.gameObject.SetActive(false);

            var rowHost = NewRect("SelectedCardHost", root);
            rowHost.anchorMin = new Vector2(0, 0); rowHost.anchorMax = new Vector2(1, 0); rowHost.pivot = new Vector2(0.5f, 0);
            rowHost.anchoredPosition = new Vector2(0, 100);
            rowHost.sizeDelta = new Vector2(-48, 0);
            VLayout(rowHost.gameObject, 0);
            rowHost.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var card = (ReportRowView)PrefabUtilityInstantiate(_rowPrefab, rowHost);
            card.name = "SelectedCard";

            var empty = EmptyState(root, "No reports match your filters.");
            var emptyRt = (RectTransform)empty.root.transform;
            Anchor(emptyRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 160));
            var loading = LoadingBlock(root);
            Anchor((RectTransform)loading.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 40));
            var banner = Banner(root);
            Anchor((RectTransform)banner.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(340, 60));

            var screen = root.gameObject.AddComponent<MapScreen>();
            Wire(screen, "manager", manager, "closeButton", close, "search", search, "chips", chips,
                "pinLayer", pinLayer, "pinPrefab", _pinPrefab, "selectedCard", card,
                "emptyState", empty.root, "loading", loading, "banner", banner,
                "mapView", mapView, "liveMapRoot", live.gameObject, "liveControls", liveControls.gameObject,
                "stylisedArt", mapArt.gameObject, "noteLabel", noteLabel, "zoomInButton", zoomIn,
                "zoomOutButton", zoomOut, "locateButton", locate, "youMarker", youDot.rectTransform);
            return root.gameObject;
        }

        static Button CircleTextButton(Transform parent, string name, string text, float size = 44)
        {
            var img = Img(parent, name, Theme.Card, _circle);
            Le(img.gameObject, prefH: size, prefW: size);
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.Border;
            outline.effectDistance = new Vector2(1f, 1f);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var label = Txt(img.transform, "Label", text, 26, Theme.TextPrimary, F.Medium, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return b;
        }

        static void Shape(RectTransform parent, string name, Sprite sprite, Color color, Vector2 center, Vector2 size, float rotation)
        {
            var img = Img(parent, name, color, sprite);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = center;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            // size is a fraction of a 390x844 reference
            rt.sizeDelta = new Vector2(size.x * 390f, size.y * 844f);
            rt.localEulerAngles = new Vector3(0, 0, rotation);
        }

        static Component PrefabUtilityInstantiate(Component prefab, Transform parent)
        {
            var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            return go.GetComponent(prefab.GetType());
        }

        // ------------------------------------------------------------------ Report

        static GameObject BuildReport(Transform parent, ScreenManager manager)
        {
            var container = NewRect("Report", parent);
            Stretch(container);

            // ---- form
            var (form, content) = NewScreen(container, "Form", 24, viewportBottomInset: 100);
            var head = NewRect("Header", content);
            HLayout(head.gameObject, 12, null, TextAnchor.MiddleLeft);
            var back = CircleIconButton(head, "BackButton", "back");
            var title = Txt(head, "Title", "Report a pet", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);

            var seg = Img(content, "TypeToggle", Theme.Card, _round16);
            HLayout(seg.gameObject, 4, new RectOffset(4, 4, 4, 4), TextAnchor.MiddleCenter, expandWidth: true);
            var segOutline = seg.gameObject.AddComponent<Outline>();
            segOutline.effectColor = Theme.Border; segOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var (lostBtn, lostBg, lostLabel) = Segment(seg.transform, "LostToggle", "I lost a pet");
            var (foundBtn, foundBg, foundLabel) = Segment(seg.transform, "FoundToggle", "I found a pet");

            var photoBox = Img(content, "PhotoBox", Theme.Forest50, _round24);
            photoBox.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Le(photoBox.gameObject, prefH: 132);
            var photoOutline = photoBox.gameObject.AddComponent<Outline>();
            photoOutline.effectColor = Theme.Forest700; photoOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var photoButton = photoBox.gameObject.AddComponent<Button>();
            photoButton.targetGraphic = photoBox;
            photoButton.transition = Selectable.Transition.None;
            var placeholder = NewRect("Placeholder", photoBox.transform);
            Stretch(placeholder);
            VLayout(placeholder.gameObject, 6, null, TextAnchor.MiddleCenter, expandWidth: false);
            var camCircle = Img(placeholder, "Circle", Theme.Forest700, _circle);
            Le(camCircle.gameObject, prefH: 48, prefW: 48);
            var camIcon = IconImg(camCircle.transform, "camera", Theme.OnBrand, 24);
            camIcon.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(camIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
            Txt(placeholder, "Title", "Add a clear photo", 15, Theme.Forest900, F.Medium, TextAlignmentOptions.Center);
            Txt(placeholder, "Caption", "Tap to open the camera", 12, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);
            CoverSlot(photoBox.rectTransform, out var photoPreview);
            photoPreview.gameObject.SetActive(false);

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
            var pet3dCaption = Txt(pet3dText, "Caption", "Optional — helps neighbours recognise your pet", 12, Theme.TextSecondary);
            IconImg(pet3d.transform, "paw", Theme.Forest700, 22);

            Txt(content, "SpeciesLabel", "What kind of pet?", 13, Theme.TextPrimary, F.Bold);
            var species = ChipRow(content, "SpeciesChips", "Dog", "Cat", "Bird", "Other");
            var location = TextField(content, "Location", "Where was it last seen?", "Street, park or neighbourhood", TMP_InputField.ContentType.Standard);
            var useLocation = NewRect("UseLocation", content);
            HLayout(useLocation.gameObject, 6, null, TextAnchor.MiddleLeft);
            IconImg(useLocation, "locate", Theme.Forest700, 16);
            var useLocationLink = LinkButton(useLocation, "Link", "Use my current location", TextAlignmentOptions.Left);
            Le(useLocationLink.gameObject, minH: 32);
            Txt(content, "WhenLabel", "When?", 13, Theme.TextPrimary, F.Bold);
            var when = ChipRow(content, "WhenChips", "Just now", "Today", "Yesterday", "Earlier");

            var more = Img(content, "MoreDetails", Theme.Card, _round16);
            HLayout(more.gameObject, 12, new RectOffset(16, 16, 14, 14), TextAnchor.MiddleLeft, expandWidth: false);
            var moreOutline = more.gameObject.AddComponent<Outline>();
            moreOutline.effectColor = Theme.Border; moreOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var moreButton = more.gameObject.AddComponent<Button>();
            moreButton.targetGraphic = more;
            moreButton.transition = Selectable.Transition.None;
            var moreText = NewRect("Text", more.transform);
            VLayout(moreText.gameObject, 2);
            Le(moreText.gameObject, flexW: 1);
            Txt(moreText, "Title", "Add more details", 15, Theme.TextPrimary, F.Medium);
            Txt(moreText, "Caption", "Name, colour, collar — optional", 12, Theme.TextSecondary);
            IconImg(more.transform, "plus", Theme.Forest700, 22);

            var moreSection = NewRect("MoreSection", content);
            VLayout(moreSection.gameObject, 16);
            var nameField = TextField(moreSection, "Name", "Pet's name (optional)", "e.g. Biscuit", TMP_InputField.ContentType.Standard);
            var descField = TextField(moreSection, "Description", "Description (optional)", "Colour, collar, markings, behaviour", TMP_InputField.ContentType.Standard, multiline: true);
            moreSection.gameObject.SetActive(false);
            var banner = Banner(content);

            var bar = Img(form.transform, "BottomBar", Theme.Cream);
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0.5f, 0); bar.rectTransform.offsetMin = Vector2.zero; bar.rectTransform.offsetMax = new Vector2(0, 100);
            VLayout(bar.gameObject, 0, new RectOffset(24, 24, 16, 28));
            var review = PrimaryButton(bar.transform, "ReviewButton", "Review lost report", Theme.Forest700, Theme.OnBrand);

            // ---- review
            var (reviewRoot, rContent) = NewScreen(container, "Review", 24, viewportBottomInset: 150);
            var rHead = NewRect("Header", rContent);
            HLayout(rHead.gameObject, 12, null, TextAnchor.MiddleLeft);
            var rBack = CircleIconButton(rHead, "BackButton", "back");
            var rTitle = Txt(rHead, "Title", "Review your report", 26, Theme.Forest900, F.Heading);
            Le(rTitle.gameObject, flexW: 1);
            var summaryCard = Img(rContent, "SummaryCard", Theme.Card, _round24);
            VLayout(summaryCard.gameObject, 12, new RectOffset(16, 16, 16, 16));
            var sOutline = summaryCard.gameObject.AddComponent<Outline>();
            sOutline.effectColor = Theme.Border; sOutline.effectDistance = new Vector2(1.2f, 1.2f);
            PhotoBox(summaryCard.transform, "Photo", 180, out var reviewPhoto);
            var summary = Txt(summaryCard.transform, "Summary", "", 15, Theme.TextPrimary);
            summary.richText = true;
            summary.lineSpacing = 8;
            var reviewBanner = Banner(rContent);
            var demoNote = Txt(rContent, "Demo", "Demo mode: your report is kept only while the app is open.", 12, Theme.TextSecondary);
            var rBar = Img(reviewRoot.transform, "BottomBar", Theme.Cream);
            rBar.rectTransform.anchorMin = new Vector2(0, 0); rBar.rectTransform.anchorMax = new Vector2(1, 0);
            rBar.rectTransform.pivot = new Vector2(0.5f, 0); rBar.rectTransform.offsetMin = Vector2.zero; rBar.rectTransform.offsetMax = new Vector2(0, 150);
            VLayout(rBar.gameObject, 4, new RectOffset(24, 24, 16, 24));
            var post = PrimaryButton(rBar.transform, "PostButton", "Post report", Theme.Forest700, Theme.OnBrand);
            var edit = LinkButton(rBar.transform, "EditButton", "Edit details", TextAlignmentOptions.Center);
            reviewRoot.SetActive(false);

            var screen = container.gameObject.AddComponent<ReportScreen>();
            Wire(screen, "manager", manager, "formRoot", form, "backButton", back, "lostToggle", lostBtn,
                "lostToggleBackground", lostBg, "lostToggleLabel", lostLabel, "foundToggle", foundBtn,
                "foundToggleBackground", foundBg, "foundToggleLabel", foundLabel, "photoBox", photoButton,
                "photoPreview", photoPreview, "photoPlaceholder", placeholder.gameObject, "speciesChips", species,
                "locationField", location, "useLocationLink", useLocationLink, "whenChips", when,
                "moreToggle", moreButton, "moreSection", moreSection.gameObject, "nameField", nameField,
                "descriptionField", descField, "reviewButton", review, "banner", banner,
                "reviewRoot", reviewRoot, "reviewSummary", summary, "reviewPhoto", reviewPhoto,
                "postButton", post, "editButton", edit, "reviewBanner", reviewBanner,
                "pet3dButton", pet3dButton, "pet3dCaption", pet3dCaption, "demoNote", demoNote.gameObject);
            // back button on the review header behaves like "Edit"
            WireBackToEdit(screen, rBack);
            return container.gameObject;
        }

        static void WireBackToEdit(ReportScreen screen, Button reviewBack)
        {
            // Persistent listener so the header back arrow on the review page returns to the form.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(reviewBack.onClick, screen.ShowFormFromReview);
        }

        static (Button button, Image background, TMP_Text label) Segment(Transform parent, string name, string text)
        {
            var img = Img(parent, name, Color.clear, _round16);
            HLayout(img.gameObject, 0, new RectOffset(0, 0, 12, 12), TextAnchor.MiddleCenter, expandWidth: true);
            Le(img.gameObject, flexW: 1);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            var label = Txt(img.transform, "Label", text, 16, Theme.TextSecondary, F.Bold, TextAlignmentOptions.Center);
            return (b, img, label);
        }

        // ------------------------------------------------------------------ Camera

        static GameObject BuildCamera(Transform parent, ScreenManager manager)
        {
            var root = NewRect("Camera", parent);
            Stretch(root);
            var bg = Img(root, "Background", Theme.Forest900);
            Stretch(bg.rectTransform);

            var previewHost = NewRect("PreviewHost", root);
            Stretch(previewHost, 0, 0, 0, 230);
            previewHost.gameObject.AddComponent<RectMask2D>();
            var previewGo = NewRect("Preview", previewHost);
            Anchor(previewGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var raw = previewGo.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            var fitter = previewGo.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 0.75f;

            var frame = Img(previewHost, "FocusFrame", new Color(1, 1, 1, 0.9f), _ring24);
            frame.raycastTarget = false;
            Anchor(frame.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 320));

            var (hint, _) = Pill(root, "Hint", new Color(0.09f, 0.23f, 0.17f, 0.85f), new RectOffset(16, 16, 10, 10));
            Txt(hint, "Label", "Centre your pet in the frame", 13, Theme.OnBrand, F.Bold);
            Anchor(hint, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 250), Vector2.zero);

            var close = CircleIconButton(root, "CloseButton", "x", 44, dark: true);
            Anchor((RectTransform)close.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(44, 44));
            close.GetComponent<LayoutElement>().ignoreLayout = true;

            var controls = NewRect("Controls", root);
            controls.anchorMin = new Vector2(0, 0); controls.anchorMax = new Vector2(1, 0); controls.pivot = new Vector2(0.5f, 0);
            controls.offsetMin = Vector2.zero; controls.offsetMax = new Vector2(0, 230);
            var gallery = RoundSquareButton(controls, "GalleryButton", "image", new Vector2(0.15f, 0.62f));
            var flip = CircleIconButton(controls, "FlipButton", "flip", 56, dark: true);
            Anchor((RectTransform)flip.transform, new Vector2(0.85f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            flip.GetComponent<LayoutElement>().ignoreLayout = true;

            var ring = Img(controls, "ShutterRing", new Color(1, 1, 1, 0.35f), _circle);
            Anchor(ring.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
            var shutterImg = Img(ring.transform, "Shutter", Color.white, _circle);
            Stretch(shutterImg.rectTransform, 6, 6, 6, 6);
            var shutter = shutterImg.gameObject.AddComponent<Button>();
            shutter.targetGraphic = shutterImg;
            var shutterColors = shutter.colors;
            shutterColors.disabledColor = new Color(1, 1, 1, 0.35f);
            shutter.colors = shutterColors;
            var cap = Txt(controls, "Caption", "Photos help match your pet with reports nearby", 12, Theme.Forest100, F.Body, TextAlignmentOptions.Center);
            Anchor(cap.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 40));

            var status = Img(root, "StatusPanel", Theme.Card, _round24);
            VLayout(status.gameObject, 8, new RectOffset(20, 20, 20, 20), TextAnchor.MiddleCenter);
            Anchor(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 0));
            status.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var statusLabel = Txt(status.transform, "Message", "", 15, Theme.TextPrimary, F.Body, TextAlignmentOptions.Center);
            status.gameObject.SetActive(false);

            var screen = root.gameObject.AddComponent<CameraScreen>();
            Wire(screen, "manager", manager, "preview", raw, "previewFitter", fitter, "closeButton", close,
                "shutterButton", shutter, "galleryButton", gallery, "flipButton", flip,
                "statusPanel", status.gameObject, "statusLabel", statusLabel);
            return root.gameObject;
        }

        static Button RoundSquareButton(RectTransform parent, string name, string icon, Vector2 anchor)
        {
            var img = Img(parent, name, new Color(1, 0.99f, 0.97f, 0.18f), _round16);
            Anchor(img.rectTransform, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var ic = IconImg(img.transform, icon, Theme.OnBrand, 26);
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            return b;
        }

        // ------------------------------------------------------------------ Details

        static GameObject BuildDetails(Transform parent, ScreenManager manager)
        {
            var container = NewRect("Details", parent);
            Stretch(container);
            var (scrollRoot, content) = NewScreen(container, "Scroll", 24, viewportBottomInset: 150);
            // Photo scrolls with the content; remove side padding so it is edge to edge.
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 24);
            layout.spacing = 0;

            var photoBox = NewRect("Photo", content);
            Le(photoBox.gameObject, prefH: 300);
            photoBox.gameObject.AddComponent<RectMask2D>();
            PlaceholderFill(photoBox, Theme.Forest100);
            CoverSlot(photoBox, out var cover);
            var sheetCap = Img(photoBox, "SheetTop", Theme.Cream, _round24);
            sheetCap.rectTransform.anchorMin = new Vector2(0, 0);
            sheetCap.rectTransform.anchorMax = new Vector2(1, 0);
            sheetCap.rectTransform.pivot = new Vector2(0.5f, 0);
            sheetCap.rectTransform.offsetMin = new Vector2(0, -30);
            sheetCap.rectTransform.offsetMax = new Vector2(0, 30);
            sheetCap.raycastTarget = false;

            var body = NewRect("Body", content);
            VLayout(body.gameObject, 14, new RectOffset(24, 24, 0, 0));
            var titleRow = NewRect("TitleRow", body);
            HLayout(titleRow.gameObject, 12, null, TextAnchor.MiddleLeft);
            var nameLabel = Txt(titleRow, "Name", "Biscuit", 32, Theme.Forest900, F.Heading);
            Le(nameLabel.gameObject, flexW: 1);
            var badge = Badge(titleRow);
            var summary = Txt(body, "Summary", "Golden Retriever", 15, Theme.TextSecondary);

            var petCard = Img(body, "Pet3DCard", Theme.Forest50, _round24);
            VLayout(petCard.gameObject, 4, new RectOffset(8, 8, 8, 12));
            var petStage = NewRect("Preview", petCard.transform);
            Le(petStage.gameObject, prefH: 220);
            petStage.gameObject.AddComponent<RawImage>();
            var petPreview = petStage.gameObject.AddComponent<Preview3D>();
            var petCaption = Txt(petCard.transform, "Caption", "", 12, Theme.TextSecondary, F.Medium, TextAlignmentOptions.Center);
            petCard.gameObject.SetActive(false);

            var info = Img(body, "InfoCard", Theme.Card, _round16);
            VLayout(info.gameObject, 0, new RectOffset(0, 0, 4, 4));
            var infoOutline = info.gameObject.AddComponent<Outline>();
            infoOutline.effectColor = Theme.Border; infoOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var locValue = InfoRow(info.transform, "pin", "Last seen");
            var whenValue = InfoRow(info.transform, "calendar", "Reported");
            var distValue = InfoRow(info.transform, "clock", "Distance");

            var description = Txt(body, "Description", "", 15, Theme.TextPrimary);
            var reporter = NewRect("Reporter", body);
            HLayout(reporter.gameObject, 12, new RectOffset(0, 0, 4, 0), TextAnchor.MiddleLeft);
            var avatar = Img(reporter, "Avatar", Theme.Forest100, _circle);
            Le(avatar.gameObject, prefH: 40, prefW: 40);
            var initials = Txt(avatar.transform, "Initials", "SK", 13, Theme.Forest900, F.Bold, TextAlignmentOptions.Center);
            Stretch(initials.rectTransform);
            var rCol = NewRect("Text", reporter);
            VLayout(rCol.gameObject, 0);
            var rName = Txt(rCol, "Name", "Reported by", 15, Theme.TextPrimary, F.Medium);
            var rMeta = Txt(rCol, "Meta", "", 12, Theme.TextSecondary);
            var matchCard = Img(body, "MatchSection", Theme.Card, _round24);
            VLayout(matchCard.gameObject, 10, new RectOffset(16, 16, 16, 16));
            var matchOutline = matchCard.gameObject.AddComponent<Outline>();
            matchOutline.effectColor = Theme.Border; matchOutline.effectDistance = new Vector2(1.2f, 1.2f);
            Txt(matchCard.transform, "Title", "Possible matches", 18, Theme.TextPrimary, F.Heading);
            Txt(matchCard.transform, "Info", "An AI helper compares your photo with nearby reports. The photos are sent to Google's Gemini to be compared.", 12, Theme.TextSecondary);
            var findMatches = PrimaryButton(matchCard.transform, "FindMatchesButton", "Find possible matches", Theme.Forest50, Theme.Forest700);
            var matchResults = NewRect("Results", matchCard.transform);
            VLayout(matchResults.gameObject, 10);
            var matchNote = Txt(matchCard.transform, "Note", "", 12, Theme.TextSecondary);
            matchCard.gameObject.SetActive(false);

            var banner = Banner(body);

            var back = CircleIconButton(container, "BackButton", "back");
            Anchor((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(44, 44));
            back.GetComponent<LayoutElement>().ignoreLayout = true;

            var bar = Img(container, "ContactBar", Theme.Card);
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0.5f, 0); bar.rectTransform.offsetMin = Vector2.zero; bar.rectTransform.offsetMax = new Vector2(0, 150);
            VLayout(bar.gameObject, 8, new RectOffset(24, 24, 14, 24));
            var action = PrimaryButton(bar.transform, "ActionButton", "I've seen this pet", Theme.Forest700, Theme.OnBrand);
            var hint = Txt(bar.transform, "Hint", "", 12, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);

            var screen = container.gameObject.AddComponent<DetailsScreen>();
            Wire(screen, "manager", manager, "backButton", back, "photo", cover, "badge", badge,
                "nameLabel", nameLabel, "summaryLabel", summary, "locationValue", locValue, "whenValue", whenValue,
                "distanceValue", distValue, "descriptionLabel", description, "reporterName", rName,
                "reporterMeta", rMeta, "reporterInitials", initials, "actionButton", action,
                "actionHint", hint, "banner", banner, "petCard", petCard.gameObject, "petPreview", petPreview,
                "petCaption", petCaption, "matchSection", matchCard.gameObject, "findMatchesButton", findMatches,
                "matchNote", matchNote, "matchResults", matchResults, "matchRowPrefab", _rowPrefab);
            return container.gameObject;
        }

        static TMP_Text InfoRow(Transform parent, string icon, string caption)
        {
            var row = NewRect("Row-" + caption, parent);
            HLayout(row.gameObject, 12, new RectOffset(16, 16, 10, 10), TextAnchor.MiddleLeft);
            IconImg(row, icon, Theme.Forest700, 20);
            var col = NewRect("Text", row);
            VLayout(col.gameObject, 0);
            Le(col.gameObject, flexW: 1);
            Txt(col, "Caption", caption, 12, Theme.TextSecondary);
            return Txt(col, "Value", "", 15, Theme.TextPrimary, F.Medium);
        }

        // ------------------------------------------------------------------ My reports

        static GameObject BuildMyReports(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "MyReports", TabScreenBottomPadding);
            Header(content, "My reports", out _);
            var chips = ChipRow(content, "Filters", "Active", "Reunited", "All");
            var demoNote = Txt(content, "DemoNote", "Demo data: reports you create are kept only while the app is open.", 12, Theme.TextSecondary, F.Medium);
            var banner = Banner(content);
            var list = NewRect("List", content);
            VLayout(list.gameObject, 12);
            var loading = LoadingBlock(content);
            var empty = EmptyState(content, "You haven't reported a pet yet.", "Report a pet");

            var screen = root.AddComponent<MyReportsScreen>();
            Wire(screen, "manager", manager, "chips", chips, "listRoot", list, "rowPrefab", _rowPrefab,
                "loading", loading, "emptyState", empty.root, "emptyLabel", empty.label,
                "emptyAction", empty.button, "demoNote", demoNote, "banner", banner);
            return root;
        }

        // ------------------------------------------------------------------ Profile

        static GameObject BuildProfile(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "Profile", TabScreenBottomPadding);
            Header(content, "Profile", out _);
            var banner = Banner(content);
            var loading = LoadingBlock(content);

            // ---- view mode
            var view = NewRect("View", content);
            VLayout(view.gameObject, 16);
            var head = NewRect("Head", view);
            HLayout(head.gameObject, 16, null, TextAnchor.MiddleLeft);
            var avatar = Img(head, "Avatar", Theme.Forest100, _circle);
            Le(avatar.gameObject, prefH: 72, prefW: 72);
            avatar.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var initials = Txt(avatar.transform, "Initials", "?", 24, Theme.Forest900, F.Heading, TextAlignmentOptions.Center);
            Stretch(initials.rectTransform);
            var portrait = Img(avatar.transform, "Portrait", Color.white);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            Stretch(portrait.rectTransform);
            portrait.gameObject.SetActive(false);
            var info = NewRect("Info", head);
            VLayout(info.gameObject, 2);
            Le(info.gameObject, flexW: 1);
            var nameLabel = Txt(info, "Name", "", 20, Theme.TextPrimary, F.Heading);
            var emailLabel = Txt(info, "Email", "", 14, Theme.TextSecondary);
            var cityLabel = Txt(info, "City", "", 13, Theme.TextSecondary, F.Medium);
            var memberLabel = Txt(info, "Member", "", 12, Theme.Forest700, F.Medium);

            var stats = NewRect("Stats", view);
            HLayout(stats.gameObject, 12, null, TextAnchor.MiddleCenter, expandWidth: true);
            var reportsCount = StatCard(stats, "Reports");
            var reunitedCount = StatCard(stats, "Reunited");
            var statsNote = Txt(view, "StatsNote", "Report counters use demo data for now.", 12, Theme.TextSecondary);

            var avatarButton = PrimaryButton(view, "AvatarButton", "Create my 3D avatar", Theme.Forest700, Theme.OnBrand);
            var avatarAr = PrimaryButton(view, "AvatarArButton", "See my avatar in AR", Theme.Forest50, Theme.Forest700);
            avatarAr.gameObject.SetActive(false);
            var edit = PrimaryButton(view, "EditButton", "Edit profile", Theme.Forest50, Theme.Forest700);

            var list = Img(view, "Settings", Theme.Card, _round24);
            VLayout(list.gameObject, 0);
            list.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            foreach (var (icon, title, sub) in new[]
            {
                ("bell", "Notifications", "Alerts for pets near you"),
                ("pin", "Search area", "Within 5 km of your location"),
                ("shield", "Privacy & contact", "Choose how finders reach you"),
                ("help", "Help & safety tips", "Meeting a finder safely")
            })
            {
                var r = NewRect("Setting-" + title, list.transform);
                HLayout(r.gameObject, 14, new RectOffset(16, 16, 14, 14), TextAnchor.MiddleLeft);
                var ib = Img(r, "IconBox", Theme.Forest50, _round16);
                Le(ib.gameObject, prefH: 40, prefW: 40);
                var ic = IconImg(ib.transform, icon, Theme.Forest700, 20);
                ic.GetComponent<LayoutElement>().ignoreLayout = true;
                Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
                var col = NewRect("Text", r);
                VLayout(col.gameObject, 2);
                Le(col.gameObject, flexW: 1);
                Txt(col, "Title", title, 15, Theme.TextPrimary, F.Medium);
                Txt(col, "Sub", sub, 12, Theme.TextSecondary);
                Txt(r, "Soon", "Soon", 11, Theme.Placeholder, F.Bold);
            }
            var signOut = PrimaryButton(view, "SignOutButton", "Sign out", Theme.Card, Theme.Forest700);
            var soOutline = signOut.gameObject.AddComponent<Outline>();
            soOutline.effectColor = Theme.Border; soOutline.effectDistance = new Vector2(1.2f, 1.2f);

            // ---- edit mode
            var editRoot = NewRect("Edit", content);
            VLayout(editRoot.gameObject, 16);
            var nameField = TextField(editRoot, "Name", "Full name", "Your name", TMP_InputField.ContentType.Name);
            var cityField = TextField(editRoot, "City", "City", "e.g. Tunis", TMP_InputField.ContentType.Standard);
            var phoneField = TextField(editRoot, "Phone", "Phone (optional)", "+216 ...", TMP_InputField.ContentType.Standard);
            Txt(editRoot, "PhoneNote", "Your phone number is never shown publicly.", 12, Theme.TextSecondary);
            var save = PrimaryButton(editRoot, "SaveButton", "Save changes", Theme.Forest700, Theme.OnBrand);
            var cancel = LinkButton(editRoot, "CancelButton", "Cancel", TextAlignmentOptions.Center);
            editRoot.gameObject.SetActive(false);

            var screen = root.AddComponent<ProfileScreen>();
            Wire(screen, "manager", manager, "viewRoot", view.gameObject, "editRoot", editRoot.gameObject,
                "initials", initials, "portrait", portrait, "avatarButton", avatarButton, "avatarArButton", avatarAr.Button, "nameLabel", nameLabel, "emailLabel", emailLabel, "cityLabel", cityLabel,
                "memberLabel", memberLabel, "reportsCount", reportsCount, "reunitedCount", reunitedCount,
                "editButton", edit.Button, "nameField", nameField, "cityField", cityField, "phoneField", phoneField,
                "saveButton", save, "cancelButton", cancel, "signOutButton", signOut, "loading", loading, "banner", banner,
                "statsNote", statsNote.gameObject);
            return root;
        }

        static TMP_Text StatCard(Transform parent, string label)
        {
            var card = Img(parent, "Stat-" + label, Theme.Card, _round16);
            VLayout(card.gameObject, 2, new RectOffset(0, 0, 14, 14), TextAnchor.MiddleCenter);
            Le(card.gameObject, flexW: 1);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.Border; outline.effectDistance = new Vector2(1.2f, 1.2f);
            var n = Txt(card.transform, "Count", "0", 26, Theme.Forest700, F.Heading, TextAlignmentOptions.Center);
            Txt(card.transform, "Label", label, 12, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);
            return n;
        }
    }
}
