using LostPetNetwork.Services.Avatar;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Builds the low-poly 3D avatar for a config out of cached shapes. The figure stands on y = 0, faces +Z
    /// and is about 2.2 units tall, so every avatar frames the same way.
    /// </summary>
    public static class AvatarFactory
    {
        /// <summary>Framing for the whole body (centre and radius in avatar space).</summary>
        public static readonly Vector3 BodyCenter = new Vector3(0f, 1.05f, 0f);
        public const float BodyRadius = 1.3f;

        /// <summary>Framing for a head-and-shoulders portrait.</summary>
        public static readonly Vector3 PortraitCenter = new Vector3(0f, 1.38f, 0f);
        public const float PortraitRadius = 0.85f;

        const float HeadY = 1.52f;

        public static GameObject Build(AvatarConfig input)
        {
            var cfg = (input ?? AvatarConfig.Default()).Clone().Clamp();
            var root = new GameObject("Avatar");

            Color skin = AvatarPalette.Colour(AvatarPalette.Skin, cfg.skin);
            Color skinShade = Color.Lerp(skin, new Color(0.45f, 0.2f, 0.12f), 0.22f);
            Color blush = Color.Lerp(skin, new Color(0.85f, 0.3f, 0.3f), 0.3f);
            Color hair = AvatarPalette.Colour(AvatarPalette.Hair, cfg.hairColor);
            Color eyeColor = AvatarPalette.Colour(AvatarPalette.Eyes, cfg.eyes);
            Color outfit = AvatarPalette.Colour(AvatarPalette.Outfit, cfg.outfitColor);
            Color trim = AvatarPalette.Trim(cfg.outfitColor);

            BuildBody(root.transform, cfg, skin, outfit);
            BuildHead(root.transform, skin, skinShade, blush, hair, eyeColor);
            BuildHair(root.transform, cfg, hair);
            BuildOutfitDetails(root.transform, cfg, outfit);
            BuildAccessory(root.transform, cfg, trim);
            return root;
        }

        // ------------------------------------------------------------------ body

        static void BuildBody(Transform root, AvatarConfig cfg, Color skin, Color outfit)
        {
            bool dress = cfg.outfitStyle == 2;
            Color legColor = dress ? skin : AvatarPalette.Pants;

            Part(root, "Torso", MeshFactory.Capsule(0.37f, 0.92f), outfit, new Vector3(0f, 0.72f, 0f), Vector3.zero, Vector3.one);

            foreach (float side in new[] { -1f, 1f })
            {
                Part(root, "Leg", MeshFactory.Capsule(0.13f, 0.62f), legColor, new Vector3(0.17f * side, 0.31f, 0f), Vector3.zero, Vector3.one);
                Part(root, "Shoe", MeshFactory.Sphere(), AvatarPalette.Shoes, new Vector3(0.17f * side, 0.07f, 0.07f), Vector3.zero, new Vector3(0.16f, 0.09f, 0.24f));

                // Arms hang from the shoulder pivot, angled slightly outwards.
                var shoulder = new GameObject("Shoulder").transform;
                shoulder.SetParent(root, false);
                shoulder.localPosition = new Vector3(0.43f * side, 1.06f, 0f);
                shoulder.localRotation = Quaternion.Euler(0f, 0f, 13f * side);
                Part(shoulder, "Arm", MeshFactory.Capsule(0.095f, 0.62f), skin, new Vector3(0f, -0.28f, 0f), Vector3.zero, Vector3.one);
                Part(shoulder, "Hand", MeshFactory.Sphere(), skin, new Vector3(0f, -0.6f, 0f), Vector3.zero, Vector3.one * 0.105f);
                float sleeve = cfg.outfitStyle == 1 ? 0.5f : 0.3f;
                Part(shoulder, "Sleeve", MeshFactory.Capsule(0.118f, sleeve), outfit, new Vector3(0f, -0.04f - sleeve * 0.5f + 0.1f, 0f), Vector3.zero, Vector3.one);
            }
        }

        static void BuildHead(Transform root, Color skin, Color skinShade, Color blush, Color hair, Color eyeColor)
        {
            var head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, HeadY, 0f);

            Part(head, "Skull", MeshFactory.Sphere(), skin, Vector3.zero, Vector3.zero, new Vector3(0.52f, 0.5f, 0.495f));

            foreach (float side in new[] { -1f, 1f })
            {
                Part(head, "Ear", MeshFactory.Sphere(), skin, new Vector3(0.5f * side, -0.02f, 0f), Vector3.zero, new Vector3(0.06f, 0.1f, 0.09f));
                Part(head, "Cheek", MeshFactory.Sphere(), blush, new Vector3(0.3f * side, -0.1f, 0.38f), Vector3.zero, new Vector3(0.07f, 0.045f, 0.03f));

                // Eye: coloured iris, dark pupil, small highlight.
                Part(head, "Iris", MeshFactory.Sphere(), eyeColor, new Vector3(0.17f * side, 0f, 0.452f), Vector3.zero, new Vector3(0.085f, 0.1f, 0.035f), 0.5f);
                Part(head, "Pupil", MeshFactory.Sphere(), AvatarPalette.Dark, new Vector3(0.17f * side, 0f, 0.468f), Vector3.zero, new Vector3(0.05f, 0.062f, 0.02f), 0.5f);
                Part(head, "Glint", MeshFactory.Sphere(), Color.white, new Vector3(0.17f * side + 0.022f, 0.032f, 0.478f), Vector3.zero, Vector3.one * 0.016f, 0.9f);

                // Brow
                Part(head, "Brow", MeshFactory.Capsule(0.02f, 0.14f), Color.Lerp(hair, Color.black, 0.25f),
                    new Vector3(0.17f * side, 0.15f, 0.445f), new Vector3(0f, 0f, 90f + 8f * side), Vector3.one);
            }

            Part(head, "Nose", MeshFactory.Sphere(), skinShade, new Vector3(0f, -0.06f, 0.49f), Vector3.zero, new Vector3(0.05f, 0.04f, 0.035f));
            // Smile: lower arc of a thin ring standing in the XY plane.
            Part(head, "Mouth", MeshFactory.Torus(0.1f, 0.014f, 25f, 130f), new Color(0.42f, 0.18f, 0.16f),
                new Vector3(0f, -0.04f, 0.466f), new Vector3(90f, 0f, 0f), Vector3.one);
        }

        // ------------------------------------------------------------------ hair

        static void BuildHair(Transform root, AvatarConfig cfg, Color hair)
        {
            if (cfg.hairStyle == 0) return;
            var head = new GameObject("Hair").transform;
            head.SetParent(root, false);
            head.localPosition = new Vector3(0f, HeadY, 0f);

            // Cap over the top of the head (kept above the brows so the face stays open).
            Part(head, "HairCap", MeshFactory.Cap(cfg.hairStyle == 4 ? 80f : 74f), hair, new Vector3(0f, 0.01f, -0.015f), Vector3.zero, new Vector3(0.545f, 0.53f, 0.53f), 0.35f);

            switch (cfg.hairStyle)
            {
                case 2: // long
                    Part(head, "Back", MeshFactory.Sphere(), hair, new Vector3(0f, -0.42f, -0.3f), Vector3.zero, new Vector3(0.5f, 0.85f, 0.2f), 0.35f);
                    foreach (float side in new[] { -1f, 1f })
                        Part(head, "Lock", MeshFactory.Sphere(), hair, new Vector3(0.47f * side, -0.18f, -0.06f), Vector3.zero, new Vector3(0.08f, 0.34f, 0.2f), 0.35f);
                    break;
                case 3: // bun
                    Part(head, "Bun", MeshFactory.Sphere(), hair, new Vector3(0f, 0.55f, -0.1f), Vector3.zero, Vector3.one * 0.2f, 0.35f);
                    break;
                case 4: // curly
                    const int curls = 16;
                    for (int i = 0; i < curls; i++)
                    {
                        float azimuth = i * 137.5f * Mathf.Deg2Rad;
                        float polar = Mathf.Lerp(18f, 78f, (i % 6) / 5f) * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                        if (dir.z > 0.55f && dir.y < 0.7f) dir.z = -dir.z * 0.6f; // keep the forehead clear
                        Part(head, "Curl", MeshFactory.Sphere(), hair, dir.normalized * 0.53f + new Vector3(0f, 0.01f, 0f), Vector3.zero, Vector3.one * 0.16f, 0.35f);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ outfit

        static void BuildOutfitDetails(Transform root, AvatarConfig cfg, Color outfit)
        {
            switch (cfg.outfitStyle)
            {
                case 1: // hoodie: hood bulge behind the neck and a pocket stripe
                    Part(root, "Hood", MeshFactory.Sphere(), outfit, new Vector3(0f, 1.14f, -0.27f), Vector3.zero, new Vector3(0.34f, 0.2f, 0.2f));
                    Part(root, "Pocket", MeshFactory.Sphere(), Color.Lerp(outfit, Color.black, 0.12f), new Vector3(0f, 0.55f, 0.33f), Vector3.zero, new Vector3(0.24f, 0.1f, 0.05f));
                    break;
                case 2: // dress: bell-shaped skirt
                    Part(root, "Skirt", MeshFactory.Sphere(), outfit, new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.56f, 0.4f, 0.5f));
                    break;
            }
        }

        // ------------------------------------------------------------------ accessories

        static void BuildAccessory(Transform root, AvatarConfig cfg, Color trim)
        {
            switch (cfg.accessory)
            {
                case 1: // glasses
                {
                    var head = HeadAnchor(root);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Part(head, "Lens", MeshFactory.Torus(0.095f, 0.014f), AvatarPalette.Dark, new Vector3(0.17f * side, 0f, 0.485f), new Vector3(90f, 0f, 0f), Vector3.one);
                        Part(head, "Arm", MeshFactory.Capsule(0.01f, 0.4f), AvatarPalette.Dark, new Vector3(0.38f * side, 0f, 0.3f), new Vector3(90f, 0f, 0f), Vector3.one);
                    }
                    Part(head, "Bridge", MeshFactory.Capsule(0.012f, 0.1f), AvatarPalette.Dark, new Vector3(0f, 0.01f, 0.488f), new Vector3(0f, 0f, 90f), Vector3.one);
                    break;
                }
                case 2: // cap
                {
                    var head = HeadAnchor(root);
                    Part(head, "Crown", MeshFactory.Cap(72f), trim, new Vector3(0f, 0.01f, -0.01f), Vector3.zero, new Vector3(0.58f, 0.56f, 0.56f), 0.3f);
                    Part(head, "Brim", MeshFactory.Sphere(), trim, new Vector3(0f, 0.2f, 0.55f), new Vector3(12f, 0f, 0f), new Vector3(0.3f, 0.03f, 0.25f), 0.3f);
                    break;
                }
                case 3: // scarf
                {
                    Part(root, "ScarfRing", MeshFactory.Torus(0.33f, 0.095f), trim, new Vector3(0f, 1.1f, 0.02f), Vector3.zero, Vector3.one, 0.2f);
                    Part(root, "ScarfTail", MeshFactory.Sphere(), trim, new Vector3(0.17f, 0.88f, 0.36f), new Vector3(0f, 0f, 8f), new Vector3(0.09f, 0.26f, 0.05f), 0.2f);
                    break;
                }
                case 4: // headphones
                {
                    var head = HeadAnchor(root);
                    Part(head, "Band", MeshFactory.Torus(0.56f, 0.035f, 190f, 160f), AvatarPalette.Dark, new Vector3(0f, -0.02f, 0f), new Vector3(90f, 0f, 0f), Vector3.one);
                    foreach (float side in new[] { -1f, 1f })
                        Part(head, "Cup", MeshFactory.Sphere(), trim, new Vector3(0.55f * side, -0.02f, 0f), Vector3.zero, new Vector3(0.1f, 0.15f, 0.15f), 0.3f);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        static Transform HeadAnchor(Transform root)
        {
            var anchor = new GameObject("Accessory").transform;
            anchor.SetParent(root, false);
            anchor.localPosition = new Vector3(0f, HeadY, 0f);
            return anchor;
        }

        static void Part(Transform parent, string name, Mesh mesh, Color color, Vector3 position, Vector3 euler, Vector3 scale, float smoothness = 0.25f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AvatarMaterials.For(color, smoothness);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
