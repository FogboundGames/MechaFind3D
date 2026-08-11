using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    /// <summary>
    /// Editor automation tool that processes all item prefabs and Mecha prefabs:
    /// 1. Ensures every prefab has proper Colliders (MeshCollider on plain items; for skinned Mecha models,
    ///    a set of tight per-limb convex hull colliders via MechaColliderBuilder instead of one loose hull/box).
    /// 2. Calculates local bounding box and places 4 edge pivots (Pivot_Top, Pivot_Bottom, Pivot_Left, Pivot_Right)
    ///    right at the outer edge skin of the collider.
    /// 3. Processes Mecha prefabs with edge pivots + Pivot_BaseContact for 100% flush surface alignment.
    /// </summary>
    public static class PrefabColliderPivotProcessor
    {
        public const string PivotTopName = "Pivot_Top";
        public const string PivotBottomName = "Pivot_Bottom";
        public const string PivotLeftName = "Pivot_Left";
        public const string PivotRightName = "Pivot_Right";
        public const string PivotFrontName = "Pivot_Front";
        public const string PivotBackName = "Pivot_Back";
        public const string PivotBaseContactName = "Pivot_BaseContact";

        [MenuItem("MechaFind3D/1-Click: Process Colliders & 4 Edge Pivots for All Library Prefabs & Mecha")]
        public static void ProcessAllPrefabsMenuItem()
        {
            ProcessAllLibraryPrefabs();
        }

        [MenuItem("MechaFind3D/Process Colliders & 4 Edge Pivots on Selection")]
        public static void ProcessSelectionMenuItem()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[PrefabColliderPivotProcessor] Lütfen önce sahne veya proje görünümünde en az bir obje/prefab seçin.");
                return;
            }

            int count = 0;
            foreach (GameObject go in selection)
            {
                string path = AssetDatabase.GetAssetPath(go);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    ProcessSinglePrefab(path);
                    count++;
                }
                else
                {
                    // Process scene instance directly
                    Undo.RegisterFullObjectHierarchyUndo(go, "Process Colliders & Pivots");
                    EnsureCollidersExist(go);
                    SetupFourEdgePivots(go.transform, isMecha: go.name.ToLowerInvariant().Contains("mecha") || go.name.ToLowerInvariant().Contains("meccha"));
                    EditorUtility.SetDirty(go);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PrefabColliderPivotProcessor] ✅ {count} adet obje/prefab başarıyla işlendi (Collider + 4 Pivot eklendi).");
        }

        public static int ProcessAllLibraryPrefabs()
        {
            List<string> prefabPaths = FindAllItemPrefabPaths();
            int processedCount = 0;

            try
            {
                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    string path = prefabPaths[i];
                    float progress = (float)i / prefabPaths.Count;
                    EditorUtility.DisplayProgressBar("Objeler İşleniyor", $"Processing ({i + 1}/{prefabPaths.Count}): {Path.GetFileName(path)}", progress);

                    if (ProcessSinglePrefab(path))
                    {
                        processedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PrefabColliderPivotProcessor] 🚀 İŞLEM TAMAMLANDI: Toplam {processedCount} adet prefab ve Mecha modeline Collider + 4 kenar pivotu eklendi!");
            EditorUtility.DisplayDialog("İşlem Tamamlandı", $"Toplam {processedCount} adet kütüphane prefabı ve Mecha modeline en dış collider kenarlarına 4 adet pivot (Pivot_Top, Pivot_Bottom, Pivot_Left, Pivot_Right) ve Collider eklendi!", "Tamam");

            return processedCount;
        }

        public static bool ProcessSinglePrefab(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;

            if (assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(assetPath);
                if (root == null) return false;

                bool isMecha = root.name.ToLowerInvariant().Contains("mecha") || root.name.ToLowerInvariant().Contains("meccha");

                EnsureCollidersExist(root);
                SetupFourEdgePivots(root.transform, isMecha);

                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                PrefabUtility.UnloadPrefabContents(root);
                return true;
            }
            else if (assetPath.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase) || assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                // GLB / FBX model file: create or update a .prefab wrapper asset in Assets/Prefabs
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (modelAsset == null) return false;

                string destDirectory = "Assets/Prefabs";
                if (!Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                }

                string prefabName = Path.GetFileNameWithoutExtension(assetPath);
                string newPrefabPath = $"{destDirectory}/{prefabName}.prefab";

                GameObject instance = Object.Instantiate(modelAsset);
                instance.name = prefabName;

                bool isMecha = prefabName.ToLowerInvariant().Contains("mecha") || prefabName.ToLowerInvariant().Contains("meccha");

                EnsureCollidersExist(instance);
                SetupFourEdgePivots(instance.transform, isMecha);

                PrefabUtility.SaveAsPrefabAsset(instance, newPrefabPath);
                Object.DestroyImmediate(instance);

                Debug.Log($"[PrefabColliderPivotProcessor] Model asset için `.prefab` wrapper oluşturuldu/güncellendi: {newPrefabPath}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ensures every mesh renderer under root has a valid Collider: a convex MeshCollider for plain
        /// items, or a tight per-limb collider set (via MechaColliderBuilder) for skinned Mecha models.
        /// </summary>
        public static void EnsureCollidersExist(GameObject root)
        {
            if (root == null) return;

            Collider[] existingColliders = root.GetComponentsInChildren<Collider>(true);
            
            // Check MeshFilters
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject targetGo = mf.gameObject;
                
                if (targetGo.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = targetGo.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true; // Required for dynamic Rigidbody physics in pile
                }
            }

            // Check SkinnedMeshRenderers (e.g. Mecha model) -> Build tight per-limb convex hull colliders
            // instead of a single whole-body hull/box. A single convex hull of a spread-limb pose bridges
            // the empty space between arms/legs (baggy shape); per-bone decomposition hugs the actual
            // silhouette so the body touches surfaces without clipping through them.
            if (root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                MechaFind3D.PhysicsInteraction.MechaColliderBuilder.BuildTightBodyColliders(root, isTrigger: false);
            }
        }

        public static void RemoveOldAnchors(Transform root)
        {
            if (root == null) return;
            List<GameObject> toDestroy = new List<GameObject>();
            foreach (Transform t in root)
            {
                if (t.name.StartsWith("MechaAnchor", System.StringComparison.OrdinalIgnoreCase))
                {
                    toDestroy.Add(t.gameObject);
                }
            }

            foreach (GameObject go in toDestroy)
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Calculates the local bounding box of the outermost colliders/renderers and places 4 edge pivots
        /// (Pivot_Top, Pivot_Bottom, Pivot_Left, Pivot_Right) right at the outer edge skin.
        /// Automatically removes old MechaAnchor children.
        /// </summary>
        public static void SetupFourEdgePivots(Transform root, bool isMecha = false)
        {
            if (root == null) return;

            // Remove old MechaAnchor pivots
            RemoveOldAnchors(root);

            Bounds localBounds = ComputeRootLocalBounds(root);

            // Pivot local coordinates right at outermost edge boundaries:
            Vector3 topLocalPos = new Vector3(localBounds.center.x, localBounds.max.y, localBounds.center.z);
            Vector3 bottomLocalPos = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
            Vector3 leftLocalPos = new Vector3(localBounds.min.x, localBounds.center.y, localBounds.center.z);
            Vector3 rightLocalPos = new Vector3(localBounds.max.x, localBounds.center.y, localBounds.center.z);

            // Broad-face pivots. On thin items (a melon slice) this face is the biggest surface by far,
            // and it's the only one the mecha can actually lie flat against — the four rim pivots leave
            // it poking out past a narrow edge.
            Vector3 frontLocalPos = new Vector3(localBounds.center.x, localBounds.center.y, localBounds.min.z);
            Vector3 backLocalPos = new Vector3(localBounds.center.x, localBounds.center.y, localBounds.max.z);

            // Create/Update edge + face pivots with outward rotations
            CreateOrUpdatePivotChild(root, PivotTopName, topLocalPos, Quaternion.identity);
            CreateOrUpdatePivotChild(root, PivotBottomName, bottomLocalPos, Quaternion.Euler(180f, 0f, 0f));
            CreateOrUpdatePivotChild(root, PivotLeftName, leftLocalPos, Quaternion.Euler(0f, 0f, 90f));
            CreateOrUpdatePivotChild(root, PivotRightName, rightLocalPos, Quaternion.Euler(0f, 0f, -90f));
            CreateOrUpdatePivotChild(root, PivotFrontName, frontLocalPos, Quaternion.Euler(-90f, 0f, 0f));
            CreateOrUpdatePivotChild(root, PivotBackName, backLocalPos, Quaternion.Euler(90f, 0f, 0f));

            // If it's a Mecha, also create Pivot_BaseContact at the bottom/belly contact line
            if (isMecha)
            {
                CreateOrUpdatePivotChild(root, PivotBaseContactName, bottomLocalPos, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private static Transform CreateOrUpdatePivotChild(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                GameObject go = new GameObject(name);
                existing = go.transform;
                existing.SetParent(parent, false);
            }

            existing.localPosition = localPosition;
            existing.localRotation = localRotation;
            existing.localScale = Vector3.one;

            return existing;
        }

        private static Bounds ComputeRootLocalBounds(Transform root)
        {
            if (root == null) return new Bounds(Vector3.zero, Vector3.one * 0.5f);

            Vector3 origPos = root.position;
            Quaternion origRot = root.rotation;
            Vector3 origScale = root.localScale;

            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Bounds combined = default;
            bool hasBounds = false;

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends != null && rends.Length > 0)
            {
                foreach (Renderer r in rends)
                {
                    if (r == null || !r.enabled) continue;
                    if (r.gameObject.name.Contains("Mecha_Head_Mascot") || r.gameObject.name.Contains("Preview_Mecha") || r.gameObject.name.StartsWith("Pivot_") || r.gameObject.name.StartsWith("MechaAnchor")) continue;

                    if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                    else combined.Encapsulate(r.bounds);
                }
            }

            if (!hasBounds)
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                if (colliders != null && colliders.Length > 0)
                {
                    foreach (Collider c in colliders)
                    {
                        if (c == null || !c.enabled) continue;
                        if (c.gameObject.name.StartsWith("Pivot_") || c.gameObject.name.StartsWith("MechaAnchor")) continue;

                        if (!hasBounds) { combined = c.bounds; hasBounds = true; }
                        else combined.Encapsulate(c.bounds);
                    }
                }
            }

            root.position = origPos;
            root.rotation = origRot;
            root.localScale = origScale;

            if (hasBounds)
            {
                return combined;
            }

            return new Bounds(Vector3.zero, Vector3.one * 0.5f);
        }

        public static List<string> FindAllItemPrefabPaths()
        {
            List<string> paths = new List<string>();

            // Search directories
            string[] searchFolders = new string[]
            {
                "Assets/ithappy",
                "Assets/kenney_food-kit",
                "Assets/Prefabs",
                "Assets/Resources"
            };

            foreach (string folder in searchFolders)
            {
                if (!Directory.Exists(folder)) continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab t:Model", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                    {
                        paths.Add(path);
                    }
                }
            }

            // Also check prefabs referenced in ItemDataSO assets
            string[] itemSoGuids = AssetDatabase.FindAssets("t:ItemDataSO");
            foreach (string guid in itemSoGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var itemSo = AssetDatabase.LoadAssetAtPath<MechaFind3D.PhysicsInteraction.ItemDataSO>(path);
                if (itemSo != null && itemSo.prefab != null)
                {
                    string pPath = AssetDatabase.GetAssetPath(itemSo.prefab);
                    if (!string.IsNullOrEmpty(pPath) && !paths.Contains(pPath))
                    {
                        paths.Add(pPath);
                    }
                }
            }

            return paths;
        }
    }
}
