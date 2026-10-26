using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LostPetNetwork.EditorTools
{
    /// <summary>Adds a standard (3D) URP renderer next to the 2D one. Safe to run repeatedly.</summary>
    public static class Renderer3DSetup
    {
        const string RendererPath = "Assets/Settings/Renderer3D.asset";
        const string PipelinePath = "Assets/Settings/UniversalRP.asset";

        [MenuItem("LostPetNetwork/Setup 3D Renderer")]
        public static void Run()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer)
                {
                    Debug.Log("3D renderer already registered at index " + i);
                    return;
                }

            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log("3D renderer registered at index " + index);
        }
    }
}
