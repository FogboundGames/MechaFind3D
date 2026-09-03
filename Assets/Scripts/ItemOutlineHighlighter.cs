using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Component that dynamically attaches a vibrant yellow/gold outline pass
    /// around a 3D item when selected or sitting in a dock slot.
    /// </summary>
    public class ItemOutlineHighlighter : MonoBehaviour
    {
        [Header("Outline Visual Settings")]
        [SerializeField] private Color outlineColor = new Color(1.0f, 0.88f, 0.0f, 1.0f); // Bright Gold Yellow
        [SerializeField] private float outlineWidth = 0.006f; // Bold, crisp outline
        [SerializeField] private float glowIntensity = 1.35f;

        private static Material sharedOutlineMaterial;
        private readonly List<GameObject> outlineOverlays = new List<GameObject>();
        private bool isOutlineActive = false;
        private Tween pulseTween;

        public bool IsOutlineActive => isOutlineActive;

        private void Awake()
        {
            EnsureMaterial();
        }

        private static void EnsureMaterial()
        {
            if (sharedOutlineMaterial != null) return;

            Shader outlineShader = Shader.Find("MechaFind3D/YellowOutline");
            if (outlineShader == null)
            {
                outlineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            }

            sharedOutlineMaterial = new Material(outlineShader)
            {
                name = "YellowOutline_Runtime_Mat"
            };

            Color gold = new Color(1.0f, 0.88f, 0.0f, 1.0f);
            if (sharedOutlineMaterial.HasProperty("_OutlineColor")) sharedOutlineMaterial.SetColor("_OutlineColor", gold);
            if (sharedOutlineMaterial.HasProperty("_OutlineWidth")) sharedOutlineMaterial.SetFloat("_OutlineWidth", 0.006f);
            if (sharedOutlineMaterial.HasProperty("_GlowIntensity")) sharedOutlineMaterial.SetFloat("_GlowIntensity", 1.35f);
            if (sharedOutlineMaterial.HasProperty("_BaseColor")) sharedOutlineMaterial.SetColor("_BaseColor", gold);
            if (sharedOutlineMaterial.HasProperty("_Color")) sharedOutlineMaterial.SetColor("_Color", gold);
        }

        /// <summary>
        /// Enables or disables the yellow outline around the object.
        /// </summary>
        public void SetOutlineActive(bool enable, Color? colorOverride = null)
        {
            if (enable == isOutlineActive && outlineOverlays.Count > 0) return;

            isOutlineActive = enable;
            ClearOutlineOverlays();

            if (!enable) return;

            EnsureMaterial();
            Color finalColor = colorOverride ?? outlineColor;

            // Find all renderers in this object (excluding child mecha models)
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;

                // Ignore child mecha model transforms
                if (r.name.Contains("Mecha") || r.name.Contains("meccha") || r.name.Contains("Chameleon")) continue;
                if (r.transform.parent != null && (r.transform.parent.name.Contains("Mecha") || r.transform.parent.name.Contains("meccha"))) continue;

                CreateOverlayForRenderer(r, finalColor);
            }

            // Start a subtle pulsing glow animation
            StartPulseAnimation();
        }

        private void CreateOverlayForRenderer(Renderer originalRenderer, Color color)
        {
            GameObject overlayObj = new GameObject($"Outline_Pass_{originalRenderer.name}");
            overlayObj.transform.SetParent(originalRenderer.transform, false);
            overlayObj.transform.localPosition = Vector3.zero;
            overlayObj.transform.localRotation = Quaternion.identity;
            overlayObj.transform.localScale = Vector3.one; // Keep 1:1 scale so only vertex extrusion along normals defines the thin border

            MeshFilter origMF = originalRenderer.GetComponent<MeshFilter>();
            if (origMF != null && origMF.sharedMesh != null)
            {
                MeshFilter newMF = overlayObj.AddComponent<MeshFilter>();
                newMF.sharedMesh = origMF.sharedMesh;

                MeshRenderer newMR = overlayObj.AddComponent<MeshRenderer>();
                newMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                newMR.receiveShadows = false;

                Material matInstance = new Material(sharedOutlineMaterial);
                if (matInstance.HasProperty("_OutlineColor")) matInstance.SetColor("_OutlineColor", color);
                if (matInstance.HasProperty("_OutlineWidth")) matInstance.SetFloat("_OutlineWidth", outlineWidth);
                if (matInstance.HasProperty("_GlowIntensity")) matInstance.SetFloat("_GlowIntensity", glowIntensity);
                if (matInstance.HasProperty("_BaseColor")) matInstance.SetColor("_BaseColor", color);
                if (matInstance.HasProperty("_Color")) matInstance.SetColor("_Color", color);

                newMR.sharedMaterial = matInstance;
                outlineOverlays.Add(overlayObj);
            }
        }

        private void StartPulseAnimation()
        {
            pulseTween?.Kill();
            if (outlineOverlays.Count == 0) return;

            // Subtle micro-pulse so outline stays very thin and crisp
            Sequence seq = DOTween.Sequence();
            foreach (GameObject obj in outlineOverlays)
            {
                if (obj != null)
                {
                    seq.Join(obj.transform.DOScale(Vector3.one * 1.004f, 0.75f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
                }
            }
            pulseTween = seq;
        }

        public void FlashMatchBlast(System.Action onComplete)
        {
            if (pulseTween != null) pulseTween.Kill();

            Sequence seq = DOTween.Sequence();
            foreach (GameObject obj in outlineOverlays)
            {
                if (obj != null)
                {
                    seq.Join(obj.transform.DOScale(Vector3.one * 1.25f, 0.25f).SetEase(Ease.OutBack));
                    Renderer r = obj.GetComponent<Renderer>();
                    if (r != null && r.sharedMaterial != null)
                    {
                        seq.Join(r.sharedMaterial.DOColor(Color.white, "_OutlineColor", 0.2f));
                    }
                }
            }
            seq.OnComplete(() =>
            {
                ClearOutlineOverlays();
                onComplete?.Invoke();
            });
        }

        private void ClearOutlineOverlays()
        {
            pulseTween?.Kill();
            foreach (GameObject obj in outlineOverlays)
            {
                if (obj != null)
                {
                    if (Application.isPlaying) Destroy(obj);
                    else DestroyImmediate(obj);
                }
            }
            outlineOverlays.Clear();
        }

        private void OnDestroy()
        {
            ClearOutlineOverlays();
        }
    }
}
