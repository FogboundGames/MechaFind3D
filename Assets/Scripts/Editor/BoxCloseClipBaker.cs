using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    /// <summary>
    /// Publishes the packaging box out of Assets/Box.fbx into Resources, so runtime code can load it.
    ///
    /// Box.fbx ships a single "BoxClose" take that folds all four lid flaps shut over ~70 frames.
    /// Two things still need doing at edit time:
    ///   1. Box.fbx lives outside Resources, so a prefab of it is saved into Resources.
    ///   2. The imported clip is a read-only sub-asset of the model and is not marked legacy, so a legacy
    ///      copy is written out - that is what lets <see cref="PackagingBoxFlaps"/> hold any intermediate
    ///      pose with AnimationClip.SampleAnimation instead of needing an AnimatorController.
    /// </summary>
    public static class BoxCloseClipBaker
    {
        // Located by NAME rather than a fixed path: the model has already been moved once (Assets/ ->
        // Assets/Prefabs/), which silently broke this tool until the path was chased down.
        private const string FbxName = "Box";
        private const string SourceClipName = "BoxClose";
        private const string OutputFolder = "Assets/Resources/CardboardBox";
        private const string ClipPath = OutputFolder + "/BoxClose.anim";
        private const string PrefabPath = OutputFolder + "/PackagingBox.prefab";

        [MenuItem("MechaFind3D/Kutu/Box.fbx Kapanma Klibini Üret")]
        public static void Bake()
        {
            string FbxPath = FindModelPath(FbxName);
            if (string.IsNullOrEmpty(FbxPath))
            {
                EditorUtility.DisplayDialog("Kutu Klibi", $"'{FbxName}.fbx' projede bulunamadı.", "Tamam");
                return;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Kutu Klibi", $"{FbxPath} yüklenemedi.", "Tamam");
                return;
            }

            List<AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();

            AnimationClip source = clips.FirstOrDefault(c => c.name == SourceClipName)
                                   ?? (clips.Count == 1 ? clips[0] : null);

            if (source == null)
            {
                string found = clips.Count == 0 ? "(hiç yok)" : string.Join(", ", clips.Select(c => c.name));
                EditorUtility.DisplayDialog("Kutu Klibi",
                    $"'{SourceClipName}' adında klip bulunamadı.\n\nFBX içindeki klipler: {found}\n\n" +
                    "Import Animation açık mı, ve take adı BoxClose mı?", "Tamam");
                return;
            }

            AnimationClip baked = BuildLegacyCopy(source, out int copied, out int skippedRootCurves);

            EnsureOutputFolder();
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(baked, existing);
                Object.DestroyImmediate(baked);
                baked = existing;
                EditorUtility.SetDirty(baked);
            }
            else
            {
                AssetDatabase.CreateAsset(baked, ClipPath);
            }

            CreateRuntimePrefab(model);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"📦 Kutu paketlendi: '{source.name}' → {ClipPath}\n" +
                      $"  süre {baked.length:0.00}s @ {baked.frameRate:0}fps, {copied} eğri kopyalandı" +
                      (skippedRootCurves > 0 ? $", {skippedRootCurves} kök eğrisi atlandı" : "") + "\n" +
                      $"  prefab → {PrefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ClipPath);
        }

        /// <summary>
        /// Copies the model's clip into a standalone legacy clip.
        ///
        /// Bindings with an empty path are dropped: those drive the box ROOT, whose position, rotation and
        /// scale are set per slot and then tweened for the jump-to-shelf move. Sampling would overwrite all
        /// three every frame and pin the box at the origin. Box.fbx currently nests everything under a
        /// CardboardBox child so no binding is actually root-level, but a re-export with the root stripped
        /// would silently produce exactly that.
        /// </summary>
        private static AnimationClip BuildLegacyCopy(AnimationClip source, out int copied, out int skippedRootCurves)
        {
            AnimationClip clip = new AnimationClip { name = SourceClipName, frameRate = source.frameRate };
            copied = 0;
            skippedRootCurves = 0;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (string.IsNullOrEmpty(binding.path))
                {
                    skippedRootCurves++;
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve == null || curve.length == 0) continue;

                AnimationUtility.SetEditorCurve(clip, binding, curve);
                copied++;
            }

            clip.legacy = true;
            return clip;
        }

        /// <summary>Finds a model asset anywhere in the project by file name, so moving it cannot break the tool.</summary>
        internal static string FindModelPath(string fileName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{fileName} t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == fileName) return path;
            }
            return null;
        }

        /// <summary>Creates the Resources output folder through the AssetDatabase so CreateAsset can write into it.</summary>
        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "CardboardBox");
            }
        }

        /// <summary>
        /// Box.fbx lives outside Resources, so runtime code cannot load it directly. Saves a prefab of the
        /// model into Resources and puts <see cref="PackagingBoxFlaps"/> on its root.
        /// </summary>
        private static void CreateRuntimePrefab(GameObject model)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null) return;

            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "PackagingBox";

                // The baked clip is the single source of truth for flap poses, so anything the model ships
                // that would also drive them is removed.
                foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    Object.DestroyImmediate(animator);
                }
                foreach (Animation animation in instance.GetComponentsInChildren<Animation>(true))
                {
                    Object.DestroyImmediate(animation);
                }

                if (instance.GetComponent<PackagingBoxFlaps>() == null)
                {
                    instance.AddComponent<PackagingBoxFlaps>();
                }

                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
