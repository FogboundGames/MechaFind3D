using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// The "chameleon" mechanic: repaints a character's body parts with the pile's color palette
    /// (same URP Lit flat-color material the pile cubes/spheres use), so the hidden mecha blends
    /// into the crowd. The only remaining "tell" is its humanoid shape among the loose shapes.
    /// </summary>
    public static class ChameleonCamouflage
    {
        /// The pile palette — mirrors PhysicsObjectSpawner's named toy colors so a camouflaged
        /// mecha part is materially indistinguishable from a real pile object of that color.
        public static readonly Color[] DefaultPalette =
        {
            new Color(0.95f, 0.20f, 0.20f), // Kırmızı
            new Color(0.20f, 0.55f, 0.95f), // Mavi
            new Color(0.20f, 0.85f, 0.35f), // Yeşil
            new Color(0.98f, 0.85f, 0.15f), // Sarı
            new Color(0.65f, 0.25f, 0.90f), // Mor
            new Color(0.98f, 0.50f, 0.15f), // Turuncu
            new Color(0.15f, 0.85f, 0.85f), // Turkuaz
            new Color(0.95f, 0.40f, 0.70f), // Pembe
        };

        /// <summary>
        /// Repaints every renderer under <paramref name="character"/> using colors from
        /// <paramref name="palette"/>. If <paramref name="perPartColor"/> is true each body part
        /// gets its own random palette color (best camouflage in a multicolor pile); otherwise the
        /// whole body takes a single color.
        /// </summary>
        public static void Apply(GameObject character, IReadOnlyList<Color> palette = null,
                                 bool perPartColor = true, float smoothness = 0.65f)
        {
            if (character == null) return;
            if (palette == null || palette.Count == 0) palette = DefaultPalette;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Color bodyColor = palette[Random.Range(0, palette.Count)];

            foreach (Renderer r in character.GetComponentsInChildren<Renderer>())
            {
                Color c = perPartColor ? palette[Random.Range(0, palette.Count)] : bodyColor;

                Material mat = new Material(shader) { name = "CamouflageMaterial" };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

                r.material = mat;
            }
        }

        /// <summary>
        /// Real texture/material matching: gives each of the mecha's body parts the ACTUAL material
        /// a random live pile object is currently wearing (patterns and all). Falls back to the
        /// shared <see cref="AppearanceLibrary"/> pool if the pile isn't spawned yet, and finally to
        /// flat-color <see cref="Apply"/> if there are no materials at all.
        /// </summary>
        public static void ApplyTexturedFromPile(GameObject character)
        {
            if (character == null) return;

            var mats = new List<Material>();
            foreach (FindTargetObject f in Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None))
            {
                if (f.isDocked) continue;
                Renderer r = f.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null) mats.Add(r.sharedMaterial);
            }

            if (mats.Count == 0) mats.AddRange(AppearanceLibrary.All);
            if (mats.Count == 0) { Apply(character); return; }

            foreach (Renderer r in character.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mats[Random.Range(0, mats.Count)];
        }

        /// <summary>
        /// Collects the colors actually present in the live pile, so the mecha matches exactly what
        /// is currently on screen. Returns an empty list if the pile hasn't spawned yet.
        /// </summary>
        public static List<Color> SamplePileColors()
        {
            var colors = new List<Color>();
            foreach (FindTargetObject f in Object.FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None))
            {
                if (!f.isDocked) colors.Add(f.objectColor);
            }
            return colors;
        }
    }
}
