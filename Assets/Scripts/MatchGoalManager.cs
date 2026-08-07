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

            // 5 Mavi Küp, 3 Kırmızı Küre
            levelGoals.Add(new MatchGoal
            {
                shapeType = ObjectShapeType.Cube,
                colorName = "Mavi",
                targetColor = new Color(0.2f, 0.55f, 0.95f),
                totalRequired = 5,
                currentCount = 0
            });

            levelGoals.Add(new MatchGoal
            {
                shapeType = ObjectShapeType.Sphere,
                colorName = "Kırmızı",
                targetColor = new Color(0.95f, 0.2f, 0.2f),
                totalRequired = 3,
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
