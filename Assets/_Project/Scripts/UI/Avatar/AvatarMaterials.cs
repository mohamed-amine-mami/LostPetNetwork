using System.Collections.Generic;
using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// One cached URP Lit material per colour. The base material lives in Resources so its shader is always
    /// included in builds (Shader.Find alone is not reliable on devices).
    /// </summary>
    public static class AvatarMaterials
    {
        static readonly Dictionary<uint, Material> Cache = new Dictionary<uint, Material>();
        static Shader _shader;

        public static Material For(Color color, float smoothness = 0.25f)
        {
            var c32 = (Color32)color;
            uint key = ((uint)c32.r << 24) | ((uint)c32.g << 16) | ((uint)c32.b << 8) | (uint)Mathf.RoundToInt(smoothness * 255f);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var material = new Material(Shader())
            {
                name = "Avatar " + ColorUtility.ToHtmlStringRGB(color),
                hideFlags = HideFlags.DontSave
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            Cache[key] = material;
            return material;
        }

        static Shader Shader()
        {
            if (_shader != null) return _shader;
            var baseMaterial = Resources.Load<Material>("AvatarLit");
            _shader = baseMaterial != null ? baseMaterial.shader : UnityEngine.Shader.Find("Universal Render Pipeline/Lit");
            return _shader;
        }
    }
}
