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

            HideAll();
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
            AnimateIn(winPanel, starsEarned, true);
        }

        public void ShowLose()
        {
            if (winPanel != null) winPanel.SetActive(false);
            AnimateIn(losePanel, 0, false);
        }

        public void HideAll()
        {
            AnimateOut(winPanel);
            AnimateOut(losePanel);
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

            if (!isWin)
            {
                seq.Append(popupRect.DOShakePosition(0.40f, new Vector3(16f, 0f, 0f), 12, 90f).SetUpdate(true));
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
