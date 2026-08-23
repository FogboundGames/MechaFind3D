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
        [SerializeField] private bool spawnOnStart = false;

        public bool SpawnOnStart => spawnOnStart;

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

        // One level can now hide more than one mecha in its pile, each in its own host object - see
        // LevelDataSO.GetAllMechaEntries(). Every entry here came from one call to SpawnOneMecha().
        private readonly List<GameObject> currentSpawnedMechas = new List<GameObject>();

        // Hosts already claimed during THIS spawn pass, so a second mecha never embeds into the same pile
        // item as an earlier one. Cleared at the start of every SpawnAllForLevel()/SpawnRandom() call, not
        // kept across calls - a host is only "taken" for the duration of one spawn pass.
        private readonly HashSet<FindTargetObject> hostsClaimedThisPass = new HashSet<FindTargetObject>();

        private void Start()
        {
            AutoFindCharacterModelsIfEmpty();
            // Do NOT auto-spawn on Start to prevent extra duplicate mechas; LevelManager controls level mecha spawning.
        }

        public void ClearAllExistingMechas()
        {
            foreach (GameObject mecha in currentSpawnedMechas)
            {
                if (mecha != null) Destroy(mecha);
            }
            currentSpawnedMechas.Clear();

            // Clean up any unattached standalone mecha objects in scene
            foreach (var obj in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (obj != null && (obj.name.StartsWith("MechaRagdoll_") || obj.name.StartsWith("meccha chameleon")))
                {
                    if (obj.transform.parent == null)
                    {
                        if (Application.isPlaying) Destroy(obj);
                        else DestroyImmediate(obj);
                    }
                }
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
                foreach (string guid in AssetDatabase.FindAssets("meccha t:Model", new[] { "Assets/Prefabs" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null && !list.Contains(go)) list.Add(go);
                }
                if (list.Count > 0) characterModels = list.ToArray();
            }
#endif
        }

        /// <summary>
        /// Spawns every mecha the active level asks for (LevelDataSO.GetAllMechaEntries() - the level's own
        /// primary mecha fields plus any entries in additionalMechas), each hiding in its own host object.
        /// This is what LevelManager.LoadLevel() calls; SpawnRandom() below is the single-mecha path kept
        /// for anything else that only ever wants one.
        /// </summary>
        public List<GameObject> SpawnAllForLevel()
        {
            AutoFindCharacterModelsIfEmpty();
            ClearAllExistingMechas();
            hostsClaimedThisPass.Clear();

            bool hasLevelData = LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null;
            if (!hasLevelData)
            {
                // No LevelManager/LevelData at all - fall back to this component's own inspector-configured
                // single mecha, matching the original pre-multi-mecha behaviour. Deliberately NOT the same
                // fallback for "LevelData exists but says zero mechas" (enableCamouflageMecha off, no
                // additionalMechas) - that case must spawn nothing, so it stays out of this branch.
                GameObject solo = SpawnOneMecha(null);
                if (solo != null) currentSpawnedMechas.Add(solo);
                return currentSpawnedMechas;
            }

            foreach (MechaSpawnEntry entry in LevelManager.Instance.ActiveLevelData.GetAllMechaEntries())
            {
                GameObject mecha = SpawnOneMecha(entry);
                if (mecha != null) currentSpawnedMechas.Add(mecha);
            }

            return currentSpawnedMechas;
        }

        /// <summary>Single-mecha entry point kept for any caller that only ever wants one (e.g. outside a level flow).</summary>
        public GameObject SpawnRandom()
        {
            AutoFindCharacterModelsIfEmpty();
            ClearAllExistingMechas();
            hostsClaimedThisPass.Clear();

            GameObject mecha = SpawnOneMecha(null);
            if (mecha != null) currentSpawnedMechas.Add(mecha);
            return mecha;
        }

        /// <summary>
        /// Instantiates and poses ONE mecha. <paramref name="entry"/> null means "use this component's own
        /// legacy single-mecha inspector fields" (customMechaPrefab/preferredHostKeyword) instead of a
        /// LevelDataSO entry - the same fallback the original single-mecha SpawnRandomAt used when no level
        /// data was present.
        /// </summary>
        private GameObject SpawnOneMecha(MechaSpawnEntry entry)
        {
            GameObject modelToSpawn = entry != null ? entry.customMechaPrefab : null;
            if (modelToSpawn == null) modelToSpawn = customMechaPrefab;
            if (modelToSpawn == null && characterModels != null && characterModels.Length > 0)
            {
                modelToSpawn = characterModels[Random.Range(0, characterModels.Length)];
            }

            if (modelToSpawn == null)
            {
                Debug.LogWarning("[MechaRagdollSpawner] No mecha model assigned or found in project.");
                return null;
            }

            GameObject spawned = Instantiate(modelToSpawn, spawnPosition, Quaternion.Euler(spawnTiltEuler));
            spawned.name = $"MechaRagdoll_{modelToSpawn.name}";

            if (spawned.GetComponent<MechaRunnerBehavior>() == null)
            {
                spawned.AddComponent<MechaRunnerBehavior>();
            }

            // Enforce compact mini size on raw mecha model by default (never a 2-meter giant!)
            spawned.transform.localScale = Vector3.one * 0.20f;

            // Skinned/rigged mechas (e.g. meccha chameleon) must NOT be ragdoll-built: turning their
            // bones into falling physics bodies scatters the skeleton in world space, so after embedding
            // the visible mesh floats away from the host. Only ragdoll simple (non-skinned) characters.
            // (The embed step poses + strips physics itself, so ragdoll isn't needed for embedded mechas.)
            if (spawned.GetComponentInChildren<SkinnedMeshRenderer>() == null)
            {
                RagdollBuilder.Build(spawned, ragdollSettings);
            }

            if (camouflage)
            {
                ApplyCamouflageToMecha(spawned, entry);
            }

            // Fixed glass look: applied last so every mecha reads as a light, see-through white silhouette
            // no matter which disguise mode ran above (or whether embedding into a host object even happened).
            // Must use THIS mecha's own opacity - the embed step above already applied it, and this call
            // used to overwrite every mecha with one hard-coded default regardless of its own setting.
            float glassOpacity = entry != null ? entry.mechaOpacity : 0.22f;

            Color hostColor = Color.white;
            if (spawned != null && spawned.transform.parent != null)
            {
                hostColor = ChameleonCamouflage.GetHostDominantColor(spawned.transform.parent.gameObject);
            }

            ChameleonCamouflage.ApplyGlassMaterial(spawned, glassOpacity, hostColor);

            if (entry != null && entry.isStickyMecha)
            {
                StickyMechaBehavior sticky = spawned.GetComponent<StickyMechaBehavior>();
                if (sticky == null) sticky = spawned.AddComponent<StickyMechaBehavior>();
                sticky.Initialize(entry);
            }

            if (entry != null && entry.isMagnetMecha)
            {
                MagnetMechaBehavior magnet = spawned.GetComponent<MagnetMechaBehavior>();
                if (magnet == null) magnet = spawned.AddComponent<MagnetMechaBehavior>();
                magnet.Initialize(entry);
            }

            return spawned;
        }

        /// <summary>
        /// <paramref name="entry"/> null means "use this component's own legacy inspector fields" (the
        /// original single-mecha fallback for when no LevelData is driving things). When spawning several
        /// mechas in one pass, <see cref="hostsClaimedThisPass"/> keeps each one out of the others' way.
        /// </summary>
        public void ApplyCamouflageToMecha(GameObject mecha, MechaSpawnEntry entry = null)
        {
            if (mecha == null) return;

            if (embedInHostObject || camouflageMode == CamouflageMode.HostObjectEmbed)
            {
                float targetScaleRatio = entry != null ? entry.mechaScaleRatio : 0.85f;
                float targetOpacity = entry != null ? entry.mechaOpacity : 0.22f;
                float absWorldSize = entry != null ? entry.mechaWorldSize : 0f;
                float wrapAmount = entry != null ? entry.mechaWrapAmount : 0f;
                Vector3 posOffset = entry != null ? entry.mechaLocalOffset : Vector3.zero;
                Vector3 rotOffset = entry != null ? entry.mechaRotationOffset : Vector3.zero;
                MechaPivotSelection pivotPref = entry != null ? entry.targetPivot : MechaPivotSelection.Auto;

                string keyword = preferredHostKeyword;
                if (entry != null)
                {
                    if (entry.hostItemSO != null) keyword = entry.hostItemSO.GetEffectiveItemId();
                    else if (!string.IsNullOrEmpty(entry.mechaHostKeyword)) keyword = entry.mechaHostKeyword;
                }

                FindTargetObject hostObj = FindBestHostObjectInPile(keyword);
                if (hostObj == null && entry != null && entry.hostItemSO != null && entry.hostItemSO.prefab != null)
                {
                    // Nothing in the live pile matched - spawn the configured host object fresh so this
                    // mecha still has somewhere to hide, exactly as the single-mecha path always did.
                    GameObject spawnedHost = Instantiate(entry.hostItemSO.prefab, new Vector3(0f, 0.1f, 0f), Quaternion.identity);
                    spawnedHost.name = $"Host_{entry.hostItemSO.GetEffectiveItemId()}";
                    hostObj = spawnedHost.AddComponent<FindTargetObject>();
                    hostObj.Initialize(ObjectShapeType.Cube, entry.hostItemSO.targetColor, entry.hostItemSO.GetEffectiveItemId());
                }

                if (hostObj != null)
                {
                    hostsClaimedThisPass.Add(hostObj);
                    var boneOvr = entry != null ? entry.boneOverrides : null;
                    ChameleonCamouflage.EmbedMechaInHostObject(mecha, hostObj.gameObject, targetScaleRatio, targetOpacity, posOffset, rotOffset, absWorldSize, pivotPref, wrapAmount, boneOvr);
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
                    if (item.isDocked || hostsClaimedThisPass.Contains(item)) continue;
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
                        if (item.isDocked || hostsClaimedThisPass.Contains(item)) continue;
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

            // 2. Fallback to any non-docked, not-yet-claimed item in pile
            List<FindTargetObject> validItems = new List<FindTargetObject>();
            foreach (var item in pileItems)
            {
                if (!item.isDocked && !hostsClaimedThisPass.Contains(item)) validItems.Add(item);
            }

            if (validItems.Count > 0)
            {
                return validItems[Random.Range(0, validItems.Count)];
            }

            return null;
        }
    }
}
