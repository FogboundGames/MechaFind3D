using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    /// <summary>
    /// Publishes CurtainPortal.fbx into Resources as a ready-to-spawn strip curtain for the end of the belt.
    ///
    /// Same two problems as the packaging box, so the same two fixes: the model lives outside Resources, and
    /// its imported clip is a read-only sub-asset that is not marked legacy - and only a legacy clip can be
    /// held at an arbitrary pose with <c>SampleAnimation</c>, which is how <see cref="CurtainPortal"/> drives
    /// the strips without needing an AnimatorController.
    /// </summary>
    public static class CurtainPortalBaker
    {
        private const string FbxName = "CurtainPortal";
        private const string ClipName = "CurtainPush";
        private const string OutputFolder = "Assets/Resources/Curtain";
        private const string ClipPath = OutputFolder + "/CurtainPush.anim";
        private const string PrefabPath = OutputFolder + "/CurtainPortal.prefab";

        [MenuItem("MechaFind3D/Konveyör/Perde Portalını Üret (CurtainPortal.fbx)")]
        public static void Bake()
        {
            string fbxPath = BoxCloseClipBaker.FindModelPath(FbxName);
            GameObject model = string.IsNullOrEmpty(fbxPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            if (model == null)
            {
                EditorUtility.DisplayDialog("Perde Portalı", $"'{FbxName}.fbx' projede bulunamadı.", "Tamam");
                return;
            }

            List<AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();

            if (clips.Count == 0)
            {
                EditorUtility.DisplayDialog("Perde Portalı",
                    $"{fbxPath} içinde hiç animasyon klibi yok. Import Animation açık mı?", "Tamam");
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

            Debug.Log($"🚪 Perde portalı üretildi: {clips.Count} klip → {ClipPath}\n" +
                      $"  süre {baked.length:0.00}s @ {baked.frameRate:0}fps, {copied} eğri kopyalandı" +
                      (skippedRootCurves > 0 ? $", {skippedRootCurves} kök eğrisi atlandı" : "") + "\n" +
                      $"  prefab → {PrefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
        }

        /// <summary>
        /// Merges the model's clips into one legacy clip.
        ///
        /// The strips are exported one AnimationStack per object, so every clip carries curves for every
        /// strip and only the stack's own strip actually moves in it. Copying a flat hold would overwrite
        /// the real motion another clip contributes for that strip, so constant curves are dropped - the
        /// same trap the packaging box hit. Empty-path bindings are dropped too: those drive the portal
        /// ROOT, whose transform is placed against the belt at runtime.
        /// </summary>
        private static AnimationClip BuildLegacyCopy(List<AnimationClip> sources, out int copied, out int skippedRootCurves)
        {
            AnimationClip clip = new AnimationClip { name = ClipName, frameRate = sources[0].frameRate };
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

        private static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Curtain");
            }
        }

        private static void CreateRuntimePrefab(GameObject model)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null) return;

            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "CurtainPortal";

                // The baked clip is the single source of truth for strip poses, so anything the model ships
                // that would also drive them is removed.
                foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    Object.DestroyImmediate(animator);
                }
                foreach (Animation animation in instance.GetComponentsInChildren<Animation>(true))
                {
                    Object.DestroyImmediate(animation);
                }

                // Decoration only: it must never take part in the pile physics or block taps.
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(c);
                }

                if (instance.GetComponent<CurtainPortal>() == null)
                {
                    instance.AddComponent<CurtainPortal>();
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
