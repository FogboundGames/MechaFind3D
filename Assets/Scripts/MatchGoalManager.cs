using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace MechaFind3D.PhysicsInteraction
{
    [System.Serializable]
    public class MatchGoal
    {
        public ObjectShapeType shapeType;
        public string colorName;
        public Color targetColor;
        public int totalRequired;
        public int currentCount;
        public GameObject displayPrefab;

        public bool IsCompleted => currentCount >= totalRequired;
        public int Remaining => Mathf.Max(0, totalRequired - currentCount);
    }

    /// <summary>
    /// Manages Level Task Goals (e.g. 5 Blue Cubes, 3 Red Spheres).
    /// Pure data & state manager (UI is handled cleanly by CanvasUIDesignManager).
    /// </summary>
    public class MatchGoalManager : MonoBehaviour
    {
        public static MatchGoalManager Instance { get; private set; }

        public List<MatchGoal> levelGoals = new List<MatchGoal>();
        public bool isLevelComplete = false;

        public float currentTime;
        public bool isTimerRunning = false;
        private UnityEngine.UI.Text cachedTimerText;
        private RectTransform cachedTimerTextRect;
        private GameObject cachedTimerBadgeObj;
        private UnityEngine.UI.Image cachedTimerBadgeImage;
        private RectTransform cachedTimerBadgeRect;
        private Color defaultBadgeColor = new Color(0.20f, 0.25f, 0.32f, 0.98f);
        private Vector2 initialTimerTextPos;
        private Vector3 initialTimerTextScale = Vector3.one;
        private Vector2 initialTimerBadgePos;
        private Vector3 initialTimerBadgeScale = Vector3.one;
        private bool hasSavedTimerPos = false;

        private void Awake()
        {
            Instance = this;
        }

        public float initialTime { get; private set; }
        public int totalMechasInLevel { get; private set; }
        public int mechasCaught { get; private set; }
        public int mechasEscaped { get; private set; }
        public int EarnedStars { get; private set; }

        public void SetupLevelGoals()
        {
            levelGoals.Clear();
            isLevelComplete = false;
            mechasCaught = 0;
            mechasEscaped = 0;
            EarnedStars = 0;
            hasSavedTimerPos = false;

            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                currentTime = LevelManager.Instance.ActiveLevelData.timeLimit;
                totalMechasInLevel = LevelManager.Instance.ActiveLevelData.GetAllMechaEntries().Count;
            }
            else
            {
                currentTime = 120f;
                totalMechasInLevel = 0;
            }
            initialTime = currentTime;
            isTimerRunning = true;

            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                LevelDataSO levelData = LevelManager.Instance.ActiveLevelData;
                List<ItemDataSO> spawnItems = levelData.BuildSpawnItemList();

                if (spawnItems != null && spawnItems.Count > 0)
                {
                    Dictionary<ItemDataSO, int> itemCounts = new Dictionary<ItemDataSO, int>();
                    foreach (var item in spawnItems)
                    {
                        if (item == null) continue;
                        if (itemCounts.ContainsKey(item)) itemCounts[item]++;
                        else itemCounts[item] = 1;
                    }

                    foreach (var kvp in itemCounts)
                    {
                        ItemDataSO itemData = kvp.Key;
                        int totalCount = kvp.Value;

                        levelGoals.Add(new MatchGoal
                        {
                            shapeType = ObjectShapeType.Cube,
                            colorName = itemData.GetEffectiveItemId(),
                            targetColor = itemData.targetColor,
                            totalRequired = totalCount,
                            currentCount = 0,
                            displayPrefab = itemData.prefab
                        });
                    }

                    // Add Mecha goal for the level based on actual configured mechas
                    int mechaCount = levelData.GetAllMechaEntries().Count;
                    if (mechaCount > 0)
                    {
                        levelGoals.Add(new MatchGoal
                        {
                            shapeType = ObjectShapeType.Cube,
                            colorName = "Mecha",
                            targetColor = new Color(0.4f, 0.95f, 1f),
                            totalRequired = mechaCount,
                            currentCount = 0,
                            displayPrefab = null
                        });
                    }

                    return;
                }
            }

            // Fallback default goals if no LevelData asset is assigned
            levelGoals.Add(new MatchGoal
            {
                shapeType = ObjectShapeType.Cube,
                colorName = "apple",
                targetColor = new Color(0.90f, 0.20f, 0.20f),
                totalRequired = 6,
                currentCount = 0
            });

            levelGoals.Add(new MatchGoal
            {
                shapeType = ObjectShapeType.Cube,
                colorName = "banana",
                targetColor = new Color(0.95f, 0.85f, 0.20f),
                totalRequired = 6,
                currentCount = 0
            });
        }

        /// <summary>
        /// Credits a shipped box against its goal.
        ///
        /// <paramref name="itemCount"/> is how many items that box actually held. It used to add a
        /// hard-coded 3, which was right back when every box took exactly three items - but a box now takes
        /// as many as the level contains of that type, so a level with nine watermelons ships ONE box of
        /// nine and only scored 3 against a goal of 9. With no items left in the pile the goal could never
        /// be finished, and the level became mathematically uncompletable.
        /// </summary>
        public bool RegisterMatchedItem(ObjectShapeType shape, string colorName, int itemCount = 3)
        {
            bool goalProgressed = false;
            int credit = Mathf.Max(1, itemCount);

            foreach (MatchGoal goal in levelGoals)
            {
                if (goal.shapeType == shape && goal.colorName.Equals(colorName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!goal.IsCompleted)
                    {
                        goal.currentCount = Mathf.Min(goal.totalRequired, goal.currentCount + credit);
                        goalProgressed = true;
                        break;
                    }
                }
            }

            CheckLevelCompletion();
            return goalProgressed;
        }

        public void NotifyMechaCaught()
        {
            mechasCaught++;
        }

        public void NotifyMechaEscaped()
        {
            mechasEscaped++;
        }

        public int CalculateEarnedStars()
        {
            if (!isLevelComplete || currentTime <= 0f) return 0;

            float timeRatio = initialTime > 0f ? (currentTime / initialTime) : 0f;
            bool allMechasCaught = (totalMechasInLevel > 0) ? (mechasCaught >= totalMechasInLevel) : true;

            // 3 Stars: Excellent speed (>35% time left) AND all mechas caught cleanly
            if (timeRatio >= 0.35f && allMechasCaught)
            {
                return 3;
            }

            // 2 Stars: Good speed (>15% time left) or caught at least 1 mecha
            if (timeRatio >= 0.15f || mechasCaught > 0)
            {
                return 2;
            }

            // 1 Star: Level completed before time ran out
            return 1;
        }

        private void CheckLevelCompletion()
        {
            bool allComplete = true;
            foreach (MatchGoal goal in levelGoals)
            {
                if (!goal.IsCompleted)
                {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete && !isLevelComplete)
            {
                isTimerRunning = false;
                isLevelComplete = true;
                EarnedStars = CalculateEarnedStars();
                if (WinLosePanelController.Instance != null)
                {
                    WinLosePanelController.Instance.ShowWin(EarnedStars);
                }
            }
        }

        private void Update()
        {
            // Debug Cheat: 'O' tuşuna basıldığında bölümü hemen kazandırarak bitir
            bool oKeyPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.oKey.wasPressedThisFrame)
            {
                oKeyPressed = true;
            }
#else
            try
            {
                if (Input.GetKeyDown(KeyCode.O)) oKeyPressed = true;
            }
            catch { }
#endif

            if (oKeyPressed)
            {
                TriggerWinCheat();
            }

            if (isTimerRunning && !isLevelComplete)
            {
                bool isMechaRunning = MechaRunnerBehavior.IsAnyMechaRunning();
                float drainMultiplier = isMechaRunning ? 1.5f : 1.0f;
                currentTime -= Time.deltaTime * drainMultiplier;

                if (currentTime <= 0f)
                {
                    currentTime = 0f;
                    isTimerRunning = false;
                    TriggerLose();
                }
                UpdateTimerUI(isMechaRunning);
            }
        }

        public void TriggerWinCheat()
        {
            if (isLevelComplete) return;

            isTimerRunning = false;
            isLevelComplete = true;

            if (levelGoals != null)
            {
                foreach (MatchGoal goal in levelGoals)
                {
                    goal.currentCount = goal.totalRequired;
                }
            }

            EarnedStars = 3;
            if (WinLosePanelController.Instance != null)
            {
                WinLosePanelController.Instance.ShowWin(EarnedStars);
            }
        }

        public void TriggerLose()
        {
            if (isLevelComplete) return;

            isLevelComplete = true;
            isTimerRunning = false;
            if (WinLosePanelController.Instance != null)
            {
                WinLosePanelController.Instance.ShowLose();
            }
        }

        private void UpdateTimerUI(bool isMechaRunning = false)
        {
            if (cachedTimerText == null)
            {
                GameObject timerObj = GameObject.Find("timer_text");
                if (timerObj != null)
                {
                    cachedTimerText = timerObj.GetComponent<UnityEngine.UI.Text>();
                    cachedTimerTextRect = timerObj.GetComponent<RectTransform>();
                }
            }

            if (cachedTimerBadgeObj == null)
            {
                cachedTimerBadgeObj = GameObject.Find("timer_badge");
                if (cachedTimerBadgeObj != null)
                {
                    cachedTimerBadgeImage = cachedTimerBadgeObj.GetComponent<UnityEngine.UI.Image>();
                    cachedTimerBadgeRect = cachedTimerBadgeObj.GetComponent<RectTransform>();
                    if (cachedTimerBadgeImage != null)
                    {
                        defaultBadgeColor = cachedTimerBadgeImage.color;
                    }
                }
            }

            if (!hasSavedTimerPos && cachedTimerTextRect != null)
            {
                initialTimerTextPos = cachedTimerTextRect.anchoredPosition;
                initialTimerTextScale = cachedTimerTextRect.localScale;
                if (cachedTimerBadgeRect != null)
                {
                    initialTimerBadgePos = cachedTimerBadgeRect.anchoredPosition;
                    initialTimerBadgeScale = cachedTimerBadgeRect.localScale;
                }
                hasSavedTimerPos = true;
            }

            if (cachedTimerText != null)
            {
                cachedTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
                cachedTimerText.verticalOverflow = VerticalWrapMode.Overflow;

                int minutes = Mathf.FloorToInt(currentTime / 60f);
                int seconds = Mathf.FloorToInt(currentTime % 60f);

                if (isMechaRunning)
                {
                    cachedTimerText.text = string.Format("⚡ {0:00}:{1:00} ⚡", minutes, seconds);

                    // Hızlı ilerlediğini gösteren belirgin canlı renk (kehribar/turuncu)
                    float pingPong = Mathf.PingPong(Time.time * 3.5f, 1f);
                    Color fastDrainColor = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.65f, 0.20f), pingPong);
                    cachedTimerText.color = fastDrainColor;

                    if (cachedTimerBadgeImage != null)
                    {
                        cachedTimerBadgeImage.color = fastDrainColor;
                    }

                    // Hafif sallanma (subtle shake & rotation) - ölçek sahnede ayarlanan boyutta kalır
                    float shakeX = Mathf.Sin(Time.time * 28f) * 2.0f;
                    float shakeY = Mathf.Cos(Time.time * 34f) * 1.0f;
                    float shakeRot = Mathf.Sin(Time.time * 22f) * 2.0f;

                    if (cachedTimerTextRect != null)
                    {
                        cachedTimerTextRect.DOKill();
                        cachedTimerTextRect.localScale = initialTimerTextScale;
                        cachedTimerTextRect.anchoredPosition = initialTimerTextPos + new Vector2(shakeX, shakeY);
                        cachedTimerTextRect.localRotation = Quaternion.Euler(0f, 0f, shakeRot);
                    }
                    if (cachedTimerBadgeRect != null)
                    {
                        cachedTimerBadgeRect.DOKill();
                        cachedTimerBadgeRect.localScale = initialTimerBadgeScale;
                        cachedTimerBadgeRect.anchoredPosition = initialTimerBadgePos + new Vector2(shakeX, shakeY);
                        cachedTimerBadgeRect.localRotation = Quaternion.Euler(0f, 0f, shakeRot);
                    }
                }
                else
                {
                    // Sallanmayı ve rotasyonu sıfırla, sahnede ayarlanan orijinal pozisyon ve boyuta getir
                    if (cachedTimerTextRect != null)
                    {
                        cachedTimerTextRect.DOKill();
                        cachedTimerTextRect.localScale = initialTimerTextScale;
                        cachedTimerTextRect.anchoredPosition = initialTimerTextPos;
                        cachedTimerTextRect.localRotation = Quaternion.identity;
                    }
                    if (cachedTimerBadgeRect != null)
                    {
                        cachedTimerBadgeRect.DOKill();
                        cachedTimerBadgeRect.localScale = initialTimerBadgeScale;
                        cachedTimerBadgeRect.anchoredPosition = initialTimerBadgePos;
                        cachedTimerBadgeRect.localRotation = Quaternion.identity;
                    }

                    if (currentTime <= 20f)
                    {
                        cachedTimerText.text = string.Format("⏱️ {0:00}:{1:00}", minutes, seconds);
                        float pingPong = Mathf.PingPong(Time.time * 6f, 1f);
                        cachedTimerText.color = Color.Lerp(Color.white, new Color(1.0f, 0.85f, 0.20f), pingPong);

                        if (cachedTimerBadgeImage != null)
                        {
                            cachedTimerBadgeImage.color = Color.Lerp(defaultBadgeColor, new Color(0.90f, 0.30f, 0.15f, 0.98f), pingPong);
                        }
                    }
                    else
                    {
                        cachedTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
                        cachedTimerText.color = Color.white;

                        if (cachedTimerBadgeImage != null)
                        {
                            cachedTimerBadgeImage.color = defaultBadgeColor;
                        }
                    }
                }
            }
        }
    }
}
