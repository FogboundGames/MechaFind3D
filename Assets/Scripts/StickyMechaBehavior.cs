using System.Collections.Generic;
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

            ResetBoneRotations();

            MechaPosePresetSO chosenPreset = FindRandomPresetForHost(target);

            float scaleRatio, opacity, worldSize, wrapAmount;
            Vector3 posOffset, rotOffset;
            MechaPivotSelection pivot;
            System.Collections.Generic.List<MechaBoneOverride> boneOvr;

            if (chosenPreset != null)
            {
                scaleRatio = chosenPreset.mechaScaleRatio;
                opacity = chosenPreset.mechaOpacity;
                worldSize = chosenPreset.mechaWorldSize;
                wrapAmount = chosenPreset.mechaWrapAmount;
                posOffset = chosenPreset.mechaLocalOffset;
                rotOffset = chosenPreset.mechaRotationOffset;
                pivot = MechaPivotSelection.Auto;
                boneOvr = chosenPreset.boneOverrides;
            }
            else
            {
                scaleRatio = spawnEntry != null ? spawnEntry.mechaScaleRatio : 0.25f;
                opacity = spawnEntry != null ? spawnEntry.mechaOpacity : 0.22f;
                worldSize = spawnEntry != null ? spawnEntry.mechaWorldSize : 0.5f;
                wrapAmount = spawnEntry != null ? spawnEntry.mechaWrapAmount : 0f;
                posOffset = spawnEntry != null ? spawnEntry.mechaLocalOffset : Vector3.zero;
                rotOffset = spawnEntry != null ? spawnEntry.mechaRotationOffset : Vector3.zero;
                pivot = spawnEntry != null ? spawnEntry.targetPivot : MechaPivotSelection.Auto;
                boneOvr = spawnEntry != null ? spawnEntry.boneOverrides : null;
            }

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

        private Dictionary<string, Quaternion> bindPoseMap;

        public void CaptureBindPose()
        {
            if (bindPoseMap != null) return;
            bindPoseMap = new Dictionary<string, Quaternion>();
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                if (bone == transform) continue;
                bindPoseMap[bone.name] = bone.localRotation;
            }
        }

        private void ResetBoneRotations()
        {
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
            }

            if (bindPoseMap == null) return;
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                if (bone == transform) continue;
                if (bindPoseMap.TryGetValue(bone.name, out Quaternion bindRot))
                    bone.localRotation = bindRot;
            }
        }

        private static MechaPosePresetSO[] cachedPresets;

        private MechaPosePresetSO FindRandomPresetForHost(FindTargetObject target)
        {
            if (cachedPresets == null)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MechaPosePresetSO");
                cachedPresets = new MechaPosePresetSO[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                    cachedPresets[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<MechaPosePresetSO>(path);
                }
#else
                cachedPresets = Resources.LoadAll<MechaPosePresetSO>("");
#endif
            }

            if (cachedPresets == null || cachedPresets.Length == 0) return null;

            string hostName = target.gameObject.name.ToLowerInvariant()
                .Replace("(clone)", "").Trim();
            string hostColorName = target.colorName != null
                ? target.colorName.ToLowerInvariant() : "";

            System.Collections.Generic.List<MechaPosePresetSO> matches =
                new System.Collections.Generic.List<MechaPosePresetSO>();

            foreach (var preset in cachedPresets)
            {
                if (preset == null || preset.targetHostItem == null) continue;

                string presetItemName = preset.targetHostItem.name.ToLowerInvariant();
                string presetItemId = preset.targetHostItem.GetEffectiveItemId().ToLowerInvariant();

                if (hostName.Contains(presetItemName) || hostName.Contains(presetItemId)
                    || presetItemName.Contains(hostName)
                    || (!string.IsNullOrEmpty(hostColorName) && (hostColorName.Contains(presetItemId) || presetItemId.Contains(hostColorName))))
                {
                    matches.Add(preset);
                }
            }

            if (matches.Count == 0) return null;
            return matches[Random.Range(0, matches.Count)];
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
