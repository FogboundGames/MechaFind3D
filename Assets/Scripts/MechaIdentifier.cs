using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    public static class MechaIdentifier
    {
        public static bool IsMechaItem(FindTargetObject item)
        {
            if (item == null) return false;
            if (item.name.Contains("Mecha") || item.name.Contains("meccha")) return true;
            if (item.colorName != null && (item.colorName.Equals("mecha", System.StringComparison.OrdinalIgnoreCase) || item.colorName.Contains("Mecha"))) return true;
            if (item.GetComponentInChildren<MechaRagdollSpawner>() != null) return true;
            if (item.transform.Find("MechaRagdoll") != null || item.transform.Find("meccha chameleon") != null) return true;

            foreach (Transform t in item.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains("Mecha") || t.name.Contains("meccha")) return true;
            }
            return false;
        }

        public static bool HasChildMecha(FindTargetObject item)
        {
            if (item == null) return false;
            foreach (Transform child in item.GetComponentsInChildren<Transform>(true))
            {
                if (child != item.transform && (child.name.Contains("Mecha") || child.name.Contains("meccha") || child.name.Contains("Ragdoll")))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsHitOnMechaCollider(FindTargetObject item, Collider hitCollider)
        {
            if (hitCollider == null) return false;

            Transform t = hitCollider.transform;
            while (t != null && (item == null || t != item.transform))
            {
                string name = t.name.ToLowerInvariant();
                if (name.Contains("mecha") || name.Contains("meccha") || name.Contains("ragdoll") ||
                    name.Contains("bodycollider") || name.Contains("mixamorig") || name.Contains("hullmesh") ||
                    name.Contains("bone") || name.Contains("arm") || name.Contains("leg") || name.Contains("head"))
                {
                    return true;
                }
                if (t.GetComponent<MechaRagdollSpawner>() != null || t.GetComponent<SkinnedMeshRenderer>() != null) return true;
                t = t.parent;
            }
            return false;
        }
    }
}
