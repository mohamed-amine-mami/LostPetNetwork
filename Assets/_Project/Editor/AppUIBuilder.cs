using System.Collections.Generic;
using System.IO;
using LostPetNetwork.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace LostPetNetwork.EditorTools
{
    /// <summary>
    /// Rebuilds fonts, prefabs and the Main scene (all ten screens) from code so the UI is reproducible and
    /// matches the approved Figma design. Menu: LostPetNetwork > Build App UI.
    /// </summary>
    public static partial class AppUIBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string PrefabDir = "Assets/_Project/Prefabs";
        const string ResourcesDir = "Assets/_Project/Resources";

        static PetCardView _cardPrefab;
        static ReportRowView _rowPrefab;
        static MapPinView _pinPrefab;

        [MenuItem("LostPetNetwork/Build App UI")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            ConfigureImporters();
            _round16 = EnsureRoundedSprite("round16", 16);
            _round24 = EnsureRoundedSprite("round24", 24);
            _circle = EnsureCircleSprite("circle");
            _ring24 = EnsureRingSprite("ring24", 24, 3);
            EnsureFonts();
            EnsurePhotoLibrary();
            BuildPrefabs();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("App UI built: " + ScenePath);
        }

        // ------------------------------------------------------------------ assets

        static void EnsureFolders()
        {
            foreach (var path in new[] { ArtDir, UiDir, IconDir, PhotoDir, FontDir, PrefabDir, ResourcesDir, "Assets/_Project/Scenes" })
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconDir, PhotoDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                bool isIcon = path.StartsWith(IconDir);
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = isIcon;
                importer.maxTextureSize = isIcon ? 256 : 1024;
                importer.filterMode = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 100;
                importer.SaveAndReimport();
            }
        }

        static void EnsureFonts()
        {
            _fHeading = EnsureFontAsset("Fraunces-SemiBold");
            _fBody = EnsureFontAsset("DMSans-Regular");
            _fMedium = EnsureFontAsset("DMSans-Medium");
            _fBold = EnsureFontAsset("DMSans-Bold");
        }

        static TMP_FontAsset EnsureFontAsset(string name)
        {
            string assetPath = $"{FontDir}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{name}.ttf");
            if (font == null) throw new FileNotFoundException("Font not imported: " + name);
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name + " SDF";
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static void EnsurePhotoLibrary()
        {
            string path = $"{ResourcesDir}/PhotoLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<PhotoLibrary>(path);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<PhotoLibrary>();
                AssetDatabase.CreateAsset(lib, path);
            }
            var so = new SerializedObject(lib);
            var list = so.FindProperty("entries");
            string[] keys = { "biscuit", "luna", "max", "auth" };
            list.arraySize = keys.Length;
            for (int i = 0; i < keys.Length; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("key").stringValue = keys[i];
                el.FindPropertyRelative("sprite").objectReferenceValue = Photo(keys[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lib);
        }

        static Sprite EnsureRoundedSprite(string name, int radius)
        {
            string path = $"{UiDir}/{name}.png";
            if (!File.Exists(path))
            {
                int size = radius * 2 + 8;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float half = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));
                }
                tex.SetPixels(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            ConfigureSprite(path, radius);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite EnsureRingSprite(string name, int radius, int thickness)
        {
            string path = $"{UiDir}/{name}.png";
            if (!File.Exists(path))
            {
                int size = radius * 2 + 8;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float half = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01(radius - d + 0.5f);
                    float inner = Mathf.Clamp01(radius - thickness - d + 0.5f);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(outer - inner));
                }
                tex.SetPixels(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            ConfigureSprite(path, radius);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite EnsureCircleSprite(string name)
        {
            string path = $"{UiDir}/{name}.png";
            if (!File.Exists(path))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float c = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(c - d + 0.5f));
                }
                tex.SetPixels(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            ConfigureSprite(path, 0);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void ConfigureSprite(string path, int border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var want = new Vector4(border, border, border, border);
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == want) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = want;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildPrefabs()
        {
            _cardPrefab = BuildPetCardPrefab();
            _rowPrefab = BuildReportRowPrefab();
            _pinPrefab = BuildMapPinPrefab();
        }

        static T SavePrefab<T>(GameObject root, string name) where T : Component
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<T>();
        }

        static PetCardView BuildPetCardPrefab()
        {
            var root = new GameObject("PetCard", typeof(RectTransform));
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

            var photoBox = NewRect("Photo", face.transform);
            Le(photoBox.gameObject, prefH: 190);
            photoBox.gameObject.AddComponent<RectMask2D>();
            PlaceholderFill(photoBox, Theme.Forest100);
            CoverSlot(photoBox, out var cover);

            var badge = Badge(photoBox);
            Anchor(badge.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), Vector2.zero);

            var demo = DemoTag(photoBox);
            Anchor(demo.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -14), Vector2.zero);

            var body = NewRect("Body", face.transform);
            VLayout(body.gameObject, 6, new RectOffset(16, 16, 14, 16));
            var nameLabel = Txt(body, "Name", "Biscuit", 20, Theme.TextPrimary, F.Heading);
            var details = Txt(body, "Details", "Golden Retriever", 15, Theme.TextSecondary);
            details.overflowMode = TextOverflowModes.Ellipsis;
            details.maxVisibleLines = 2;
            var locRow = NewRect("LocationRow", body);
            HLayout(locRow.gameObject, 6, new RectOffset(0, 0, 4, 0), TextAnchor.MiddleLeft);
            IconImg(locRow, "pin", Theme.Forest700, 16);
            var loc = Txt(locRow, "Location", "Parc du Belvédère · 2h ago", 13, Theme.Forest700, F.Bold);

            var view = root.AddComponent<PetCardView>();
            Wire(view, "photo", cover, "badge", badge, "nameLabel", nameLabel, "detailsLabel", details,
                "locationLabel", loc, "demoTag", demo);
            return SavePrefab<PetCardView>(root, "PetCard");
        }

        static ReportRowView BuildReportRowPrefab()
        {
            var root = new GameObject("ReportRow", typeof(RectTransform));
            var rootImg = root.AddComponent<Image>();
            rootImg.color = Theme.Border;
            rootImg.sprite = _round16;
            rootImg.type = Image.Type.Sliced;
            VLayout(root, 0, new RectOffset(1, 1, 1, 1));
            var button = root.AddComponent<Button>();
            button.targetGraphic = rootImg;

            var face = Img(root.transform, "Face", Theme.Card, _round16);
            HLayout(face.gameObject, 14, new RectOffset(12, 12, 12, 12), TextAnchor.MiddleLeft);

            var thumbMask = Img(face.transform, "Thumb", Theme.Forest100, _round16);
            thumbMask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Le(thumbMask.gameObject, prefH: 76, prefW: 76);
            var thumbPaw = IconImg(thumbMask.transform, "paw", new Color(Theme.Forest700.r, Theme.Forest700.g, Theme.Forest700.b, 0.35f), 28);
            thumbPaw.GetComponent<LayoutElement>().ignoreLayout = true;
            Anchor(thumbPaw.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
            CoverSlot(thumbMask.rectTransform, out var cover);

            var info = NewRect("Info", face.transform);
            VLayout(info.gameObject, 4);
            Le(info.gameObject, flexW: 1);
            var left = LeftAligned(info);
            var badge = Badge(left.transform);
            var nameLabel = Txt(info, "Name", "Biscuit", 17, Theme.TextPrimary, F.Heading);
            var meta = Txt(info, "Meta", "Posted 2h ago", 12, Theme.TextSecondary);

            var view = root.AddComponent<ReportRowView>();
            Wire(view, "thumb", cover, "badge", badge, "nameLabel", nameLabel, "metaLabel", meta);
            return SavePrefab<ReportRowView>(root, "ReportRow");
        }

        static MapPinView BuildMapPinPrefab()
        {
            var root = new GameObject("MapPin", typeof(RectTransform));
            var ring = root.AddComponent<Image>();
            ring.sprite = _circle;
            ring.color = Theme.Terracotta;
            ((RectTransform)root.transform).sizeDelta = new Vector2(42, 42);
            var button = root.AddComponent<Button>();
            button.targetGraphic = ring;
            button.transition = Selectable.Transition.None;

            var photo = Img(root.transform, "Photo", Theme.Forest100, _circle);
            photo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Stretch(photo.rectTransform, 3, 3, 3, 3);
            CoverSlot(photo.rectTransform, out var cover);

            var view = root.AddComponent<MapPinView>();
            Wire(view, "ring", ring, "photo", cover);
            return SavePrefab<MapPinView>(root, "MapPin");
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Cream;
            cam.orthographic = true;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var bg = Img(canvasGo.transform, "Background", Theme.Cream);
            Stretch(bg.rectTransform);

            var safe = NewRect("SafeArea", canvasGo.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaPadding>();

            var manager = new GameObject("ScreenManager", typeof(ScreenManager)).GetComponent<ScreenManager>();
            manager.transform.SetParent(canvasGo.transform, false);

            var entries = new List<(ScreenId, GameObject)>
            {
                (ScreenId.Splash, BuildSplash(safe)),
                (ScreenId.SignIn, BuildSignIn(safe, manager)),
                (ScreenId.SignUp, BuildSignUp(safe, manager)),
                (ScreenId.ResetPassword, BuildResetPassword(safe, manager)),
                (ScreenId.Home, BuildHome(safe, manager)),
                (ScreenId.Explore, BuildExplore(safe, manager)),
                (ScreenId.Map, BuildMap(safe, manager)),
                (ScreenId.Report, BuildReport(safe, manager)),
                (ScreenId.Camera, BuildCamera(safe, manager)),
                (ScreenId.Details, BuildDetails(safe, manager)),
                (ScreenId.MyReports, BuildMyReports(safe, manager)),
                (ScreenId.Profile, BuildProfile(safe, manager)),
            };
            var tabBar = BuildTabBar(safe, manager);

            var so = new SerializedObject(manager);
            var list = so.FindProperty("screens");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").enumValueIndex = (int)entries[i].Item1;
                el.FindPropertyRelative("root").objectReferenceValue = entries[i].Item2;
                entries[i].Item2.SetActive(false);
            }
            so.FindProperty("tabBar").objectReferenceValue = tabBar;
            so.ApplyModifiedPropertiesWithoutUndo();
            tabBar.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) buildScenes.Add(s);
            EditorBuildSettings.scenes = buildScenes.ToArray();
        }

        static TabBarView BuildTabBar(Transform parent, ScreenManager manager)
        {
            var bar = Img(parent, "TabBar", Theme.Card);
            Anchor(bar.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 84));
            bar.rectTransform.anchorMin = new Vector2(0, 0);
            bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.offsetMin = new Vector2(0, 0);
            bar.rectTransform.offsetMax = new Vector2(0, 84);

            var line = Img(bar.transform, "TopLine", Theme.Border);
            line.rectTransform.anchorMin = new Vector2(0, 1);
            line.rectTransform.anchorMax = new Vector2(1, 1);
            line.rectTransform.pivot = new Vector2(0.5f, 1);
            line.rectTransform.sizeDelta = new Vector2(0, 1);
            line.raycastTarget = false;

            var row = NewRect("Tabs", bar.transform);
            Stretch(row, 8, 8, 8, 8);
            HLayout(row.gameObject, 0, null, TextAnchor.MiddleCenter, expandWidth: true);

            var view = bar.gameObject.AddComponent<TabBarView>();
            var tabs = new (ScreenId id, string label, string icon)[]
            {
                (ScreenId.Home, "Home", "home"), (ScreenId.Explore, "Explore", "search"),
                (ScreenId.MyReports, "My reports", "reports"), (ScreenId.Profile, "Profile", "user")
            };
            var so = new SerializedObject(view);
            var list = so.FindProperty("tabs");
            list.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = Img(row, "Tab-" + tabs[i].label, new Color(0, 0, 0, 0));
                VLayout(tab.gameObject, 4, null, TextAnchor.MiddleCenter);
                Le(tab.gameObject, flexW: 1);
                var button = tab.gameObject.AddComponent<Button>();
                button.targetGraphic = tab;
                button.transition = Selectable.Transition.None;
                var icon = IconImg(tab.transform, tabs[i].icon, Theme.Placeholder, 24);
                var iconRow = icon.GetComponent<LayoutElement>();
                var label = Txt(tab.transform, "Label", tabs[i].label, 12, Theme.Placeholder, F.Medium, TextAlignmentOptions.Center);

                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("id").enumValueIndex = (int)tabs[i].id;
                el.FindPropertyRelative("button").objectReferenceValue = button;
                el.FindPropertyRelative("icon").objectReferenceValue = icon;
                el.FindPropertyRelative("label").objectReferenceValue = label;
            }
            so.FindProperty("manager").objectReferenceValue = manager;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }
    }
}
