using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Spawns a random character model at runtime, turns it into a physics ragdoll via <see cref="RagdollBuilder"/>,
    /// and disguises/embeds it into a host pile item (e.g. Cake 🍰 or Apple 🍎) so it looks like a hybrid mecha object!
    /// </summary>
    public class MechaRagdollSpawner : MonoBehaviour
    {
        public enum CamouflageMode
        {
            HostObjectEmbed,
            SingleGoalDisguise,
            TextureFromPile,
            FlatColorPalette
        }

        [Header("Custom Mecha Prefab (Assets/Prefabs)")]
        [Tooltip("Primary custom mecha prefab (e.g. meccha chameleon.glb). If set, this exact mecha is used.")]
        [SerializeField] private GameObject customMechaPrefab;

        [Header("Character Pool")]
        [Tooltip("List of character models to pick from. If empty, auto-finds meccha chameleon or Kenney models in project.")]
        [SerializeField] private GameObject[] characterModels;

        [Header("Host Object Embedding (e.g. Cake 🍰)")]
        [Tooltip("Embed the mecha into a host item (e.g. Cake) so it emerges from the item as a hybrid disguise.")]
        [SerializeField] private bool embedInHostObject = true;

        [Tooltip("Keyword for preferred host item (e.g. 'cake', 'apple', 'burger'). Prioritizes cakes in the pile.")]
        [SerializeField] private string preferredHostKeyword = "cake";

        [Header("Spawn Settings")]
        [Tooltip("Where the ragdoll character drops from if not embedded in a host item.")]
        [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 3f, 0f);
        [SerializeField] private Vector3 spawnTiltEuler = new Vector3(20f, 180f, 15f);
        [Tooltip("Spawn one automatically when the level starts.")]
        [SerializeField] private bool spawnOnStart = true;

        [Header("Ragdoll Tuning")]
        [SerializeField] private RagdollBuilder.Settings ragdollSettings = RagdollBuilder.Settings.Default;

        [Header("Object Appearance & Camouflage")]
        [Tooltip("Enable chameleon camouflage so the mecha matches the pile appearance.")]
        [SerializeField] private bool camouflage = true;

        [Tooltip("How the mecha disguised appearance is chosen.")]
        [SerializeField] private CamouflageMode camouflageMode = CamouflageMode.HostObjectEmbed;

        [Tooltip("Attach a mini 3D mascot costume version of the target object onto the mecha's head!")]
        [SerializeField] private bool attachHeadMascot = true;

        [Tooltip("Fallback color palette if flat color mode is selected.")]
        [SerializeField] private Color[] camouflagePalette = ChameleonCamouflage.DefaultPalette;

        private GameObject currentSpawnedMecha;

        private void Start()
        {
            AutoFindCharacterModelsIfEmpty();
            if (spawnOnStart)
            {
                // Slight delay so PhysicsObjectSpawner finishes spawning the pile objects first
                Invoke(nameof(SpawnRandom), 0.08f);
            }
        }

        public void AutoFindCharacterModelsIfEmpty()
        {
#if UNITY_EDITOR
            if (customMechaPrefab == null)
            {
                string[] mechaGuids = AssetDatabase.FindAssets("meccha t:Model", new[] { "Assets/Prefabs" });
                if (mechaGuids != null && mechaGuids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(mechaGuids[0]);
                    customMechaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Debug.Log($"🤖 Custom Mecha Model Loaded: {path}");
                }
            }

            if (characterModels == null || characterModels.Length == 0)
            {
                List<GameObject> list = new List<GameObject>();
                if (customMechaPrefab != null) list.Add(customMechaPrefab);

                string[] guids = AssetDatabase.FindAssets("character- t:Model", new[] { "Assets/kenney_blocky-characters_20" });
                if (guids != null && guids.Length > 0)
                {
                    foreach (string guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (model != null && !list.Contains(model)) list.Add(model);
                    }
                }
                characterModels = list.ToArray();
            }
#endif
        }

        /// <summary>Instantiates a random character model at the spawn point and builds its ragdoll.</summary>
        public GameObject SpawnRandom()
        {
            return SpawnRandomAt(spawnPosition, Quaternion.Euler(spawnTiltEuler));
        }

        /// <summary>Instantiates a random character model at an arbitrary pose and builds its ragdoll.</summary>
        public GameObject SpawnRandomAt(Vector3 position, Quaternion rotation)
        {
            AutoFindCharacterModelsIfEmpty();

            GameObject modelToSpawn = null;
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null && LevelManager.Instance.ActiveLevelData.customMechaPrefab != null)
            {
                modelToSpawn = LevelManager.Instance.ActiveLevelData.customMechaPrefab;
            }
            if (modelToSpawn == null)
            {
                modelToSpawn = customMechaPrefab;
            }
            if (modelToSpawn == null && characterModels != null && characterModels.Length > 0)
            {
                modelToSpawn = characterModels[Random.Range(0, characterModels.Length)];
            }

            if (modelToSpawn == null)
            {
                Debug.LogWarning("[MechaRagdollSpawner] No mecha model assigned or found in project.");
                return null;
            }

            if (currentSpawnedMecha != null)
            {
                Destroy(currentSpawnedMecha);
            }

            currentSpawnedMecha = Instantiate(modelToSpawn, position, rotation);
            currentSpawnedMecha.name = $"MechaRagdoll_{modelToSpawn.name}";

            // Enforce compact mini size on raw mecha model by default (never a 2-meter giant!)
            currentSpawnedMecha.transform.localScale = Vector3.one * 0.20f;

            // Skinned/rigged mechas (e.g. meccha chameleon) must NOT be ragdoll-built: turning their
            // bones into falling physics bodies scatters the skeleton in world space, so after embedding
            // the visible mesh floats away from the host. Only ragdoll simple (non-skinned) characters.
            // (The embed step poses + strips physics itself, so ragdoll isn't needed for embedded mechas.)
            if (currentSpawnedMecha.GetComponentInChildren<SkinnedMeshRenderer>() == null)
            {
                RagdollBuilder.Build(currentSpawnedMecha, ragdollSettings);
            }

            if (camouflage)
            {
                ApplyCamouflageToMecha(currentSpawnedMecha);
            }

            // Fixed glass look: applied last so every mecha reads as a light, see-through white silhouette
            // no matter which disguise mode ran above (or whether embedding into a host object even happened).
            // It must use the LEVEL's opacity: the embed path above already applied that value, and this
            // call used to overwrite it with the hard-coded default, so a level asking for a fainter, better
            // hidden mecha never actually got one.
            float glassOpacity = 0.22f;
            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                glassOpacity = LevelManager.Instance.ActiveLevelData.mechaOpacity;
            }
            ChameleonCamouflage.ApplyGlassMaterial(currentSpawnedMecha, glassOpacity);

            return currentSpawnedMecha;
        }

        public void ApplyCamouflageToMecha(GameObject mecha)
        {
            if (mecha == null) return;

            if (embedInHostObject || camouflageMode == CamouflageMode.HostObjectEmbed)
            {
                float targetScaleRatio = 0.85f;
                float targetOpacity = 0.22f;
                float absWorldSize = 0f;
                float wrapAmount = 0f;
                string keyword = preferredHostKeyword;
                Vector3 posOffset = Vector3.zero;
                Vector3 rotOffset = Vector3.zero;

                if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
                {
                    targetScaleRatio = LevelManager.Instance.ActiveLevelData.mechaScaleRatio;
                    targetOpacity = LevelManager.Instance.ActiveLevelData.mechaOpacity;
                    absWorldSize = LevelManager.Instance.ActiveLevelData.mechaWorldSize;
                    wrapAmount = LevelManager.Instance.ActiveLevelData.mechaWrapAmount;
                    posOffset = LevelManager.Instance.ActiveLevelData.mechaLocalOffset;
                    rotOffset = LevelManager.Instance.ActiveLevelData.mechaRotationOffset;

                    if (LevelManager.Instance.ActiveLevelData.hostItemSO != null)
                    {
                        keyword = LevelManager.Instance.ActiveLevelData.hostItemSO.GetEffectiveItemId();
                    }
                    else if (!string.IsNullOrEmpty(LevelManager.Instance.ActiveLevelData.mechaHostKeyword))
                    {
                        keyword = LevelManager.Instance.ActiveLevelData.mechaHostKeyword;
                    }
                }

                FindTargetObject hostObj = FindBestHostObjectInPile(keyword);
                if (hostObj == null && LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null && LevelManager.Instance.ActiveLevelData.hostItemSO != null && LevelManager.Instance.ActiveLevelData.hostItemSO.prefab != null)
                {
                    GameObject spawnedHost = Instantiate(LevelManager.Instance.ActiveLevelData.hostItemSO.prefab, new Vector3(0f, 0.1f, 0f), Quaternion.identity);
                    spawnedHost.name = $"Host_{LevelManager.Instance.ActiveLevelData.hostItemSO.GetEffectiveItemId()}";
                    hostObj = spawnedHost.AddComponent<FindTargetObject>();
                    hostObj.Initialize(ObjectShapeType.Cube, LevelManager.Instance.ActiveLevelData.hostItemSO.targetColor, LevelManager.Instance.ActiveLevelData.hostItemSO.GetEffectiveItemId());
                }

                MechaPivotSelection pivotPref = MechaPivotSelection.Auto;
                if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
                {
                    pivotPref = LevelManager.Instance.ActiveLevelData.targetPivot;
                }

                if (hostObj != null)
                {
                    ChameleonCamouflage.EmbedMechaInHostObject(mecha, hostObj.gameObject, targetScaleRatio, targetOpacity, posOffset, rotOffset, absWorldSize, pivotPref, wrapAmount);
                    return;
                }
            }

            switch (camouflageMode)
            {
                case CamouflageMode.SingleGoalDisguise:
                    GameObject targetGoalPrefab = null;
                    if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
                    {
                        var goals = LevelManager.Instance.ActiveLevelData.targetGoals;
                        if (goals != null && goals.Count > 0)
                        {
                            var chosenGoal = goals[Random.Range(0, goals.Count)];
                            if (chosenGoal.itemData != null)
                            {
                                targetGoalPrefab = chosenGoal.itemData.prefab;
                            }
                        }
                    }

                    if (targetGoalPrefab != null)
                    {
                        ChameleonCamouflage.ApplyObjectDisguise(mecha, targetGoalPrefab, attachHeadMascot);
                    }
                    else
                    {
                        ChameleonCamouflage.ApplyDisguiseFromLivePileItem(mecha, attachHeadMascot);
                    }
                    break;

                case CamouflageMode.TextureFromPile:
                    ChameleonCamouflage.ApplyTexturedFromPile(mecha);
                    if (attachHeadMascot)
                    {
                        ChameleonCamouflage.ApplyDisguiseFromLivePileItem(mecha, true);
                    }
                    break;

                case CamouflageMode.FlatColorPalette:
                    ChameleonCamouflage.Apply(mecha, camouflagePalette, true);
                    break;
            }
        }

        private FindTargetObject FindBestHostObjectInPile(string overrideKeyword = null)
        {
            FindTargetObject[] pileItems = FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
            if (pileItems == null || pileItems.Length == 0) return null;

            string targetKeyword = !string.IsNullOrEmpty(overrideKeyword) ? overrideKeyword : preferredHostKeyword;

            // 1. Try finding host matching preferred keyword (e.g. "avocado_003" or "cake")
            if (!string.IsNullOrEmpty(targetKeyword))
            {
                foreach (var item in pileItems)
                {
                    if (item.isDocked) continue;
                    string nameLower = item.gameObject.name.ToLowerInvariant();
                    string colorLower = item.colorName != null ? item.colorName.ToLowerInvariant() : "";

                    if (nameLower.Contains(targetKeyword.ToLowerInvariant()) ||
                        colorLower.Contains(targetKeyword.ToLowerInvariant()))
                    {
                        return item;
                    }
                }

                // 1b. Fuzzy match base word without numbers (e.g. "avocado_003" -> "avocado")
                string baseKeyword = targetKeyword;
                int underscoreIdx = baseKeyword.IndexOf('_');
                if (underscoreIdx > 0)
                {
                    baseKeyword = baseKeyword.Substring(0, underscoreIdx);
                }

                if (!string.IsNullOrEmpty(baseKeyword) && baseKeyword != targetKeyword)
                {
                    foreach (var item in pileItems)
                    {
                        if (item.isDocked) continue;
                        string nameLower = item.gameObject.name.ToLowerInvariant();
                        string colorLower = item.colorName != null ? item.colorName.ToLowerInvariant() : "";

                        if (nameLower.Contains(baseKeyword.ToLowerInvariant()) ||
                            colorLower.Contains(baseKeyword.ToLowerInvariant()))
                        {
                            return item;
                        }
                    }
                }
            }

            // 2. Fallback to any non-docked item in pile
            List<FindTargetObject> validItems = new List<FindTargetObject>();
            foreach (var item in pileItems)
            {
                if (!item.isDocked) validItems.Add(item);
            }

            if (validItems.Count > 0)
            {
                return validItems[Random.Range(0, validItems.Count)];
            }

            return null;
        }
    }
}
