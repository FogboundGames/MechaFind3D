using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Manages the Level Roadmap / Progression Map Canvas.
    /// Supports smooth transitions from completed level to next level,
    /// animated path dots, pulsing current level star, and starting selected levels.
    /// </summary>
    public class LevelMapManager : MonoBehaviour
    {
        public static LevelMapManager Instance { get; private set; }

        [Header("Canvas & Container References")]
        [Tooltip("Root canvas or panel for the level map.")]
        [SerializeField] private GameObject mapCanvasObject;

        [Tooltip("Transform containing the level star nodes.")]
        [SerializeField] private Transform nodesContainer;

        [Tooltip("ScrollRect if map is scrollable (optional).")]
        [SerializeField] private ScrollRect scrollRect;

        [Header("Top Bar & Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text coinCounterText;
        [SerializeField] private TMPro.TMP_Text coinCounterTMP;

        [Header("Visual Colors & Sprites")]
        [SerializeField] private Color completedStarColor = new Color(1f, 0.85f, 0.1f, 1f); // Golden yellow
        [SerializeField] private Color currentStarColor = new Color(1f, 0.95f, 0.3f, 1f);   // Bright yellow
        [SerializeField] private Color lockedStarColor = new Color(0.45f, 0.55f, 0.65f, 0.7f); // Dim slate
        [SerializeField] private Color activeDotColor = new Color(1f, 0.88f, 0.15f, 1f);   // Bright yellow dot
        [SerializeField] private Color lockedDotColor = new Color(0.3f, 0.4f, 0.5f, 0.5f);  // Dim dot
        [SerializeField] private Sprite starFilledSprite;
        [SerializeField] private Sprite starOutlineSprite;
        [SerializeField] private Sprite dotSprite;

        [Header("Animation Settings")]
        [SerializeField] private float transitionDotDelay = 0.18f;
        [SerializeField] private float starPopDuration = 0.55f;
        [SerializeField] private float starPulseScale = 1.15f;

        [System.Serializable]
        public class LevelNodeItem
        {
            public int levelIndex; // 0-based
            public Transform nodeRoot;
            public Image starImage;
            public Component levelNumberText; // Text or TMP_Text
            public Button nodeButton;
            public List<Image> incomingDots = new List<Image>();
            public Tween pulseTween;
        }

        [Header("Detected Nodes")]
        [SerializeField] private List<LevelNodeItem> levelNodes = new List<LevelNodeItem>();

        private int selectedLevelIndex = 0;
        private Coroutine activeTransitionRoutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (mapCanvasObject == null)
            {
                // If attached directly to LevelMap_Canvas
                if (GetComponent<Canvas>() != null) mapCanvasObject = gameObject;
                else
                {
                    Transform t = transform.Find("LevelMap_Canvas");
                    if (t != null) mapCanvasObject = t.gameObject;
                }
            }

            LoadSpritesIfMissing();
            FindAndSetupNodes();
            SetupButtons();
        }

        private void Start()
        {
            UpdateCoinDisplay();

            // Default state: ensure canvas is closed on game start unless explicitly opened
            if (mapCanvasObject != null && mapCanvasObject.activeSelf && LevelManager.Instance != null)
            {
                // If this is the main gameplay scene, close map on fresh launch
                // (or keep open if designed as main menu)
                mapCanvasObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }

        private void KillAllTweens()
        {
            foreach (var node in levelNodes)
            {
                if (node.pulseTween != null && node.pulseTween.IsActive())
                {
                    node.pulseTween.Kill();
                    node.pulseTween = null;
                }
            }
        }

        private void LoadSpritesIfMissing()
        {
#if UNITY_EDITOR
            if (starFilledSprite == null)
                starFilledSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Star.png");
            if (starOutlineSprite == null)
                starOutlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/White Icons/White Star Hollow.png");
            if (dotSprite == null)
                dotSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Violet Theme Ui/Colored Icons/Coin.png");
#endif
        }

        /// <summary>
        /// Auto-finds all level star nodes in children if not manually configured in inspector.
        /// </summary>
        public void FindAndSetupNodes()
        {
            if (nodesContainer == null)
            {
                Transform found = transform.Find("LevelMap_Canvas/Content_Scroll/NodesContainer");
                if (found == null) found = transform.Find("NodesContainer");
                if (found == null) found = transform.Find("Content/NodesContainer");
                if (found == null && mapCanvasObject != null)
                {
                    Transform c = mapCanvasObject.transform.Find("NodesContainer");
                    if (c != null) found = c;
                    else
                    {
                        var rects = mapCanvasObject.GetComponentsInChildren<RectTransform>(true);
                        foreach (var r in rects)
                        {
                            if (r.name.ToLowerInvariant().Contains("node") || r.name.ToLowerInvariant().Contains("content"))
                            {
                                found = r;
                                break;
                            }
                        }
                    }
                }
                nodesContainer = found;
            }

            if (levelNodes.Count == 0 && nodesContainer != null)
            {
                List<Transform> nodeRoots = new List<Transform>();
                foreach (Transform child in nodesContainer)
                {
                    if (child.name.ToLowerInvariant().Contains("dot")) continue;
                    nodeRoots.Add(child);
                }

                // Sort by name or Y position (bottom to top)
                nodeRoots.Sort((a, b) =>
                {
                    int numA = ExtractNumber(a.name);
                    int numB = ExtractNumber(b.name);
                    if (numA != -1 && numB != -1) return numA.CompareTo(numB);
                    return a.position.y.CompareTo(b.position.y);
                });

                for (int i = 0; i < nodeRoots.Count; i++)
                {
                    Transform r = nodeRoots[i];
                    LevelNodeItem item = new LevelNodeItem
                    {
                        levelIndex = i,
                        nodeRoot = r,
                        starImage = r.GetComponentInChildren<Image>(true),
                        nodeButton = r.GetComponentInChildren<Button>(true)
                    };

                    // Text
                    Text txt = r.GetComponentInChildren<Text>(true);
                    if (txt != null) item.levelNumberText = txt;
                    else item.levelNumberText = r.GetComponentInChildren<TMPro.TMP_Text>(true);

                    // Incoming dots
                    Transform dotsT = r.Find("IncomingDots");
                    if (dotsT != null)
                    {
                        foreach (Image img in dotsT.GetComponentsInChildren<Image>(true))
                        {
                            item.incomingDots.Add(img);
                        }
                    }

                    levelNodes.Add(item);
                }
            }

            // Wire node buttons
            for (int i = 0; i < levelNodes.Count; i++)
            {
                int idx = i;
                LevelNodeItem item = levelNodes[i];
                if (item.nodeButton != null)
                {
                    item.nodeButton.onClick.RemoveAllListeners();
                    item.nodeButton.onClick.AddListener(() => OnNodeClicked(idx));
                }
            }
        }

        private static int ExtractNumber(string name)
        {
            string digits = "";
            foreach (char c in name)
            {
                if (char.IsDigit(c)) digits += c;
            }
            if (int.TryParse(digits, out int val)) return val;
            return -1;
        }

        private void SetupButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(OnPlayButtonClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseLevelMap);
            }
        }

        /// <summary>
        /// Opens the level map directly and highlights the current level.
        /// </summary>
        public void OpenLevelMap()
        {
            int currentLevel = GetSavedLevelIndex();
            selectedLevelIndex = currentLevel;

            if (mapCanvasObject != null) mapCanvasObject.SetActive(true);
            UpdateCoinDisplay();
            RefreshAllNodesVisuals(currentLevel, -1);
            FocusOnNode(selectedLevelIndex);
        }

        /// <summary>
        /// Closes the level map and restores normal game UI.
        /// </summary>
        public void CloseLevelMap()
        {
            if (activeTransitionRoutine != null)
            {
                StopCoroutine(activeTransitionRoutine);
                activeTransitionRoutine = null;
            }

            KillAllTweens();

            if (mapCanvasObject != null) mapCanvasObject.SetActive(false);

            // Restore gameplay canvas if needed
            if (CanvasUIDesignManager.Instance != null)
            {
                CanvasUIDesignManager.Instance.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Plays the animated transition on the map:
        /// lights up dots from previous level to new level, pops the new level star.
        /// </summary>
        public void ShowLevelMapTransition(int fromLevel, int toLevel)
        {
            selectedLevelIndex = toLevel;
            if (mapCanvasObject != null) mapCanvasObject.SetActive(true);
            UpdateCoinDisplay();

            if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = StartCoroutine(TransitionRoutine(fromLevel, toLevel));
        }

        private IEnumerator TransitionRoutine(int fromLevel, int toLevel)
        {
            // Initial state: show fromLevel as completed, toLevel as locked or beginning
            RefreshAllNodesVisuals(fromLevel, toLevel);
            FocusOnNode(fromLevel);

            yield return new WaitForSeconds(0.35f);

            // Animate path dots towards toLevel
            if (toLevel < levelNodes.Count)
            {
                LevelNodeItem targetNode = levelNodes[toLevel];
                if (targetNode.incomingDots != null && targetNode.incomingDots.Count > 0)
                {
                    foreach (var dot in targetNode.incomingDots)
                    {
                        if (dot != null)
                        {
                            dot.color = activeDotColor;
                            dot.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f, 10, 1f);
                            yield return new WaitForSeconds(transitionDotDelay);
                        }
                    }
                }
            }

            // Now unlock and pop the target level star!
            FocusOnNode(toLevel);
            if (toLevel < levelNodes.Count)
            {
                LevelNodeItem targetNode = levelNodes[toLevel];
                ApplyNodeVisual(targetNode, NodeState.Current);

                if (targetNode.starImage != null)
                {
                    targetNode.starImage.transform.localScale = Vector3.one * 0.3f;
                    targetNode.starImage.transform.DOScale(Vector3.one * 1.35f, starPopDuration * 0.6f)
                        .SetEase(Ease.OutBack)
                        .OnComplete(() =>
                        {
                            targetNode.starImage.transform.DOScale(Vector3.one, starPopDuration * 0.4f);
                        });
                }
            }

            yield return new WaitForSeconds(0.4f);
            activeTransitionRoutine = null;
        }

        private enum NodeState { Completed, Current, Locked }

        private void RefreshAllNodesVisuals(int currentUnlockedLevel, int pendingUnlockLevel = -1)
        {
            KillAllTweens();

            for (int i = 0; i < levelNodes.Count; i++)
            {
                var item = levelNodes[i];
                NodeState state;

                if (i < currentUnlockedLevel)
                {
                    state = NodeState.Completed;
                }
                else if (i == currentUnlockedLevel)
                {
                    state = (pendingUnlockLevel == currentUnlockedLevel) ? NodeState.Locked : NodeState.Current;
                }
                else
                {
                    state = NodeState.Locked;
                }

                ApplyNodeVisual(item, state);

                // Dots leading into this node
                bool dotsActive = (i <= currentUnlockedLevel && i != pendingUnlockLevel);
                foreach (var dot in item.incomingDots)
                {
                    if (dot != null)
                    {
                        dot.color = dotsActive ? activeDotColor : lockedDotColor;
                    }
                }
            }
        }

        private void ApplyNodeVisual(LevelNodeItem item, NodeState state)
        {
            if (item == null || item.nodeRoot == null) return;

            string levelTextStr = (item.levelIndex + 1).ToString();
            SetText(item.levelNumberText, levelTextStr);

            if (item.starImage != null)
            {
                switch (state)
                {
                    case NodeState.Completed:
                        item.starImage.color = completedStarColor;
                        if (starFilledSprite != null) item.starImage.sprite = starFilledSprite;
                        item.starImage.transform.localScale = Vector3.one;
                        break;

                    case NodeState.Current:
                        item.starImage.color = currentStarColor;
                        if (starFilledSprite != null) item.starImage.sprite = starFilledSprite;
                        // Gentle breathing pulse
                        item.pulseTween = item.starImage.transform.DOScale(Vector3.one * starPulseScale, 0.75f)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(-1, LoopType.Yoyo);
                        break;

                    case NodeState.Locked:
                        item.starImage.color = lockedStarColor;
                        if (starOutlineSprite != null) item.starImage.sprite = starOutlineSprite;
                        item.starImage.transform.localScale = Vector3.one * 0.9f;
                        break;
                }
            }
        }

        private void OnNodeClicked(int levelIndex)
        {
            int maxUnlocked = GetSavedLevelIndex();
            // Can only play completed or current level
            if (levelIndex <= maxUnlocked)
            {
                selectedLevelIndex = levelIndex;
                FocusOnNode(selectedLevelIndex);
                StartSelectedLevel();
            }
            else
            {
                // Locked wiggle animation
                if (levelIndex < levelNodes.Count && levelNodes[levelIndex].nodeRoot != null)
                {
                    levelNodes[levelIndex].nodeRoot.DOShakePosition(0.3f, 8f, 15, 90f);
                }
            }
        }

        private void OnPlayButtonClicked()
        {
            StartSelectedLevel();
        }

        private void StartSelectedLevel()
        {
            int levelToLoad = selectedLevelIndex;

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.currentLevelIndex = levelToLoad;
                LevelManager.Instance.LoadLevel(levelToLoad);
            }

            PlayerPrefs.SetInt("SavedCurrentLevelIndex", levelToLoad);
            PlayerPrefs.Save();

            CloseLevelMap();
        }

        private void FocusOnNode(int index)
        {
            if (scrollRect == null || index < 0 || index >= levelNodes.Count) return;
            var target = levelNodes[index];
            if (target == null || target.nodeRoot == null) return;

            // Smooth scroll to node position
            RectTransform content = scrollRect.content;
            if (content != null)
            {
                float targetNormalized = Mathf.Clamp01((float)index / Mathf.Max(1, levelNodes.Count - 1));
                scrollRect.DOVerticalNormalizedPos(targetNormalized, 0.4f).SetEase(Ease.OutCubic);
            }
        }

        private void UpdateCoinDisplay()
        {
            int coins = CoinManager.Coins;
            if (coinCounterText != null) coinCounterText.text = coins.ToString();
            if (coinCounterTMP != null) coinCounterTMP.text = coins.ToString();
        }

        private static int GetSavedLevelIndex()
        {
            if (LevelManager.Instance != null) return LevelManager.Instance.currentLevelIndex;
            return PlayerPrefs.GetInt("SavedCurrentLevelIndex", 0);
        }

        private static void SetText(Component comp, string val)
        {
            if (comp == null) return;
            if (comp is Text t) t.text = val;
            else if (comp is TMPro.TMP_Text tmp) tmp.text = val;
        }
    }
}
