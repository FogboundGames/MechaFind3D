using UnityEditor;
using UnityEngine;

namespace MechaFind3D.EditorTools
{
    /// <summary>
    /// Automatic Editor Fixer to set Mecha 3D models and FBX Animation clips to Humanoid Rig type.
    /// In Unity, setting both character model and animation clips to Humanoid enables Mecanim Retargeting,
    /// allowing Mixamo/metarig animation clips to animate character limbs seamlessly (eliminating T-pose freeze).
    /// </summary>
    [InitializeOnLoad]
    public class MechaHumanoidFixer : EditorWindow
    {
        static MechaHumanoidFixer()
        {
            EditorApplication.delayCall += AutoFixRigsOnLoad;
        }

        [MenuItem("MechaTools/Fix Mecha & Animation Rigs to Humanoid", false, 5)]
        public static void FixRigsToHumanoid()
        {
            string[] searchFiles = new string[]
            {
                "Assets/Prefabs/meccha chameleon.glb",
                "Assets/Prefabs/meccha chameleon.fbx",
                "Assets/meccha chameleon@Running (1).fbx",
                "Assets/Running.fbx"
            };

            int fixedCount = 0;
            foreach (string path in searchFiles)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    if (importer.animationType != ModelImporterAnimationType.Human)
                    {
                        importer.animationType = ModelImporterAnimationType.Human;
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        importer.SaveAndReimport();
                        fixedCount++;
                        Debug.Log($"✅ Converted Rig to Humanoid for: {path}");
                    }
                }
            }

            // Also search project for any animation clips starting with meccha or Running
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.ToLowerInvariant().Contains("running") || p.ToLowerInvariant().Contains("meccha"))
                {
                    ModelImporter imp = AssetImporter.GetAtPath(p) as ModelImporter;
                    if (imp != null && imp.animationType != ModelImporterAnimationType.Human)
                    {
                        imp.animationType = ModelImporterAnimationType.Human;
                        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        imp.SaveAndReimport();
                        fixedCount++;
                        Debug.Log($"✅ Converted Rig to Humanoid for: {p}");
                    }
                }
            }

            // Re-generate Animator Controller linked to Humanoid Running clip
            MechaRunnerBehavior.GetOrCreateMechaAnimatorController();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Mecha Rig Fixer", $"Fixed {fixedCount} model asset(s) to Humanoid Rig!\n\nYour Mecha character will now run with full skeletal movement!", "Awesome!");
        }

        private static void AutoFixRigsOnLoad()
        {
            string[] searchFiles = new string[]
            {
                "Assets/Prefabs/meccha chameleon.glb",
                "Assets/Prefabs/meccha chameleon.fbx",
                "Assets/meccha chameleon@Running (1).fbx"
            };

            bool reimported = false;
            foreach (string path in searchFiles)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                    reimported = true;
                }
            }

            if (reimported)
            {
                MechaRunnerBehavior.GetOrCreateMechaAnimatorController();
                AssetDatabase.Refresh();
            }
        }
    }
}
