using System.Collections.Generic;
using UnityEngine;

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

        private void Awake()
        {
            Instance = this;
        }

        public void SetupLevelGoals()
        {
            levelGoals.Clear();
            isLevelComplete = false;

            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                currentTime = LevelManager.Instance.ActiveLevelData.timeLimit;
            }
            else
            {
                currentTime = 120f;
            }
            isTimerRunning = true;

            if (LevelManager.Instance != null && LevelManager.Instance.ActiveLevelData != null)
            {
                LevelDataSO levelData = LevelManager.Instance.ActiveLevelData;
                if (levelData.targetGoals != null && levelData.targetGoals.Count > 0)
                {
                    foreach (var req in levelData.targetGoals)
                    {
                        if (req.itemData != null)
                        {
                            levelGoals.Add(new MatchGoal
                            {
                                shapeType = ObjectShapeType.Cube,
                                colorName = req.itemData.GetEffectiveItemId(),
                                targetColor = req.itemData.targetColor,
                                totalRequired = req.requiredCount,
                                currentCount = 0,
                                displayPrefab = req.itemData.prefab
                            });
                        }
                    }

                    // Add Mecha goal for the level
                    levelGoals.Add(new MatchGoal
                    {
                        shapeType = ObjectShapeType.Cube,
                        colorName = "Mecha",
                        targetColor = new Color(0.4f, 0.95f, 1f),
                        totalRequired = 1,
                        currentCount = 0,
                        displayPrefab = null
                    });

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
                if (WinLosePanelController.Instance != null)
                {
                    WinLosePanelController.Instance.ShowWin();
                }
            }
        }

        private void Update()
        {
            if (isTimerRunning && !isLevelComplete)
            {
                currentTime -= Time.deltaTime;
                if (currentTime <= 0f)
                {
                    currentTime = 0f;
                    isTimerRunning = false;
                    TriggerLose();
                }
                UpdateTimerUI();
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

        private void UpdateTimerUI()
        {
            if (cachedTimerText == null)
            {
                GameObject timerObj = GameObject.Find("timer_text");
                if (timerObj != null) cachedTimerText = timerObj.GetComponent<UnityEngine.UI.Text>();
            }

            if (cachedTimerText != null)
            {
                int minutes = Mathf.FloorToInt(currentTime / 60f);
                int seconds = Mathf.FloorToInt(currentTime % 60f);
                cachedTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }
        }
    }
}
