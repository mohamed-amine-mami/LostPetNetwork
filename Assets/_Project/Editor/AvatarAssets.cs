using UnityEditor;
using UnityEngine;

namespace LostPetNetwork.EditorTools
{
    /// <summary>Creates the base URP Lit material the avatar shapes copy (kept in Resources so builds include its shader).</summary>
    public static class AvatarAssets
    {
        const string MaterialPath = "Assets/_Project/Resources/AvatarLit.mat";

        [MenuItem("LostPetNetwork/Setup Avatar Assets")]
        public static void Ensure()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader not found; avatar material was not created.");
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader) { name = "AvatarLit" }, MaterialPath);
            AssetDatabase.SaveAssets();
        }
    }
}
