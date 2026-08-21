using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    [System.Serializable]
    public class LevelGoalRequirement
    {
        [Tooltip("Item asset reference for this match-3 goal.")]
        public ItemDataSO itemData;

        [Tooltip("Total count required to complete this goal (MUST be a multiple of 3, e.g. 3, 6, 9).")]
        public int requiredCount = 6;
    }

    /// <summary>
    /// One mecha's full spawn config: which model, which host object to hide in, and how it's posed there.
    /// The level's original single set of mecha fields (still on LevelDataSO directly, for save-file
    /// backward compatibility) describes mecha #1; every entry in <see cref="LevelDataSO.additionalMechas"/>
    /// is mecha #2, #3, etc., each with its own independent config.
    /// </summary>
    [System.Serializable]
    public class MechaSpawnEntry
    {
        [Tooltip("Which pivot point on the host object to place the mecha on.")]
        public MechaPivotSelection targetPivot = MechaPivotSelection.PivotTop;

        [Tooltip("Optional custom mecha model prefab for this mecha (defaults to Assets/Prefabs/meccha chameleon.glb if empty).")]
        public GameObject customMechaPrefab;

        [Tooltip("Direct asset reference for the host object this mecha should attach to (e.g. Cake, Apple).")]
        public ItemDataSO hostItemSO;

        [Tooltip("Keyword for target host object to attach mecha (e.g. 'cake', 'apple', 'burger'). Auto-set if hostItemSO is assigned.")]
        public string mechaHostKeyword = "";

        [Range(0.05f, 1.0f)]
        [Tooltip("Host-relative scale (used only when Mecha World Size is 0).")]
        public float mechaScaleRatio = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("How much the mecha hugs/wraps around the host instead of lying flat on it. 0 = flat spread-eagle pose, 1 = arms and legs fully curled around the object (e.g. clinging to a soda can).")]
        public float mechaWrapAmount = 0f;

        [Tooltip("ABSOLUTE mecha size in world units. If > 0, the mecha is exactly this size regardless of host scale. Set to 0 to use Mecha Scale Ratio (host-relative) instead.")]
        public float mechaWorldSize = 0.5f;

        [Range(0.1f, 1.0f)]
        [Tooltip("Opacity/Transparency of the mecha's white glass silhouette (0.22 = light, subtle white haze; the host object underneath stays clearly visible).")]
        public float mechaOpacity = 0.22f;

        [Tooltip("Local position offset of mecha on the host object surface.")]
        public Vector3 mechaLocalOffset = Vector3.zero;

        [Tooltip("Local rotation Euler angles of mecha on the host object surface (default: 90, 0, 0 for flat).")]
        public Vector3 mechaRotationOffset = new Vector3(90f, 0f, 0f);
    }

    public enum MechaPivotSelection
    {
        Auto,
        PivotTop,
        PivotBottom,
        PivotLeft,
        PivotRight,
        // Front/Back sit on the object's broad face. For thin/flat items (a melon slice, a bread slice)
        // that face is by far the largest surface, so it's the only side the mecha can actually lie
        // against instead of poking out past a narrow edge.
        PivotFront,
        PivotBack,
        MechaAnchor
    }

    [CreateAssetMenu(fileName = "Level_01", menuName = "MechaFind3D/Level Data", order = 2)]
    public class LevelDataSO : ScriptableObject
    {
        [Header("Level Information")]
        public int levelNumber = 1;
        public string levelTitle = "Seviye 1";
        
        [Tooltip("Maksimum oynanış süresi (saniye).")]
        public float timeLimit = 120f;

        [Header("Pile Physics Config")]
        [Tooltip("Total number of 3D objects spawned in the pile tray.")]
        public int totalPileCount = 30;

        [Tooltip("Visual size scale target for food/item prefabs.")]
        public float foodTargetSize = 1.20f;

        [Header("Match-3 Goals")]
        [Tooltip("Required goals for this level. Each item's count should be a multiple of 3.")]
        public List<LevelGoalRequirement> targetGoals = new List<LevelGoalRequirement>();

        [Header("Extra Filler Items (Obstacles)")]
        [Tooltip("Additional prefabs to spawn in the pile to increase difficulty (won't be listed in goals).")]
        public List<ItemDataSO> fillerItems = new List<ItemDataSO>();

        [Header("Mecha Ragdoll & Disguise Config")]
        [Tooltip("Should a camouflaged mecha character drop into this level's pile?")]
        public bool enableCamouflageMecha = true;

        [Tooltip("Which pivot point on the host object to place the mecha on.")]
        public MechaPivotSelection targetPivot = MechaPivotSelection.PivotTop;

        [Tooltip("Optional custom mecha model prefab for this level (defaults to Assets/Prefabs/meccha chameleon.glb if empty).")]
        public GameObject customMechaPrefab;

        [Tooltip("Direct asset reference for the host object mecha should attach to (e.g. Cake, Apple).")]
        public ItemDataSO hostItemSO;

        [Tooltip("Keyword for target host object to attach mecha (e.g. 'cake', 'apple', 'burger'). Auto-set if hostItemSO is assigned.")]
        public string mechaHostKeyword = "cake";

        [Range(0.05f, 1.0f)]
        [Tooltip("Host-relative scale (used only when Mecha World Size is 0).")]
        public float mechaScaleRatio = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("How much the mecha hugs/wraps around the host instead of lying flat on it. 0 = flat spread-eagle pose, 1 = arms and legs fully curled around the object (e.g. clinging to a soda can).")]
        public float mechaWrapAmount = 0f;

        [Tooltip("ABSOLUTE mecha size in world units. If > 0, the mecha is exactly this size regardless of host scale, so the tool preview and gameplay match AND you tune the size from one place. Set to 0 to use Mecha Scale Ratio (host-relative) instead.")]
        public float mechaWorldSize = 0.5f;

        [Range(0.1f, 1.0f)]
        [Tooltip("Opacity/Transparency of the mecha's white glass silhouette (0.22 = light, subtle white haze; the host object underneath stays clearly visible).")]
        public float mechaOpacity = 0.22f;

        [Tooltip("Local position offset of mecha on the host object surface.")]
        public Vector3 mechaLocalOffset = Vector3.zero;

        [Tooltip("Local rotation Euler angles of mecha on the host object surface (default: 90, 0, 0 for flat).")]
        public Vector3 mechaRotationOffset = new Vector3(90f, 0f, 0f);

        [Tooltip("Extra mechas beyond the one configured above. Each hides in its own host object - MechaRagdollSpawner never lets two mechas share a host, so add one entry per extra mecha you want in the pile.")]
        public List<MechaSpawnEntry> additionalMechas = new List<MechaSpawnEntry>();

        /// <summary>
        /// Every mecha this level spawns, as a flat list: the level's own single set of fields folded into
        /// entry 0 (only when <see cref="enableCamouflageMecha"/> is on), followed by <see cref="additionalMechas"/>
        /// in order. This is the one place that reconciles "one mecha, fields on the level itself" (how every
        /// level was authored before multi-mecha support existed) with "N mechas, one entry each" - callers
        /// should always go through this rather than reading the singular fields or additionalMechas directly.
        /// </summary>
        public List<MechaSpawnEntry> GetAllMechaEntries()
        {
            var result = new List<MechaSpawnEntry>();

            if (enableCamouflageMecha)
            {
                result.Add(new MechaSpawnEntry
                {
                    targetPivot = targetPivot,
                    customMechaPrefab = customMechaPrefab,
                    hostItemSO = hostItemSO,
                    mechaHostKeyword = mechaHostKeyword,
                    mechaScaleRatio = mechaScaleRatio,
                    mechaWrapAmount = mechaWrapAmount,
                    mechaWorldSize = mechaWorldSize,
                    mechaOpacity = mechaOpacity,
                    mechaLocalOffset = mechaLocalOffset,
                    mechaRotationOffset = mechaRotationOffset,
                });
            }

            if (additionalMechas != null)
            {
                foreach (MechaSpawnEntry entry in additionalMechas)
                {
                    if (entry != null) result.Add(entry);
                }
            }

            return result;
        }

        private void OnValidate()
        {
            if (hostItemSO != null)
            {
                mechaHostKeyword = hostItemSO.GetEffectiveItemId();
            }

            if (additionalMechas != null)
            {
                foreach (MechaSpawnEntry entry in additionalMechas)
                {
                    if (entry != null && entry.hostItemSO != null)
                    {
                        entry.mechaHostKeyword = entry.hostItemSO.GetEffectiveItemId();
                    }
                }
            }

            // Ensure goal counts are multiples of 3
            if (targetGoals != null)
            {
                foreach (var g in targetGoals)
                {
                    if (g.requiredCount < 3) g.requiredCount = 3;
                    int remainder = g.requiredCount % 3;
                    if (remainder != 0)
                    {
                        g.requiredCount += (3 - remainder);
                    }
                }
            }

            int totalGoalItems = GetTotalGoalRequiredCount();
            if (totalPileCount < totalGoalItems)
            {
                totalPileCount = totalGoalItems;
            }
        }

        public int GetTotalGoalRequiredCount()
        {
            int total = 0;
            if (targetGoals == null) return 0;
            foreach (var g in targetGoals)
            {
                if (g.itemData != null)
                {
                    total += Mathf.Max(0, g.requiredCount);
                }
            }
            return total;
        }

        /// <summary>
        /// Builds an exact list of items to spawn for this level:
        /// Spawns ONLY the explicitly configured target goals + filler items.
        /// </summary>
        public List<ItemDataSO> BuildSpawnItemList()
        {
            List<ItemDataSO> spawnList = new List<ItemDataSO>();

            // 1. Add required goal items
            if (targetGoals != null)
            {
                foreach (var goal in targetGoals)
                {
                    if (goal.itemData != null && goal.requiredCount > 0)
                    {
                        for (int i = 0; i < goal.requiredCount; i++)
                        {
                            spawnList.Add(goal.itemData);
                        }
                    }
                }
            }

            // 2. Add configured filler items (3 instances per filler item)
            if (fillerItems != null)
            {
                foreach (var filler in fillerItems)
                {
                    if (filler != null)
                    {
                        for (int f = 0; f < 3; f++)
                        {
                            spawnList.Add(filler);
                        }
                    }
                }
            }

            // 3. Guarantee host object for Mecha camouflage if enabled
            if (enableCamouflageMecha)
            {
                foreach (var mechaEntry in GetAllMechaEntries())
                {
                    if (mechaEntry != null && mechaEntry.hostItemSO != null)
                    {
                        if (!spawnList.Contains(mechaEntry.hostItemSO))
                        {
                            spawnList.Add(mechaEntry.hostItemSO);
                        }
                    }
                }
            }

            // Fallback: If list is empty, return empty list
            if (spawnList.Count == 0) return spawnList;

            // Sync totalPileCount to exact count of configured items
            totalPileCount = spawnList.Count;

            // Shuffle the spawn list so items aren't grouped by type
            ShuffleList(spawnList);
            return spawnList;
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int rnd = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[rnd];
                list[rnd] = temp;
            }
        }
    }
}
