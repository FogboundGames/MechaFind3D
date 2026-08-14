using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    /// <summary>
    /// Editor helper for the mecha positioning system. Adds a "MechaAnchor" child to the selected
    /// food object(s). It automatically tries to find the widest/flattest surface and snaps to it.
    /// Move/rotate that anchor in the Scene view to define exactly where the camouflage mecha sits
    /// on that food — set once per food prefab, reused in every level. 
    /// Add more than one ("MechaAnchor 2", ...) for per-spawn variety.
    /// </summary>
    public static class MechaAnchorTool
    {
        private const string AnchorName = "MechaAnchor";

        [MenuItem("MechaFind3D/Add Mecha Anchor to Selection")]
        private static void AddAnchorToSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[MechaAnchorTool] Önce bir obje (yiyecek prefab/instance) seç.");
                return;
            }

            var created = new System.Collections.Generic.List<Object>();
            foreach (GameObject go in selection)
            {
                Transform anchor = MakeUniqueAnchor(go.transform);
                created.Add(anchor.gameObject);
            }

            Selection.objects = created.ToArray();
            Debug.Log($"[MechaAnchorTool] {created.Count} adet MechaAnchor eklendi. Sahne görünümünde konumlandır/döndür; birden fazla eklersen çeşitlilik olur.");
        }

        [MenuItem("MechaFind3D/Add 4 Edge Pivots (Top, Bottom, Left, Right) to Selection")]
        private static void AddFourEdgePivotsToSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[MechaAnchorTool] Önce bir obje seç.");
                return;
            }

            foreach (GameObject go in selection)
            {
                PrefabColliderPivotProcessor.EnsureCollidersExist(go);
                PrefabColliderPivotProcessor.SetupFourEdgePivots(go.transform, isMecha: go.name.ToLowerInvariant().Contains("mecha") || go.name.ToLowerInvariant().Contains("meccha"));
                EditorUtility.SetDirty(go);
            }

            Debug.Log($"[MechaAnchorTool] {selection.Length} adet objeye en dış collider kenarlarında 4 adet pivot (Pivot_Top, Pivot_Bottom, Pivot_Left, Pivot_Right) ve Collider eklendi.");
        }

        [MenuItem("MechaFind3D/Remove Old Mecha Anchors from Selection")]
        private static void RemoveOldAnchorsFromSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[MechaAnchorTool] Önce bir obje seç.");
                return;
            }

            foreach (GameObject go in selection)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Remove Old Mecha Anchors");
                PrefabColliderPivotProcessor.RemoveOldAnchors(go.transform);
                EditorUtility.SetDirty(go);
            }

            Debug.Log($"[MechaAnchorTool] {selection.Length} adet objeden eski MechaAnchor'lar temizlendi.");
        }

        [MenuItem("MechaFind3D/Add Mecha Anchor to Selection", true)]
        private static bool ValidateAddAnchor() => Selection.activeGameObject != null;

        private static Transform MakeUniqueAnchor(Transform host)
        {
            // Unique name so multiple anchors (MechaAnchor, MechaAnchor 2, ...) can coexist for variety.
            string name = AnchorName;
            int n = 2;
            while (host.Find(name) != null)
            {
                name = $"{AnchorName} {n}";
                n++;
            }

            GameObject anchorGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(anchorGo, "Create Mecha Anchor");
            Undo.SetTransformParent(anchorGo.transform, host, "Parent Mecha Anchor");

            // Smart Alignment: Find the largest flat surface of the bounding box
            Bounds b = ComputeWorldBounds(host);
            
            float areaX = b.size.y * b.size.z;
            float areaY = b.size.x * b.size.z;
            float areaZ = b.size.x * b.size.y;

            Vector3 bestAxis = Vector3.up; // default Top
            float maxArea = areaY;

            if (areaX > maxArea)
            {
                maxArea = areaX;
                bestAxis = Vector3.right; // Left/Right side
            }
            if (areaZ > maxArea)
            {
                maxArea = areaZ;
                bestAxis = Vector3.forward; // Front/Back side
            }

            // Temporarily add a MeshCollider if needed so we can raycast accurately
            MeshCollider tempCollider = null;
            if (host.GetComponentsInChildren<Collider>().Length == 0)
            {
                MeshFilter mf = host.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    tempCollider = mf.gameObject.AddComponent<MeshCollider>();
                    tempCollider.sharedMesh = mf.sharedMesh;
                }
            }

            // Raycast from outside towards the center to find the exact surface
            Vector3 rayStart = b.center + bestAxis * (b.size.magnitude * 0.6f);
            Vector3 rayDir = -bestAxis;

            // Fallback position/normal if raycast fails
            Vector3 hitPos = b.center + bestAxis * (bestAxis == Vector3.right ? b.extents.x : bestAxis == Vector3.up ? b.extents.y : b.extents.z);
            Vector3 hitNormal = bestAxis;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, rayDir, b.size.magnitude);
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(host))
                {
                    hitPos = h.point;
                    hitNormal = h.normal;
                    break;
                }
            }

            if (tempCollider != null)
            {
                Object.DestroyImmediate(tempCollider);
            }

            anchorGo.transform.position = hitPos;
            
            // Auto-rotate the anchor so the mecha lies flat on the surface by default.
            // The default flat rotation for an UP surface is (90, 0, 0).
            anchorGo.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitNormal) * Quaternion.Euler(90f, 0f, 0f);
            anchorGo.transform.localScale = Vector3.one;

            EditorUtility.SetDirty(host);
            return anchorGo.transform;
        }

        private static Bounds ComputeWorldBounds(Transform host)
        {
            Renderer[] rends = host.GetComponentsInChildren<Renderer>();
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                return b;
            }
            return new Bounds(host.position, Vector3.one * 0.5f);
        }
    }
}
