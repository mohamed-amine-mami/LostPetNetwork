using LostPetNetwork.Services.Pets;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Builds the low-poly 3D dog or cat for a config. The pet stands on y = 0, faces +Z and is built at
    /// life-size proportions (a medium dog is about 1 unit tall), scaled by the chosen size.
    /// </summary>
    public static class PetFactory
    {
        /// <summary>Framing for a medium pet (centre and radius in pet space, before size scaling).</summary>
        public static readonly Vector3 FrameCenter = new Vector3(0f, 0.55f, 0.08f);
        public const float FrameRadius = 1.1f;

        /// <summary>
        /// Multiply the built pet by this to get approximate real-world size in metres (the preview model is
        /// drawn larger than life so it fills the screen).
        /// </summary>
        public static float LifeSizeScale(PetConfig config) => config != null && config.IsCat ? 0.4f : 0.55f;

        /// <summary>Three-quarter view that shows the face and the body together.</summary>
        public const float DefaultYaw = 35f;

        public static GameObject Build(PetConfig input)
        {
            var cfg = (input ?? PetConfig.Default()).Clone().Clamp();
            var root = new GameObject(cfg.IsCat ? "Cat" : "Dog");
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            body.localScale = Vector3.one * PetPalette.SizeScale[cfg.size];

            Color coat = PetPalette.Coat[cfg.coat];
            Color second = PetPalette.Secondary(coat);
            Color light = Color.Lerp(coat, Color.white, 0.55f);
            bool cat = cfg.IsCat;
            bool pointed = cat || cfg.ears == 1;

            float bodyLen = cat ? 0.5f : 0.62f;
            float bodyH = cat ? 0.27f : 0.34f;
            float bodyW = cat ? 0.27f : 0.34f;
            float headR = cat ? 0.25f : 0.3f;
            float legH = cat ? 0.36f : 0.5f;
            float bodyY = legH + bodyH * 0.55f;
            float headY = bodyY + (cat ? 0.2f : 0.26f);
            float headZ = bodyLen + headR * 0.45f;

            // torso
            Part(body, "Torso", MeshFactory.Sphere(), coat, new Vector3(0f, bodyY, 0f), Vector3.zero, new Vector3(bodyW, bodyH, bodyLen));
            if (cfg.pattern == 2) Part(body, "Chest", MeshFactory.Sphere(), Color.white, new Vector3(0f, bodyY - 0.05f, bodyLen * 0.8f), Vector3.zero, new Vector3(bodyW * 0.6f, bodyH * 0.6f, 0.2f));

            // legs and paws
            foreach (float sz in new[] { -1f, 1f })
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = (bodyW * 0.6f) * sx;
                float z = bodyLen * 0.62f * sz;
                float r = cat ? 0.07f : 0.09f;
                Part(body, "Leg", MeshFactory.Capsule(r, legH + 0.08f), coat, new Vector3(x, (legH + 0.08f) * 0.5f, z), Vector3.zero, Vector3.one);
                Color paw = cfg.pattern == 2 ? Color.white : coat;
                Part(body, "Paw", MeshFactory.Sphere(), paw, new Vector3(x, r * 0.8f, z + 0.03f), Vector3.zero, new Vector3(r * 1.15f, r * 0.9f, r * 1.4f));
            }

            // head
            var head = new GameObject("Head").transform;
            head.SetParent(body, false);
            head.localPosition = new Vector3(0f, headY, headZ);
            Part(head, "Skull", MeshFactory.Sphere(), coat, Vector3.zero, Vector3.zero, new Vector3(headR, headR * 0.95f, headR));
            float snoutLen = cat ? 0.08f : 0.17f;
            Part(head, "Snout", MeshFactory.Sphere(), cat ? coat : light, new Vector3(0f, -headR * 0.28f, headR * 0.78f), Vector3.zero, new Vector3(cat ? 0.12f : 0.14f, cat ? 0.09f : 0.1f, snoutLen + 0.05f));
            Part(head, "Nose", MeshFactory.Sphere(), cat ? new Color(0.9f, 0.55f, 0.55f) : new Color(0.1f, 0.09f, 0.09f), new Vector3(0f, -headR * 0.15f, headR * 0.78f + snoutLen), Vector3.zero, Vector3.one * (cat ? 0.03f : 0.05f));

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 eye = new Vector3(headR * 0.42f * side, headR * 0.18f, headR * 0.86f);
                Part(head, "Eye", MeshFactory.Sphere(), cat ? new Color(0.35f, 0.7f, 0.4f) : new Color(0.15f, 0.1f, 0.08f), eye, Vector3.zero, new Vector3(0.05f, 0.058f, 0.03f), 0.6f);
                Part(head, "Pupil", MeshFactory.Sphere(), new Color(0.04f, 0.04f, 0.04f), eye + new Vector3(0f, 0f, 0.012f), Vector3.zero, new Vector3(cat ? 0.017f : 0.03f, 0.04f, 0.02f), 0.6f);
                Part(head, "Glint", MeshFactory.Sphere(), Color.white, eye + new Vector3(0.012f * side * -1f, 0.02f, 0.025f), Vector3.zero, Vector3.one * 0.011f, 0.9f);

                if (pointed)
                    Part(head, "Ear", MeshFactory.Cone(headR * 0.36f, headR * 0.85f), coat,
                        new Vector3(headR * 0.62f * side, headR * 0.72f, -headR * 0.05f), new Vector3(0f, 0f, -18f * side), Vector3.one);
                else
                    Part(head, "Ear", MeshFactory.Sphere(), cfg.pattern == 1 ? second : coat,
                        new Vector3(headR * 0.92f * side, headR * 0.15f, -headR * 0.05f), new Vector3(0f, 0f, 14f * side), new Vector3(headR * 0.28f, headR * 0.7f, headR * 0.42f));
            }

            if (cat)
            {
                foreach (float side in new[] { -1f, 1f })
                for (int w = 0; w < 3; w++)
                    Part(head, "Whisker", MeshFactory.Capsule(0.004f, 0.22f), Color.white,
                        new Vector3(0.14f * side, -headR * 0.22f - w * 0.018f, headR * 0.85f), new Vector3(0f, 0f, 90f + (w - 1) * 10f), Vector3.one, 0.5f);
            }

            if (cfg.pattern == 1)
            {
                Part(body, "Patch", MeshFactory.Sphere(), second, new Vector3(0.06f, bodyY + bodyH * 0.55f, -0.05f), new Vector3(0f, 12f, 0f), new Vector3(bodyW * 0.7f, 0.05f, bodyLen * 0.5f));
                Part(head, "EyePatch", MeshFactory.Sphere(), second, new Vector3(headR * 0.42f, headR * 0.18f, headR * 0.8f), Vector3.zero, new Vector3(0.09f, 0.1f, 0.04f));
            }

            // tail
            if (cat)
            {
                Part(body, "TailBase", MeshFactory.Capsule(0.04f, 0.5f), coat, new Vector3(0f, bodyY + 0.12f, -bodyLen - 0.02f), new Vector3(-35f, 0f, 0f), Vector3.one);
                Part(body, "TailTip", MeshFactory.Capsule(0.04f, 0.3f), cfg.pattern == 1 ? second : coat, new Vector3(0f, bodyY + 0.45f, -bodyLen - 0.27f), new Vector3(-10f, 0f, 0f), Vector3.one);
            }
            else
            {
                Part(body, "Tail", MeshFactory.Capsule(0.055f, 0.5f), cfg.pattern == 1 ? second : coat, new Vector3(0f, bodyY + 0.2f, -bodyLen - 0.1f), new Vector3(-55f, 0f, 0f), Vector3.one);
            }

            // collar and tag
            if (cfg.collar > 0)
            {
                Color collar = PetPalette.Collar[cfg.collar];
                float neckZ = bodyLen + headR * 0.05f;
                float neckY = (bodyY + headY) * 0.5f + 0.02f;
                float ring = cat ? 0.2f : 0.25f;
                Part(body, "Collar", MeshFactory.Torus(ring, 0.032f), collar, new Vector3(0f, neckY, neckZ), new Vector3(90f, 0f, 0f), Vector3.one, 0.4f);
                Part(body, "Tag", MeshFactory.Sphere(), new Color(0.95f, 0.8f, 0.3f), new Vector3(0f, neckY - ring, neckZ + 0.02f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.015f), 0.7f);
            }

            return root;
        }

        static void Part(Transform parent, string name, Mesh mesh, Color color, Vector3 position, Vector3 euler, Vector3 scale, float smoothness = 0.2f)
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
