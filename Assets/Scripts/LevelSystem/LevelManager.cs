using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Central manager for loading and managing Level Data assets.
    /// Attached to Physics_Scene_Controller in scene.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Level Sequence")]
        [Tooltip("List of LevelDataSO assets ordered by progression.")]
        public List<LevelDataSO> levels = new List<LevelDataSO>();

        [Tooltip("Current level index (0-based).")]
        public int currentLevelIndex = 0;

        [Header("Active Level Override")]
        [Tooltip("Optional custom LevelDataSO override for testing a specific level.")]
        public LevelDataSO debugLevelOverride;

        private LevelDataSO defaultFallbackLevel;

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

            AutoFindLevelsIfEmpty();
        }

        private void Start()
        {
            AutoFindLevelsIfEmpty();
            int savedIndex = PlayerPrefs.GetInt("SavedCurrentLevelIndex", 0);
            LoadLevel(savedIndex);
        }

        public LevelDataSO ActiveLevelData
        {
            get
            {
                if (debugLevelOverride != null) return debugLevelOverride;

                if (levels != null && levels.Count > 0)
                {
                    int clampedIdx = Mathf.Clamp(currentLevelIndex, 0, levels.Count - 1);
                    if (levels[clampedIdx] != null) return levels[clampedIdx];
                }

                return GetOrCreateDefaultFallbackLevel();
            }
        }

        public void AutoFindLevelsIfEmpty()
        {
            if (levels == null || levels.Count == 0)
            {
#if UNITY_EDITOR
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LevelDataSO");
                if (guids != null && guids.Length > 0)
                {
                    levels = new List<LevelDataSO>();
                    foreach (string g in guids)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                        LevelDataSO lvl = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDataSO>(path);
                        if (lvl != null && !levels.Contains(lvl)) levels.Add(lvl);
                    }
                    levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
                }
#endif
                if (levels == null || levels.Count == 0)
                {
                    LevelDataSO[] loadedLevels = Resources.LoadAll<LevelDataSO>("Levels");
                    if (loadedLevels != null && loadedLevels.Length > 0)
                    {
                        levels = new List<LevelDataSO>(loadedLevels);
                        levels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
                    }
                }
            }
        }

        public void LoadLevel(int index)
        {
            AutoFindLevelsIfEmpty();

            if (levels != null && levels.Count > 0)
            {
                currentLevelIndex = Mathf.Clamp(index, 0, levels.Count - 1);
            }
            else
            {
                currentLevelIndex = 0;
            }

            // Save player progress
            PlayerPrefs.SetInt("SavedCurrentLevelIndex", currentLevelIndex);
            PlayerPrefs.Save();

            // 1. Clear existing physics objects in scene
            if (PhysicsObjectSpawner.Instance != null)
            {
                PhysicsObjectSpawner.Instance.ClearCurrentPile();
                PhysicsObjectSpawner.Instance.SpawnObjects();
            }

            // 2. Setup goals for this level
            if (MatchGoalManager.Instance != null)
            {
                MatchGoalManager.Instance.SetupLevelGoals();
            }

            // 2b. Re-roll customer orders for this level - the header row's cards are driven by these,
            // not directly by MatchGoalManager, so without this it would keep showing the previous
            // level's customers.
            if (CustomerOrderManager.Instance != null)
            {
                CustomerOrderManager.Instance.SetupCustomerOrders();
            }

            // 3. Refresh HUD UI cards and title
            if (CanvasUIDesignManager.Instance != null)
            {
                CanvasUIDesignManager.Instance.RefreshTargetGoalsUI();
                CanvasUIDesignManager.Instance.HideAllOverlayPanels();
            }

            // 4. Spawn every camouflaged Mecha this level configures (LevelDataSO.GetAllMechaEntries()) -
            // SpawnAllForLevel() reads the active level itself and handles clearing the old ones, spawning
            // zero when the level asks for none, and each mecha hiding in its own host object.
            MechaRagdollSpawner mechaSpawner = FindFirstObjectByType<MechaRagdollSpawner>();
            if (mechaSpawner != null)
            {
                mechaSpawner.SpawnAllForLevel();
            }

            Debug.Log($"🎮 Seviye {currentLevelIndex + 1} Başarıyla Yüklendi! (Başlık: {ActiveLevelData.levelTitle})");
        }

        public void LoadNextLevel()
        {
            AutoFindLevelsIfEmpty();
            if (levels != null && levels.Count > 0)
            {
                int nextIdx = (currentLevelIndex + 1) % levels.Count;
                LoadLevel(nextIdx);
            }
            else
            {
                LoadLevel(0);
            }
        }

        public void RestartCurrentLevel()
        {
            LoadLevel(currentLevelIndex);
        }

        private LevelDataSO GetOrCreateDefaultFallbackLevel()
        {
            if (defaultFallbackLevel == null)
            {
                defaultFallbackLevel = ScriptableObject.CreateInstance<LevelDataSO>();
                defaultFallbackLevel.levelNumber = 1;
                defaultFallbackLevel.levelTitle = "SEVİYE 1";
                defaultFallbackLevel.totalPileCount = 30;
                defaultFallbackLevel.foodTargetSize = 0.22f;
                defaultFallbackLevel.enableCamouflageMecha = true;
            }
            return defaultFallbackLevel;
        }
    }
}
