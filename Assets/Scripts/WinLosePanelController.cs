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

        [Header("Coin Ödülü")]
        [Tooltip("Bölüm kazanıldığında oyuncuya verilecek coin miktarı.")]
        [SerializeField] private int winCoinReward = 5;

        [Header("Coin & Konfeti Efekti")]
        [Tooltip("Konfeti olarak yağacak coin sprite'ı (boşsa AssetDatabase'den otomatik yüklenir).")]
        [SerializeField] private Sprite coinSprite;
        [Tooltip("Yıldız parıltı ve geçiş efektleri için yıldız sprite'ı.")]
        [SerializeField] private Sprite starSprite;
        [Tooltip("Ekrana yağacak coin sayısı.")]
        [SerializeField] private int confettiCoinCount = 45;
        [Tooltip("Kazanma panelinde seviye ilerlemesi aşağıdan yukarıya mı olsun?")]
        [SerializeField] private bool progressionBottomToTop = true;
        [Tooltip("Sonraki Seviye butonuna basıldığında doğrudan sonraki oyuna mı başlansın (false) yoksa Seviye Haritası mı açılsın (true)?")]
        [SerializeField] private bool openMapOnNextLevel = false;

        private GameObject winPanel;
        private GameObject losePanel;
        private RectTransform confettiContainer;

        private class WinLevelRow
        {
            public Transform starT;
            public Image starImg;
            public Component levelTextComp; // Text or TMP_Text
            public float posY;
            public int levelNumber;
        }

        private List<WinLevelRow> currentWinRows = new List<WinLevelRow>();
        private int winningRowIdx = -1;
        private int nextRowIdx = -1;
        private bool isLevelTransitioning = false;

        private void Awake()
        {
            Instance = this;

            Transform winT = transform.Find("WinPanel");
            Transform loseT = transform.Find("LosePanel");
            winPanel = winT != null ? winT.gameObject : null;
            losePanel = loseT != null ? loseT.gameObject : null;

            LoadCoinSpriteIfMissing();

            WireButton(winT, "PopupPanel/ActionButton", () =>
            {
                if (isLevelTransitioning) return;
                isLevelTransitioning = true;

                // Buton basılma yaylanma efekti
                Transform btnT = winT != null ? winT.Find("PopupPanel/ActionButton") : null;
                if (btnT != null)
                {
                    btnT.DOKill();
                    btnT.DOPunchScale(new Vector3(0.12f, -0.08f, 0f), 0.22f, 8, 0.5f).SetUpdate(true);
                }

                // Butona basılınca coşkulu coin & konfeti patlaması
                SpawnCoinConfettiShower(30);

                // Yıldızlar arası sihirli seviye geçiş animasyonunu oynat!
                PlayStarLevelTransitionAnimation(() =>
                {
                    HideAll();
                    isLevelTransitioning = false;

                    int currentIdx = (LevelManager.Instance != null) ? LevelManager.Instance.currentLevelIndex : 0;
                    int totalLevels = (LevelManager.Instance != null && LevelManager.Instance.levels != null && LevelManager.Instance.levels.Count > 0)
                        ? LevelManager.Instance.levels.Count
                        : 10;
                    int nextIdx = (currentIdx + 1) % Mathf.Max(1, totalLevels);

                    if (LevelManager.Instance != null)
                    {
                        LevelManager.Instance.currentLevelIndex = nextIdx;
                        PlayerPrefs.SetInt("SavedCurrentLevelIndex", nextIdx);
                        PlayerPrefs.Save();
                    }

                    if (openMapOnNextLevel && LevelMapManager.Instance != null)
                    {
                        LevelMapManager.Instance.ShowLevelMapTransition(currentIdx, nextIdx);
                    }
                    else if (LevelManager.Instance != null)
                    {
                        LevelManager.Instance.LoadLevel(nextIdx);
                    }
                });
            });
            WireButton(winT, "PopupPanel/HomeText", () =>
            {
                HideAll();
                if (LevelMapManager.Instance != null)
                {
                    LevelMapManager.Instance.OpenLevelMap();
                }
            });

            WireButton(loseT, "PopupPanel/ActionButton", () =>
            {
                HideAll();
                if (LevelManager.Instance != null) LevelManager.Instance.RestartCurrentLevel();
            });
            WireButton(loseT, "PopupPanel/HomeText", () =>
            {
                HideAll();
                if (LevelMapManager.Instance != null)
                {
                    LevelMapManager.Instance.OpenLevelMap();
                }
            });
        }

        private void Start()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
            LoadCoinSpriteIfMissing();
        }

        private void LoadCoinSpriteIfMissing()
        {
#if UNITY_EDITOR
            if (coinSprite == null)
            {
                coinSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Coin.png");
            }
            if (starSprite == null)
            {
                starSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Star.png");
            }
#endif
        }

        private static void WireButton(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            if (root == null) return;
            Transform t = root.Find(path);
            if (t == null) return;

            Button btn = t.GetComponent<Button>();
            if (btn == null) btn = t.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        public void ShowWin(int starsEarned = 3)
        {
            if (losePanel != null) losePanel.SetActive(false);
            HideTimerUI();

            // Bölüm kazanma coin ödülü
            CoinManager.AddCoins(winCoinReward);

            // Ekrana konfeti gibi coinler yağsın
            SpawnCoinConfettiShower();

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
            isLevelTransitioning = false;
            KillAllRowTweens();
            AnimateOut(winPanel);
            AnimateOut(losePanel);
            ShowTimerUI();
        }

        private void KillAllRowTweens()
        {
            if (currentWinRows != null)
            {
                foreach (var row in currentWinRows)
                {
                    if (row.starT != null) row.starT.DOKill();
                    if (row.starImg != null) row.starImg.DOKill();
                    if (row.levelTextComp != null) row.levelTextComp.transform.DOKill();
                }
            }
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

        private void SetupWinPanelLevelRows(Transform popupT, Sequence seq, float popupInDuration)
        {
            if (popupT == null) return;

            // 1. Yıldızları bul
            List<Transform> stars = new List<Transform>();
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

            // 2. Seviye yazılarını bul (Başlık, Buton ve Home hariç)
            List<Component> levelTextComponents = new List<Component>();
            foreach (Text txt in popupT.GetComponentsInChildren<Text>(true))
            {
                string pName = txt.transform.name.ToLowerInvariant();
                string txtContent = txt.text.ToLowerInvariant();
                if (pName.Contains("title") || pName.Contains("header") || txtContent.Contains("sevkiyat") || txtContent.Contains("tamam")) continue;
                if (pName.Contains("action") || txtContent.Contains("sonraki") || txtContent.Contains("devam") || txtContent.Contains("restart")) continue;
                if (pName.Contains("home")) continue;
                levelTextComponents.Add(txt);
            }
            foreach (TMPro.TMP_Text tmp in popupT.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                string pName = tmp.transform.name.ToLowerInvariant();
                string txtContent = tmp.text.ToLowerInvariant();
                if (pName.Contains("title") || pName.Contains("header") || txtContent.Contains("sevkiyat") || txtContent.Contains("tamam")) continue;
                if (pName.Contains("action") || txtContent.Contains("sonraki") || txtContent.Contains("devam") || txtContent.Contains("restart")) continue;
                if (pName.Contains("home")) continue;
                if (!levelTextComponents.Contains(tmp)) levelTextComponents.Add(tmp);
            }

            // 3. Yıldızlar ile seviye yazılarını Y pozisyonuna göre eşleştir
            List<WinLevelRow> rows = new List<WinLevelRow>();
            List<Component> availableTexts = new List<Component>(levelTextComponents);

            foreach (Transform star in stars)
            {
                float starY = popupT.InverseTransformPoint(star.position).y;

                Component closestText = null;
                float closestDist = float.MaxValue;
                foreach (Component comp in availableTexts)
                {
                    float compY = popupT.InverseTransformPoint(comp.transform.position).y;
                    float dist = Mathf.Abs(starY - compY);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestText = comp;
                    }
                }

                if (closestText != null)
                {
                    availableTexts.Remove(closestText);
                }

                rows.Add(new WinLevelRow
                {
                    starT = star,
                    starImg = star.GetComponent<Image>(),
                    levelTextComp = closestText,
                    posY = starY
                });
            }

            // 4. Düğümleri dikeyde sırala
            // progressionBottomToTop ise: En alttaki düğüm (en düşük Y) 1. adım olur
            if (progressionBottomToTop)
            {
                rows.Sort((a, b) => a.posY.CompareTo(b.posY));
            }
            else
            {
                rows.Sort((a, b) => b.posY.CompareTo(a.posY));
            }

            // 5. Seviye numaralarını hesapla (Kazanılan seviyeye göre kayan pencere)
            int currentLvl = (LevelManager.Instance != null) ? LevelManager.Instance.currentLevelIndex + 1 : 1;
            int totalLevels = (LevelManager.Instance != null && LevelManager.Instance.levels != null && LevelManager.Instance.levels.Count > 0)
                ? LevelManager.Instance.levels.Count
                : 10;

            int rowCount = rows.Count;
            int[] rowLevels = new int[rowCount];

            if (currentLvl <= 2 || rowCount <= 2)
            {
                for (int i = 0; i < rowCount; i++) rowLevels[i] = i + 1;
            }
            else if (currentLvl >= totalLevels)
            {
                for (int i = 0; i < rowCount; i++) rowLevels[i] = Mathf.Max(1, totalLevels - (rowCount - 1) + i);
            }
            else
            {
                // Kazanılan seviye ortada (index 1) yer alsın
                int midIdx = Mathf.Min(1, rowCount - 1);
                for (int i = 0; i < rowCount; i++)
                {
                    rowLevels[i] = currentLvl - midIdx + i;
                }
            }

            // Satırları ve indeksleri önbelleğe al
            currentWinRows = rows;
            winningRowIdx = -1;
            nextRowIdx = -1;

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].levelNumber = rowLevels[i];
                if (rowLevels[i] == currentLvl) winningRowIdx = i;
                else if (rowLevels[i] == currentLvl + 1) nextRowIdx = i;
            }

            // 6. Yıldızları ve yazıları güncelle ve canlandır
            for (int i = 0; i < rows.Count; i++)
            {
                WinLevelRow row = rows[i];
                int rowLvlNum = rowLevels[i];

                SetLevelText(row.levelTextComp, $"LEVEL {rowLvlNum}");

                if (row.starT == null) continue;
                row.starT.DOKill();

                // Yıldızın üzerine tıklanabilirlik ekle (tatlı yaylanma ve ışıltı tepkisi)
                Button starBtn = row.starT.GetComponent<Button>();
                if (starBtn == null) starBtn = row.starT.gameObject.AddComponent<Button>();
                starBtn.transition = Selectable.Transition.None;
                starBtn.onClick.RemoveAllListeners();
                Transform capturedStar = row.starT;
                starBtn.onClick.AddListener(() =>
                {
                    if (capturedStar != null)
                    {
                        capturedStar.DOKill();
                        capturedStar.DOPunchScale(Vector3.one * 0.28f, 0.25f, 7, 0.5f).SetUpdate(true);
                        SpawnSparkleBurst(capturedStar.position, 8, new Color(1f, 0.95f, 0.3f, 1f));
                        HapticHelper.Vibrate();
                    }
                });

                float delay = popupInDuration * 0.4f + i * 0.18f;

                if (rowLvlNum < currentLvl)
                {
                    // Daha önce tamamlanmış seviye (Altın yıldız)
                    if (row.starImg != null)
                    {
                        row.starImg.enabled = true;
                        row.starImg.color = new Color(1.0f, 0.88f, 0.10f, 1.0f);
                    }
                    SetTextColor(row.levelTextComp, Color.white);

                    row.starT.localScale = Vector3.zero;
                    row.starT.localRotation = Quaternion.Euler(0f, 0f, -20f);
                    seq.Insert(delay, row.starT.DOScale(Vector3.one, 0.30f).SetEase(Ease.OutBack, 2.2f));
                    seq.Insert(delay, row.starT.DORotate(Vector3.zero, 0.32f).SetEase(Ease.OutBack));
                }
                else if (rowLvlNum == currentLvl)
                {
                    // YENİ KAZANILAN SEVİYE - Coşkulu Altın Zıplama & Pop VFX
                    if (row.starImg != null)
                    {
                        row.starImg.enabled = true;
                        row.starImg.color = new Color(1.0f, 0.95f, 0.15f, 1.0f);
                    }
                    SetTextColor(row.levelTextComp, new Color(1f, 0.96f, 0.35f, 1f));

                    row.starT.localScale = Vector3.zero;
                    row.starT.localRotation = Quaternion.Euler(0f, 0f, -40f);
                    int starIdx = i;

                    seq.Insert(delay, row.starT.DOScale(Vector3.one * 1.30f, 0.38f).SetEase(Ease.OutBack, 2.8f));
                    seq.Insert(delay, row.starT.DORotate(Vector3.zero, 0.40f).SetEase(Ease.OutBack));
                    seq.InsertCallback(delay + 0.38f, () =>
                    {
                        if (row.starT != null)
                        {
                            row.starT.DOPunchScale(Vector3.one * 0.45f, 0.32f, 8, 0.6f).SetUpdate(true);
                            SpawnSparkleBurst(row.starT.position, 12, new Color(1f, 0.95f, 0.2f, 1f));
                        }
                        if (row.levelTextComp != null)
                        {
                            row.levelTextComp.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6, 0.5f).SetUpdate(true);
                        }
                        if (VFXManager.Instance != null && row.starT != null)
                        {
                            VFXManager.Instance.PlayStarPopVFX(row.starT.position, starIdx);
                        }
                        HapticHelper.Vibrate();
                    });
                }
                else if (rowLvlNum == currentLvl + 1)
                {
                    // SIRADAKİ SEVİYE - Canlı Nabız ve Hafif Salınım (Pulsing Glow & Float)
                    if (row.starImg != null)
                    {
                        row.starImg.enabled = true;
                        row.starImg.color = new Color(0.95f, 0.95f, 1.0f, 0.95f);
                    }
                    SetTextColor(row.levelTextComp, Color.white);

                    row.starT.localScale = Vector3.zero;
                    seq.Insert(delay, row.starT.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutBack, 2.0f));
                    seq.InsertCallback(delay + 0.32f, () =>
                    {
                        if (row.starT != null)
                        {
                            row.starT.DOKill();
                            // Büyüyüp küçülen tatlı nefes alma efekti
                            row.starT.DOScale(Vector3.one * 1.20f, 0.65f)
                                .SetLoops(-1, LoopType.Yoyo)
                                .SetEase(Ease.InOutSine)
                                .SetUpdate(true);
                            // Hafif açısal salınım
                            row.starT.DORotate(new Vector3(0f, 0f, 8f), 0.85f)
                                .SetLoops(-1, LoopType.Yoyo)
                                .SetEase(Ease.InOutSine)
                                .SetUpdate(true);
                        }
                    });
                }
                else
                {
                    // KİLİTLİ GELECEK SEVİYE
                    if (row.starImg != null)
                    {
                        row.starImg.enabled = true;
                        row.starImg.color = new Color(0.35f, 0.38f, 0.45f, 0.55f);
                    }
                    SetTextColor(row.levelTextComp, new Color(0.65f, 0.7f, 0.8f, 0.55f));

                    row.starT.localScale = Vector3.zero;
                    seq.Insert(delay, row.starT.DOScale(Vector3.one * 0.85f, 0.22f).SetEase(Ease.OutQuad));
                }
            }
        }

        /// <summary>
        /// Yeni seviyeye geçerken yıldızlar arasında sihirli enerji akışı,
        /// sıradaki yıldızın coşkulu uyanışı, 360 dönüşü ve ışıltı patlaması animasyonunu oynatır.
        /// </summary>
        public void PlayStarLevelTransitionAnimation(System.Action onComplete)
        {
            if (currentWinRows == null || currentWinRows.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            WinLevelRow fromRow = (winningRowIdx >= 0 && winningRowIdx < currentWinRows.Count) ? currentWinRows[winningRowIdx] : null;
            WinLevelRow toRow = (nextRowIdx >= 0 && nextRowIdx < currentWinRows.Count) ? currentWinRows[nextRowIdx] : null;

            if (fromRow != null && toRow != null && fromRow.starT != null && toRow.starT != null)
            {
                // 1. Kazanılan yıldız enerji fırlatır: Hafif zıplar ve parıldar
                fromRow.starT.DOPunchScale(Vector3.one * 0.35f, 0.25f, 8, 0.5f).SetUpdate(true);
                SpawnSparkleBurst(fromRow.starT.position, 8, new Color(1f, 0.95f, 0.25f, 1f));

                // 2. Bir sonraki yıldıza doğru kavisli altın enerji küreleri uçar
                PlayStarToStarEnergyStream(fromRow.starT.position, toRow.starT.position, () =>
                {
                    // 3. Enerji hedefe çarptığı anda sıradaki yıldız uyanır ve patlar!
                    if (toRow.starT != null)
                    {
                        toRow.starT.DOKill();
                        toRow.starT.localScale = Vector3.one;
                        toRow.starT.localRotation = Quaternion.identity;

                        // Büyüme ve zıplama
                        toRow.starT.DOScale(Vector3.one * 1.55f, 0.26f).SetEase(Ease.OutBack, 3.2f).SetUpdate(true)
                            .OnComplete(() =>
                            {
                                toRow.starT.DOScale(Vector3.one * 1.22f, 0.35f).SetEase(Ease.InOutSine).SetUpdate(true);
                            });

                        // 360 derece zafer dönüşü
                        toRow.starT.DORotate(new Vector3(0f, 0f, 360f), 0.45f, RotateMode.FastBeyond360)
                            .SetEase(Ease.OutCubic)
                            .SetUpdate(true);

                        // Renk geçişi: Beyaz flaş ardından altın sarısı
                        if (toRow.starImg != null)
                        {
                            toRow.starImg.DOKill();
                            Sequence colorSeq = DOTween.Sequence().SetUpdate(true);
                            colorSeq.Append(toRow.starImg.DOColor(Color.white, 0.08f));
                            colorSeq.Append(toRow.starImg.DOColor(new Color(1.0f, 0.93f, 0.15f, 1.0f), 0.25f));
                        }

                        // Yazının zıplaması ve altın rengine dönmesi
                        if (toRow.levelTextComp != null)
                        {
                            toRow.levelTextComp.transform.DOKill();
                            toRow.levelTextComp.transform.DOPunchScale(Vector3.one * 0.35f, 0.32f, 8, 0.6f).SetUpdate(true);
                            SetTextColor(toRow.levelTextComp, new Color(1f, 0.96f, 0.35f, 1f));
                        }

                        // Etrafına 16 adet ışıltı parçacığı fışkırır
                        SpawnSparkleBurst(toRow.starT.position, 16, new Color(1f, 0.95f, 0.2f, 1f));

                        HapticHelper.Vibrate();
                        if (VFXManager.Instance != null)
                        {
                            VFXManager.Instance.PlayStarPopVFX(toRow.starT.position, 1);
                        }
                    }

                    // 4. Oyuncunun bu zafer anını görmesi için kısa bir süre bekleyip geçişi tamamla
                    DOVirtual.DelayedCall(0.55f, () =>
                    {
                        onComplete?.Invoke();
                    }).SetUpdate(true);
                });
            }
            else
            {
                // Eğer bir sonraki satır yoksa (örneğin piramidin en tepesi)
                if (fromRow != null && fromRow.starT != null)
                {
                    fromRow.starT.DOPunchScale(Vector3.one * 0.5f, 0.4f, 8, 0.6f).SetUpdate(true);
                    fromRow.starT.DORotate(new Vector3(0f, 0f, 360f), 0.45f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic).SetUpdate(true);
                    SpawnSparkleBurst(fromRow.starT.position, 18, new Color(1f, 0.95f, 0.2f, 1f));
                    HapticHelper.Vibrate();
                }

                DOVirtual.DelayedCall(0.55f, () =>
                {
                    onComplete?.Invoke();
                }).SetUpdate(true);
            }
        }

        /// <summary>
        /// İki yıldız arasında kavisli yay çizerek uçan altın enerji küreleri fırlatır.
        /// </summary>
        private void PlayStarToStarEnergyStream(Vector3 fromWorldPos, Vector3 toWorldPos, System.Action onTargetReached)
        {
            EnsureConfettiContainer();
            if (confettiContainer == null)
            {
                onTargetReached?.Invoke();
                return;
            }

            Vector2 startLocal = confettiContainer.InverseTransformPoint(fromWorldPos);
            Vector2 endLocal = confettiContainer.InverseTransformPoint(toWorldPos);

            int orbCount = 5;
            float streamDuration = 0.42f;
            float staggerDelay = 0.06f;

            for (int i = 0; i < orbCount; i++)
            {
                int orbIdx = i;
                float delay = i * staggerDelay;
                bool isLast = (i == orbCount - 1);

                DOVirtual.DelayedCall(delay, () =>
                {
                    if (confettiContainer == null) return;

                    GameObject orbGO = new GameObject($"EnergyOrb_{orbIdx}");
                    orbGO.transform.SetParent(confettiContainer, false);

                    RectTransform orbRT = orbGO.AddComponent<RectTransform>();
                    orbRT.sizeDelta = new Vector2(26f, 26f);
                    orbRT.anchoredPosition = startLocal;

                    Image img = orbGO.AddComponent<Image>();
                    if (coinSprite != null) img.sprite = coinSprite;
                    img.color = new Color(1f, 0.95f, 0.3f, 1f);
                    img.raycastTarget = false;

                    // Kavisli kontrol noktası (dışa doğru tatlı bir yay çizer)
                    Vector2 midPoint = Vector2.Lerp(startLocal, endLocal, 0.5f);
                    Vector2 dir = (endLocal - startLocal).normalized;
                    Vector2 perp = new Vector2(-dir.y, dir.x) * (orbIdx % 2 == 0 ? 50f : -50f);
                    Vector2 controlPoint = midPoint + perp;

                    orbRT.localScale = Vector3.one * 1.3f;
                    orbRT.DOScale(Vector3.one * 0.7f, streamDuration).SetEase(Ease.InQuad).SetUpdate(true);

                    // Bezier interpolasyonu ile kavisli uçuş
                    DOVirtual.Float(0f, 1f, streamDuration, t =>
                    {
                        if (orbRT == null) return;
                        float oneMinusT = 1f - t;
                        Vector2 currentPos = oneMinusT * oneMinusT * startLocal + 2f * oneMinusT * t * controlPoint + t * t * endLocal;
                        orbRT.anchoredPosition = currentPos;

                        // Hareket yönüne göre rotasyon
                        Vector2 tangent = 2f * oneMinusT * (controlPoint - startLocal) + 2f * t * (endLocal - controlPoint);
                        if (tangent.sqrMagnitude > 0.001f)
                        {
                            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                            orbRT.localRotation = Quaternion.Euler(0f, 0f, angle);
                        }
                    }).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
                    {
                        if (orbGO != null) Destroy(orbGO);
                        if (isLast)
                        {
                            onTargetReached?.Invoke();
                        }
                    });
                }).SetUpdate(true);
            }
        }

        /// <summary>
        /// Verilen dünya pozisyonundan dışarıya doğru fırlayan parlak ışıltı parçacıkları oluşturur.
        /// </summary>
        private void SpawnSparkleBurst(Vector3 worldPos, int count = 12, Color? mainColor = null)
        {
            EnsureConfettiContainer();
            if (confettiContainer == null) return;

            Color color = mainColor ?? new Color(1f, 0.92f, 0.2f, 1f);
            Vector2 centerLocal = confettiContainer.InverseTransformPoint(worldPos);

            Sprite useSprite = starSprite ?? coinSprite;

            for (int i = 0; i < count; i++)
            {
                GameObject sparkGO = new GameObject($"Sparkle_{i}");
                sparkGO.transform.SetParent(confettiContainer, false);

                RectTransform sparkRT = sparkGO.AddComponent<RectTransform>();
                sparkRT.sizeDelta = new Vector2(22f, 22f);
                sparkRT.anchoredPosition = centerLocal;

                Image img = sparkGO.AddComponent<Image>();
                if (useSprite != null) img.sprite = useSprite;
                img.color = color;
                img.raycastTarget = false;

                float angle = (i * (360f / count) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                float dist = Random.Range(60f, 130f);
                Vector2 targetPos = centerLocal + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                float duration = Random.Range(0.40f, 0.60f);

                sparkRT.localScale = Vector3.one * Random.Range(0.8f, 1.3f);
                sparkRT.DOAnchorPos(targetPos, duration).SetEase(Ease.OutQuad).SetUpdate(true);
                sparkRT.DORotate(new Vector3(0f, 0f, Random.Range(-180f, 180f)), duration).SetUpdate(true);
                sparkRT.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad).SetUpdate(true);
                img.DOFade(0f, duration).SetEase(Ease.InQuad).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (sparkGO != null) Destroy(sparkGO);
                    });
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

            if (isWin)
            {
                // Kullanıcının yeni tasarladığı Win Panel yıldız ve seviye satırlarını ayarla
                SetupWinPanelLevelRows(popupT, seq, popupInDuration);

                HapticHelper.Vibrate();
                SpawnWinCelebration(Camera.main);

                Transform titleT = popupT.Find("TitleText") ?? popupT.Find("Title");
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

        private void EnsureConfettiContainer()
        {
            if (confettiContainer != null) return;

            Transform canvasTr = transform;
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null) canvasTr = parentCanvas.transform;

            Transform existing = canvasTr.Find("Coin_Confetti_Container");
            if (existing != null)
            {
                confettiContainer = existing.GetComponent<RectTransform>();
                return;
            }

            GameObject containerObj = new GameObject("Coin_Confetti_Container");
            containerObj.transform.SetParent(canvasTr, false);
            confettiContainer = containerObj.AddComponent<RectTransform>();
            confettiContainer.anchorMin = Vector2.zero;
            confettiContainer.anchorMax = Vector2.one;
            confettiContainer.sizeDelta = Vector2.zero;
            confettiContainer.SetAsLastSibling();

            CanvasGroup cg = containerObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        /// <summary>
        /// Ekrana konfeti gibi gökten altın coinler ve renkli pullar yağdıran coşkulu efekt.
        /// </summary>
        public void SpawnCoinConfettiShower(int count = -1)
        {
            EnsureConfettiContainer();
            LoadCoinSpriteIfMissing();

            int totalCoins = count > 0 ? count : confettiCoinCount;

            Color[] confettiColors = new Color[]
            {
                new Color(1f, 0.85f, 0.1f, 1f), // Altın Sarı
                new Color(1f, 0.4f, 0.85f, 1f), // Pembe
                new Color(0.2f, 0.85f, 1f, 1f), // Camgöbeği
                new Color(0.4f, 1f, 0.4f, 1f),  // Parlak Yeşil
                new Color(1f, 0.95f, 0.3f, 1f)  // Açık Altın
            };

            for (int i = 0; i < totalCoins; i++)
            {
                float delay = Random.Range(0f, 0.75f);
                int coinIdx = i;

                DOVirtual.DelayedCall(delay, () =>
                {
                    if (confettiContainer == null) return;

                    GameObject coinObj = new GameObject($"ConfettiCoin_{coinIdx}");
                    coinObj.transform.SetParent(confettiContainer, false);
                    RectTransform rect = coinObj.AddComponent<RectTransform>();

                    float startX = Random.Range(-520f, 520f);
                    float startY = Random.Range(950f, 1250f);
                    float targetX = startX + Random.Range(-120f, 120f);
                    float targetY = -1250f;
                    float coinSize = Random.Range(44f, 74f);
                    float duration = Random.Range(1.3f, 2.2f);

                    rect.anchoredPosition = new Vector2(startX, startY);
                    rect.sizeDelta = new Vector2(coinSize, coinSize);

                    CanvasGroup cg = coinObj.AddComponent<CanvasGroup>();
                    cg.blocksRaycasts = false;
                    cg.interactable = false;

                    Image img = coinObj.AddComponent<Image>();
                    if (coinSprite != null && coinIdx % 4 != 3)
                    {
                        img.sprite = coinSprite;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.color = confettiColors[coinIdx % confettiColors.Length];
                    }

                    // 1. 3D takla atma efekti (ScaleX yoyo loop)
                    rect.DOScaleX(-1f, Random.Range(0.2f, 0.42f))
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.Linear)
                        .SetUpdate(true);

                    // 2. Kendi etrafında fırıl fırıl dönme
                    rect.DORotate(new Vector3(0f, 0f, Random.Range(-360f, 360f)), Random.Range(1.5f, 2.8f), RotateMode.FastBeyond360)
                        .SetLoops(-1, LoopType.Incremental)
                        .SetEase(Ease.Linear)
                        .SetUpdate(true);

                    // 3. Yerçekimi ivmesiyle aşağı düşme
                    rect.DOAnchorPosY(targetY, duration)
                        .SetEase(Ease.InQuad)
                        .SetUpdate(true);

                    // 4. Havada sağa sola süzülme (Sway)
                    rect.DOAnchorPosX(targetX, duration)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);

                    // 5. Ekranın altına yaklaşırken kaybolma ve yok olma
                    cg.DOFade(0f, 0.35f)
                        .SetDelay(duration - 0.35f)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            if (coinObj != null) Destroy(coinObj);
                        });

                }).SetUpdate(true);
            }
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
