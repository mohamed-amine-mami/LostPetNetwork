using System.Collections.Generic;
using UnityEngine;

namespace LostPetNetwork.UI
{
    /// <summary>
    /// Builds the smooth low-poly shapes the avatar is made of (spheres, capsules, caps, tori) by revolving a
    /// 2D profile around the Y axis. Meshes are cached and shared by every avatar.
    /// </summary>
    public static class MeshFactory
    {
        const int Segments = 24;
        const int Rings = 14;

        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Unit sphere (radius 1).</summary>
        public static Mesh Sphere() => Cached("sphere", () => Lathe(Arc(0f, 180f, Vector2.zero, 1f, Rings), Segments));

        /// <summary>Part of a unit sphere from the top pole down to polarMaxDegrees (a hair or cap crown).</summary>
        public static Mesh Cap(float polarMaxDegrees) =>
            Cached("cap" + Mathf.RoundToInt(polarMaxDegrees), () =>
                Lathe(Arc(0f, polarMaxDegrees, Vector2.zero, 1f, Mathf.Max(4, Mathf.RoundToInt(Rings * polarMaxDegrees / 180f))), Segments));

        /// <summary>Capsule along Y with the given radius and total height.</summary>
        public static Mesh Capsule(float radius, float totalHeight)
        {
            string key = $"capsule{Mathf.RoundToInt(radius * 1000)}x{Mathf.RoundToInt(totalHeight * 1000)}";
            return Cached(key, () =>
            {
                float half = Mathf.Max(0f, totalHeight * 0.5f - radius);
                var profile = new List<Vector2>();
                profile.AddRange(Arc(0f, 90f, new Vector2(0f, half), radius, 7));
                profile.AddRange(Arc(90f, 180f, new Vector2(0f, -half), radius, 7));
                return Lathe(profile, Segments);
            });
        }

        /// <summary>Cone along Y with a softly rounded tip at +height/2 and the base at -height/2 (ears).</summary>
        public static Mesh Cone(float radius, float height)
        {
            string key = $"cone{Mathf.RoundToInt(radius * 1000)}x{Mathf.RoundToInt(height * 1000)}";
            return Cached(key, () =>
            {
                float h = height * 0.5f;
                var profile = new List<Vector2>
                {
                    new Vector2(0f, h), new Vector2(radius * 0.12f, h - height * 0.06f),
                    new Vector2(radius * 0.55f, h - height * 0.5f), new Vector2(radius, -h + height * 0.04f),
                    new Vector2(radius * 0.7f, -h), new Vector2(0f, -h)
                };
                return Lathe(profile, 16);
            });
        }

        /// <summary>A ring (or part of one) lying in the XZ plane, centred on the origin.</summary>
        public static Mesh Torus(float major, float minor, float startDegrees = 0f, float sweepDegrees = 360f)
        {
            string key = $"torus{Mathf.RoundToInt(major * 1000)}x{Mathf.RoundToInt(minor * 1000)}a{Mathf.RoundToInt(startDegrees)}s{Mathf.RoundToInt(sweepDegrees)}";
            return Cached(key, () =>
            {
                var profile = new List<Vector2>();
                const int tube = 10;
                // Clockwise so the outward normal points away from the tube centre.
                for (int i = 0; i <= tube; i++)
                {
                    float a = (90f - 360f * i / tube) * Mathf.Deg2Rad;
                    profile.Add(new Vector2(major + minor * Mathf.Cos(a), minor * Mathf.Sin(a)));
                }
                int segments = Mathf.Max(6, Mathf.RoundToInt(Segments * 1.5f * sweepDegrees / 360f));
                return Lathe(profile, segments, startDegrees, sweepDegrees);
            });
        }

        /// <summary>Profile points (radius, y) of an arc on a circle, from polarStart to polarEnd degrees measured from the top.</summary>
        public static List<Vector2> Arc(float polarStart, float polarEnd, Vector2 center, float radius, int steps)
        {
            var points = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(polarStart, polarEnd, (float)i / steps) * Mathf.Deg2Rad;
                points.Add(new Vector2(center.x + radius * Mathf.Sin(a), center.y + radius * Mathf.Cos(a)));
            }
            return points;
        }

        /// <summary>
        /// Revolves a profile (x = distance from the Y axis, y = height; listed top to bottom) around Y.
        /// Normals come from the profile so shading is smooth; winding is corrected to face outwards.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments, float startDegrees = 0f, float sweepDegrees = 360f)
        {
            int rows = profile.Count;
            int cols = segments + 1;
            var vertices = new Vector3[rows * cols];
            var normals = new Vector3[rows * cols];
            var uvs = new Vector2[rows * cols];

            for (int i = 0; i < rows; i++)
            {
                Vector2 prev = profile[Mathf.Max(0, i - 1)];
                Vector2 next = profile[Mathf.Min(rows - 1, i + 1)];
                Vector2 tangent = next - prev;
                var n2 = new Vector2(-tangent.y, tangent.x);
                n2 = n2.sqrMagnitude > 1e-10f ? n2.normalized : Vector2.up;

                for (int j = 0; j < cols; j++)
                {
                    float a = (startDegrees + sweepDegrees * j / segments) * Mathf.Deg2Rad;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    int idx = i * cols + j;
                    vertices[idx] = new Vector3(profile[i].x * c, profile[i].y, profile[i].x * s);
                    normals[idx] = new Vector3(n2.x * c, n2.y, n2.x * s);
                    uvs[idx] = new Vector2((float)j / segments, 1f - (float)i / Mathf.Max(1, rows - 1));
                }
            }

            var triangles = new List<int>();
            for (int i = 0; i < rows - 1; i++)
            for (int j = 0; j < segments; j++)
            {
                int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                AddTriangle(triangles, vertices, normals, a, c, b);
                AddTriangle(triangles, vertices, normals, b, c, d);
            }

            var mesh = new Mesh { name = "Lathe", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddTriangle(List<int> triangles, Vector3[] v, Vector3[] n, int a, int b, int c)
        {
            Vector3 geometric = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (geometric.sqrMagnitude < 1e-12f) return; // collapsed at a pole
            if (Vector3.Dot(geometric, n[a] + n[b] + n[c]) < 0f)
            {
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
            }
            else
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
        }

        static Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (Cache.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            mesh = build();
            mesh.name = key;
            Cache[key] = mesh;
            return mesh;
        }
    }
}
