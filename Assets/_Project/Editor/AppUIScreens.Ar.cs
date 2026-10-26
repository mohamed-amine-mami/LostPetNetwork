using System.IO;
using LostPetNetwork.AR;
using LostPetNetwork.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LostPetNetwork.EditorTools
{
    public static partial class AppUIBuilder
    {
        const string ArSceneDir = "Assets/_Project/Scenes/AR";

        /// <summary>Builds the two AR scenes (placement and compass). They open on top of the main scene.</summary>
        static void BuildArScenes()
        {
            if (!AssetDatabase.IsValidFolder(ArSceneDir)) AssetDatabase.CreateFolder("Assets/_Project/Scenes", "AR");
            BuildArMarkerPrefab();
            var planePrefab = BuildArPlanePrefab();
            string place = BuildArPlaceScene(null);
            string compass = BuildArCompassScene();

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in new[] { place, compass })
                if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ shared rig

        static Camera CreateArRig(bool detectPlanes, GameObject planePrefab, out ARRaycastManager raycast, out ARPlaneManager planes, out ARCameraManager cameraManager)
        {
            new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));

            var origin = new GameObject("XR Origin", typeof(XROrigin));
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);
            var camGo = new GameObject("AR Camera", typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset.transform, false);

            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.SetRenderer(Preview3D.RendererIndex);
            data.renderPostProcessing = false;

            var driver = camGo.GetComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value, "<HandheldARInputDevice>/devicePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value, "<HandheldARInputDevice>/deviceRotation", expectedControlType: "Quaternion"));
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

            var xro = origin.GetComponent<XROrigin>();
            xro.Origin = origin;
            xro.CameraFloorOffsetObject = offset;
            xro.Camera = cam;
            xro.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.NotSpecified;

            cameraManager = camGo.GetComponent<ARCameraManager>();
            raycast = null;
            planes = null;
            if (detectPlanes)
            {
                raycast = origin.AddComponent<ARRaycastManager>();
                planes = origin.AddComponent<ARPlaneManager>();
                planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                planes.planePrefab = planePrefab;
                // Lets the real room hide parts of the pet on phones that can measure depth (ignored elsewhere).
                var occlusion = camGo.AddComponent<AROcclusionManager>();
                occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            }

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1f;
            sun.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(ArEventSystemGuard));
            return cam;
        }

        static Canvas CreateArCanvas(out RectTransform safe)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            safe = NewRect("SafeArea", canvasGo.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaPadding>();
            return canvas;
        }

        static (Button close, TMP_Text title) ArTopBar(RectTransform safe)
        {
            var bar = NewRect("TopBar", safe);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1); bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = Vector2.zero;
            HLayout(bar.gameObject, 12, new RectOffset(16, 16, 12, 12), TextAnchor.MiddleLeft);
            bar.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var close = CircleIconButton(bar, "CloseButton", "x");
            var (pill, _) = Pill(bar, "TitlePill", new Color(1f, 0.99f, 0.97f, 0.92f), new RectOffset(16, 16, 8, 8));
            var title = Txt(pill, "Title", "AR", 15, Theme.Forest900, F.Bold);
            return (close, title);
        }

        static (RectTransform root, TMP_Text text) ArPanel(RectTransform safe, string name, Vector2 anchor, float y, float width, float size, Color textColor)
        {
            var (pill, _) = Pill(safe, name, new Color(0.09f, 0.23f, 0.17f, 0.82f), new RectOffset(16, 16, 10, 10));
            Anchor(pill, anchor, new Vector2(0.5f, 0), new Vector2(0, y), Vector2.zero);
            var text = Txt(pill, "Text", "", size, textColor, F.Medium, TextAlignmentOptions.Center);
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            return (pill, text);
        }

        // ------------------------------------------------------------------ placement scene

        static GameObject BuildArPlanePrefab()
        {
            string matPath = ArSceneDir + "/ArPlane.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetColor("_BaseColor", new Color(0.12f, 0.3f, 0.23f, 0.28f));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            string prefabPath = ArSceneDir + "/ArPlane.prefab";
            var go = new GameObject("ArPlane", typeof(ARPlane), typeof(ARPlaneMeshVisualizer), typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static string BuildArPlaceScene(GameObject unused)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArSceneDir + "/ArPlane.prefab");
            var cam = CreateArRig(true, planePrefab, out var raycast, out var planes, out var cameraManager);
            var sun = Object.FindAnyObjectByType<Light>();
            CreateArCanvas(out var safe);
            var (close, title) = ArTopBar(safe);

            var (hintRoot, hint) = ArPanel(safe, "HintPanel", new Vector2(0.5f, 0), 190f, 300f, 15, Theme.OnBrand);
            var (infoRoot, info) = ArPanel(safe, "InfoPanel", new Vector2(0.5f, 0), 120f, 320f, 13, Theme.Forest100);
            var reset = PrimaryButton(safe, "ResetButton", "Place again", Theme.Card, Theme.Forest700);
            var resetRt = (RectTransform)reset.transform;
            Anchor(resetRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(240, 52));
            reset.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;

            var controller = new GameObject("ArPlacementController").AddComponent<ArPlacementController>();
            Wire(controller, "raycastManager", raycast, "planeManager", planes, "cameraManager", cameraManager,
                "arCamera", cam, "sun", sun, "titleLabel", title, "hintLabel", hint, "infoLabel", info,
                "closeButton", close, "resetButton", reset.Button);

            string path = ArSceneDir + "/ArPlace.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ------------------------------------------------------------------ compass scene

        static ArMarkerView BuildArMarkerPrefab()
        {
            var root = new GameObject("ArMarker", typeof(RectTransform));
            var rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(1f, 0.99f, 0.97f, 0.94f);
            rootImg.sprite = _round16;
            rootImg.type = Image.Type.Sliced;
            ((RectTransform)root.transform).sizeDelta = new Vector2(150, 72);
            var button = root.AddComponent<Button>();
            button.targetGraphic = rootImg;
            VLayout(root, 2, new RectOffset(10, 10, 8, 8), TextAnchor.MiddleCenter);

            var row = NewRect("Row", root.transform);
            HLayout(row.gameObject, 6, null, TextAnchor.MiddleCenter);
            var dot = Img(row, "Dot", Theme.Terracotta, _circle);
            Le(dot.gameObject, prefH: 14, prefW: 14);
            var nameLabel = Txt(row, "Name", "Name", 14, Theme.TextPrimary, F.Bold, TextAlignmentOptions.Center);
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            var distance = Txt(root.transform, "Distance", "", 12, Theme.TextSecondary, F.Medium, TextAlignmentOptions.Center);

            var view = root.AddComponent<ArMarkerView>();
            Wire(view, "dot", dot, "nameLabel", nameLabel, "distanceLabel", distance, "button", button);
            return SavePrefab<ArMarkerView>(root, "ArMarker");
        }

        static string BuildArCompassScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = CreateArRig(false, null, out _, out _, out _);
            // Loaded after the scene switch: a reference taken before it is unloaded with the old scene.
            var markerPrefab = AssetDatabase.LoadAssetAtPath<ArMarkerView>(PrefabDir + "/ArMarker.prefab");
            CreateArCanvas(out var safe);
            var (close, title) = ArTopBar(safe);

            var strip = NewRect("Markers", safe);
            strip.anchorMin = new Vector2(0, 0.5f); strip.anchorMax = new Vector2(1, 0.5f); strip.pivot = new Vector2(0.5f, 0.5f);
            strip.anchoredPosition = new Vector2(0, 40); strip.sizeDelta = new Vector2(0, 80);

            var arrow = NewRect("Arrow", safe);
            Anchor(arrow, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 210), new Vector2(110, 110));
            var arrowBg = Img(arrow, "Disc", new Color(0.09f, 0.23f, 0.17f, 0.82f), _circle);
            arrowBg.raycastTarget = false;
            Stretch(arrowBg.rectTransform);
            var icon = Img(arrow, "Icon", Theme.OnBrand);
            icon.sprite = Icon("locate");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
            icon.rectTransform.localEulerAngles = new Vector3(0, 0, 45f); // the icon points up-right; turn it to point up

            var (distRoot, distance) = ArPanel(safe, "DistancePanel", new Vector2(0.5f, 0), 110f, 300f, 18, Theme.OnBrand);
            var (statusRoot, status) = ArPanel(safe, "StatusPanel", new Vector2(0.5f, 0), 40f, 320f, 13, Theme.Forest100);

            var controller = new GameObject("ArCompassController").AddComponent<ArCompassController>();
            Wire(controller, "arCamera", cam, "markerRoot", strip, "markerPrefab", markerPrefab, "arrow", arrow,
                "titleLabel", title, "statusLabel", status, "distanceLabel", distance, "closeButton", close);

            string path = ArSceneDir + "/ArCompass.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
