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

        private void Awake()
        {
            Instance = this;
        }

        public void SetupLevelGoals()
        {
            levelGoals.Clear();
            isLevelComplete = false;

            // Food-type goals. colorName holds the food item id (must match a spawned food model
            // name); shapeType stays Cube because identity now comes from the food type, not shape.
            // totalRequired is a multiple of 3 so a whole number of match-3s completes it.
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

        public bool RegisterMatchedItem(ObjectShapeType shape, string colorName)
        {
            bool goalProgressed = false;

            foreach (MatchGoal goal in levelGoals)
            {
                if (goal.shapeType == shape && goal.colorName.Equals(colorName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!goal.IsCompleted)
                    {
                        goal.currentCount += 3;
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
                isLevelComplete = true;
            }
        }
    }
}
