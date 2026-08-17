using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    /// <summary>
    /// Publishes Assets/Prefabs/ConveyorTile.fbx into Resources as a ready-to-spawn 3D conveyor belt,
    /// replacing the old flat UI conveyor (MatchFactory_Canvas/Conveyor_Belt_Panel and its Chevron_Arrows Text).
    ///
    /// The model needs no clip baking - it ships no animation at all - so this only assembles the prefab:
    /// attaches <see cref="ConveyorBelt"/>, and hides BeltPath if the model still carries one (the old
    /// Conveyor.fbx had a degenerate zero-thickness guide strip that rendered as a stray sliver;
    /// ConveyorTile.fbx no longer ships it).
    /// </summary>
    public static class ConveyorPrefabBaker
    {
        // Located by NAME rather than a fixed path - the models were moved from Assets/ to Assets/Prefabs/
        // once already, which silently broke this tool. Exact name: the superseded Conveyor.fbx is still
        // in the project, and a prefix match would happily pick it up instead.
        private const string FbxName = "ConveyorTile";
        private const string OutputFolder = "Assets/Resources/Conveyor";
        private const string PrefabPath = OutputFolder + "/ConveyorBelt.prefab";

        [MenuItem("MechaFind3D/Konveyör/ConveyorTile.fbx Prefab'ını Üret")]
        public static void Bake()
        {
            string fbxPath = BoxCloseClipBaker.FindModelPath(FbxName);
            GameObject model = string.IsNullOrEmpty(fbxPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Konveyör", $"'{FbxName}.fbx' projede bulunamadı.", "Tamam");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null)
            {
                EditorUtility.DisplayDialog("Konveyör", "Model sahneye alınamadı.", "Tamam");
                return;
            }

            int dashCount = 0;
            bool hidBeltPath = false;

            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "ConveyorBelt";

                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Arrow_")) dashCount++;

                    if (t.name == "BeltPath")
                    {
                        // Degenerate guide geometry (zero extent on one axis) - keep the transform as the
                        // authored path reference, but stop it from drawing.
                        var r = t.GetComponent<Renderer>();
                        if (r != null) { r.enabled = false; hidBeltPath = true; }
                    }
                }

                // Purely decorative: it must never take part in the pile physics or block taps.
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c);
                }

                if (instance.GetComponent<ConveyorBelt>() == null)
                {
                    instance.AddComponent<ConveyorBelt>();
                }

                EnsureOutputFolder();
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"🎞️ Konveyör prefab'ı üretildi → {PrefabPath}\n" +
                      $"  {dashCount} şerit parçası, BeltPath render {(hidBeltPath ? "kapatıldı" : "bulunamadı")}, " +
                      $"kök rotasyonu {model.transform.rotation.eulerAngles} (Z-up dönüşümü, korunuyor)");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
        }

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Conveyor");
            }
        }
    }
}
