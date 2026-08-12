using DG.Tweening;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Drives the four lid flaps of the Box.fbx packaging box using the artist-authored closing
    /// animation that ships inside the FBX, instead of tweening hand-tuned quaternions.
    ///
    /// Box.fbx exports one AnimationStack per (flap x action) pair - 68 clips in total - so no single
    /// imported clip closes the whole box. <see cref="MechaFind3D.PhysicsInteraction.EditorTools.BoxCloseClipBaker"/>
    /// merges the four "flap folds shut" clips into one <c>BoxClose</c> clip; this component plays that
    /// merged clip by sampling it, which keeps the authored timing (including the small backswing before
    /// each flap folds) while still letting the caller pick the duration and drive it from a DOTween
    /// sequence.
    ///
    /// t = 0 is the fully open pose, t = clip.length is the sealed pose.
    /// </summary>
    public class PackagingBoxFlaps : MonoBehaviour
    {
        public const string ClipResourcePath = "CardboardBox/BoxClose";

        [Tooltip("Merged four-flap closing clip. Auto-loaded from Resources/" + ClipResourcePath + " if left empty.")]
        [SerializeField] private AnimationClip closeClip;

        private static AnimationClip cachedClip;
        private static bool cachedClipMissingLogged;

        private float normalizedFold;

        /// <summary>0 = fully open, 1 = fully sealed.</summary>
        public float NormalizedFold => normalizedFold;

        public AnimationClip Clip
        {
            get
            {
                if (closeClip == null) closeClip = LoadSharedClip();
                return closeClip;
            }
        }

        private static AnimationClip LoadSharedClip()
        {
            if (cachedClip == null)
            {
                cachedClip = Resources.Load<AnimationClip>(ClipResourcePath);
                if (cachedClip == null && !cachedClipMissingLogged)
                {
                    cachedClipMissingLogged = true;
                    Debug.LogError(
                        $"📦 Kutu kapanma klibi bulunamadı (Resources/{ClipResourcePath}). " +
                        "Unity menüsünden 'MechaFind3D → Kutu → Box.fbx Kapanma Klibini Üret' komutunu bir kez çalıştır.");
                }
            }
            return cachedClip;
        }

        private void Awake()
        {
            if (closeClip == null) closeClip = LoadSharedClip();
        }

        /// <summary>Snaps the flaps to the open or sealed pose with no animation.</summary>
        public void SetClosedInstant(bool closed)
        {
            SetFold(closed ? 1f : 0f);
        }

        /// <summary>Samples the merged clip at <paramref name="t"/> (0 = open, 1 = sealed).</summary>
        public void SetFold(float t)
        {
            normalizedFold = Mathf.Clamp01(t);

            AnimationClip clip = Clip;
            if (clip == null) return;

            clip.SampleAnimation(gameObject, normalizedFold * clip.length);
        }

        /// <summary>
        /// Returns a tween that folds the flaps shut (or back open) over <paramref name="duration"/>.
        /// The clip's own curves carry the easing, so the tween itself stays linear - adding a second
        /// ease on top would double up on the authored anticipation and read as a stutter.
        /// </summary>
        public Tween Fold(bool closing, float duration)
        {
            float to = closing ? 1f : 0f;

            // Targeted at this component rather than the transform: the box's transform is separately
            // tweened (and DOKill'd) for the jump-to-shelf move, and the fold must not be caught by that.
            return DOTween.To(() => normalizedFold, SetFold, to, duration)
                .SetEase(Ease.Linear)
                .SetTarget(this);
        }
    }
}
