using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Component that renders an object pitch black with a clean, backgroundless bold text counter directly on the object.
    /// As lockCounter decreases, the pitch-black tint progressively erodes/fades, revealing the object's own color and texture.
    /// Small black particles break away on each count reduction.
    /// When lockCounter reaches 0, the item unlocks, its original materials are fully restored,
    /// the text disappears, and it becomes fully interactable.
    /// </summary>
    public class BlackLockItem : MonoBehaviour
    {
        private static readonly List<BlackLockItem> activeLockedItems = new List<BlackLockItem>();

        [Header("Lock Status")]
        [SerializeField] private int lockCounter = 3;
        [SerializeField] private int maxLockCounter = 3;
        [SerializeField] private bool isLocked = true;

        private FindTargetObject targetObject;
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        private GameObject badgeCanvasObject;
        private Text badgeText;
        private Canvas badgeCanvas;

        private float currentErodeProgress = 0.0f; // 0.0 = Pitch Black, 1.0 = Full Original Color
        private Tween erodeTween;
        private MaterialPropertyBlock mpb;

        public bool IsLocked => isLocked && lockCounter > 0;
        public int LockCounter => lockCounter;

        private void Awake()
        {
            targetObject = GetComponent<FindTargetObject>();
        }

        private void Start()
        {
            if (isLocked && lockCounter > 0)
            {
                if (maxLockCounter <= 0) maxLockCounter = Mathf.Max(1, lockCounter);
                if (originalMaterials.Count == 0) SaveOriginalMaterials();
                ApplyBlacknessTint(currentErodeProgress);
                if (badgeCanvasObject == null) CreateWorldBadgeUI();
            }
        }

        private void OnEnable()
        {
            if (isLocked && lockCounter > 0)
            {
                if (!activeLockedItems.Contains(this))
                {
                    activeLockedItems.Add(this);
                }
            }
        }

        private void OnDisable()
        {
            activeLockedItems.Remove(this);
        }

        private void OnDestroy()
        {
            activeLockedItems.Remove(this);
            erodeTween?.Kill();
            ClearMaterialPropertyBlocks();
            if (badgeCanvasObject != null)
            {
                if (Application.isPlaying) Destroy(badgeCanvasObject);
                else DestroyImmediate(badgeCanvasObject);
            }
        }

        /// <summary>
        /// Global event trigger: called whenever any item is placed into the dock slot.
        /// Decrements lock counter on all active black locked objects in the scene.
        /// </summary>
        public static void NotifyItemDocked()
        {
            for (int i = activeLockedItems.Count - 1; i >= 0; i--)
            {
                if (activeLockedItems[i] != null)
                {
                    activeLockedItems[i].DecrementLockCounter();
                }
            }
        }

        /// <summary>
        /// Initializes the black lock mechanic on this item with a given initial countdown count.
        /// </summary>
        public void InitializeLock(int initialCount)
        {
            lockCounter = Mathf.Max(1, initialCount);
            maxLockCounter = lockCounter;
            isLocked = true;
            currentErodeProgress = 0.0f;

            SaveOriginalMaterials();
            ApplyBlacknessTint(currentErodeProgress);
            CreateWorldBadgeUI();

            if (!activeLockedItems.Contains(this))
            {
                activeLockedItems.Add(this);
            }
        }

        private static bool IsMechaRenderer(Renderer rend)
        {
            if (rend == null) return true;
            if (rend.GetComponentInParent<MechaRunnerBehavior>() != null) return true;

            Transform t = rend.transform;
            while (t != null)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("mecha") || n.Contains("meccha") || n.Contains("ragdoll") || n.Contains("chameleon")) return true;
                t = t.parent;
            }
            return false;
        }

        private void SaveOriginalMaterials()
        {
            originalMaterials.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;
                if (IsMechaRenderer(rend)) continue;
                if (badgeCanvasObject != null && rend.transform.IsChildOf(badgeCanvasObject.transform)) continue;

                originalMaterials[rend] = rend.sharedMaterials;
            }
        }

        private void ApplyBlacknessTint(float progress)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();

            Color tintColor = Color.Lerp(Color.black, Color.white, progress);

            foreach (var kvp in originalMaterials)
            {
                Renderer rend = kvp.Key;
                if (rend == null || IsMechaRenderer(rend)) continue;
                if (badgeCanvasObject != null && rend.transform.IsChildOf(badgeCanvasObject.transform)) continue;

                if (rend.sharedMaterials != kvp.Value)
                {
                    rend.sharedMaterials = kvp.Value;
                }

                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", tintColor);
                mpb.SetColor("_Color", tintColor);
                rend.SetPropertyBlock(mpb);
            }
        }

        private void ClearMaterialPropertyBlocks()
        {
            foreach (var kvp in originalMaterials)
            {
                Renderer rend = kvp.Key;
                if (rend != null && !IsMechaRenderer(rend))
                {
                    rend.sharedMaterials = kvp.Value;
                    rend.SetPropertyBlock(null);
                }
            }
        }

        private Vector3 GetObjectCenterAndRadius(out float maxExtent, out Bounds combinedBounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            bool hasBounds = false;
            combinedBounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || IsMechaRenderer(r)) continue;
                if (badgeCanvasObject != null && r.transform.IsChildOf(badgeCanvasObject.transform)) continue;

                if (!hasBounds)
                {
                    combinedBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(r.bounds);
                }
            }
            maxExtent = hasBounds ? Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y, combinedBounds.extents.z) : 0.25f;
            return hasBounds ? combinedBounds.center : transform.position;
        }

        private void CreateWorldBadgeUI()
        {
            Transform existingUI = transform.Find("BlackLock_Text_UI");
            if (existingUI != null)
            {
                if (Application.isPlaying) Destroy(existingUI.gameObject);
                else DestroyImmediate(existingUI.gameObject);
            }

            badgeCanvasObject = new GameObject("BlackLock_Text_UI");
            badgeCanvasObject.transform.SetParent(transform);
            badgeCanvasObject.transform.position = GetObjectCenterAndRadius(out _, out _);
            badgeCanvasObject.transform.localScale = Vector3.one * 0.012f; // Bold world space text scale

            badgeCanvas = badgeCanvasObject.AddComponent<Canvas>();
            badgeCanvas.renderMode = RenderMode.WorldSpace;
            badgeCanvas.overrideSorting = true;
            badgeCanvas.sortingOrder = 999; // Render on top of 3D objects

            CanvasScaler scaler = badgeCanvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;

            // Pure crisp white text component
            GameObject textObj = new GameObject("LockText");
            textObj.transform.SetParent(badgeCanvasObject.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(300, 300);
            textRect.anchoredPosition = Vector2.zero;

            badgeText = textObj.AddComponent<Text>();
            badgeText.text = lockCounter.ToString();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 80);

            badgeText.font = defaultFont;
            badgeText.fontSize = 120;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = Color.white; // Pure crisp white!
        }

        private void LateUpdate()
        {
            if (isLocked)
            {
                if (badgeCanvasObject == null)
                {
                    CreateWorldBadgeUI();
                }

                if (badgeCanvasObject != null)
                {
                    Camera cam = Camera.main;
                    if (cam == null) cam = FindFirstObjectByType<Camera>();
                    if (cam != null)
                    {
                        Vector3 centerPos = GetObjectCenterAndRadius(out float maxExtent, out _);
                        Vector3 camDir = (cam.transform.position - centerPos).normalized;

                        // Position text cleanly outside the front face of 3D mesh towards camera
                        float offsetDistance = Mathf.Max(maxExtent + 0.05f, 0.20f);
                        badgeCanvasObject.transform.position = centerPos + camDir * offsetDistance;
                        badgeCanvasObject.transform.rotation = cam.transform.rotation;
                    }
                }
            }
        }

        public void DecrementLockCounter()
        {
            if (!isLocked || lockCounter <= 0) return;

            lockCounter--;

            float targetProgress = 1.0f - Mathf.Clamp01((float)lockCounter / Mathf.Max(1, maxLockCounter));

            // Smoothly animate blackness tint reduction to reveal object's own colors
            erodeTween?.Kill();
            erodeTween = DOTween.To(() => currentErodeProgress, x => currentErodeProgress = x, targetProgress, 0.35f)
                .SetEase(Ease.OutCubic)
                .OnUpdate(() => ApplyBlacknessTint(currentErodeProgress));

            if (badgeText != null)
            {
                badgeText.text = lockCounter.ToString();
                badgeText.transform.DOKill();
                badgeText.transform.localScale = Vector3.one;
                badgeText.transform.DOPunchScale(Vector3.one * 0.45f, 0.35f, 6, 0.5f);
            }

            // Punch scale animation on item when layer of blackness breaks
            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.12f, 0.35f, 5, 0.5f);

            // Spawn small black particle dust breaking away from the object
            PlayBlackParticleDissolveVFX(false);

            if (lockCounter <= 0)
            {
                UnlockItem();
            }
        }

        public void UnlockItem()
        {
            isLocked = false;
            activeLockedItems.Remove(this);

            erodeTween?.Kill();
            currentErodeProgress = 1.0f;
            ClearMaterialPropertyBlocks();

            // Spawn a burst of small black particle flakes as the blackness completely dissolves
            PlayBlackParticleDissolveVFX(true);

            if (badgeCanvasObject != null)
            {
                badgeCanvasObject.transform.DOKill();
                badgeCanvasObject.transform.DOScale(Vector3.zero, 0.25f).OnComplete(() =>
                {
                    if (badgeCanvasObject != null) Destroy(badgeCanvasObject);
                });
            }

            // Punch scale animation on item to celebrate unlock!
            transform.DOKill();
            transform.DOPunchScale(transform.localScale * 0.25f, 0.45f, 6, 0.5f);

            // Play touch ripple or VFX if available
            if (VFXManager.Instance != null)
            {
                VFXManager.Instance.PlayTouchRippleVFX(transform.position);
            }
        }

        /// <summary>
        /// Plays a locked shake/wiggle animation when player taps a locked black object.
        /// </summary>
        public void PlayLockedWiggle()
        {
            if (badgeCanvasObject != null)
            {
                badgeCanvasObject.transform.DOKill();
                badgeCanvasObject.transform.localScale = Vector3.one * 0.012f;
                badgeCanvasObject.transform.DOShakeRotation(0.35f, new Vector3(0, 0, 25f), 15, 90f);
            }
            transform.DOKill();
            transform.DOShakeRotation(0.35f, new Vector3(0, 0, 15f), 15, 90f);
        }

        /// <summary>
        /// Spawns tiny black particle dust flakes breaking off the object surface and dissolving into thin air.
        /// </summary>
        private void PlayBlackParticleDissolveVFX(bool isFinalUnlock)
        {
            Vector3 centerPos = GetObjectCenterAndRadius(out _, out Bounds bounds);

            GameObject pObj = new GameObject("VFX_BlackLock_Dissolve");
            pObj.transform.position = centerPos;

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psRend = pObj.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f); // Tiny dark flakes
            main.gravityModifier = -0.15f; // Float gently upwards as they dissolve
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.05f, 0.05f, 0.05f, 0.9f),
                new Color(0.18f, 0.18f, 0.18f, 0.75f)
            );
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int burstCount = isFinalUnlock ? 65 : 35;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, burstCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            Vector3 boxScale = bounds.extents * 2f;
            if (boxScale.magnitude < 0.2f) boxScale = Vector3.one * 0.3f;
            shape.scale = boxScale;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 1.0f);
            sizeCurve.AddKey(0.6f, 0.7f);
            sizeCurve.AddKey(1.0f, 0.0f);
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.black, 0.0f), new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.85f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;

            // Sprites/Default is guaranteed to render safely in all render pipelines without quad bugs
            Shader spriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Material particleMat = new Material(spriteShader);
            psRend.sharedMaterial = particleMat;

            ps.Play();
        }
    }
}

