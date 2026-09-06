using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace MechaFind3D.LevelSystem
{
    public enum LevelState
    {
        Locked,      // Henüz sırası gelmemiş (%30-40 parlaklık / kilitli, Scale 0.9, Glow kapalı)
        Unlocked,    // Sırada, oynanabilir (%70-100 parlaklık, Scale 0.95, Glow kapalı)
        Completed    // Bitirilmiş (%100 parlaklık, Scale 1.0, Glow açık)
    }

    /// <summary>
    /// Level progression listesindeki her bir level elemanını temsil eder.
    /// Yıldız, yazı (Level N), glow efekti ve path bağlantı noktasını barındırır.
    /// </summary>
    public class LevelItem : MonoBehaviour
    {
        [Header("UI Referansları")]
        [SerializeField] private Image starImage;
        [SerializeField] private GameObject glowObject;
        [SerializeField] private Image glowImage;
        [SerializeField] private Component levelTextComponent; // Text veya TMP_Text
        [SerializeField] private RectTransform pathPoint;
        [SerializeField] private GameObject lockIconObject;
        [SerializeField] private GameObject completedBadgeObject;
        [SerializeField] private GameObject highlightRingObject;
        [SerializeField] private Image highlightRingImage;

        [Header("State Parametreleri")]
        [SerializeField] private Color lockedColor = new Color(0.35f, 0.38f, 0.45f, 0.55f);
        [SerializeField] private Color unlockedColor = new Color(0.95f, 0.95f, 1.0f, 0.95f);
        [SerializeField] private Color completedColor = new Color(1.0f, 0.92f, 0.15f, 1.0f);

        [SerializeField] private float lockedScale = 0.90f;
        [SerializeField] private float unlockedScale = 0.95f;
        [SerializeField] private float completedScale = 1.00f;

        public LevelState CurrentState { get; private set; } = LevelState.Locked;
        public int LevelNumber { get; private set; } = 1;

        public RectTransform PathPoint
        {
            get
            {
                if (pathPoint != null) return pathPoint;
                return transform as RectTransform;
            }
        }

        public RectTransform RectTransform => transform as RectTransform;

        private void Awake()
        {
            AutoAssignReferencesIfMissing();
        }

        public void AutoAssignReferencesIfMissing()
        {
            if (starImage == null)
            {
                Transform starT = transform.Find("Star");
                starImage = starT != null ? starT.GetComponent<Image>() : GetComponentInChildren<Image>(true);
            }

            if (glowObject == null && starImage != null)
            {
                Transform glowT = starImage.transform.Find("Glow");
                if (glowT != null) glowObject = glowT.gameObject;
            }

            if (glowImage == null && glowObject != null)
            {
                glowImage = glowObject.GetComponent<Image>();
            }

            if (levelTextComponent == null)
            {
                levelTextComponent = GetComponentInChildren<TMPro.TMP_Text>(true);
                if (levelTextComponent == null)
                {
                    levelTextComponent = GetComponentInChildren<Text>(true);
                }
            }

            if (pathPoint == null)
            {
                Transform pt = transform.Find("PathPoint");
                if (pt != null) pathPoint = pt.GetComponent<RectTransform>();
                else pathPoint = transform as RectTransform;
            }

            if (lockIconObject == null && starImage != null)
            {
                Transform t = starImage.transform.Find("LockIcon");
                if (t != null) lockIconObject = t.gameObject;
            }

            if (completedBadgeObject == null && starImage != null)
            {
                Transform t = starImage.transform.Find("CheckBadge");
                if (t != null) completedBadgeObject = t.gameObject;
            }

            if (highlightRingObject == null)
            {
                // HighlightRing, Star'ın gerisinde (arka planda) görünmesi için Star'ın kardeşi
                // olarak konur (Star'ın çocuğu olsaydı üstünde çizilirdi).
                Transform t = transform.Find("HighlightRing");
                if (t != null)
                {
                    highlightRingObject = t.gameObject;
                    highlightRingImage = t.GetComponent<Image>();
                }
            }
        }

        public void SetupLevel(int levelNum, LevelState initialState)
        {
            LevelNumber = levelNum;
            AutoAssignReferencesIfMissing();
            SetLevelText(levelTextComponent, $"LEVEL {levelNum}");
            SetState(initialState, true);
        }

        public void SetState(LevelState state, bool immediate = true)
        {
            CurrentState = state;
            AutoAssignReferencesIfMissing();

            Color targetColor = GetColorForState(state);
            float targetScale = GetScaleForState(state);
            bool glowActive = (state == LevelState.Completed);

            if (lockIconObject != null) lockIconObject.SetActive(state == LevelState.Locked);
            if (completedBadgeObject != null) completedBadgeObject.SetActive(state == LevelState.Completed);
            SetHighlightRing(state == LevelState.Unlocked);

            if (glowObject != null) glowObject.SetActive(glowActive);
            if (glowImage != null)
            {
                glowImage.DOKill();
                Color gCol = glowImage.color;
                gCol.a = glowActive ? 1f : 0f;
                glowImage.color = gCol;
            }

            if (starImage != null)
            {
                starImage.DOKill();
                if (immediate) starImage.color = targetColor;
                else starImage.DOColor(targetColor, 0.35f).SetUpdate(true);
            }

            if (levelTextComponent != null)
            {
                levelTextComponent.transform.DOKill();
                Color textCol = (state == LevelState.Completed) ? new Color(1f, 0.96f, 0.35f, 1f) :
                                (state == LevelState.Unlocked) ? Color.white : new Color(0.65f, 0.7f, 0.8f, 0.55f);
                SetTextColor(levelTextComponent, textCol);
            }

            transform.DOKill();
            if (immediate)
            {
                transform.localScale = Vector3.one * targetScale;
            }
            else
            {
                transform.DOScale(Vector3.one * targetScale, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        /// <summary>
        /// Unlocked (sıradaki oynanabilir) seviyeyi "buradasın" halkasıyla nabız gibi vurgular.
        /// </summary>
        private void SetHighlightRing(bool active)
        {
            if (highlightRingObject == null) return;

            highlightRingObject.SetActive(active);
            if (highlightRingImage != null) highlightRingImage.transform.DOKill();

            if (active && highlightRingImage != null)
            {
                highlightRingImage.transform.localScale = Vector3.one;
                Color c = highlightRingImage.color;
                c.a = 0.45f;
                highlightRingImage.color = c;

                highlightRingImage.transform.DOScale(1.28f, 0.75f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
                highlightRingImage.DOFade(0.10f, 0.75f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }
        }

        public Sequence AnimateToStateSequence(LevelState newState)
        {
            CurrentState = newState;
            Sequence seq = DOTween.Sequence().SetUpdate(true);

            if (lockIconObject != null) lockIconObject.SetActive(newState == LevelState.Locked);
            if (completedBadgeObject != null) completedBadgeObject.SetActive(newState == LevelState.Completed);
            SetHighlightRing(newState == LevelState.Unlocked);

            Color targetColor = GetColorForState(newState);
            float targetScale = GetScaleForState(newState);
            bool glowActive = (newState == LevelState.Completed);

            if (starImage != null)
            {
                seq.Join(starImage.DOColor(targetColor, 0.40f).SetEase(Ease.OutQuad));
            }

            if (glowObject != null)
            {
                glowObject.SetActive(true);
                if (glowImage != null)
                {
                    glowImage.color = new Color(glowImage.color.r, glowImage.color.g, glowImage.color.b, 0f);
                    seq.Join(glowImage.DOFade(glowActive ? 1f : 0f, 0.35f).SetEase(Ease.OutQuad));
                }
            }

            if (levelTextComponent != null)
            {
                Color textCol = (newState == LevelState.Completed) ? new Color(1f, 0.96f, 0.35f, 1f) :
                                (newState == LevelState.Unlocked) ? Color.white : new Color(0.65f, 0.7f, 0.8f, 0.55f);
                seq.Join(DOTween.To(() => GetTextColor(levelTextComponent), c => SetTextColor(levelTextComponent, c), textCol, 0.35f));
            }

            seq.Join(transform.DOScale(Vector3.one * targetScale, 0.38f).SetEase(Ease.OutBack, 2.2f));

            return seq;
        }

        private Color GetColorForState(LevelState state)
        {
            switch (state)
            {
                case LevelState.Completed: return completedColor;
                case LevelState.Unlocked: return unlockedColor;
                case LevelState.Locked:
                default: return lockedColor;
            }
        }

        private float GetScaleForState(LevelState state)
        {
            switch (state)
            {
                case LevelState.Completed: return completedScale;
                case LevelState.Unlocked: return unlockedScale;
                case LevelState.Locked:
                default: return lockedScale;
            }
        }

        private static void SetLevelText(Component comp, string text)
        {
            if (comp is Text uiTxt) uiTxt.text = text;
            else if (comp is TMPro.TMP_Text tmpTxt) tmpTxt.text = text;
        }

        private static void SetTextColor(Component comp, Color c)
        {
            if (comp is Text uiTxt) uiTxt.color = c;
            else if (comp is TMPro.TMP_Text tmpTxt) tmpTxt.color = c;
        }

        private static Color GetTextColor(Component comp)
        {
            if (comp is Text uiTxt) return uiTxt.color;
            if (comp is TMPro.TMP_Text tmpTxt) return tmpTxt.color;
            return Color.white;
        }
    }
}
