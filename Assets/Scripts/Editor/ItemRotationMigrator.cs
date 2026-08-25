using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    public static class ItemRotationMigrator
    {
        [MenuItem("Tools/Migrate Item Rotation Overrides (one-time)")]
        public static void Migrate()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDataSO");
            int updated = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemDataSO so = AssetDatabase.LoadAssetAtPath<ItemDataSO>(path);
                if (so == null) continue;
                if (so.overrideDockRotation || so.overrideCardRotation) continue;

                string id = so.GetEffectiveItemId().ToLowerInvariant();
                bool changed = false;

                if (id.Equals("watermelon_001"))
                {
                    so.overrideDockRotation = true;
                    so.dockRotationEuler = new Vector3(-90f, 0f, 0f);
                    so.overrideCardRotation = true;
                    so.cardRotationEuler = new Vector3(-75f, 0f, 0f);
                    changed = true;
                }
                else if (id.Contains("watermelon"))
                {
                    so.overrideDockRotation = true;
                    so.dockRotationEuler = new Vector3(-90f, 0f, 45f);
                    so.overrideCardRotation = true;
                    so.cardRotationEuler = new Vector3(15f, -30f, 40f);
                    changed = true;
                }
                else if (id.Contains("fish"))
                {
                    so.overrideDockRotation = true;
                    so.dockRotationEuler = new Vector3(0f, 0f, 0f);
                    so.overrideCardRotation = true;
                    so.cardRotationEuler = new Vector3(15f, -45f, 10f);
                    changed = true;
                }
                else if (id.Contains("sausage") || id.Contains("sasuage") || id.Contains("shrimp") || id.Contains("chili"))
                {
                    so.overrideDockRotation = true;
                    so.dockRotationEuler = new Vector3(0f, 0f, 0f);
                    changed = true;
                }
                else if (id.Contains("egg") && !id.Contains("eggplant"))
                {
                    so.overrideDockRotation = true;
                    so.dockRotationEuler = new Vector3(180f, 15f, 0f);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(so);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemRotationMigrator] {updated} ItemDataSO asset güncellendi.");
        }
    }
}
