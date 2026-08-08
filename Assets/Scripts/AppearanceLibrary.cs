using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Central, adjustable "texture" system for the game's objects. Procedurally builds a pool of
    /// URP Lit materials, each = one palette color × one pattern (solid / stripes / checker / dots).
    /// Both the pile (PhysicsObjectSpawner) and the chameleon mecha pull from this same pool, so a
    /// camouflaged mecha part is materially identical to a real pile object. More patterns × colors
    /// here == more visual variety == more distinct levels (the design note's insight).
    /// </summary>
    public static class AppearanceLibrary
    {
        public enum Pattern { Solid, Stripes, Checker, Dots }

        private struct Entry
        {
            public Color color;
            public Material material;
        }

        private const int TexSize = 64;
        private const float Smoothness = 0.65f;

        private static List<Entry> _entries;

        private static List<Entry> Entries => _entries ?? (_entries = Build());

        /// <summary>All generated materials (color × pattern combinations).</summary>
        public static IReadOnlyList<Material> All
        {
            get
            {
                var list = new List<Material>(Entries.Count);
                foreach (var e in Entries) list.Add(e.material);
                return list;
            }
        }

        /// <summary>A random material of any color/pattern.</summary>
        public static Material Random() => Entries[UnityEngine.Random.Range(0, Entries.Count)].material;

        /// <summary>A random pattern material whose base color matches <paramref name="color"/> (keeps color identity for gameplay, adds texture variety).</summary>
        public static Material RandomForColor(Color color)
        {
            var matches = new List<Material>();
            foreach (var e in Entries)
                if (ColorsClose(e.color, color)) matches.Add(e.material);

            if (matches.Count > 0) return matches[UnityEngine.Random.Range(0, matches.Count)];
            return Random();
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        private static List<Entry> Build()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var entries = new List<Entry>();

            foreach (Color baseColor in ChameleonCamouflage.DefaultPalette)
            {
                foreach (Pattern pattern in (Pattern[])System.Enum.GetValues(typeof(Pattern)))
                {
                    Texture2D tex = MakePatternTexture(pattern, baseColor);
                    Material mat = new Material(shader) { name = $"Appearance_{pattern}" };

                    // With a texture, keep _BaseColor white so the texture's own colors show through.
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Smoothness);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Smoothness);

                    entries.Add(new Entry { color = baseColor, material = mat });
                }
            }
            return entries;
        }

        private static Texture2D MakePatternTexture(Pattern pattern, Color color)
        {
            // Pattern is drawn in the base color over a darker shade of the SAME hue, so the object
            // still reads as "that color" (match-3 identity) while gaining a visible texture.
            Color fg = color;
            Color bg = color * 0.5f; bg.a = 1f;

            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            int cell = TexSize / 8;   // 8 stripes/cells across
            var pixels = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    bool on;
                    switch (pattern)
                    {
                        case Pattern.Stripes:
                            on = (x / cell) % 2 == 0;
                            break;
                        case Pattern.Checker:
                            on = ((x / cell) + (y / cell)) % 2 == 0;
                            break;
                        case Pattern.Dots:
                            int cx = (x % cell) - cell / 2;
                            int cy = (y % cell) - cell / 2;
                            on = (cx * cx + cy * cy) <= (cell / 3) * (cell / 3);
                            break;
                        default: // Solid
                            on = true;
                            break;
                    }
                    pixels[y * TexSize + x] = on ? fg : bg;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
