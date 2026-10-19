using System.Collections.Generic;
using System.IO;
using LostPetNetwork.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    public enum F { Heading, Body, Medium, Bold }

    /// <summary>Primitive UI construction helpers shared by the scene/prefab builder.</summary>
    public static partial class AppUIBuilder
    {
        const string ArtDir = "Assets/_Project/Art";
        const string UiDir = ArtDir + "/UI";
        const string IconDir = ArtDir + "/Icons";
        const string PhotoDir = ArtDir + "/Photos";
        const string FontDir = ArtDir + "/Fonts";

        static Sprite _round16, _round24, _ring24, _circle;
        static TMP_FontAsset _fHeading, _fBody, _fMedium, _fBold;
        static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        // ------------------------------------------------------------------ primitives

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static Image Img(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
            return img;
        }

        static TMP_FontAsset FontOf(F kind)
        {
            switch (kind)
            {
                case F.Heading: return _fHeading;
                case F.Medium: return _fMedium;
                case F.Bold: return _fBold;
                default: return _fBody;
            }
        }

        static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color,
            F kind = F.Body, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = FontOf(kind);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        static VerticalLayoutGroup VLayout(GameObject go, float spacing = 0, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset();
            v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = expandWidth;
            v.childForceExpandHeight = false;
            return v;
        }

        static HorizontalLayoutGroup HLayout(GameObject go, float spacing = 0, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool expandWidth = false)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset();
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = expandWidth;
            h.childForceExpandHeight = false;
            return h;
        }

        static LayoutElement Le(GameObject go, float prefH = -1, float prefW = -1, float minH = -1, float flexW = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (prefH >= 0) { le.preferredHeight = prefH; le.minHeight = prefH; }
            if (minH >= 0) le.minHeight = minH;
            if (prefW >= 0) { le.preferredWidth = prefW; le.minWidth = prefW; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        static void Spacer(Transform parent, float height)
        {
            var s = NewRect("Spacer", parent);
            Le(s.gameObject, prefH: height);
        }

        static Sprite Icon(string name)
        {
            if (!IconCache.TryGetValue(name, out var sprite) || sprite == null)
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{name}.png");
                IconCache[name] = sprite;
            }
            if (sprite == null) Debug.LogError("Missing icon: " + name);
            return sprite;
        }

        static Sprite Photo(string key) => AssetDatabase.LoadAssetAtPath<Sprite>($"{PhotoDir}/{key}.jpg");

        static Image IconImg(Transform parent, string icon, Color color, float size)
        {
            var img = Img(parent, "Icon-" + icon, color);
            img.sprite = Icon(icon);
            img.preserveAspect = true;
            img.raycastTarget = false;
            Le(img.gameObject, prefH: size, prefW: size);
            return img;
        }

        static void Wire(Object target, params object[] pairs)
        {
            var so = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                var prop = so.FindProperty((string)pairs[i]);
                if (prop == null) { Debug.LogError($"Missing field {pairs[i]} on {target.GetType().Name}"); continue; }
                prop.objectReferenceValue = (Object)pairs[i + 1];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ screens & layout

        static (GameObject root, RectTransform content) NewScreen(Transform parent, string name, int bottomPadding = 40,
            RectTransform viewportInsetHost = null, float viewportBottomInset = 0)
        {
            var root = NewRect(name, parent);
            Stretch(root);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30;

            var viewport = NewRect("Viewport", root);
            Stretch(viewport, 0, 0, 0, viewportBottomInset);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            VLayout(content.gameObject, 16, new RectOffset(24, 24, 24, bottomPadding));
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return (root.gameObject, content);
        }

        // ------------------------------------------------------------------ widgets

        static GameObject Row(Transform parent, string name, float spacing = 12, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var r = NewRect(name, parent);
            HLayout(r.gameObject, spacing, null, align, expandWidth: false);
            return r.gameObject;
        }

        /// <summary>A circle button with an icon (Figma 44px icon button).</summary>
        static Button CircleIconButton(Transform parent, string name, string icon, float size = 44, bool dark = false)
        {
            var img = Img(parent, name, dark ? new Color(1f, 0.99f, 0.97f, 0.18f) : Theme.Card, _circle);
            Le(img.gameObject, prefH: size, prefW: size);
            if (!dark)
            {
                var outline = img.gameObject.AddComponent<Outline>();
                outline.effectColor = Theme.Border;
                outline.effectDistance = new Vector2(1f, 1f);
            }
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var ic = IconImg(img.transform, icon, dark ? Theme.OnBrand : Theme.TextPrimary, 22);
            Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            return b;
        }

        static TextFieldView TextField(Transform parent, string name, string label, string placeholder,
            TMP_InputField.ContentType type, bool multiline = false)
        {
            var root = NewRect(name, parent);
            VLayout(root.gameObject, 8);
            Txt(root, "Label", label, 13, Theme.TextPrimary, F.Bold);

            float height = multiline ? 104 : 52;
            var box = Img(root, "Box", Theme.Border, _round16);
            Le(box.gameObject, prefH: height);
            var inner = Img(box.transform, "Inner", Theme.Card, _round16);
            Stretch(inner.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);

            var area = NewRect("TextArea", box.transform);
            Stretch(area, 16, 16, multiline ? 12 : 0, multiline ? 12 : 0);
            area.gameObject.AddComponent<RectMask2D>();
            var anchor = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            var ph = Txt(area, "Placeholder", placeholder, 15, Theme.Placeholder);
            Stretch(ph.rectTransform);
            ph.alignment = anchor;
            var text = Txt(area, "Text", "", 15, Theme.TextPrimary);
            Stretch(text.rectTransform);
            text.alignment = anchor;
            if (!multiline)
            {
                ph.textWrappingMode = TextWrappingModes.NoWrap;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }

            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = inner;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.contentType = type;
            if (multiline) input.lineType = TMP_InputField.LineType.MultiLineNewline;
            if (type == TMP_InputField.ContentType.EmailAddress) input.keyboardType = TouchScreenKeyboardType.EmailAddress;
            if (type == TMP_InputField.ContentType.IntegerNumber) input.keyboardType = TouchScreenKeyboardType.NumberPad;
            input.customCaretColor = true;
            input.caretColor = Theme.Forest700;
            input.selectionColor = new Color(Theme.Forest700.r, Theme.Forest700.g, Theme.Forest700.b, 0.25f);
            input.characterLimit = multiline ? 500 : 128;

            var error = Txt(root, "Error", "", 13, Theme.Error);
            error.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<TextFieldView>();
            Wire(view, "input", input, "errorLabel", error, "border", box);
            return view;
        }

        static TMP_InputField SearchField(Transform parent, string name, string placeholder)
        {
            var box = Img(parent, name, Theme.Border, _round16);
            Le(box.gameObject, prefH: 52, flexW: 1);
            var inner = Img(box.transform, "Inner", Theme.Card, _round16);
            Stretch(inner.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);
            var ic = IconImg(box.transform, "search", Theme.TextSecondary, 20);
            ic.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(20, 20));

            var area = NewRect("TextArea", box.transform);
            Stretch(area, 46, 16, 0, 0);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Txt(area, "Placeholder", placeholder, 15, Theme.Placeholder);
            Stretch(ph.rectTransform);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            var text = Txt(area, "Text", "", 15, Theme.TextPrimary);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = inner;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = Theme.Forest700;
            input.characterLimit = 60;
            return input;
        }

        static MessageBanner Banner(Transform parent)
        {
            var bg = Img(parent, "Banner", Theme.TerracottaSoft, _round16);
            VLayout(bg.gameObject, 0, new RectOffset(14, 14, 12, 12));
            var label = Txt(bg.transform, "Message", "", 14, Theme.TerracottaInk);
            var banner = bg.gameObject.AddComponent<MessageBanner>();
            Wire(banner, "background", bg, "label", label);
            bg.gameObject.SetActive(false);
            return banner;
        }

        static LoadingButton PrimaryButton(Transform parent, string name, string text, Color back, Color fore)
        {
            var img = Img(parent, name, back, _round16);
            Le(img.gameObject, prefH: 52);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
            button.colors = colors;

            var label = Txt(img.transform, "Label", text, 16, fore, F.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            var lb = img.gameObject.AddComponent<LoadingButton>();
            Wire(lb, "label", label);
            return lb;
        }

        static Button LinkButton(Transform parent, string name, string text, TextAlignmentOptions align)
        {
            var t = Txt(parent, name, text, 14, Theme.Forest700, F.Bold, align);
            t.raycastTarget = true;
            Le(t.gameObject, minH: 44);
            var b = t.gameObject.AddComponent<Button>();
            b.targetGraphic = t;
            b.transition = Selectable.Transition.None;
            return b;
        }

        static Button FooterLink(Transform parent, string lead, string link)
        {
            var row = NewRect("Footer", parent);
            HLayout(row.gameObject, 6, null, TextAnchor.MiddleCenter);
            Le(row.gameObject, minH: 44);
            Txt(row, "Lead", lead, 15, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);
            var t = Txt(row, "Link", link, 15, Theme.Forest700, F.Bold, TextAlignmentOptions.Center);
            t.raycastTarget = true;
            var b = t.gameObject.AddComponent<Button>();
            b.targetGraphic = t;
            b.transition = Selectable.Transition.None;
            return b;
        }

        static ChipGroup ChipRow(Transform parent, string name, params string[] labels)
        {
            var row = NewRect(name, parent);
            HLayout(row.gameObject, 6, null, TextAnchor.MiddleLeft);
            var group = row.gameObject.AddComponent<ChipGroup>();

            var so = new SerializedObject(group);
            var list = so.FindProperty("chips");
            list.arraySize = labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                var chip = Img(row, "Chip-" + labels[i], Theme.Card, _round24);
                HLayout(chip.gameObject, 0, new RectOffset(13, 13, 0, 0), TextAnchor.MiddleCenter);
                Le(chip.gameObject, prefH: 40);
                var outline = chip.gameObject.AddComponent<Outline>();
                outline.effectColor = Theme.Border;
                outline.effectDistance = new Vector2(1.2f, 1.2f);
                var button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = chip;
                button.transition = Selectable.Transition.None;
                var label = Txt(chip.transform, "Label", labels[i], 13, Theme.TextPrimary, F.Bold, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;

                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = button;
                el.FindPropertyRelative("background").objectReferenceValue = chip;
                el.FindPropertyRelative("label").objectReferenceValue = label;
                el.FindPropertyRelative("outline").objectReferenceValue = outline;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return group;
        }

        /// <summary>
        /// A content-sized pill. The background Image lives on a child so its sprite size does not leak into the
        /// layout (an Image on the layout root would force a 56px minimum size).
        /// </summary>
        static (RectTransform root, Image background) Pill(Transform parent, string name, Color color, RectOffset padding)
        {
            var root = NewRect(name, parent);
            HLayout(root.gameObject, 0, padding, TextAnchor.MiddleCenter);
            var fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var bg = Img(root, "Background", color, _round24);
            bg.raycastTarget = false;
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Stretch(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
            return (root, bg);
        }

        static StatusBadge Badge(Transform parent)
        {
            var (root, bg) = Pill(parent, "Badge", Theme.TerracottaSoft, new RectOffset(10, 10, 4, 4));
            var label = Txt(root, "Label", "LOST", 12, Theme.TerracottaInk, F.Bold, TextAlignmentOptions.Center);
            label.characterSpacing = 4;
            var badge = root.gameObject.AddComponent<StatusBadge>();
            Wire(badge, "background", bg, "label", label);
            return badge;
        }

        static GameObject LeftAligned(Transform parent, string name = "Left")
        {
            var r = NewRect(name, parent);
            HLayout(r.gameObject, 0, null, TextAnchor.MiddleLeft, expandWidth: false);
            return r.gameObject;
        }

        static GameObject LoadingBlock(Transform parent)
        {
            var t = Txt(parent, "Loading", "Loading…", 14, Theme.TextSecondary, F.Medium, TextAlignmentOptions.Center);
            Le(t.gameObject, prefH: 40);
            t.gameObject.SetActive(false);
            return t.gameObject;
        }

        static (GameObject root, TMP_Text label, Button button) EmptyState(Transform parent, string text, string buttonText = null)
        {
            var card = Img(parent, "EmptyState", Theme.Card, _round24);
            VLayout(card.gameObject, 12, new RectOffset(20, 20, 24, 24), TextAnchor.MiddleCenter);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.Border;
            outline.effectDistance = new Vector2(1f, 1f);
            var pawBg = Img(card.transform, "Paw", Theme.Forest50, _circle);
            Le(pawBg.gameObject, prefH: 56, prefW: 56);
            var paw = IconImg(pawBg.transform, "paw", Theme.Forest700, 28);
            Anchor(paw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
            paw.GetComponent<LayoutElement>().ignoreLayout = true;
            var label = Txt(card.transform, "Text", text, 15, Theme.TextSecondary, F.Body, TextAlignmentOptions.Center);
            Button button = null;
            if (buttonText != null)
            {
                var lb = PrimaryButton(card.transform, "Action", buttonText, Theme.Forest700, Theme.OnBrand);
                button = lb.Button;
            }
            card.gameObject.SetActive(false);
            return (card.gameObject, label, button);
        }

        static GameObject DemoTag(Transform parent, string text = "DEMO DATA")
        {
            var (root, _) = Pill(parent, "DemoTag", new Color(0.09f, 0.23f, 0.17f, 0.78f), new RectOffset(8, 8, 3, 3));
            var t = Txt(root, "Label", text, 10, Theme.OnBrand, F.Bold, TextAlignmentOptions.Center);
            t.characterSpacing = 3;
            return root.gameObject;
        }

        static void CoverSlot(RectTransform container, out CoverImage cover)
        {
            var rt = NewRect("Cover", container);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = false;
            Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1.33f;
            cover = rt.gameObject.AddComponent<CoverImage>();
            img.enabled = false;
        }

        static Image PlaceholderFill(Transform parent, Color color)
        {
            var img = Img(parent, "PlaceholderFill", color);
            Stretch(img.rectTransform);
            img.raycastTarget = false;
            var paw = IconImg(img.transform, "paw", new Color(Theme.Forest700.r, Theme.Forest700.g, Theme.Forest700.b, 0.35f), 40);
            paw.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(paw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            return img;
        }
    }
}
