using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using MechaFind3D.PhysicsInteraction;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace MechaFind3D
{
    /// <summary>
    /// Handles the 2-step Mecha interaction with the new meccha chameleon@Running (1).fbx animation:
    /// 1st Tap: Unparents from host, plays smooth DOTween "Appear" sequence, starts Running animation, and wanders in tray area.
    /// 2nd Tap: Plays DOTween spin-shrink vanish exit animation, completes goal, and disappears.
    /// </summary>
    public class MechaRunnerBehavior : MonoBehaviour
    {
        public enum MechaState
        {
            CamouflagedOnHost,
            RunningInArea,
            Vanishing
        }

        [Header("State")]
        public MechaState currentState = MechaState.CamouflagedOnHost;

        [Header("Running Bounds & Speed")]
        public float moveSpeed = 1.8f;
        public Vector2 boundsX = new Vector2(-1.3f, 1.3f);
        public Vector2 boundsZ = new Vector2(-1.3f, 1.3f);

        [Header("Animation Controller")]
        public RuntimeAnimatorController runnerController;

        private Tween moveTween;
        private Tween rotateTween;
        private Animator animator;

        private static readonly List<MechaRunnerBehavior> activeRunners = new List<MechaRunnerBehavior>();

        public static bool IsAnyMechaRunning()
        {
            for (int i = activeRunners.Count - 1; i >= 0; i--)
            {
                if (activeRunners[i] == null)
                {
                    activeRunners.RemoveAt(i);
                    continue;
                }
                if (activeRunners[i].currentState == MechaState.RunningInArea)
                {
                    return true;
                }
            }
            return false;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null) animator = gameObject.AddComponent<Animator>();
        }

        /// <summary>
        /// 1st Tap: Plays DOTween Appear sequence (revealing in scene), then starts Running animation & wandering loop.
        /// </summary>
        public void StartRunningMode(GameObject host)
        {
            if (currentState != MechaState.CamouflagedOnHost) return;

            currentState = MechaState.RunningInArea;
            if (!activeRunners.Contains(this)) activeRunners.Add(this);

            // 1. Unparent from host object so host object is freed and can now be collected
            transform.SetParent(null, true);

            // 2. Disable physics forces so DOTween controls smooth grounded movement
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // Ensure colliders are enabled for 2nd tap detection
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                c.enabled = true;
                c.isTrigger = false;
            }

            // 3. Reveal solid opaque mecha material so it stands out clearly
            ChameleonCamouflage.ApplyRevealedMaterial(gameObject);

            if (PhysicsInteraction.VFXManager.Instance != null)
            {
                PhysicsInteraction.VFXManager.Instance.PlayMechaRevealVFX(transform.position);
            }

            // 4. STEP 1: APPEAR ANIMATION (Sahnede Belirme)
            Vector3 startPos = host != null ? host.transform.position : transform.position;
            startPos.y = 0.05f;
            Vector3 origScale = transform.localScale;

            Sequence appearSeq = DOTween.Sequence();

            // Pop up slightly with scale jump and upright orientation
            appearSeq.Append(transform.DOMove(startPos + Vector3.up * 0.25f, 0.20f).SetEase(Ease.OutQuad));
            appearSeq.Join(transform.DORotate(Vector3.zero, 0.20f));
            appearSeq.Join(transform.DOScale(origScale * 1.25f, 0.15f));

            // Settle back to ground floor
            appearSeq.Append(transform.DOMoveY(0.05f, 0.18f).SetEase(Ease.InQuad));
            appearSeq.Join(transform.DOScale(origScale, 0.18f));

            // STEP 2: START RUNNING ANIMATION & WANDERING LOOP
            appearSeq.OnComplete(() =>
            {
                SetupAndPlayRunningAnimation();
                PickNextWaypoint();
            });
        }

        private void SetupAndPlayRunningAnimation()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null) animator = gameObject.AddComponent<Animator>();

            animator.enabled = true;
            animator.applyRootMotion = false; // Let DOTween drive transform position smoothly!

            // Ensure valid Humanoid Avatar is assigned for Mecanim animation retargeting
            if (animator.avatar == null || !animator.avatar.isValid)
            {
                Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
                foreach (var ca in childAnimators)
                {
                    if (ca != null && ca.avatar != null && ca.avatar.isValid)
                    {
                        animator.avatar = ca.avatar;
                        break;
                    }
                }
            }

            RuntimeAnimatorController controllerToUse = runnerController;

#if UNITY_EDITOR
            controllerToUse = GetOrCreateMechaAnimatorController();
#endif

            if (controllerToUse != null)
            {
                animator.runtimeAnimatorController = controllerToUse;
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }
        }

#if UNITY_EDITOR
        private static RuntimeAnimatorController cachedMechaRunnerController;

        public static RuntimeAnimatorController GetOrCreateMechaAnimatorController()
        {
            if (cachedMechaRunnerController != null) return cachedMechaRunnerController;

            string controllerPath = "Assets/Prefabs/MechaRunnerController.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            AnimationClip runClip = GetRunningAnimationClip();

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                if (runClip != null)
                {
                    AnimatorState state = controller.layers[0].stateMachine.AddState("Running");
                    state.motion = runClip;
                    controller.layers[0].stateMachine.defaultState = state;
                }
                AssetDatabase.SaveAssets();
            }
            else if (runClip != null && controller.layers.Length > 0)
            {
                var stateMachine = controller.layers[0].stateMachine;
                if (stateMachine.states.Length > 0 && stateMachine.states[0].state.motion != runClip)
                {
                    stateMachine.states[0].state.motion = runClip;
                    EditorUtility.SetDirty(controller);
                    AssetDatabase.SaveAssets();
                }
            }

            cachedMechaRunnerController = controller;
            return controller;
        }

        private static void EnsureClipLoops(AnimationClip clip)
        {
            if (clip == null) return;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!settings.loopTime)
            {
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
        }

        private static AnimationClip GetRunningAnimationClip()
        {
            // Priority 1: Check meccha chameleon@Running (1).fbx
            Object[] newAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/meccha chameleon@Running (1).fbx");
            if (newAssets != null)
            {
                foreach (Object o in newAssets)
                {
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        EnsureClipLoops(clip);
                        return clip;
                    }
                }
            }

            // Priority 2: Check any file matching meccha chameleon@Running
            string[] guids = AssetDatabase.FindAssets("Running t:AnimationClip");
            if (guids != null && guids.Length > 0)
            {
                foreach (string g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    if (path.Contains("meccha chameleon@Running"))
                    {
                        AnimationClip prioClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        EnsureClipLoops(prioClip);
                        return prioClip;
                    }
                }
                AnimationClip fallbackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
                EnsureClipLoops(fallbackClip);
                return fallbackClip;
            }

            return null;
        }
#endif

        private void PickNextWaypoint()
        {
            if (currentState != MechaState.RunningInArea) return;

            Vector3 targetPos = new Vector3(
                Random.Range(boundsX.x, boundsX.y),
                0.05f,
                Random.Range(boundsZ.x, boundsZ.y)
            );

            float dist = Vector3.Distance(transform.position, targetPos);
            float duration = Mathf.Max(0.6f, dist / moveSpeed);

            Vector3 moveDir = (targetPos - transform.position).normalized;
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                rotateTween = transform.DORotateQuaternion(targetRot, 0.20f);
            }

            moveTween = transform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    if (currentState == MechaState.RunningInArea)
                    {
                        DOVirtual.DelayedCall(0.10f, PickNextWaypoint);
                    }
                });
        }

        /// <summary>
        /// 2nd Tap: Plays DOTween squish-and-shrink vanish exit animation, completes goal, and destroys mecha.
        /// </summary>
        public void VanishAndDisappear()
        {
            if (currentState == MechaState.Vanishing) return;
            currentState = MechaState.Vanishing;
            activeRunners.Remove(this);

            if (moveTween != null && moveTween.IsActive()) moveTween.Kill();
            if (rotateTween != null && rotateTween.IsActive()) rotateTween.Kill();

            // Freeze the running pose so the squish reads clearly instead of fighting leg movement.
            if (animator != null) animator.enabled = false;

            Vector3 origScale = transform.localScale;
            Vector3 groundPos = transform.position;
            groundPos.y = 0.05f;

            Sequence vanishSeq = DOTween.Sequence();

            // 1. Tiny anticipation hop before the squash lands.
            vanishSeq.Append(transform.DOMoveY(groundPos.y + 0.12f, 0.08f).SetEase(Ease.OutQuad));
            vanishSeq.Join(transform.DOScale(new Vector3(origScale.x * 0.9f, origScale.y * 1.15f, origScale.z * 0.9f), 0.08f));

            // 2. Squash flat into the ground: wide and short, cartoon-style.
            vanishSeq.Append(transform.DOMoveY(groundPos.y, 0.10f).SetEase(Ease.InQuad));
            vanishSeq.Join(transform.DOScale(new Vector3(origScale.x * 1.5f, origScale.y * 0.12f, origScale.z * 1.5f), 0.10f).SetEase(Ease.OutQuad));

            // 3. Hold the squashed pose briefly so it reads, then suck it away to nothing.
            vanishSeq.AppendInterval(0.05f);
            vanishSeq.Append(transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack));

            vanishSeq.OnComplete(() =>
            {
                if (MatchGoalManager.Instance != null)
                {
                    MatchGoalManager.Instance.NotifyMechaCaught();
                }
                if (CanvasUIDesignManager.Instance != null)
                {
                    CanvasUIDesignManager.Instance.OnMechaVanished();
                }
                Destroy(gameObject);
            });
        }

        private void OnDestroy()
        {
            activeRunners.Remove(this);
            if (moveTween != null && moveTween.IsActive()) moveTween.Kill();
            if (rotateTween != null && rotateTween.IsActive()) rotateTween.Kill();
        }
    }
}
