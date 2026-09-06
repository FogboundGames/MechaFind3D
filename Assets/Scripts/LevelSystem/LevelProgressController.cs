using System;
using System.Collections.Generic;
using DG.Tweening;
using MechaFind3D.PhysicsInteraction;
using UnityEngine;
using UnityEngine.UI;

namespace MechaFind3D.LevelSystem
{
    /// <summary>
    /// Win Panel seviye ilerleme (Level Progression) animasyonu ve layout yöneticisi.
    /// Dikey hiyerarşide (Viewport -> LevelContent) 3'lü seviye penceresini,
    /// state geçişlerini ve DOTween Sequence tabanlı 2.5s animasyon akışını yönetir.
    /// </summary>
    public class LevelProgressController : MonoBehaviour
    {
        public static LevelProgressController Instance { get; private set; }

        [Header("Layout Ayarları")]
        [Tooltip("Seviye ögeleri arasındaki sabit dikey mesafe (px).")]
        [SerializeField] private float levelSpacing = 220f;
        [Tooltip("Viewport alanında aynı anda görünecek seviye sayısı.")]
        [SerializeField] private int visibleLevelCount = 3;
        [Header("UI Yapısı")]
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform contentRect;
        [SerializeField] private RectTransform popupPanelRect;

        [Header("Prefab & Görsel Hazırlık")]
        [SerializeField] private GameObject levelItemPrefab;
        [SerializeField] private Sprite starSprite;
        [SerializeField] private Sprite lockSprite;
        [SerializeField] private Sprite checkSprite;

        [Header("Path Çizgisi")]
        [SerializeField] private float pathLineWidth = 8f;
        [SerializeField] private Color pathTrackColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color pathFillColor = new Color(1f, 0.85f, 0.15f, 1f);

        private List<LevelItem> activeItems = new List<LevelItem>();
        private List<Image> activePathFills = new List<Image>();
        private Sequence currentSequence;

        private void Awake()
        {
            Instance = this;
            LoadSpritesIfMissing();
            EnsureLayoutStructure();
        }

        private void LoadSpritesIfMissing()
        {
#if UNITY_EDITOR
            if (starSprite == null)
            {
                starSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Star.png");
            }
            if (lockSprite == null)
            {
                lockSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Lock.png");
            }
            if (checkSprite == null)
            {
                checkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Check.png");
            }
#endif
        }

        /// <summary>
        /// Viewport ve LevelContent yapısını ve dikey hizalamayı garantiye alır.
        /// Title'ı ekranın üstüne, ActionButton'ı altına sabitler.
        /// </summary>
        public void EnsureLayoutStructure()
        {
            if (popupPanelRect == null)
            {
                Transform p = transform.Find("PopupPanel") ?? transform.Find("Popup") ?? transform.Find("Panel") ?? transform;
                popupPanelRect = p as RectTransform;
            }

            if (popupPanelRect != null)
            {
                // Title'ı üst nota (Top-Center) sabitle (X:0, Y:-140)
                Transform titleT = popupPanelRect.Find("Title") ?? popupPanelRect.Find("TitleText");
                if (titleT != null)
                {
                    RectTransform titleRT = titleT as RectTransform;
                    if (titleRT != null)
                    {
                        titleRT.anchorMin = new Vector2(0.5f, 1.0f);
                        titleRT.anchorMax = new Vector2(0.5f, 1.0f);
                        titleRT.pivot = new Vector2(0.5f, 1.0f);
                        titleRT.anchoredPosition = new Vector2(0f, -140f);
                        titleRT.sizeDelta = new Vector2(800f, 120f);
                    }
                }

                // ActionButton'ı alt nota (Bottom-Center) sabitle (X:0, Y:140)
                Transform actionBtnT = popupPanelRect.Find("ActionButton") ?? popupPanelRect.Find("NextButton");
                if (actionBtnT != null)
                {
                    RectTransform btnRT = actionBtnT as RectTransform;
                    if (btnRT != null)
                    {
                        btnRT.anchorMin = new Vector2(0.5f, 0.0f);
                        btnRT.anchorMax = new Vector2(0.5f, 0.0f);
                        btnRT.pivot = new Vector2(0.5f, 0.0f);
                        btnRT.anchoredPosition = new Vector2(0f, 140f);
                        btnRT.sizeDelta = new Vector2(600f, 160f);
                    }
                }
            }

            if (viewportRect == null && popupPanelRect != null)
            {
                Transform vp = popupPanelRect.Find("Viewport");
                if (vp != null) viewportRect = vp as RectTransform;
                else
                {
                    // Otomatik Viewport oluştur
                    GameObject vpGO = new GameObject("Viewport", typeof(RectTransform));
                    vpGO.transform.SetParent(popupPanelRect, false);
                    viewportRect = vpGO.GetComponent<RectTransform>();
                }
            }

            if (viewportRect != null)
            {
                viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
                viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
                viewportRect.pivot = new Vector2(0.5f, 0.5f);
                viewportRect.sizeDelta = new Vector2(700f, levelSpacing * visibleLevelCount);
                viewportRect.anchoredPosition = Vector2.zero;

                var mask2D = viewportRect.GetComponent<RectMask2D>();
                if (mask2D == null) viewportRect.gameObject.AddComponent<RectMask2D>();

                var maskComp = viewportRect.GetComponent<Mask>();
                if (maskComp == null)
                {
                    var img = viewportRect.GetComponent<Image>();
                    if (img == null) img = viewportRect.gameObject.AddComponent<Image>();
                    img.color = new Color(0f, 0f, 0f, 0.01f);
                    maskComp = viewportRect.gameObject.AddComponent<Mask>();
                    maskComp.showMaskGraphic = false;
                }

                EnsureEdgeFade(true);
                EnsureEdgeFade(false);
            }

            if (contentRect == null && viewportRect != null)
            {
                Transform cnt = viewportRect.Find("LevelContent");
                if (cnt != null) contentRect = cnt as RectTransform;
                else
                {
                    GameObject cntGO = new GameObject("LevelContent", typeof(RectTransform));
                    cntGO.transform.SetParent(viewportRect, false);
                    contentRect = cntGO.GetComponent<RectTransform>();
                }
            }

            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0.5f, 0.5f);
                contentRect.anchorMax = new Vector2(0.5f, 0.5f);
                contentRect.pivot = new Vector2(0.5f, 0.5f);
                contentRect.sizeDelta = new Vector2(700f, 1500f);
            }
        }

        /// <summary>
        /// Seviye listesini ve durumlarını sıfırlayıp hazırlar.
        /// </summary>
        public void SetupLevelList(int currentLevelIndex, int totalLevels = 10)
        {
            EnsureLayoutStructure();

            if (contentRect == null) return;

            // Eski ögeleri temizle
            foreach (Transform child in contentRect)
            {
                Destroy(child.gameObject);
            }
            activeItems.Clear();
            activePathFills.Clear();

            // Görüntülenecek seviye sayısı
            int count = Mathf.Max(visibleLevelCount + 2, totalLevels);
            int currentLvlNumber = currentLevelIndex + 1;

            for (int i = 0; i < count; i++)
            {
                int lvlNum = i + 1;
                GameObject itemGO = CreateLevelItemObject(lvlNum);
                itemGO.transform.SetParent(contentRect, false);

                RectTransform itemRT = itemGO.GetComponent<RectTransform>();
                // Tüm yıldızlar ve seviye metinleri kesin olarak aynı X (0) hizasında dikey dizilir!
                itemRT.anchoredPosition = new Vector2(0f, (i * levelSpacing));

                LevelItem levelItem = itemGO.GetComponent<LevelItem>();
                if (levelItem == null) levelItem = itemGO.AddComponent<LevelItem>();

                LevelState state;
                if (lvlNum < currentLvlNumber) state = LevelState.Completed;
                else if (lvlNum == currentLvlNumber) state = LevelState.Unlocked; // Bitirilmeden hemen önce Unlocked
                else state = LevelState.Locked;

                levelItem.SetupLevel(lvlNum, state);
                activeItems.Add(levelItem);
            }

            BuildPathLines(currentLvlNumber);

            // İçerik başlangıç pozisyonu: Şu anki kazanılacak seviye ortalansın
            float startY = -((currentLvlNumber - 1) * levelSpacing);
            contentRect.anchoredPosition = new Vector2(0f, startY);
        }

        /// <summary>
        /// Her ardışık seviye çifti arasına kalıcı bir path çizgisi kurar: geçilmiş (tamamlanmış)
        /// bölüm baştan altın renkte dolu, henüz geçilmemiş bölüm soluk/boş görünür.
        /// PlayProgressAnimation, tam olarak bu çizgilerden birinin fillAmount'unu 0'dan 1'e
        /// animasyonla doldurarak "path" ilerlemesini gösterir (dot spawn yerine).
        /// </summary>
        private void BuildPathLines(int currentLvlNumber)
        {
            for (int i = 0; i < activeItems.Count - 1; i++)
            {
                LevelItem a = activeItems[i];
                LevelItem b = activeItems[i + 1];

                GameObject trackGO = new GameObject($"PathTrack_{i}", typeof(RectTransform), typeof(Image));
                trackGO.transform.SetParent(contentRect, false);
                trackGO.transform.SetAsFirstSibling();
                RectTransform trackRT = trackGO.GetComponent<RectTransform>();
                ConfigurePathLineRect(trackRT, a, b);
                Image trackImg = trackGO.GetComponent<Image>();
                trackImg.color = pathTrackColor;
                trackImg.raycastTarget = false;

                GameObject fillGO = new GameObject($"PathFill_{i}", typeof(RectTransform), typeof(Image));
                fillGO.transform.SetParent(contentRect, false);
                fillGO.transform.SetSiblingIndex(trackGO.transform.GetSiblingIndex() + 1);
                RectTransform fillRT = fillGO.GetComponent<RectTransform>();
                ConfigurePathLineRect(fillRT, a, b);
                Image fillImg = fillGO.GetComponent<Image>();
                fillImg.color = pathFillColor;
                fillImg.raycastTarget = false;
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Vertical;
                fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;

                // Aşağıdaki (numarası küçük) seviye zaten Completed ise bu bacak geçilmiş demektir.
                bool alreadyTraversed = a.LevelNumber < currentLvlNumber;
                fillImg.fillAmount = alreadyTraversed ? 1f : 0f;

                activePathFills.Add(fillImg);
            }
        }

        private void ConfigurePathLineRect(RectTransform rt, LevelItem from, LevelItem to)
        {
            // Parent referans noktası itemRT/star ile aynı (contentRect merkezi) olmalı ki
            // anchoredPosition.y doğrudan yıldızlarla aynı koordinat sisteminde karşılaştırılabilsin.
            // Sadece pivot alt kenara alınır ki dikdörtgen anchoredPosition'dan yukarı doğru büyüsün.
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);

            float bottomY = (from.LevelNumber - 1) * levelSpacing;
            float topY = (to.LevelNumber - 1) * levelSpacing;
            float length = topY - bottomY;

            rt.sizeDelta = new Vector2(pathLineWidth, length);
            rt.anchoredPosition = new Vector2(-160f, bottomY);
        }

        /// <summary>
        /// Seviye bitirme ekranı açıldığında 2.5s'lik tam animasyon sekansını başlatır.
        /// </summary>
        public void PlayProgressionSequence(int currentLevelIndex, Action onComplete = null)
        {
            SetupLevelList(currentLevelIndex);

            int currentIdxInList = currentLevelIndex;
            if (currentIdxInList < 0 || currentIdxInList >= activeItems.Count)
            {
                onComplete?.Invoke();
                return;
            }

            LevelItem currentItem = activeItems[currentIdxInList];
            LevelItem nextItem = (currentIdxInList + 1 < activeItems.Count) ? activeItems[currentIdxInList + 1] : null;

            PlayProgressAnimation(currentItem, nextItem, onComplete);
        }

        /// <summary>
        /// İki LevelItem arasındaki tam progression animasyon sekansını (2.5s) çalıştırır.
        /// </summary>
        public void PlayProgressAnimation(LevelItem current, LevelItem next, Action onComplete = null)
        {
            if (currentSequence != null) currentSequence.Kill();

            currentSequence = DOTween.Sequence().SetUpdate(true);

            // 0.00s - Panel açılış (scale/fade) WinLosePanelController.AnimateIn tarafından zaten
            // yönetiliyor (aynı WinPanel CanvasGroup/RectTransform'u burada da tween'lemek çakışmaya
            // yol açardı) — bu yüzden sekans doğrudan 0.30s'deki star pulse ile başlar.

            // ---------------------------------------------------------------------------------------------
            // 0.30s - Completed Star Pulse (LevelN star: Scale 0.8 -> 1.15 -> 0.95 -> 1.0)
            // ---------------------------------------------------------------------------------------------
            if (current != null)
            {
                currentSequence.InsertCallback(0.30f, () =>
                {
                    current.SetState(LevelState.Completed, false);
                    current.transform.localScale = Vector3.one * 0.8f;
                });

                currentSequence.Insert(0.30f, current.transform.DOScale(1.15f, 0.12f).SetEase(Ease.OutBack));
                currentSequence.Insert(0.42f, current.transform.DOScale(0.95f, 0.08f).SetEase(Ease.InQuad));
                currentSequence.Insert(0.50f, current.transform.DOScale(1.00f, 0.10f).SetEase(Ease.OutCubic));

                // 0.45s - Glow + Particle Burst
                currentSequence.InsertCallback(0.45f, () =>
                {
                    SpawnStarBurstParticles(current.transform.position);
                    HapticHelper.Vibrate();
                });
            }

            // ---------------------------------------------------------------------------------------------
            // 0.80s - Path Animasyonu (LevelN -> Level(N+1) arası kalıcı çizginin fillAmount'u 0->1 dolar)
            // ---------------------------------------------------------------------------------------------
            if (current != null && next != null)
            {
                float pathDelay = 0.80f;
                int gapIndex = current.LevelNumber - 1;
                if (gapIndex >= 0 && gapIndex < activePathFills.Count)
                {
                    Image fillImg = activePathFills[gapIndex];
                    fillImg.fillAmount = 0f;
                    currentSequence.Insert(pathDelay, fillImg.DOFillAmount(1f, 0.5f).SetEase(Ease.OutQuad));
                }

                // ---------------------------------------------------------------------------------------------
                // 1.30s - Path hedefe ulaşır (Next Level star Locked -> Unlocked geçişi + pulse)
                // ---------------------------------------------------------------------------------------------
                float nextUnlockTime = 1.30f;
                currentSequence.InsertCallback(nextUnlockTime, () =>
                {
                    next.SetState(LevelState.Unlocked, false);
                    next.transform.DOKill();
                    next.transform.localScale = Vector3.one * 0.85f;
                    next.transform.DOScale(1.10f, 0.15f).SetEase(Ease.OutBack).SetUpdate(true)
                        .OnComplete(() =>
                        {
                            next.transform.DOScale(0.95f, 0.10f).SetEase(Ease.OutQuad).SetUpdate(true);
                        });
                });
            }

            // ---------------------------------------------------------------------------------------------
            // 2.00s - Content Kayması (LevelContent.DOAnchorPosY(targetY, 0.5f))
            // ---------------------------------------------------------------------------------------------
            if (next != null && contentRect != null)
            {
                float scrollTime = 2.00f;
                float targetY = -((next.LevelNumber - 1) * levelSpacing);

                currentSequence.Insert(scrollTime, contentRect.DOAnchorPosY(targetY, 0.5f).SetEase(Ease.InOutCubic));

                // Aynı anda eski completed star'ı hafifçe küçült (Scale 1.0 -> 0.85) geride kaldı hissi için
                if (current != null)
                {
                    currentSequence.Insert(scrollTime, current.transform.DOScale(0.85f, 0.50f).SetEase(Ease.InOutQuad));
                }
            }

            // ---------------------------------------------------------------------------------------------
            // 2.50s - Bitiş & OnComplete
            // ---------------------------------------------------------------------------------------------
            currentSequence.InsertCallback(2.50f, () =>
            {
                onComplete?.Invoke();
            });

            currentSequence.Play();
        }

        /// <summary>
        /// Kod ile oluşturulan RectTransform'lar için anchor/pivot'u merkeze sabitler.
        /// Unity'nin AddComponent ile eklenen RectTransform varsayılanı (0,0) anchor kullanır;
        /// bu, anchoredPosition'ın parent'ın merkezine göre değil sol-alt köşesine göre
        /// hesaplanmasına yol açıp yıldız/yazı hizasını bozuyordu.
        /// </summary>
        private static void SetCenteredAnchors(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private GameObject CreateLevelItemObject(int levelNum)
        {
            if (levelItemPrefab != null)
            {
                return Instantiate(levelItemPrefab);
            }

            // Kod ile temiz varsayılan LevelItem prefab yapısı oluştur
            GameObject itemGO = new GameObject($"LevelItem_{levelNum}", typeof(RectTransform));
            RectTransform itemRT = itemGO.GetComponent<RectTransform>();
            SetCenteredAnchors(itemRT);
            itemRT.sizeDelta = new Vector2(500f, 140f);

            const float starColumnX = -160f;

            // 0. Highlight Ring - Star'ın kardeşi olarak ÖNCE eklenir ki Star'ın arkasında kalsın
            GameObject ringGO = new GameObject("HighlightRing", typeof(RectTransform), typeof(Image));
            ringGO.transform.SetParent(itemGO.transform, false);
            RectTransform ringRT = ringGO.GetComponent<RectTransform>();
            SetCenteredAnchors(ringRT);
            ringRT.sizeDelta = new Vector2(150f, 150f);
            ringRT.anchoredPosition = new Vector2(starColumnX, 0f);
            Image ringImg = ringGO.GetComponent<Image>();
            if (starSprite != null) ringImg.sprite = starSprite;
            ringImg.color = new Color(0.5f, 0.95f, 1f, 0.45f);
            ringImg.raycastTarget = false;
            ringGO.SetActive(false);

            // 1. Yıldız (Star)
            GameObject starGO = new GameObject("Star", typeof(RectTransform), typeof(Image));
            starGO.transform.SetParent(itemGO.transform, false);
            RectTransform starRT = starGO.GetComponent<RectTransform>();
            SetCenteredAnchors(starRT);
            starRT.sizeDelta = new Vector2(110f, 110f);
            starRT.anchoredPosition = new Vector2(starColumnX, 0f);

            Image starImg = starGO.GetComponent<Image>();
            if (starSprite != null) starImg.sprite = starSprite;

            // Star Glow (Completed)
            GameObject glowGO = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGO.transform.SetParent(starGO.transform, false);
            RectTransform glowRT = glowGO.GetComponent<RectTransform>();
            SetCenteredAnchors(glowRT);
            glowRT.sizeDelta = new Vector2(148f, 148f);
            glowRT.anchoredPosition = Vector2.zero;
            Image glowImg = glowGO.GetComponent<Image>();
            if (starSprite != null) glowImg.sprite = starSprite;
            glowImg.color = new Color(1f, 0.95f, 0.3f, 0.6f);
            glowGO.SetActive(false);

            // Lock Icon (Locked) - Star'ın üzerinde overlay
            GameObject lockGO = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
            lockGO.transform.SetParent(starGO.transform, false);
            RectTransform lockRT = lockGO.GetComponent<RectTransform>();
            SetCenteredAnchors(lockRT);
            lockRT.sizeDelta = new Vector2(48f, 48f);
            lockRT.anchoredPosition = Vector2.zero;
            Image lockImg = lockGO.GetComponent<Image>();
            if (lockSprite != null) lockImg.sprite = lockSprite;
            lockImg.color = new Color(1f, 1f, 1f, 0.85f);
            lockGO.SetActive(false);

            // Check Badge (Completed) - Star'ın sağ üst köşesinde rozet
            GameObject checkGO = new GameObject("CheckBadge", typeof(RectTransform), typeof(Image));
            checkGO.transform.SetParent(starGO.transform, false);
            RectTransform checkRT = checkGO.GetComponent<RectTransform>();
            SetCenteredAnchors(checkRT);
            checkRT.sizeDelta = new Vector2(42f, 42f);
            checkRT.anchoredPosition = new Vector2(38f, 38f);
            Image checkImg = checkGO.GetComponent<Image>();
            if (checkSprite != null) checkImg.sprite = checkSprite;
            checkImg.color = new Color(0.25f, 0.95f, 0.45f, 1f);
            checkGO.SetActive(false);

            // 2. Level Text ("LEVEL N")
            GameObject textGO = new GameObject("LevelText", typeof(RectTransform));
            textGO.transform.SetParent(itemGO.transform, false);
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            SetCenteredAnchors(textRT);
            textRT.sizeDelta = new Vector2(260f, 90f);
            textRT.anchoredPosition = new Vector2(60f, 0f);

            Text uiTxt = textGO.AddComponent<Text>();
            uiTxt.text = $"LEVEL {levelNum}";
            uiTxt.fontSize = 40;
            uiTxt.alignment = TextAnchor.MiddleLeft;
            uiTxt.color = Color.white;
            uiTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 40);

            Outline textOutline = textGO.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 3. PathPoint (Anchor) - yıldızla aynı sütunda, kalıcı path çizgisi bu noktalar arasında uzanır
            GameObject pathGO = new GameObject("PathPoint", typeof(RectTransform));
            pathGO.transform.SetParent(itemGO.transform, false);
            RectTransform pathRT = pathGO.GetComponent<RectTransform>();
            SetCenteredAnchors(pathRT);
            pathRT.anchoredPosition = new Vector2(starColumnX, 0f);

            LevelItem item = itemGO.AddComponent<LevelItem>();
            item.AutoAssignReferencesIfMissing();

            return itemGO;
        }

        private void SpawnStarBurstParticles(Vector3 worldPos)
        {
            if (contentRect == null) return;
            Vector2 localPos = contentRect.InverseTransformPoint(worldPos);

            for (int i = 0; i < 6; i++)
            {
                GameObject spark = new GameObject($"Spark_{i}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                spark.transform.SetParent(contentRect, false);
                RectTransform rt = spark.GetComponent<RectTransform>();
                rt.anchoredPosition = localPos;
                rt.sizeDelta = new Vector2(14f, 14f);

                Image img = spark.GetComponent<Image>();
                if (starSprite != null) img.sprite = starSprite;
                img.color = new Color(1f, 0.95f, 0.3f, 1f);

                CanvasGroup cg = spark.GetComponent<CanvasGroup>();

                float angle = (i * 60f + UnityEngine.Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                float dist = UnityEngine.Random.Range(40f, 85f);
                Vector2 targetPos = localPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                rt.DOAnchorPos(targetPos, 0.45f).SetEase(Ease.OutQuad).SetUpdate(true);
                rt.DOScale(0f, 0.45f).SetEase(Ease.InQuad).SetUpdate(true);
                cg.DOFade(0f, 0.45f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() => Destroy(spark));
            }
        }

        /// <summary>
        /// Viewport'un üst/alt kenarında panel arka plan rengiyle eşleşen bir gradient fade
        /// oluşturur/günceller; yıldızlar RectMask2D kenarında sert kesilmek yerine yumuşakça belirir/kaybolur.
        /// </summary>
        private void EnsureEdgeFade(bool top)
        {
            if (popupPanelRect == null || viewportRect == null) return;

            string goName = top ? "TopFade" : "BottomFade";
            Transform existing = popupPanelRect.Find(goName);
            GameObject fadeGO = existing != null ? existing.gameObject : new GameObject(goName, typeof(RectTransform), typeof(Image));
            if (existing == null) fadeGO.transform.SetParent(popupPanelRect, false);
            fadeGO.transform.SetAsLastSibling();

            RectTransform fadeRT = fadeGO.GetComponent<RectTransform>();
            SetCenteredAnchors(fadeRT);

            float fadeHeight = 70f;
            float viewportHeight = viewportRect.sizeDelta.y;
            fadeRT.sizeDelta = new Vector2(viewportRect.sizeDelta.x, fadeHeight);
            float edgeY = viewportHeight * 0.5f - fadeHeight * 0.5f;
            fadeRT.anchoredPosition = new Vector2(0f, top ? edgeY : -edgeY);
            fadeRT.localScale = new Vector3(1f, top ? 1f : -1f, 1f);

            Image fadeImg = fadeGO.GetComponent<Image>();
            fadeImg.sprite = GetFadeGradientSprite();
            fadeImg.type = Image.Type.Simple;
            fadeImg.raycastTarget = false;
            fadeImg.color = GetPanelBackgroundColor();
        }

        private Sprite cachedFadeSprite;

        private Sprite GetFadeGradientSprite()
        {
            if (cachedFadeSprite != null) return cachedFadeSprite;

            const int h = 64;
            Texture2D tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float alpha = (float)y / (h - 1); // alt: saydam, üst: opak
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
            }
            tex.Apply();

            cachedFadeSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, h), new Vector2(0.5f, 0.5f));
            return cachedFadeSprite;
        }

        private Color GetPanelBackgroundColor()
        {
            if (popupPanelRect != null)
            {
                Image bg = popupPanelRect.GetComponent<Image>();
                if (bg != null) return new Color(bg.color.r, bg.color.g, bg.color.b, 1f);
            }
            return new Color(0.141f, 0.204f, 0.302f, 1f);
        }
    }
}
