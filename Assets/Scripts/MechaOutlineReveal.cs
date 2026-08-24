using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;

namespace MechaFind3D.PhysicsInteraction
{
    public class MechaOutlineReveal : MonoBehaviour
    {
        private readonly List<GameObject> overlays = new List<GameObject>();
        private Tween pulseTween;
        private static Material outlineMat;

        public void ShowOutline(Color color)
        {
            ClearOverlays();
            EnsureMaterial();

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;

                MeshFilter mf = r.GetComponent<MeshFilter>();
                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;

                Mesh mesh = null;
                if (mf != null && mf.sharedMesh != null)
                    mesh = mf.sharedMesh;
                else if (smr != null && smr.sharedMesh != null)
                    mesh = smr.sharedMesh;

                if (mesh == null) continue;

                GameObject overlay = new GameObject($"MechaOutline_{r.name}");
                overlay.transform.SetParent(r.transform, false);
                overlay.transform.localPosition = Vector3.zero;
                overlay.transform.localRotation = Quaternion.identity;
                overlay.transform.localScale = Vector3.one;

                Material matInst = new Material(outlineMat);
                if (matInst.HasProperty("_OutlineColor")) matInst.SetColor("_OutlineColor", color);
                if (matInst.HasProperty("_BaseColor")) matInst.SetColor("_BaseColor", color);
                if (matInst.HasProperty("_Color")) matInst.SetColor("_Color", color);

                if (smr != null)
                {
                    SkinnedMeshRenderer newSmr = overlay.AddComponent<SkinnedMeshRenderer>();
                    newSmr.sharedMesh = smr.sharedMesh;
                    newSmr.bones = smr.bones;
                    newSmr.rootBone = smr.rootBone;
                    newSmr.shadowCastingMode = ShadowCastingMode.Off;
                    newSmr.receiveShadows = false;
                    newSmr.sharedMaterial = matInst;
                }
                else
                {
                    MeshFilter newMf = overlay.AddComponent<MeshFilter>();
                    newMf.sharedMesh = mesh;

                    MeshRenderer newMr = overlay.AddComponent<MeshRenderer>();
                    newMr.shadowCastingMode = ShadowCastingMode.Off;
                    newMr.receiveShadows = false;
                    newMr.sharedMaterial = matInst;
                }

                overlays.Add(overlay);
            }

            StartPulse();
        }

        public void HideOutline()
        {
            pulseTween?.Kill();

            Sequence fadeSeq = DOTween.Sequence();
            foreach (GameObject obj in overlays)
            {
                if (obj == null) continue;
                Renderer r = obj.GetComponent<Renderer>();
                if (r == null) r = obj.GetComponent<SkinnedMeshRenderer>();
                if (r != null && r.material != null)
                {
                    Color c = r.material.HasProperty("_OutlineColor")
                        ? r.material.GetColor("_OutlineColor")
                        : r.material.color;
                    Color fadeTarget = new Color(c.r, c.g, c.b, 0f);

                    if (r.material.HasProperty("_OutlineColor"))
                        fadeSeq.Join(r.material.DOColor(fadeTarget, "_OutlineColor", 0.5f));
                    if (r.material.HasProperty("_BaseColor"))
                        fadeSeq.Join(r.material.DOColor(fadeTarget, "_BaseColor", 0.5f));
                    if (r.material.HasProperty("_Color"))
                        fadeSeq.Join(r.material.DOColor(fadeTarget, "_Color", 0.5f));
                }
            }

            fadeSeq.OnComplete(ClearOverlays);
        }

        private void StartPulse()
        {
            pulseTween?.Kill();
            Sequence seq = DOTween.Sequence();
            foreach (GameObject obj in overlays)
            {
                if (obj != null)
                    seq.Join(obj.transform.DOScale(Vector3.one * 1.006f, 0.6f)
                        .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
            }
            pulseTween = seq;
        }

        private void ClearOverlays()
        {
            pulseTween?.Kill();
            foreach (GameObject obj in overlays)
            {
                if (obj != null) Destroy(obj);
            }
            overlays.Clear();
        }

        private static void EnsureMaterial()
        {
            if (outlineMat != null) return;

            Shader shader = Shader.Find("MechaFind3D/MechaRevealOutline");
            if (shader == null)
                shader = Shader.Find("MechaFind3D/YellowOutline");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            outlineMat = new Material(shader) { name = "MechaRevealOutline_Mat" };
            if (outlineMat.HasProperty("_OutlineWidth"))
                outlineMat.SetFloat("_OutlineWidth", 0.008f);
            if (outlineMat.HasProperty("_GlowIntensity"))
                outlineMat.SetFloat("_GlowIntensity", 1.5f);
        }

        private void OnDestroy()
        {
            ClearOverlays();
        }
    }
}
