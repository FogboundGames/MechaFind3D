using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Builds a set of small convex MeshColliders, one per skeleton bone group, that hug the mecha's
    /// actual skinned-mesh silhouette (head/torso/arms/legs) instead of a single whole-body convex hull
    /// or bounding box. A single convex hull of a T-pose/spread-pose body bridges the empty space between
    /// limbs (baggy diamond shape); this decomposes the mesh by dominant bone weight so each collider only
    /// covers its own limb, giving near-100% body-surface coverage without ballooning into empty space.
    /// Used by both the editor batch tool (PrefabColliderPivotProcessor) and the runtime embed step
    /// (ChameleonCamouflage.AddExactMeshColliderToMecha) so prefab-baked and runtime-built colliders match.
    /// </summary>
    public static class MechaColliderBuilder
    {
        public const string GeneratedColliderPrefix = "BodyCollider_";
        private const int MinVerticesPerGroup = 6;
        private const int MaxGroups = 12;

        /// <summary>
        /// Replaces any previous mecha colliders on <paramref name="mechaRoot"/> with tight per-bone
        /// convex hull colliders. Returns true if at least one collider was built.
        /// </summary>
        public static bool BuildTightBodyColliders(GameObject mechaRoot, bool isTrigger)
        {
            if (mechaRoot == null) return false;

            RemoveGeneratedColliders(mechaRoot);

            SkinnedMeshRenderer[] renderers = mechaRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool any = false;

            if (renderers != null)
            {
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (BuildForRenderer(smr, isTrigger)) any = true;
                }
            }

            // Non-skinned mecha fallback: no bones to decompose by, so give each mesh its own tight hull
            // (still far tighter than a single bounding box).
            if (!any)
            {
                MeshFilter[] meshFilters = mechaRoot.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter mf in meshFilters)
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    if (mf.GetComponent<SkinnedMeshRenderer>() != null) continue;

                    GameObject targetGo = mf.gameObject;
                    MeshCollider mc = targetGo.GetComponent<MeshCollider>();
                    if (mc == null) mc = targetGo.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                    mc.isTrigger = isTrigger;
                    any = true;
                }
            }

            return any;
        }

        private static bool BuildForRenderer(SkinnedMeshRenderer smr, bool isTrigger)
        {
            if (smr == null || smr.sharedMesh == null) return false;

            Mesh mesh = smr.sharedMesh;
            Transform[] bones = smr.bones;
            Matrix4x4[] bindposes = mesh.bindposes;
            BoneWeight[] weights = mesh.boneWeights;
            Vector3[] verts = mesh.vertices;

            // Remove any old whole-mesh collider sitting directly on the renderer so it can't stack
            // with the new per-bone colliders.
            MeshCollider directCollider = smr.GetComponent<MeshCollider>();
            if (directCollider != null) SafeDestroy(directCollider);

            if (bones == null || bones.Length == 0 || bindposes == null || bindposes.Length != bones.Length ||
                weights == null || weights.Length != verts.Length)
            {
                // No usable skin data (e.g. rigid single-bone import): fall back to one hull for the whole mesh.
                MeshCollider mc = smr.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
                mc.isTrigger = isTrigger;
                return true;
            }

            // 1. Dominant bone per vertex (highest of the up-to-4 skin weights).
            int[] dominantBone = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                BoneWeight w = weights[i];
                int bi = w.boneIndex0; float bw = w.weight0;
                if (w.weight1 > bw) { bw = w.weight1; bi = w.boneIndex1; }
                if (w.weight2 > bw) { bw = w.weight2; bi = w.boneIndex2; }
                if (w.weight3 > bw) { bw = w.weight3; bi = w.boneIndex3; }
                dominantBone[i] = bi;
            }

            // 2. Group vertex indices by dominant bone.
            Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < verts.Length; i++)
            {
                int bi = dominantBone[i];
                if (bi < 0 || bi >= bones.Length || bones[bi] == null) continue;
                if (!groups.TryGetValue(bi, out List<int> list))
                {
                    list = new List<int>();
                    groups[bi] = list;
                }
                list.Add(i);
            }

            if (groups.Count == 0)
            {
                MeshCollider mc = smr.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
                mc.isTrigger = isTrigger;
                return true;
            }

            // 3. Map each bone to its nearest ancestor that is also a skin bone (for merging small groups upward).
            int[] parentBoneIndex = BuildParentBoneMap(bones);

            // 4. Merge groups that are too small to form a stable convex hull into their parent bone's group.
            MergeSmallGroups(groups, parentBoneIndex, MinVerticesPerGroup);

            // 5. Cap total collider count so a highly-segmented rig doesn't spawn dozens of colliders.
            MergeSmallGroups(groups, parentBoneIndex, 0, MaxGroups);

            // 6. Build one convex hull MeshCollider per surviving group, parented to its bone in bind-pose-
            //    relative local space so it deforms with the bone exactly like the skinned mesh does.
            int created = 0;
            foreach (KeyValuePair<int, List<int>> kv in groups)
            {
                int boneIdx = kv.Key;
                List<int> vertIdx = kv.Value;
                if (vertIdx.Count < 4) continue;

                Transform bone = bones[boneIdx];
                Matrix4x4 bind = bindposes[boneIdx];

                List<Vector3> localPositions = new List<Vector3>(vertIdx.Count);
                foreach (int vi in vertIdx)
                {
                    localPositions.Add(bind.MultiplyPoint3x4(verts[vi]));
                }

                Mesh hullMesh = new Mesh { name = $"HullMesh_{bone.name}" };
                if (localPositions.Count > 65000)
                {
                    hullMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }
                hullMesh.SetVertices(localPositions);

                int[] tris = new int[(localPositions.Count - 2) * 3];
                int t = 0;
                for (int i = 1; i < localPositions.Count - 1; i++)
                {
                    tris[t++] = 0;
                    tris[t++] = i;
                    tris[t++] = i + 1;
                }
                hullMesh.SetTriangles(tris, 0);
                hullMesh.RecalculateBounds();

                string childName = GeneratedColliderPrefix + bone.name;
                Transform existingChild = bone.Find(childName);
                GameObject colliderGo = existingChild != null ? existingChild.gameObject : new GameObject(childName);
                colliderGo.transform.SetParent(bone, false);
                colliderGo.transform.localPosition = Vector3.zero;
                colliderGo.transform.localRotation = Quaternion.identity;
                colliderGo.transform.localScale = Vector3.one;

                MeshCollider mc = colliderGo.GetComponent<MeshCollider>();
                if (mc == null) mc = colliderGo.AddComponent<MeshCollider>();
                mc.sharedMesh = hullMesh;
                mc.convex = true;
                mc.isTrigger = isTrigger;

                created++;
            }

            return created > 0;
        }

        private static int[] BuildParentBoneMap(Transform[] bones)
        {
            int[] parentBoneIndex = new int[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                parentBoneIndex[b] = -1;
                if (bones[b] == null) continue;

                Transform p = bones[b].parent;
                while (p != null)
                {
                    int idx = System.Array.IndexOf(bones, p);
                    if (idx >= 0)
                    {
                        parentBoneIndex[b] = idx;
                        break;
                    }
                    p = p.parent;
                }
            }
            return parentBoneIndex;
        }

        private static void MergeSmallGroups(Dictionary<int, List<int>> groups, int[] parentBoneIndex, int minCount, int maxGroupCount = -1)
        {
            while (groups.Count > 1)
            {
                int smallestBone = -1;
                int smallestCount = int.MaxValue;
                foreach (KeyValuePair<int, List<int>> kv in groups)
                {
                    if (kv.Value.Count < smallestCount)
                    {
                        smallestCount = kv.Value.Count;
                        smallestBone = kv.Key;
                    }
                }

                bool underMinThreshold = smallestCount < minCount;
                bool overGroupCap = maxGroupCount > 0 && groups.Count > maxGroupCount;
                if (!underMinThreshold && !overGroupCap) break;

                int targetBone = parentBoneIndex[smallestBone];
                while (targetBone >= 0 && !groups.ContainsKey(targetBone))
                {
                    targetBone = parentBoneIndex[targetBone];
                }

                if (targetBone < 0)
                {
                    int bestBone = -1, bestCount = -1;
                    foreach (KeyValuePair<int, List<int>> kv in groups)
                    {
                        if (kv.Key != smallestBone && kv.Value.Count > bestCount)
                        {
                            bestCount = kv.Value.Count;
                            bestBone = kv.Key;
                        }
                    }
                    targetBone = bestBone;
                }

                if (targetBone < 0 || targetBone == smallestBone) break;

                groups[targetBone].AddRange(groups[smallestBone]);
                groups.Remove(smallestBone);
            }
        }

        /// <summary>Removes previously generated per-bone colliders and the legacy whole-body BoxCollider.</summary>
        public static void RemoveGeneratedColliders(GameObject root)
        {
            if (root == null) return;

            List<GameObject> toRemove = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.StartsWith(GeneratedColliderPrefix, System.StringComparison.Ordinal))
                {
                    toRemove.Add(t.gameObject);
                }
            }
            foreach (GameObject g in toRemove) SafeDestroy(g);

            BoxCollider legacyBox = root.GetComponent<BoxCollider>();
            if (legacyBox != null) SafeDestroy(legacyBox);
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
