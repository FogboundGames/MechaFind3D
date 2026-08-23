using UnityEngine;
using DG.Tweening;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Yapışkan mecha: belirli sürede bulunamazsa host'tan ayrılır, koşarak pile'daki
    /// başka bir objeye gidip yapışır. Max N atlama, sonra yerinde kalır.
    /// </summary>
    public class StickyMechaBehavior : MonoBehaviour
    {
        [Header("Config")]
        public float jumpInterval = 15f;
        public int maxJumps = 2;

        [Header("Koşma")]
        public float runSpeed = 2.5f;
        public float groundY = 0.05f;

        [Header("State")]
        public int jumpCount = 0;
        public bool isActive = true;
        public bool isRunning = false;

        private float timer;
        private MechaSpawnEntry spawnEntry;
        private MechaRunnerBehavior runner;
        private Tween moveTween;
        private Tween rotateTween;
        private FindTargetObject pendingHost;

        public void Initialize(MechaSpawnEntry entry)
        {
            spawnEntry = entry;
            jumpInterval = entry.stickyJumpInterval;
            maxJumps = entry.stickyMaxJumps;
            timer = jumpInterval;
            jumpCount = 0;
            isActive = true;
            isRunning = false;
        }

        private void Start()
        {
            runner = GetComponent<MechaRunnerBehavior>();
        }

        private void Update()
        {
            if (!isActive || isRunning) return;
            if (jumpCount >= maxJumps) return;
            if (runner != null && runner.currentState != MechaRunnerBehavior.MechaState.CamouflagedOnHost) return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                StartJump();
                timer = jumpInterval;
            }
        }

        private void StartJump()
        {
            Transform currentHost = transform.parent;

            FindTargetObject[] pileItems = FindObjectsByType<FindTargetObject>(FindObjectsSortMode.None);
            if (pileItems == null || pileItems.Length == 0) return;

            FindTargetObject newHostTarget = null;
            int attempts = 0;
            while (attempts < 20)
            {
                FindTargetObject candidate = pileItems[Random.Range(0, pileItems.Length)];
                if (candidate == null || candidate.isDocked) { attempts++; continue; }
                if (currentHost != null && candidate.transform == currentHost) { attempts++; continue; }
                if (candidate.GetComponentInChildren<MechaRunnerBehavior>() != null) { attempts++; continue; }
                newHostTarget = candidate;
                break;
            }

            if (newHostTarget == null) return;

            pendingHost = newHostTarget;
            isRunning = true;

            // 1. Detach from current host
            Vector3 worldPos = transform.position;
            Quaternion worldRot = transform.rotation;
            Vector3 worldScale = transform.lossyScale;
            transform.SetParent(null, true);
            transform.position = worldPos;
            transform.rotation = worldRot;
            transform.localScale = worldScale;

            // 2. Reveal (opaque white material so it's visible while running)
            ChameleonCamouflage.ApplyRevealedMaterial(gameObject);

            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayMechaRevealVFX(worldPos);

            // 3. Disable physics so DOTween drives movement
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                c.enabled = true;
                c.isTrigger = false;
            }

            // 4. Pop-up appear animation, then run to target
            Vector3 startPos = worldPos;
            startPos.y = groundY;
            Vector3 origScale = transform.localScale;

            Sequence appearSeq = DOTween.Sequence();
            appearSeq.Append(transform.DOMove(startPos + Vector3.up * 0.25f, 0.20f).SetEase(Ease.OutQuad));
            appearSeq.Join(transform.DORotate(Vector3.zero, 0.20f));
            appearSeq.Join(transform.DOScale(origScale * 1.2f, 0.15f));
            appearSeq.Append(transform.DOMoveY(groundY, 0.15f).SetEase(Ease.InQuad));
            appearSeq.Join(transform.DOScale(origScale, 0.15f));

            appearSeq.OnComplete(() =>
            {
                if (!isActive || pendingHost == null)
                {
                    isRunning = false;
                    return;
                }
                RunToTarget(pendingHost);
            });
        }

        private void RunToTarget(FindTargetObject target)
        {
            if (target == null)
            {
                isRunning = false;
                return;
            }

            // Setup running animation
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
#if UNITY_EDITOR
                RuntimeAnimatorController ctrl = MechaRunnerBehavior.GetOrCreateMechaAnimatorController();
                if (ctrl != null)
                {
                    animator.runtimeAnimatorController = ctrl;
                    animator.Play(0, 0, 0f);
                    animator.Update(0f);
                }
#endif
            }

            Vector3 targetPos = target.transform.position;
            targetPos.y = groundY;

            // Face toward target
            Vector3 dir = (targetPos - transform.position).normalized;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
                rotateTween = transform.DORotateQuaternion(lookRot, 0.2f);
            }

            float dist = Vector3.Distance(transform.position, targetPos);
            float duration = Mathf.Max(0.5f, dist / runSpeed);

            moveTween = transform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    if (animator != null) animator.enabled = false;
                    StickToHost(target);
                });
        }

        private void StickToHost(FindTargetObject target)
        {
            if (target == null)
            {
                isRunning = false;
                return;
            }

            float scaleRatio = spawnEntry != null ? spawnEntry.mechaScaleRatio : 0.25f;
            float opacity = spawnEntry != null ? spawnEntry.mechaOpacity : 0.22f;
            float worldSize = spawnEntry != null ? spawnEntry.mechaWorldSize : 0.5f;
            float wrapAmount = spawnEntry != null ? spawnEntry.mechaWrapAmount : 0f;
            Vector3 posOffset = spawnEntry != null ? spawnEntry.mechaLocalOffset : Vector3.zero;
            Vector3 rotOffset = spawnEntry != null ? spawnEntry.mechaRotationOffset : Vector3.zero;
            MechaPivotSelection pivot = spawnEntry != null ? spawnEntry.targetPivot : MechaPivotSelection.Auto;
            var boneOvr = spawnEntry != null ? spawnEntry.boneOverrides : null;

            ChameleonCamouflage.EmbedMechaInHostObject(
                gameObject, target.gameObject,
                scaleRatio, opacity, posOffset, rotOffset,
                worldSize, pivot, wrapAmount, boneOvr
            );

            Color hostColor = ChameleonCamouflage.GetHostDominantColor(target.gameObject);
            ChameleonCamouflage.ApplyGlassMaterial(gameObject, opacity, hostColor);

            // Reset runner state so 1st tap works again on new host
            if (runner != null)
                runner.currentState = MechaRunnerBehavior.MechaState.CamouflagedOnHost;

            // Brief shake on new host
            target.transform.DOShakePosition(0.3f, 0.05f, 10, 90f, false, true)
                .SetEase(Ease.OutQuad);

            jumpCount++;
            isRunning = false;
            pendingHost = null;
        }

        public void Stop()
        {
            isActive = false;
            isRunning = false;
            if (moveTween != null && moveTween.IsActive()) moveTween.Kill();
            if (rotateTween != null && rotateTween.IsActive()) rotateTween.Kill();
        }

        private void OnDestroy()
        {
            if (moveTween != null && moveTween.IsActive()) moveTween.Kill();
            if (rotateTween != null && rotateTween.IsActive()) rotateTween.Kill();
        }
    }
}
