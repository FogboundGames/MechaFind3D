using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Component that renders an object pitch black with a clean, backgroundless bold text counter directly on the object.
    /// As long as lockCounter > 0, the object cannot be selected/collected into dock slots.
    /// Every time any other regular item is placed into a dock slot, lockCounter decreases.
    /// When lockCounter reaches 0, the item unlocks, its original materials are restored,
    /// the text disappears, and it becomes fully interactable.
    /// </summary>
    public class BlackLockItem : MonoBehaviour
    {
        private static readonly List<BlackLockItem> activeLockedItems = new List<BlackLockItem>();

        [Header("Lock Status")]
        [SerializeField] private int lockCounter = 3;
        [SerializeField] private bool isLocked = true;

        private FindTargetObject targetObject;
        private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        private static Material sharedPitchBlackMaterial;

        private GameObject badgeCanvasObject;
        private Text badgeText;
        private Canvas badgeCanvas;

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
                if (originalMaterials.Count == 0) SaveOriginalMaterials();
                ApplyPitchBlackMaterial();
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
            lockCounter = initialCount;
            isLocked = true;

            SaveOriginalMaterials();
            ApplyPitchBlackMaterial();
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

        private static Material GetOrCreatePitchBlackMaterial()
        {
            if (sharedPitchBlackMaterial != null) return sharedPitchBlackMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Unlit/Color");

            sharedPitchBlackMaterial = new Material(shader)
            {
                name = "PitchBlack_Lock_Material"
            };

            Color pitchBlack = Color.black;
            if (sharedPitchBlackMaterial.HasProperty("_BaseColor")) sharedPitchBlackMaterial.SetColor("_BaseColor", pitchBlack);
            if (sharedPitchBlackMaterial.HasProperty("_Color")) sharedPitchBlackMaterial.SetColor("_Color", pitchBlack);
            if (sharedPitchBlackMaterial.HasProperty("_Smoothness")) sharedPitchBlackMaterial.SetFloat("_Smoothness", 0.35f);
            if (sharedPitchBlackMaterial.HasProperty("_Metallic")) sharedPitchBlackMaterial.SetFloat("_Metallic", 0.1f);
            if (sharedPitchBlackMaterial.HasProperty("_Glossiness")) sharedPitchBlackMaterial.SetFloat("_Glossiness", 0.35f);

            return sharedPitchBlackMaterial;
        }

        private void ApplyPitchBlackMaterial()
        {
            Material blackMat = GetOrCreatePitchBlackMaterial();
            foreach (var kvp in originalMaterials)
            {
                Renderer rend = kvp.Key;
                if (rend == null || IsMechaRenderer(rend)) continue;

                int matCount = kvp.Value.Length;
                Material[] blackMats = new Material[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    blackMats[i] = blackMat;
                }
                rend.sharedMaterials = blackMats;
            }
        }

        private void RestoreOriginalMaterials()
        {
            foreach (var kvp in originalMaterials)
            {
                Renderer rend = kvp.Key;
                if (rend != null && !IsMechaRenderer(rend))
                {
                    rend.sharedMaterials = kvp.Value;
                }
            }
        }

        private Vector3 GetObjectCenterAndRadius(out float maxExtent)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            bool hasBounds = false;
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || IsMechaRenderer(r)) continue;
                if (badgeCanvasObject != null && r.transform.IsChildOf(badgeCanvasObject.transform)) continue;

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            maxExtent = hasBounds ? Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) : 0.25f;
            return hasBounds ? bounds.center : transform.position;
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
            badgeCanvasObject.transform.position = GetObjectCenterAndRadius(out _);
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
                        Vector3 centerPos = GetObjectCenterAndRadius(out float maxExtent);
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

            if (badgeText != null)
            {
                badgeText.text = lockCounter.ToString();
            }

            if (badgeCanvasObject != null)
            {
                badgeCanvasObject.transform.DOKill();
                badgeCanvasObject.transform.localScale = Vector3.one * 0.012f;
                badgeCanvasObject.transform.DOPunchScale(Vector3.one * 0.005f, 0.35f, 5, 0.5f);
            }

            if (lockCounter <= 0)
            {
                UnlockItem();
            }
        }

        public void UnlockItem()
        {
            isLocked = false;
            activeLockedItems.Remove(this);

            RestoreOriginalMaterials();

            if (badgeCanvasObject != null)
            {
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
    }
}
