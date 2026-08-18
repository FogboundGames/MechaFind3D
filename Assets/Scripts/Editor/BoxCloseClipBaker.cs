using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    /// <summary>
    /// Publishes the packaging box out of Assets/Box.fbx into Resources, so runtime code can load it.
    ///
    /// Box.fbx ships one take per animated object (four lid flaps, the tape gun and the three tape
    /// strips), all authored on one shared 1-140 frame timeline: the flaps fold shut over the first
    /// ~70 frames, then the gun runs the tape over the seam. They are merged here into one clip.
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
        private const string SourceClipName = "BoxClose";
        private const string OutputFolder = "Assets/Resources/CardboardBox";
        private const string ClipPath = OutputFolder + "/BoxClose.anim";
        private const string PrefabPath = OutputFolder + "/PackagingBox.prefab";

        // Both boxes bake to the SAME clip and prefab paths, so swapping which one the game uses is a menu
        // click rather than a code edit. Whichever was baked last is what PackagingBoxFlaps loads.
        [MenuItem("MechaFind3D/Kutu/Pastane Kutusunu Üret (BakeryBox.fbx)")]
        public static void BakeBakeryBox() => Bake("BakeryBox");

        [MenuItem("MechaFind3D/Kutu/Eski Koliyi Üret (Box.fbx)")]
        public static void BakeCardboardBox() => Bake("Box");

        public static void Bake(string FbxName)
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

            if (clips.Count == 0)
            {
                EditorUtility.DisplayDialog("Kutu Klibi",
                    $"{FbxPath} içinde hiç animasyon klibi yok.\n\nImport Animation açık mı?", "Tamam");
                return;
            }

            AnimationClip baked = BuildLegacyCopy(clips, out int copied, out int skippedRootCurves);

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

            Debug.Log($"📦 Kutu paketlendi: {clips.Count} klip birleştirildi → {ClipPath}\n" +
                      $"  süre {baked.length:0.00}s @ {baked.frameRate:0}fps, {copied} eğri kopyalandı" +
                      (skippedRootCurves > 0 ? $", {skippedRootCurves} kök eğrisi atlandı" : "") + "\n" +
                      $"  prefab → {PrefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ClipPath);
        }

        /// <summary>
        /// Merges the model's clips into one standalone legacy clip.
        ///
        /// A correctly exported Box.fbx holds ONE take covering the whole 1-140 frame timeline, so normally
        /// there is a single clip and every curve is copied. A re-export with Blender's "all actions" bake
        /// option on instead emits one stack per (object x action) pair; those are merged here, and in that
        /// case constant curves are dropped, since another clip's flat hold for an object would otherwise
        /// overwrite the real motion. (That export is still worth avoiding: it also applies each action to
        /// objects it was never authored for, which no merge can undo.)
        ///
        /// Bindings with an empty path are dropped: those drive the box ROOT, whose position, rotation and
        /// scale are set per slot and then tweened for the jump-to-shelf move. Sampling would overwrite all
        /// three every frame and pin the box at the origin. Box.fbx currently nests everything under a
        /// CardboardBox child so no binding is actually root-level, but a re-export with the root stripped
        /// would silently produce exactly that.
        /// </summary>
        private static AnimationClip BuildLegacyCopy(List<AnimationClip> sources, out int copied, out int skippedRootCurves)
        {
            AnimationClip clip = new AnimationClip { name = SourceClipName, frameRate = sources[0].frameRate };
            copied = 0;
            skippedRootCurves = 0;
            bool dropHolds = sources.Count > 1;

            foreach (AnimationClip source in sources)
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
                {
                    if (string.IsNullOrEmpty(binding.path))
                    {
                        skippedRootCurves++;
                        continue;
                    }

                    AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                    if (curve == null || curve.length == 0) continue;
                    if (dropHolds && IsConstant(curve)) continue;

                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                    copied++;
                }
            }

            clip.legacy = true;
            return clip;
        }

        /// <summary>True when the curve never leaves its first value, i.e. it is a hold rather than motion.</summary>
        private static bool IsConstant(AnimationCurve curve)
        {
            float first = curve[0].value;
            for (int i = 1; i < curve.length; i++)
            {
                if (!Mathf.Approximately(curve[i].value, first)) return false;
            }
            return true;
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

                // The bakery box ships four recolourable material zones; its palette component is what
                // makes them switchable from the Inspector instead of by editing materials. Added only
                // when the model actually has those parts, so the old cardboard box is left alone.
                if (instance.transform.Find("Ribbon_Bow") != null && instance.GetComponent<BakeryBoxPalette>() == null)
                {
                    instance.AddComponent<BakeryBoxPalette>();
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
