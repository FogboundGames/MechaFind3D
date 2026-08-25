using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Shows/hides the Win and Lose popup panels built under UI_Canvas, and wires their
    /// buttons to the real game flow (LevelManager). Attached to UI_Canvas.
    /// </summary>
    public class WinLosePanelController : MonoBehaviour
    {
        public static WinLosePanelController Instance { get; private set; }

        [Header("Overlay Backdrop")]
        [SerializeField] private float backdropFadeDuration = 0.25f;
        [SerializeField] private float backdropAlpha = 0.6f;

        [Header("Popup Panel - Giriş Animasyonu")]
        [SerializeField] private float popupInDuration = 0.48f;
        [SerializeField] private float popupInStartScale = 0.72f;
        [SerializeField] private float popupInStartY = -180f;

        [Header("Popup Panel - Çıkış Animasyonu")]
        [SerializeField] private float popupOutDuration = 0.22f;
        [SerializeField] private float popupOutEndScale = 0.82f;

        [Header("Buton Animasyonları")]
        [SerializeField] private float buttonStaggerDelay = 0.08f;
        [SerializeField] private float buttonFadeInDuration = 0.28f;
        [SerializeField] private float buttonSlideInY = -30f;

        private GameObject winPanel;
        private GameObject losePanel;

        private void Awake()
        {
            Instance = this;

            Transform winT = transform.Find("WinPanel");
            Transform loseT = transform.Find("LosePanel");
            winPanel = winT != null ? winT.gameObject : null;
            losePanel = loseT != null ? loseT.gameObject : null;

            WireButton(winT, "PopupPanel/ActionButton", () =>
            {
                HideAll();
                if (LevelManager.Instance != null) LevelManager.Instance.LoadNextLevel();
            });
            WireButton(winT, "PopupPanel/HomeText", HideAll);

            WireButton(loseT, "PopupPanel/ActionButton", () =>
            {
                HideAll();
                if (LevelManager.Instance != null) LevelManager.Instance.RestartCurrentLevel();
            });
            WireButton(loseT, "PopupPanel/HomeText", HideAll);
        }

        private void Start()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
        }

        private static void WireButton(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            if (root == null) return;
            Transform t = root.Find(path);
            if (t == null) return;

            Button btn = t.GetComponent<Button>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(action);
        }

        public void ShowWin(int starsEarned = 3)
        {
            if (losePanel != null) losePanel.SetActive(false);
            HideTimerUI();
            AnimateIn(winPanel, starsEarned, true);
        }

        public void ShowLose()
        {
            if (winPanel != null) winPanel.SetActive(false);
            HideTimerUI();
            AnimateIn(losePanel, 0, false);
        }

        private void HideTimerUI()
        {
            Transform header = transform.Find("Header_Goal_Panel");
            if (header == null)
            {
                if (CanvasUIDesignManager.Instance == null) return;
                Transform canvasTr = CanvasUIDesignManager.Instance.transform.Find("MatchFactory_Canvas");
                if (canvasTr != null) header = canvasTr.Find("Header_Goal_Panel");
                if (header == null) return;
            }
            Transform timerBadge = header.Find("timer_badge");
            Transform timerText = header.Find("timer_text");
            if (timerBadge != null) timerBadge.gameObject.SetActive(false);
            if (timerText != null) timerText.gameObject.SetActive(false);
        }

        public void HideAll()
        {
            AnimateOut(winPanel);
            AnimateOut(losePanel);
            ShowTimerUI();
        }

        private void ShowTimerUI()
        {
            Transform header = transform.Find("Header_Goal_Panel");
            if (header == null)
            {
                if (CanvasUIDesignManager.Instance == null) return;
                Transform canvasTr = CanvasUIDesignManager.Instance.transform.Find("MatchFactory_Canvas");
                if (canvasTr != null) header = canvasTr.Find("Header_Goal_Panel");
                if (header == null) return;
            }
            Transform timerBadge = header.Find("timer_badge");
            Transform timerText = header.Find("timer_text");
            if (timerBadge != null) timerBadge.gameObject.SetActive(true);
            if (timerText != null) timerText.gameObject.SetActive(true);
        }

        private List<Transform> FindExistingStars(Transform popupT)
        {
            List<Transform> stars = new List<Transform>();
            if (popupT == null) return stars;

            foreach (Transform t in popupT.GetComponentsInChildren<Transform>(true))
            {
                if (t == popupT) continue;
                if (t.name.ToLowerInvariant().Contains("star"))
                {
                    if (t.childCount == 0 || t.GetComponent<Image>() != null)
                    {
                        if (!stars.Contains(t)) stars.Add(t);
                    }
                }
            }

            stars.Sort((a, b) => a.position.x.CompareTo(b.position.x));
            return stars;
        }

        private void AnimateIn(GameObject panel, int starsEarned, bool isWin)
        {
            if (panel == null) return;

            panel.SetActive(true);

            CanvasGroup panelCG = panel.GetComponent<CanvasGroup>();
            if (panelCG == null) panelCG = panel.AddComponent<CanvasGroup>();
            panelCG.DOKill();
            panelCG.alpha = 0f;
            panelCG.interactable = false;
            panelCG.blocksRaycasts = true;
            panelCG.DOFade(backdropAlpha, backdropFadeDuration)
                .SetUpdate(true)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    panelCG.alpha = 1f;
                    panelCG.interactable = true;
                });

            Transform popupT = panel.transform.Find("PopupPanel");
            if (popupT == null) return;

            RectTransform popupRect = popupT.GetComponent<RectTransform>();
            popupT.DOKill();
            popupRect.localScale = Vector3.one * popupInStartScale;
            popupRect.anchoredPosition = new Vector2(0f, popupInStartY);

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(popupRect.DOAnchorPosY(0f, popupInDuration).SetEase(Ease.OutBack, 1.1f));
            seq.Join(popupRect.DOScale(Vector3.one, popupInDuration).SetEase(Ease.OutBack, 1.3f));

            seq.Insert(popupInDuration, popupRect.DOPunchRotation(new Vector3(0f, 0f, 4f), 0.5f, 8, 0.5f).SetUpdate(true));

            // Animate pre-existing UI stars in-place without creating any new UI objects
            List<Transform> existingStars = FindExistingStars(popupT);
            if (existingStars != null && existingStars.Count > 0)
            {
                for (int i = 0; i < existingStars.Count; i++)
                {
                    Transform starT = existingStars[i];
                    if (starT == null) continue;

                    starT.DOKill();
                    Image starImg = starT.GetComponent<Image>();
                    bool isEarned = isWin && (i < starsEarned);

                    if (isEarned)
                    {
                        if (starImg != null)
                        {
                            starImg.enabled = true;
                            starImg.color = new Color(1.0f, 0.88f, 0.10f, 1.0f);
                        }
                        starT.localScale = Vector3.zero;

                        float starDelay = popupInDuration * 0.45f + i * 0.22f;
                        int starIndex = i;

                        seq.Insert(starDelay, starT.DOScale(Vector3.one, 0.32f).SetEase(Ease.OutBack, 2.2f));
                        seq.InsertCallback(starDelay + 0.32f, () =>
                        {
                            if (starT != null)
                            {
                                starT.DOPunchScale(Vector3.one * 0.35f, 0.25f, 6, 0.6f).SetUpdate(true);
                            }
                            if (VFXManager.Instance != null && starT != null)
                            {
                                VFXManager.Instance.PlayStarPopVFX(starT.position, starIndex);
                            }
                        });
                    }
                    else
                    {
                        if (starImg != null)
                        {
                            starImg.enabled = true;
                            starImg.color = new Color(0.30f, 0.30f, 0.35f, 0.30f);
                        }
                        starT.localScale = Vector3.zero;

                        float starDelay = popupInDuration * 0.45f + i * 0.15f;
                        seq.Insert(starDelay, starT.DOScale(Vector3.one * 0.75f, 0.22f).SetEase(Ease.OutQuad));
                    }
                }
            }

            if (isWin)
            {
                HapticHelper.Vibrate();
                SpawnWinCelebration(Camera.main);

                Transform titleT = popupT.Find("TitleText");
                if (titleT == null)
                {
                    foreach (Transform t in popupT.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.ToLowerInvariant().Contains("title") || t.name.ToLowerInvariant().Contains("header"))
                        {
                            titleT = t;
                            break;
                        }
                    }
                }
                if (titleT != null)
                {
                    titleT.localScale = Vector3.zero;
                    seq.Insert(popupInDuration * 0.3f, titleT.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack, 2.5f).SetUpdate(true));
                    seq.InsertCallback(popupInDuration * 0.3f + 0.35f, () =>
                    {
                        if (titleT != null) titleT.DOPunchScale(Vector3.one * 0.2f, 0.3f, 8, 0.5f).SetUpdate(true);
                    });
                }
            }
            else
            {
                HapticHelper.Vibrate();

                seq.Append(popupRect.DOShakePosition(0.55f, new Vector3(22f, 8f, 0f), 16, 90f).SetUpdate(true));

                CanvasGroup popupCG = popupT.GetComponent<CanvasGroup>();
                if (popupCG == null) popupCG = popupT.gameObject.AddComponent<CanvasGroup>();
                seq.InsertCallback(0f, () =>
                {
                    Image bgImg = panel.GetComponent<Image>();
                    if (bgImg != null)
                    {
                        Color origColor = bgImg.color;
                        Color redFlash = new Color(0.8f, 0.1f, 0.08f, 0.55f);
                        Sequence flashSeq = DOTween.Sequence().SetUpdate(true);
                        for (int f = 0; f < 3; f++)
                        {
                            flashSeq.Append(bgImg.DOColor(redFlash, 0.12f).SetEase(Ease.OutQuad));
                            flashSeq.Append(bgImg.DOColor(origColor, 0.18f).SetEase(Ease.InQuad));
                        }
                    }
                });
            }

            string[] buttonPaths = { "ActionButton", "HomeText" };
            for (int i = 0; i < buttonPaths.Length; i++)
            {
                Transform btnT = popupT.Find(buttonPaths[i]);
                if (btnT == null) continue;

                CanvasGroup btnCG = btnT.GetComponent<CanvasGroup>();
                if (btnCG == null) btnCG = btnT.gameObject.AddComponent<CanvasGroup>();
                RectTransform btnRect = btnT.GetComponent<RectTransform>();

                btnCG.alpha = 0f;
                Vector2 origPos = btnRect.anchoredPosition;
                btnRect.anchoredPosition = origPos + new Vector2(0f, buttonSlideInY);

                float delay = popupInDuration * 0.55f + i * buttonStaggerDelay;
                seq.Insert(delay, btnCG.DOFade(1f, buttonFadeInDuration).SetEase(Ease.OutQuad));
                seq.Insert(delay, btnRect.DOAnchorPos(origPos, buttonFadeInDuration).SetEase(Ease.OutQuad));
            }

            seq.Play();
        }

        private void SpawnWinCelebration(Camera cam)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            float[][] positions = {
                new[] { 0.15f, 0.75f },
                new[] { 0.85f, 0.70f },
                new[] { 0.50f, 0.90f },
                new[] { 0.25f, 0.40f },
                new[] { 0.75f, 0.45f },
                new[] { 0.50f, 0.55f },
            };

            for (int i = 0; i < positions.Length; i++)
            {
                float delay = i * 0.25f;
                int idx = i;
                float vx = positions[i][0];
                float vy = positions[i][1];
                Camera c = cam;
                DOVirtual.DelayedCall(delay, () => SpawnFireworkBurst(c, idx, vx, vy)).SetUpdate(true);
            }
        }

        private static ParticleSystem CreateParticleSystem(string name, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.position = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static void SpawnFireworkBurst(Camera cam, int index, float vx, float vy)
        {
            if (cam == null) return;
            vx += Random.Range(-0.05f, 0.05f);
            vy += Random.Range(-0.05f, 0.05f);
            Vector3 pos = cam.ViewportToWorldPoint(new Vector3(vx, vy, 4f));

            ParticleSystem ps = CreateParticleSystem($"WinVFX_Firework_{index}", pos);
            ParticleSystemRenderer psRend = ps.GetComponent<ParticleSystemRenderer>();

            Color[] colors = {
                new Color(1f, 0.2f, 0.3f), new Color(0.2f, 1f, 0.4f),
                new Color(0.2f, 0.5f, 1f), new Color(1f, 0.85f, 0.1f),
                new Color(1f, 0.4f, 0.85f)
            };
            Color c = colors[index % colors.Length];

            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.gravityModifier = 1.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startColor = new ParticleSystem.MinMaxGradient(c, Color.white);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 80) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(0.5f, 0.7f);
            sizeCurve.AddKey(1f, 0f);
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(Color.white, 0.7f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            psRend.sharedMaterial = mat;

            ps.Play();
        }

        private void AnimateOut(GameObject panel)
        {
            if (panel == null || !panel.activeSelf) return;

            Transform popupT = panel.transform.Find("PopupPanel");
            CanvasGroup panelCG = panel.GetComponent<CanvasGroup>();
            if (panelCG == null) panelCG = panel.AddComponent<CanvasGroup>();

            panelCG.interactable = false;

            Sequence seq = DOTween.Sequence().SetUpdate(true);

            if (popupT != null)
            {
                RectTransform popupRect = popupT.GetComponent<RectTransform>();
                seq.Append(popupRect.DOScale(Vector3.one * popupOutEndScale, popupOutDuration).SetEase(Ease.InBack));
                seq.Join(panelCG.DOFade(0f, popupOutDuration).SetEase(Ease.InQuad));
            }
            else
            {
                seq.Append(panelCG.DOFade(0f, popupOutDuration).SetEase(Ease.InQuad));
            }

            seq.OnComplete(() => panel.SetActive(false));
            seq.Play();
        }
    }
}
