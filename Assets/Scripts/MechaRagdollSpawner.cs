using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Spawns a random Kenney character model at runtime and turns it into a physics ragdoll
    /// via <see cref="RagdollBuilder"/>. This is the seed of the hidden-"mecha" mechanic: a
    /// character dropped into the pile that flops and lies among the other objects.
    /// Assign the character FBX models to <see cref="characterModels"/> in the Inspector.
    /// </summary>
    public class MechaRagdollSpawner : MonoBehaviour
    {
        [Header("Characters")]
        [Tooltip("The Kenney blocky-character models (character-a … character-r). One is picked at random per spawn.")]
        [SerializeField] private GameObject[] characterModels;

        [Header("Spawn")]
        [Tooltip("Where the ragdoll character drops from. A little height + tilt makes it topple into a natural pose.")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 3f, 0f);
        [SerializeField] private Vector3 spawnTiltEuler = new Vector3(20f, 180f, 15f);
        [Tooltip("Spawn one automatically when the scene starts.")]
        [SerializeField] private bool spawnOnStart = true;

        [Header("Ragdoll Tuning")]
        [SerializeField] private RagdollBuilder.Settings ragdollSettings = RagdollBuilder.Settings.Default;

        [Header("Camouflage (chameleon)")]
        [Tooltip("Repaint the mecha with the pile's appearance so it blends into the crowd.")]
        [SerializeField] private bool camouflage = true;
        [Tooltip("Copy the pile objects' ACTUAL materials (patterns/textures included) for a perfect blend. Off = flat-color camouflage only.")]
        [SerializeField] private bool matchTextures = true;
        [Tooltip("Color mode only: each body part a different pile color vs one color for the whole body.")]
        [SerializeField] private bool perPartColor = true;
        [Tooltip("Sample colors from the live pile so the mecha matches exactly what's on screen. Falls back to the palette below if the pile isn't ready yet.")]
        [SerializeField] private bool sampleColorsFromPile = true;
        [Tooltip("Fallback color palette (defaults to the pile's toy colors).")]
        [SerializeField] private Color[] camouflagePalette = ChameleonCamouflage.DefaultPalette;

        private void Start()
        {
            if (spawnOnStart) SpawnRandom();
        }

        /// <summary>Instantiates a random character model at the spawn point and builds its ragdoll.</summary>
        public GameObject SpawnRandom()
        {
            return SpawnRandomAt(spawnPosition, Quaternion.Euler(spawnTiltEuler));
        }

        /// <summary>Instantiates a random character model at an arbitrary pose and builds its ragdoll.</summary>
        public GameObject SpawnRandomAt(Vector3 position, Quaternion rotation)
        {
            if (characterModels == null || characterModels.Length == 0)
            {
                Debug.LogWarning("[MechaRagdollSpawner] No character models assigned.");
                return null;
            }

            GameObject model = characterModels[Random.Range(0, characterModels.Length)];
            if (model == null) return null;

            GameObject instance = Instantiate(model, position, rotation);
            RagdollBuilder.Build(instance, ragdollSettings);

            if (camouflage)
            {
                if (matchTextures)
                {
                    // Real texture matching: wear the pile's actual (patterned) materials.
                    ChameleonCamouflage.ApplyTexturedFromPile(instance);
                }
                else
                {
                    IReadOnlyList<Color> palette = camouflagePalette;
                    if (sampleColorsFromPile)
                    {
                        var sampled = ChameleonCamouflage.SamplePileColors();
                        if (sampled.Count > 0) palette = sampled;
                    }
                    ChameleonCamouflage.Apply(instance, palette, perPartColor);
                }
            }

            return instance;
        }
    }
}
