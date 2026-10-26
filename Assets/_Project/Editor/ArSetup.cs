using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.ARKit;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace LostPetNetwork.EditorTools
{
    /// <summary>
    /// One-time project setup for AR: XR loaders for Android (ARCore), iOS (ARKit) and the Editor (XR Simulation),
    /// the AR camera background in the 3D renderer, and the usage texts iOS needs. Safe to run repeatedly.
    /// </summary>
    public static class ArSetup
    {
        const string RendererPath = "Assets/Settings/Renderer3D.asset";

        [MenuItem("LostPetNetwork/Setup AR")]
        public static void Run()
        {
            SetupLoaders();
            SetupRendererFeature();
            SetupPlatformSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("AR setup done.");
        }

        static void SetupLoaders()
        {
            // The per-target settings container has an internal factory; the XR settings window uses the same one.
            var type = typeof(XRGeneralSettingsPerBuildTarget);
            var factory = type.GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.NonPublic);
            var perTarget = (XRGeneralSettingsPerBuildTarget)factory.Invoke(null, null);

            Configure(perTarget, BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader");
            Configure(perTarget, BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader");
            Configure(perTarget, BuildTargetGroup.Standalone, "UnityEngine.XR.Simulation.SimulationLoader");
            EditorUtility.SetDirty(perTarget);
        }

        static void Configure(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group, string loaderType)
        {
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

            var settings = perTarget.SettingsForBuildTarget(group);
            // The AR session is started only inside the AR scenes, so the normal app never uses the AR camera.
            settings.InitManagerOnStart = false;
            var manager = perTarget.ManagerSettingsForBuildTarget(group);
            if (!XRPackageMetadataStore.IsLoaderAssigned(loaderType, group))
            {
                if (!XRPackageMetadataStore.AssignLoader(manager, loaderType, group))
                    Debug.LogWarning($"Could not assign {loaderType} for {group}.");
            }
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(manager);
        }

        static void SetupRendererFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) { Debug.LogWarning("Run 'Setup 3D Renderer' first."); return; }
            foreach (var feature in data.rendererFeatures)
                if (feature is ARBackgroundRendererFeature) return;

            var added = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            added.name = "ARBackgroundRendererFeature";
            AssetDatabase.AddObjectToAsset(added, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(added, out _, out long localId);

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = added;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        static void SetupPlatformSettings()
        {
            // AR is optional: phones without ARCore / ARKit still install and run the rest of the app.
            ARCoreSettings.GetOrCreateSettings().requirement = ARCoreSettings.Requirement.Optional;
            ARKitSettings.GetOrCreateSettings().requirement = ARKitSettings.Requirement.Optional;
            PlayerSettings.iOS.locationUsageDescription = "LostPetNetwork uses your location to show pets and alerts near you.";
            PlayerSettings.iOS.microphoneUsageDescription = "LostPetNetwork records a short voice note when you send an SOS.";
            if (string.IsNullOrEmpty(PlayerSettings.iOS.cameraUsageDescription))
                PlayerSettings.iOS.cameraUsageDescription = "LostPetNetwork uses the camera to photograph pets and for AR.";
        }
    }
}
