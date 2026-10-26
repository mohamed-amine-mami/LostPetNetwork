using LostPetNetwork.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    public static partial class AppUIBuilder
    {
        static SosRowView _sosRowPrefab;

        static SosRowView BuildSosRowPrefab()
        {
            var root = new GameObject("SosRow", typeof(RectTransform));
            var rootImg = root.AddComponent<Image>();
            rootImg.color = Theme.Border;
            rootImg.sprite = _round16;
            rootImg.type = Image.Type.Sliced;
            VLayout(root, 0, new RectOffset(1, 1, 1, 1));
            var button = root.AddComponent<Button>();
            button.targetGraphic = rootImg;

            var face = Img(root.transform, "Face", Theme.Card, _round16);
            VLayout(face.gameObject, 8, new RectOffset(16, 16, 14, 14));
            var top = NewRect("Top", face.transform);
            HLayout(top.gameObject, 8, null, TextAnchor.MiddleLeft);
            var (pill, pillBg) = Pill(top, "Status", Theme.TerracottaSoft, new RectOffset(10, 10, 4, 4));
            var status = Txt(pill, "Label", "NEEDS HELP", 11, Theme.TerracottaInk, F.Bold, TextAlignmentOptions.Center);
            status.characterSpacing = 3;
            var flex = NewRect("Flex", top);
            Le(flex.gameObject, flexW: 1);
            var demo = DemoTag(top);
            var note = Txt(face.transform, "Note", "Injured dog near the market", 15, Theme.TextPrimary, F.Medium);
            note.overflowMode = TextOverflowModes.Ellipsis;
            note.maxVisibleLines = 2;
            var meta = Txt(face.transform, "Meta", "", 12, Theme.TextSecondary);

            var view = root.AddComponent<SosRowView>();
            Wire(view, "statusBackground", pillBg, "statusLabel", status, "noteLabel", note, "metaLabel", meta, "demoTag", demo);
            return SavePrefab<SosRowView>(root, "SosRow");
        }

        static GameObject BuildSosHub(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "Sos", 40);
            var head = NewRect("Header", content);
            HLayout(head.gameObject, 12, null, TextAnchor.MiddleLeft);
            var back = CircleIconButton(head, "BackButton", "back");
            var title = Txt(head, "Title", "Rescue", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);

            var card = Img(content, "SosCard", Theme.Terracotta, _round24);
            VLayout(card.gameObject, 10, new RectOffset(20, 20, 20, 20));
            Txt(card.transform, "Title", "Animal in danger?", 24, Theme.OnBrand, F.Heading);
            Txt(card.transform, "Text", "Send an SOS with your location. Neighbours and rescuers nearby see it right away.", 14, Theme.TerracottaSoft);
            var send = PrimaryButton(card.transform, "SendButton", "Send an SOS", Theme.Card, Theme.TerracottaInk);
            Txt(card.transform, "Tip", "Tip: shake your phone to open this quickly.", 12, Theme.TerracottaSoft);

            SectionTitle(content, "Open alerts");
            var count = Txt(content, "Count", "0 open alerts", 12, Theme.TextSecondary, F.Medium);
            var banner = Banner(content);
            var list = NewRect("List", content);
            VLayout(list.gameObject, 12);
            var loading = LoadingBlock(content);
            var empty = EmptyState(content, "No open alerts right now. Thank you for keeping watch.");

            var screen = root.AddComponent<SosHubScreen>();
            Wire(screen, "manager", manager, "backButton", back, "sendButton", send.Button, "countLabel", count,
                "listRoot", list, "rowPrefab", _sosRowPrefab, "loading", loading, "emptyState", empty.root, "banner", banner);
            return root;
        }

        static GameObject BuildSosCreate(Transform parent, ScreenManager manager)
        {
            var (root, content) = NewScreen(parent, "SosCreate", 40);
            var head = NewRect("Header", content);
            HLayout(head.gameObject, 12, null, TextAnchor.MiddleLeft);
            var back = CircleIconButton(head, "BackButton", "back");
            var title = Txt(head, "Title", "Send an SOS", 26, Theme.Forest900, F.Heading);
            Le(title.gameObject, flexW: 1);
            var banner = Banner(content);

            // ---- form
            var form = NewRect("Form", content);
            VLayout(form.gameObject, 16);
            Txt(form, "Intro", "For an injured, trapped or abandoned animal. Your location is shared with rescuers and neighbours nearby.", 14, Theme.TextSecondary);

            var loc = Img(form, "LocationCard", Theme.Card, _round16);
            HLayout(loc.gameObject, 12, new RectOffset(16, 12, 12, 12), TextAnchor.MiddleLeft, expandWidth: false);
            var locOutline = loc.gameObject.AddComponent<Outline>();
            locOutline.effectColor = Theme.Border; locOutline.effectDistance = new Vector2(1.2f, 1.2f);
            IconImg(loc.transform, "pin", Theme.Terracotta, 22);
            var locLabel = Txt(loc.transform, "Location", "Finding your location…", 14, Theme.TextPrimary, F.Medium);
            Le(locLabel.gameObject, flexW: 1);
            var refresh = LinkButton(loc.transform, "RefreshButton", "Refresh", TextAlignmentOptions.Right);

            var note = TextField(form, "Note", "What's happening? (optional)", "Injured, trapped, abandoned… what should rescuers know?", TMP_InputField.ContentType.Standard, multiline: true);

            var voice = Img(form, "VoiceCard", Theme.Card, _round16);
            VLayout(voice.gameObject, 8, new RectOffset(16, 16, 14, 14));
            var voiceOutline = voice.gameObject.AddComponent<Outline>();
            voiceOutline.effectColor = Theme.Border; voiceOutline.effectDistance = new Vector2(1.2f, 1.2f);
            Txt(voice.transform, "Title", "Voice note", 15, Theme.TextPrimary, F.Medium);
            var recordImg = Img(voice.transform, "RecordButton", Theme.Forest100, _round24);
            Le(recordImg.gameObject, prefH: 48);
            var record = recordImg.gameObject.AddComponent<Button>();
            record.targetGraphic = recordImg;
            var recordLabel = Txt(recordImg.transform, "Label", "Record a voice note", 15, Theme.Forest900, F.Bold, TextAlignmentOptions.Center);
            Stretch(recordLabel.rectTransform);
            var recordHint = Txt(voice.transform, "Hint", "Optional · up to 10 seconds", 12, Theme.TextSecondary);
            var voiceActions = NewRect("VoiceActions", voice.transform);
            HLayout(voiceActions.gameObject, 16, null, TextAnchor.MiddleLeft);
            var play = LinkButton(voiceActions, "PlayButton", "Play", TextAlignmentOptions.Left);
            var removeVoice = LinkButton(voiceActions, "RemoveVoiceButton", "Delete", TextAlignmentOptions.Left);
            play.gameObject.SetActive(false);
            removeVoice.gameObject.SetActive(false);

            var send = PrimaryButton(form, "SendButton", "Send SOS", Theme.Terracotta, Theme.OnBrand);

            // ---- sent
            var sent = NewRect("Sent", content);
            VLayout(sent.gameObject, 14);
            var sentCard = Img(sent, "SentCard", Theme.Forest50, _round24);
            VLayout(sentCard.gameObject, 8, new RectOffset(20, 20, 20, 20));
            var sentTitle = Txt(sentCard.transform, "Title", "Your alert is live", 26, Theme.Forest900, F.Heading);
            var sentText = Txt(sentCard.transform, "Text", "", 15, Theme.TextPrimary);
            var resolve = PrimaryButton(sent, "ResolveButton", "Mark as resolved", Theme.Forest700, Theme.OnBrand);
            var seeAlerts = LinkButton(sent, "SeeAlertsButton", "See all alerts", TextAlignmentOptions.Center);
            sent.gameObject.SetActive(false);

            var screen = root.AddComponent<SosCreateScreen>();
            Wire(screen, "manager", manager, "backButton", back, "formRoot", form.gameObject, "sentRoot", sent.gameObject,
                "locationLabel", locLabel, "refreshLocationButton", refresh, "noteField", note, "recordButton", record,
                "recordLabel", recordLabel, "recordHint", recordHint, "playButton", play, "removeVoiceButton", removeVoice,
                "sendButton", send, "sentTitle", sentTitle, "sentText", sentText, "resolveButton", resolve,
                "seeAlertsButton", seeAlerts, "banner", banner);
            return root;
        }

        static GameObject BuildSosDetails(Transform parent, ScreenManager manager)
        {
            var container = NewRect("SosDetails", parent);
            Stretch(container);
            var (scrollRoot, content) = NewScreen(container, "Scroll", 24, viewportBottomInset: 150);

            var head = NewRect("Header", content);
            HLayout(head.gameObject, 12, null, TextAnchor.MiddleLeft);
            var back = CircleIconButton(head, "BackButton", "back");
            var (pill, pillBg) = Pill(head, "Status", Theme.TerracottaSoft, new RectOffset(12, 12, 6, 6));
            var status = Txt(pill, "Label", "NEEDS HELP", 12, Theme.TerracottaInk, F.Bold, TextAlignmentOptions.Center);
            status.characterSpacing = 3;

            Txt(content, "Title", "Emergency alert", 28, Theme.Forest900, F.Heading);
            var note = Txt(content, "Note", "", 16, Theme.TextPrimary);

            var info = Img(content, "InfoCard", Theme.Card, _round16);
            VLayout(info.gameObject, 0, new RectOffset(0, 0, 4, 4));
            var infoOutline = info.gameObject.AddComponent<Outline>();
            infoOutline.effectColor = Theme.Border; infoOutline.effectDistance = new Vector2(1.2f, 1.2f);
            var reporter = InfoRow(info.transform, "user", "Sent by");
            var when = InfoRow(info.transform, "clock", "When");
            var distance = InfoRow(info.transform, "locate", "Distance");

            var maps = PillButton(content, "MapsButton", "Directions in Maps", "map");
            var playVoice = PillButton(content, "PlayVoiceButton", "Play voice note", "zap");
            var guide = PillButton(content, "GuideButton", "Guide me in AR", "locate");
            playVoice.gameObject.SetActive(false);
            guide.gameObject.SetActive(false);
            var banner = Banner(content);

            var bar = Img(container, "ActionBar", Theme.Card);
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0.5f, 0); bar.rectTransform.offsetMin = Vector2.zero; bar.rectTransform.offsetMax = new Vector2(0, 150);
            VLayout(bar.gameObject, 8, new RectOffset(24, 24, 14, 24));
            var action = PrimaryButton(bar.transform, "ActionButton", "I'm on my way", Theme.Terracotta, Theme.OnBrand);
            var hint = Txt(bar.transform, "Hint", "", 12, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);

            var screen = container.gameObject.AddComponent<SosDetailsScreen>();
            Wire(screen, "manager", manager, "backButton", back, "statusBackground", pillBg, "statusLabel", status,
                "noteLabel", note, "reporterValue", reporter, "whenValue", when, "distanceValue", distance,
                "mapsButton", maps, "playButton", playVoice, "guideButton", guide, "actionButton", action,
                "hint", hint, "banner", banner);
            return container.gameObject;
        }
    }
}
