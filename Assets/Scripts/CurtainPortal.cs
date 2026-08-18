using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// The strip curtain at the end of the conveyor: shipped boxes ride through it and push the strips aside.
    ///
    /// The push is the model's own authored <c>CurtainPush</c> clip, sampled by hand rather than played
    /// through an AnimatorController - the same approach <see cref="PackagingBoxFlaps"/> uses for the box,
    /// and for the same reason: sampling lets the curtain be driven from whatever is passing through it and
    /// settle back to rest afterwards, with no state machine to keep in step.
    /// </summary>
    public class CurtainPortal : MonoBehaviour
    {
        public const string ClipResourcePath = "Curtain/CurtainPush";

        [Tooltip("Strip-swing clip. Auto-loaded from Resources/" + ClipResourcePath + " if left empty.")]
        [SerializeField] private AnimationClip pushClip;

        [Tooltip("How long one full swing-and-settle takes, in seconds.")]
        [Min(0.05f)]
        [SerializeField] private float pushDuration = 0.9f;

        private static AnimationClip cachedClip;
        private static bool cachedClipMissingLogged;

        private float pushTime = -1f;

        public AnimationClip Clip
        {
            get
            {
                if (pushClip == null) pushClip = LoadSharedClip();
                return pushClip;
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
                        $"🚪 Perde klibi bulunamadı (Resources/{ClipResourcePath}). " +
                        "Unity menüsünden 'MechaFind3D → Konveyör → Perde Portalını Üret' komutunu bir kez çalıştır.");
                }
            }
            return cachedClip;
        }

        private void Awake()
        {
            if (pushClip == null) pushClip = LoadSharedClip();
            SampleAt(0f);
        }

        /// <summary>Starts a swing. Called again mid-swing, it restarts - so a second box right behind the first keeps the strips moving.</summary>
        public void Push()
        {
            pushTime = 0f;
        }

        private void Update()
        {
            if (pushTime < 0f) return;

            AnimationClip clip = Clip;
            if (clip == null)
            {
                pushTime = -1f;
                return;
            }

            pushTime += Time.deltaTime;

            float t = Mathf.Clamp01(pushTime / Mathf.Max(0.05f, pushDuration));
            SampleAt(t * clip.length);

            if (t >= 1f)
            {
                pushTime = -1f;
                SampleAt(0f);   // park at rest so the strips never end up frozen mid-swing
            }
        }

        private void SampleAt(float time)
        {
            AnimationClip clip = Clip;
            if (clip == null) return;
            clip.SampleAnimation(gameObject, time);
        }
    }
}
