using UnityEngine;
using DG.Tweening;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Singleton Manager for procedural gameplay Particle Effects & Visual Polish (VFX).
    /// Handles Match-3 confetti bursts, touch ripples, and Mecha reveal sparkles.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Spawns a vibrant Gold Star & Confetti particle explosion at the match location.
        /// </summary>
        public void PlayMatchBlastVFX(Vector3 worldPos, Color? themeColor = null)
        {
            GameObject pObj = new GameObject("VFX_Match_Blast");
            pObj.transform.position = worldPos;

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psRend = pObj.GetComponent<ParticleSystemRenderer>();

            // Configure Main Module
            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 6.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.gravityModifier = 0.8f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            // Colors: mix of bright yellow/gold + item theme color
            Color mainColor = themeColor ?? new Color(1.0f, 0.84f, 0.0f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.90f, 0.2f), mainColor);

            // Configure Emission & Shape
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // Size Over Lifetime
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.2f);
            sizeCurve.AddKey(0.3f, 1.0f);
            sizeCurve.AddKey(1.0f, 0.0f);
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // Material setup
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Particles/Standard Unlit");
            Material particleMat = new Material(shader);
            if (particleMat.HasProperty("_BaseColor")) particleMat.SetColor("_BaseColor", mainColor);
            if (particleMat.HasProperty("_Color")) particleMat.SetColor("_Color", mainColor);
            psRend.sharedMaterial = particleMat;

            ps.Play();

            // Spawn floating "+100" score text animation
            SpawnFloatingMatchText(worldPos);
        }

        /// <summary>
        /// Spawns a floating animated text indicator when a match completes.
        /// </summary>
        private void SpawnFloatingMatchText(Vector3 worldPos)
        {
            GameObject txtObj = new GameObject("VFX_Floating_Text");
            txtObj.transform.position = worldPos + Vector3.up * 0.2f;

            TextMesh textMesh = txtObj.AddComponent<TextMesh>();
            textMesh.text = "EŞLEŞTİ!";
            textMesh.fontSize = 28;
            textMesh.characterSize = 0.08f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = new Color(1.0f, 0.88f, 0.1f, 1.0f);
            textMesh.fontStyle = FontStyle.Bold;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                txtObj.transform.rotation = Quaternion.LookRotation(txtObj.transform.position - mainCam.transform.position);
            }

            Sequence seq = DOTween.Sequence();
            seq.Append(txtObj.transform.DOMoveY(worldPos.y + 0.8f, 0.65f).SetEase(Ease.OutCubic));
            seq.Join(txtObj.transform.DOPunchScale(Vector3.one * 0.35f, 0.3f, 6, 0.8f));
            seq.OnComplete(() => Destroy(txtObj));
        }

        /// <summary>
        /// Spawns a expanding touch ripple circle at finger tap position.
        /// </summary>
        public void PlayTouchRippleVFX(Vector3 worldPos)
        {
            GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ringObj.name = "VFX_Touch_Ripple";
            ringObj.transform.position = worldPos + Vector3.up * 0.04f;
            ringObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ringObj.transform.localScale = Vector3.one * 0.1f;

            Collider col = ringObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = ringObj.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                Material mat = new Material(shader);
                Color goldColor = new Color(1.0f, 0.85f, 0.1f, 0.7f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", goldColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", goldColor);
                rend.sharedMaterial = mat;
            }

            Sequence seq = DOTween.Sequence();
            seq.Append(ringObj.transform.DOScale(Vector3.one * 0.7f, 0.25f).SetEase(Ease.OutQuad));
            seq.OnComplete(() => Destroy(ringObj));
        }

        /// <summary>
        /// Spawns glitch/sparkle hologram burst when Mecha breaks camouflage.
        /// </summary>
        public void PlayMechaRevealVFX(Vector3 worldPos)
        {
            GameObject pObj = new GameObject("VFX_Mecha_Reveal");
            pObj.transform.position = worldPos;

            ParticleSystem ps = pObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psRend = pObj.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.0f, 8.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.20f);
            main.startColor = new Color(0.2f, 0.9f, 1.0f, 1.0f); // Bright Cyan / Holo Blue
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material particleMat = new Material(shader);
            Color cyan = new Color(0.2f, 0.95f, 1.0f, 1.0f);
            if (particleMat.HasProperty("_BaseColor")) particleMat.SetColor("_BaseColor", cyan);
            if (particleMat.HasProperty("_Color")) particleMat.SetColor("_Color", cyan);
            psRend.sharedMaterial = particleMat;

            ps.Play();
        }
    }
}
