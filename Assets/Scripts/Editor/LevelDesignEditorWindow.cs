using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    public class LevelDesignEditorWindow : EditorWindow
    {
        private enum Tab { LevelManager, ItemLibrary, PrefabBatchImporter }
        private Tab selectedTab = Tab.LevelManager;

        private Vector2 scrollPos;
        private LevelDataSO selectedLevel;
        private string itemSearchQuery = "";
        private Object dragAndDropPrefabTarget;

        // Expand/collapse state per additionalMechas entry, keyed by list index. Session-only (not saved
        // with the asset) - purely so re-drawing the same OnGUI frame doesn't reset every foldout shut.
        private readonly Dictionary<int, bool> mechaEntryFoldouts = new Dictionary<int, bool>();

        [MenuItem("Tools/Level Design Manager", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelDesignEditorWindow>("Level Design Manager");
            window.minSize = new Vector2(750, 600);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawNavigationTabs();

            EditorGUILayout.Space(10);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (selectedTab)
            {
                case Tab.LevelManager:
                    DrawLevelManagerTab();
                    break;
                case Tab.ItemLibrary:
                    DrawItemLibraryTab();
                    break;
                case Tab.PrefabBatchImporter:
                    DrawPrefabBatchImporterTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.7f, 1.0f) }
            };
            GUILayout.Label("🎨 MechaFind3D Level Design & Prefab Manager", headerStyle);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawNavigationTabs()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedTab == Tab.LevelManager, "🎮 Seviye Yöneticisi (Levels)", "LargeButton", GUILayout.Height(35)))
                selectedTab = Tab.LevelManager;
            if (GUILayout.Toggle(selectedTab == Tab.ItemLibrary, "📦 Obje Kütüphanesi (Items)", "LargeButton", GUILayout.Height(35)))
                selectedTab = Tab.ItemLibrary;
            if (GUILayout.Toggle(selectedTab == Tab.PrefabBatchImporter, "🚀 Toplu Prefab Yükleyici", "LargeButton", GUILayout.Height(35)))
                selectedTab = Tab.PrefabBatchImporter;
            EditorGUILayout.EndHorizontal();
        }

        // ====================================================================
        // TAB 1: LEVEL MANAGER & CREATOR
        // ====================================================================
        private void DrawLevelManagerTab()
        {
            EditorGUILayout.BeginHorizontal();

            // Left Sidebar: List of Levels
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(240));
            GUILayout.Label("Seviyeler Listesi", EditorStyles.boldLabel);

            if (GUILayout.Button("+ Yeni Seviye Oluştur", GUILayout.Height(30)))
            {
                CreateNewLevelAsset();
            }

            EditorGUILayout.Space(5);

            LevelDataSO[] levels = FindAllAssets<LevelDataSO>();
            System.Array.Sort(levels, (a, b) => a.levelNumber.CompareTo(b.levelNumber));

            LevelManager manager = Object.FindFirstObjectByType<LevelManager>();
            int currentIndex = Application.isPlaying && manager != null
                ? manager.currentLevelIndex
                : PlayerPrefs.GetInt("SavedCurrentLevelIndex", manager != null ? manager.currentLevelIndex : 0);

            for (int i = 0; i < levels.Length; i++)
            {
                LevelDataSO lvl = levels[i];
                if (lvl == null) continue;

                EditorGUILayout.BeginHorizontal();

                // Reorder. The play order comes from levelNumber, so moving a level really means swapping
                // that number with its neighbour - and every level is then renumbered 1..N, because two
                // levels sharing a number would leave the sort order undefined.
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(24), GUILayout.Height(28))) MoveLevel(levels, i, -1);
                }
                using (new EditorGUI.DisabledScope(i == levels.Length - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(24), GUILayout.Height(28))) MoveLevel(levels, i, +1);
                }

                bool isCurrent = (i == currentIndex);
                GUI.backgroundColor = (selectedLevel == lvl) ? new Color(0.3f, 0.8f, 1.0f) : Color.white;
                string label = (isCurrent ? "▶ " : "") + $"Seviye {lvl.levelNumber}: {lvl.levelTitle}";
                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    selectedLevel = lvl;
                }
                GUI.backgroundColor = Color.white;

                // Pick which level the game starts on. LevelManager.Start() reads the saved index from
                // PlayerPrefs, so setting the field alone would be undone the moment you pressed Play.
                using (new EditorGUI.DisabledScope(isCurrent || manager == null))
                {
                    if (GUILayout.Button("Başlat", GUILayout.Width(56), GUILayout.Height(28)))
                    {
                        SetCurrentLevel(manager, levels, i);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);

            if (manager == null)
            {
                EditorGUILayout.HelpBox("Sahnede LevelManager yok, bu yüzden aktif seviye seçilemiyor.", MessageType.Warning);
            }
            else if (manager.debugLevelOverride != null)
            {
                // Worth shouting about: the override wins over currentLevelIndex entirely, so with it set
                // the game replays that one level forever no matter what is picked here.
                EditorGUILayout.HelpBox(
                    $"Debug Level Override = '{manager.debugLevelOverride.name}'. Bu alan doluyken aktif seviye " +
                    "seçimi TAMAMEN yok sayılır ve hep o seviye oynanır.", MessageType.Error);
                if (GUILayout.Button("Override'ı temizle"))
                {
                    Undo.RecordObject(manager, "Override temizle");
                    manager.debugLevelOverride = null;
                    EditorUtility.SetDirty(manager);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
            }

            EditorGUILayout.EndVertical();

            // Right Panel: Selected Level Editor
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (selectedLevel != null)
            {
                DrawSelectedLevelEditor(selectedLevel);
            }
            else
            {
                EditorGUILayout.HelpBox("Düzenlemek veya yeni hedefler eklemek için soldaki listeden bir seviye seçin veya yeni bir seviye oluşturun.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Swaps a level with its neighbour in play order, then renumbers every level 1..N.
        ///
        /// Order is read off levelNumber, so a bare swap would work - but only until two levels ended up
        /// sharing a number, at which point the sort becomes ambiguous and the list order starts flickering.
        /// Renumbering the whole set keeps it unambiguous.
        /// </summary>
        private void MoveLevel(LevelDataSO[] levels, int index, int direction)
        {
            int target = index + direction;
            if (target < 0 || target >= levels.Length) return;

            var ordered = new List<LevelDataSO>(levels);
            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i] == null) continue;
                Undo.RecordObject(ordered[i], "Seviye sırası değiştir");
                ordered[i].levelNumber = i + 1;
                EditorUtility.SetDirty(ordered[i]);
            }

            AssetDatabase.SaveAssets();

            // The manager caches its own ordered list, so it has to be re-synced or it would keep playing
            // the old sequence until someone cleared it by hand.
            LevelManager manager = Object.FindFirstObjectByType<LevelManager>();
            if (manager != null)
            {
                Undo.RecordObject(manager, "Seviye sırası değiştir");
                manager.levels = new List<LevelDataSO>(ordered);
                EditorUtility.SetDirty(manager);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
        }

        /// <summary>Makes the given level the one the game starts on.</summary>
        private void SetCurrentLevel(LevelManager manager, LevelDataSO[] levels, int index)
        {
            if (manager == null) return;

            Undo.RecordObject(manager, "Aktif seviye seç");
            manager.levels = new List<LevelDataSO>(levels);
            manager.currentLevelIndex = index;
            EditorUtility.SetDirty(manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

            // LevelManager.Start() loads PlayerPrefs["SavedCurrentLevelIndex"], so without writing that too
            // the choice would be overwritten by the last-played level as soon as Play was pressed.
            PlayerPrefs.SetInt("SavedCurrentLevelIndex", index);
            PlayerPrefs.Save();

            if (Application.isPlaying)
            {
                manager.LoadLevel(index);
            }
        }

        private void DrawSelectedLevelEditor(LevelDataSO level)
        {
            SerializedObject so = new SerializedObject(level);
            so.Update();

            // TOP PROMINENT SAVE & APPLY BUTTON
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Seviye {level.levelNumber} Düzenleyici", EditorStyles.boldLabel);
            
            Color pBgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
            if (GUILayout.Button("💾 DEĞİŞİKLİKLERİ KAYDET VE SAHNEYE UYGULA", GUILayout.Height(32)))
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
                ApplyLevelToActiveScene(level);
                ShowNotification(new GUIContent($"✅ Seviye {level.levelNumber} Başarıyla Kaydedildi!"));
            }
            GUI.backgroundColor = pBgColor;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(so.FindProperty("levelNumber"), new GUIContent("Seviye Numarası"));
            EditorGUILayout.PropertyField(so.FindProperty("levelTitle"), new GUIContent("Seviye Başlığı"));
            EditorGUILayout.PropertyField(so.FindProperty("timeLimit"), new GUIContent("Süre Sınırı (Saniye)"));
            EditorGUILayout.PropertyField(so.FindProperty("foodTargetSize"), new GUIContent("Obje Hedef Ölçeği (Varsayılan 0.55):"));

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🔒 Siyah Kilitli Obje Mekaniği (Black Lock)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("enableBlackLockedObjects"), new GUIContent("Siyah Kilitli Objeler Olsun Mu?"));

            if (so.FindProperty("enableBlackLockedObjects").boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Bu seviyede belirlenen sayıda obje simsiyah görünecek ve üzerlerinde sayaç text'i yer alacaktır. " +
                    "Oyuncu diğer objeleri slota her koyduğunda sayaç 1 düşer. Sayaç 0 olduğunda obje orijinal rengine döner ve tıklanabilir hale gelir.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(so.FindProperty("blackObjectCount"), new GUIContent("Kaç Obje Siyah Olsun?"));
                EditorGUILayout.PropertyField(so.FindProperty("blackObjectUnlockCount"), new GUIContent("Açılma Sayacı (İlk Text Değeri)"));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUILayout.Label("🤖 Bukalemun Mecha & Kamuflaj Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("enableCamouflageMecha"), new GUIContent("Mecha Karakteri Olsun Mu?"));

            if (so.FindProperty("enableCamouflageMecha").boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Bu seviyedeki tüm Mechalar aşağıda kart/pencere olarak listelenmiştir. İstediğiniz Mecha kartına tıklayarak alttan detaylarını açabilir ve sahnede nasıl durduğunu önizleyebilirsiniz.",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // Upper Action Bar
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.3f, 0.85f, 1.0f);
                if (GUILayout.Button("➕ Yeni Mecha Ekle", GUILayout.Height(30)))
                {
                    AddNewMechaToLevel(so, level);
                }

                var allEntries = level.GetAllMechaEntries();
                if (allEntries.Count > 1)
                {
                    GUI.backgroundColor = new Color(0.1f, 0.65f, 0.95f);
                    if (GUILayout.Button($"🌐 TÜM MECHALARI ({allEntries.Count} Adet) SAHNEDE YAN YANA GÖSTER", GUILayout.Height(30)))
                    {
                        GenerateAllScene3DPreviews(level);
                    }
                }

                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
                if (GUILayout.Button("📦 GLB -> FBX Converter", GUILayout.Width(150), GUILayout.Height(30)))
                {
                    MechaFind3D.EditorTools.GLBToFBXConverterTool.ConvertMechaGLBToFBX();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8);

                // Render each mecha as a collapsible window card
                int mechaToRemoveIdx = -1;
                int totalMechas = 1 + (level.additionalMechas != null ? level.additionalMechas.Count : 0);
                for (int m = 0; m < totalMechas; m++)
                {
                    if (DrawMechaCard(so, level, m))
                    {
                        mechaToRemoveIdx = m - 1; // additionalMechas index is m - 1
                    }
                }

                if (mechaToRemoveIdx >= 0)
                {
                    SerializedProperty mechasProp = so.FindProperty("additionalMechas");
                    if (mechaToRemoveIdx < mechasProp.arraySize)
                    {
                        mechasProp.DeleteArrayElementAtIndex(mechaToRemoveIdx);
                        mechaEntryFoldouts.Remove(mechaToRemoveIdx + 1);
                        so.ApplyModifiedProperties();
                    }
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🎯 Match-3 Seviye Hedefleri Yapılandırması", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Her hedef nesnesi için kaç adet üretileceğini (-3 / +3) butonlarıyla kolayca belirleyebilirsiniz. Obje Kütüphanesinden de '➕ Hedef Ekle' butonuna basarak tek tıkla ekleyebilirsiniz.", MessageType.Info);

            int goalToRemoveIdx = -1;
            SerializedProperty goalsProp = so.FindProperty("targetGoals");
            for (int i = 0; i < goalsProp.arraySize; i++)
            {
                SerializedProperty elem = goalsProp.GetArrayElementAtIndex(i);
                SerializedProperty itemProp = elem.FindPropertyRelative("itemData");
                SerializedProperty countProp = elem.FindPropertyRelative("requiredCount");

                ItemDataSO targetItem = itemProp.objectReferenceValue as ItemDataSO;

                EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(52));
                
                // 1. 2D Thumbnail Image
                if (targetItem != null && targetItem.prefab != null)
                {
                    Texture2D thumb = AssetPreview.GetAssetPreview(targetItem.prefab);
                    if (thumb != null)
                    {
                        GUILayout.Label(thumb, GUILayout.Width(46), GUILayout.Height(46));
                    }
                    else
                    {
                        GUILayout.Box("3D", GUILayout.Width(46), GUILayout.Height(46));
                    }
                }
                else
                {
                    GUILayout.Box("❌", GUILayout.Width(46), GUILayout.Height(46));
                }

                // 2. Object Picker & Name
                EditorGUILayout.BeginVertical(GUILayout.Width(230));
                GUILayout.Label($"Hedef #{i + 1}: {(targetItem != null ? targetItem.displayName : "Seçilmedi")}", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(itemProp, GUIContent.none);
                EditorGUILayout.EndVertical();
                
                GUILayout.FlexibleSpace();

                // 3. Large, Clear Adet (Quantity) Control Group
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                Color prevColor = GUI.backgroundColor;

                GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f);
                if (GUILayout.Button("-3", GUILayout.Width(35), GUILayout.Height(28)))
                {
                    countProp.intValue = Mathf.Max(3, countProp.intValue - 3);
                }

                GUI.backgroundColor = Color.white;
                GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    normal = { textColor = new Color(0.2f, 0.85f, 1.0f) }
                };
                GUILayout.Label($"  {countProp.intValue} Adet  ", countStyle, GUILayout.Height(28));

                GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
                if (GUILayout.Button("+3", GUILayout.Width(35), GUILayout.Height(28)))
                {
                    countProp.intValue += 3;
                }

                GUI.backgroundColor = prevColor;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                // 4. Remove Button
                GUI.backgroundColor = new Color(1.0f, 0.3f, 0.3f);
                if (GUILayout.Button("🗑️ Kaldır", GUILayout.Width(65), GUILayout.Height(30)))
                {
                    goalToRemoveIdx = i;
                }
                GUI.backgroundColor = prevColor;

                EditorGUILayout.EndHorizontal();
            }

            if (goalToRemoveIdx >= 0 && goalToRemoveIdx < goalsProp.arraySize)
            {
                goalsProp.DeleteArrayElementAtIndex(goalToRemoveIdx);
            }

            GUI.backgroundColor = new Color(0.3f, 0.85f, 1.0f);
            if (GUILayout.Button("➕ Yeni Hedef Satırı Ekle", GUILayout.Height(32)))
            {
                goalsProp.arraySize++;
                SerializedProperty newElem = goalsProp.GetArrayElementAtIndex(goalsProp.arraySize - 1);
                newElem.FindPropertyRelative("requiredCount").intValue = 6;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            GUILayout.Label("📦 Yığın Engel/Dolgu Objeleri (Filler Items)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Seviyede hedef olmayan ama yığında kalabalık yaratacak diğer objeler.", MessageType.None);

            SerializedProperty fillersProp = so.FindProperty("fillerItems");
            EditorGUILayout.PropertyField(fillersProp, new GUIContent("Dolgu Objeleri"), true);

            // Detailed Summary Breakdown
            EditorGUILayout.Space(10);
            int totalGoalItems = level.GetTotalGoalRequiredCount();
            int totalFillerCount = (level.fillerItems != null) ? level.fillerItems.Count * 3 : 0;
            int exactTotalPile = totalGoalItems + totalFillerCount;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("📊 Seviye Obje Dağılım Özeti", EditorStyles.boldLabel);
            GUILayout.Label($"🎯 Toplam Hedef Obje Sayısı: {totalGoalItems} Adet", EditorStyles.miniLabel);
            GUILayout.Label($"📦 Toplam Dolgu Obje Sayısı: {totalFillerCount} Adet ({level.fillerItems?.Count ?? 0} Tür)", EditorStyles.miniLabel);
            
            GUIStyle summaryStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.3f, 0.85f, 1.0f) }
            };
            EditorGUILayout.Space(10);
            Color pBgBottom = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
            if (GUILayout.Button("💾 DEĞİŞİKLİKLERİ KAYDET VE SAHNEYE UYGULA", GUILayout.Height(36)))
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
                ApplyLevelToActiveScene(level);
                ShowNotification(new GUIContent($"✅ Seviye {level.levelNumber} Başarıyla Kaydedildi ve Sahnede Güncellendi!"));
            }
            GUI.backgroundColor = pBgBottom;
            EditorGUILayout.EndVertical();

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// Editor for LevelDataSO.additionalMechas - every mecha beyond the primary one configured just
        /// above. Each entry gets its own collapsible box with the exact same fields as the primary mecha
        /// (LevelDataSO.MechaSpawnEntry mirrors those fields 1:1), since MechaRagdollSpawner spawns every
        /// entry through the identical per-mecha path (SpawnOneMecha) - there's no meaningful difference
        /// between "the level's own mecha" and "an additional one" at spawn time, only in how they're stored.
        /// </summary>
        /// <summary>
        /// Renders one Mecha card (window foldout). Handles both Mecha #1 (bound to LevelDataSO primary fields)
        /// and Mecha #2+ (bound to LevelDataSO.additionalMechas elements).
        /// </summary>
        private bool DrawMechaCard(SerializedObject so, LevelDataSO level, int mechaIdx)
        {
            bool deleteRequested = false;
            var entries = level.GetAllMechaEntries();
            MechaSpawnEntry entry = mechaIdx < entries.Count ? entries[mechaIdx] : null;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Default foldout state: Mecha 1 (index 0) starts expanded by default, others start collapsed
            if (!mechaEntryFoldouts.ContainsKey(mechaIdx))
            {
                mechaEntryFoldouts[mechaIdx] = (mechaIdx == 0);
            }
            bool expanded = mechaEntryFoldouts[mechaIdx];

            ItemDataSO hostSO = entry != null ? entry.hostItemSO : null;
            string hostName = hostSO != null ? hostSO.displayName : "Henüz Seçilmedi";
            string title = $"🤖 Mecha #{mechaIdx + 1} — {hostName}";

            // Header layout: Reserve clean 24px height, split into left Foldout and right Delete button
            Rect headerRect = EditorGUILayout.GetControlRect(true, 24);
            float buttonWidth = 75f;
            float foldoutWidth = mechaIdx > 0 ? headerRect.width - buttonWidth - 5f : headerRect.width;

            Rect foldoutRect = new Rect(headerRect.x, headerRect.y + 2, foldoutWidth, 20);
            expanded = EditorGUI.Foldout(foldoutRect, expanded, title, true, EditorStyles.foldoutHeader);
            mechaEntryFoldouts[mechaIdx] = expanded;

            // Sleek Delete button neatly placed on far right (no text overlap)
            if (mechaIdx > 0)
            {
                Rect buttonRect = new Rect(headerRect.xMax - buttonWidth, headerRect.y + 1, buttonWidth, 20);
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1.0f, 0.3f, 0.3f);
                if (GUI.Button(buttonRect, "🗑️ Kaldır", EditorStyles.miniButton))
                {
                    deleteRequested = true;
                }
                GUI.backgroundColor = prevBg;
            }

            if (expanded)
            {
                EditorGUILayout.Space(4);
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();

                if (mechaIdx == 0)
                {
                    // Bind to Primary Mecha fields on LevelDataSO
                    EditorGUILayout.PropertyField(so.FindProperty("customMechaPrefab"), new GUIContent("Özel Mecha Model Prefab'ı:"));
                    EditorGUILayout.PropertyField(so.FindProperty("targetPivot"), new GUIContent("Yerleşeceği Pivot Noktası:"));
                    EditorGUILayout.PropertyField(so.FindProperty("hostItemSO"), new GUIContent("Yapışacağı Hedef Obje (ItemData):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaHostKeyword"), new GUIContent("Hedef Obje Arama İnce Ayarı:"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaWorldSize"), new GUIContent("Mecha Boyu (dünya birimi, 0=oran kullan):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaScaleRatio"), new GUIContent("Mecha Ölçek Oranı (yalnızca Boy=0 ise):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaWrapAmount"), new GUIContent("Objeye Sarılma (0=düz, 1=tam sarılır):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaOpacity"), new GUIContent("Mecha Saydamlığı (0.55 = %55):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaLocalOffset"), new GUIContent("Mecha Konum Öteleme (Offset):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaRotationOffset"), new GUIContent("Mecha Dönüş Açısı (Euler):"));
                }
                else
                {
                    // Bind to additionalMechas[addIdx] fields
                    SerializedProperty mechasProp = so.FindProperty("additionalMechas");
                    int addIdx = mechaIdx - 1;
                    if (addIdx < mechasProp.arraySize)
                    {
                        SerializedProperty elem = mechasProp.GetArrayElementAtIndex(addIdx);
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("customMechaPrefab"), new GUIContent("Özel Mecha Model Prefab'ı:"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("targetPivot"), new GUIContent("Yerleşeceği Pivot Noktası:"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("hostItemSO"), new GUIContent("Yapışacağı Hedef Obje (ItemData):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaHostKeyword"), new GUIContent("Hedef Obje Arama İnce Ayarı:"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaWorldSize"), new GUIContent("Mecha Boyu (dünya birimi, 0=oran kullan):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaScaleRatio"), new GUIContent("Mecha Ölçek Oranı (yalnızca Boy=0 ise):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaWrapAmount"), new GUIContent("Objeye Sarılma (0=düz, 1=tam sarılır):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaOpacity"), new GUIContent("Mecha Saydamlığı (0.55 = %55):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaLocalOffset"), new GUIContent("Mecha Konum Öteleme (Offset):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaRotationOffset"), new GUIContent("Mecha Dönüş Açısı (Euler):"));
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    var updatedEntries = level.GetAllMechaEntries();
                    if (mechaIdx < updatedEntries.Count)
                    {
                        LiveUpdateScene3DPreview(level, updatedEntries[mechaIdx], mechaIdx);
                    }
                }

                EditorGUILayout.Space(6);

                // 3D Scene View Preview Button
                Color btnBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.8f, 1.0f);
                if (GUILayout.Button($"🔍 3D Canlı Önizlemeyi Sahnede Göster ve Odaklan (Mecha #{mechaIdx + 1})", GUILayout.Height(30)))
                {
                    var updatedEntries = level.GetAllMechaEntries();
                    if (mechaIdx < updatedEntries.Count)
                    {
                        GenerateScene3DPreview(level, updatedEntries[mechaIdx], mechaIdx);
                    }
                }
                GUI.backgroundColor = btnBg;

                // Rich Thumbnail & Size Info Box
                var currentEntries = level.GetAllMechaEntries();
                if (mechaIdx < currentEntries.Count)
                {
                    DrawMechaSizeInfoBox(currentEntries[mechaIdx]);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            return deleteRequested;
        }

        private void AddNewMechaToLevel(SerializedObject so, LevelDataSO level)
        {
            SerializedProperty mechasProp = so.FindProperty("additionalMechas");
            mechasProp.arraySize++;
            SerializedProperty newElem = mechasProp.GetArrayElementAtIndex(mechasProp.arraySize - 1);
            newElem.FindPropertyRelative("customMechaPrefab").objectReferenceValue = null;
            newElem.FindPropertyRelative("targetPivot").enumValueIndex = (int)MechaPivotSelection.PivotTop;
            newElem.FindPropertyRelative("hostItemSO").objectReferenceValue = null;
            newElem.FindPropertyRelative("mechaHostKeyword").stringValue = "";
            newElem.FindPropertyRelative("mechaScaleRatio").floatValue = 0.25f;
            newElem.FindPropertyRelative("mechaWrapAmount").floatValue = 0f;
            newElem.FindPropertyRelative("mechaWorldSize").floatValue = 0.5f;
            newElem.FindPropertyRelative("mechaOpacity").floatValue = 0.22f;
            newElem.FindPropertyRelative("mechaLocalOffset").vector3Value = Vector3.zero;
            newElem.FindPropertyRelative("mechaRotationOffset").vector3Value = new Vector3(90f, 0f, 0f);

            int newMechaIndex = mechasProp.arraySize; // index 0 is Primary Mecha, index arraySize is the new entry
            mechaEntryFoldouts[newMechaIndex] = true;
            so.ApplyModifiedProperties();
        }

        private void DrawItemLibraryTab()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Kayıtlı Obje Prefab Kütüphanesi", EditorStyles.boldLabel);
            itemSearchQuery = EditorGUILayout.TextField("Ara:", itemSearchQuery, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            Color oldBgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f);
            if (GUILayout.Button("⚡ 1-Tık: Tüm Objelere ve Mechaya Collider & 4 Edge Pivot Ekle", GUILayout.Height(34)))
            {
                PrefabColliderPivotProcessor.ProcessAllLibraryPrefabs();
            }
            GUI.backgroundColor = oldBgColor;

            EditorGUILayout.Space(5);

            ItemDataSO[] items = FindAllAssets<ItemDataSO>();
            if (items == null || items.Length == 0)
            {
                EditorGUILayout.HelpBox("Henüz hiçbir ItemDataSO varlığı bulunamadı. Toplu Prefab Yükleyici sekmesinden 3D prefablarınızı tek tıkla yükleyebilirsiniz!", MessageType.Info);
                return;
            }

            List<ItemDataSO> validItems = new List<ItemDataSO>();
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(itemSearchQuery) && !item.displayName.ToLower().Contains(itemSearchQuery.ToLower()))
                    continue;
                validItems.Add(item);
            }

            if (validItems.Count == 0)
            {
                EditorGUILayout.HelpBox("Arama kriterlerine uygun obje bulunamadı.", MessageType.Info);
                return;
            }

            int columns = 3;
            for (int i = 0; i < validItems.Count; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    int idx = i + c;
                    if (idx < validItems.Count)
                    {
                        DrawItemCard(validItems[idx]);
                    }
                    else
                    {
                        GUILayout.Box("", GUIStyle.none, GUILayout.Width(220), GUILayout.Height(130));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawItemCard(ItemDataSO item)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(220), GUILayout.Height(130));

            EditorGUILayout.BeginHorizontal();
            Texture2D preview = item.prefab != null ? AssetPreview.GetAssetPreview(item.prefab) : null;
            if (preview != null)
            {
                GUILayout.Label(preview, GUILayout.Width(50), GUILayout.Height(50));
            }
            else
            {
                GUILayout.Label("3D", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(50), GUILayout.Height(50));
            }

            EditorGUILayout.BeginVertical();
            GUILayout.Label(item.displayName, EditorStyles.boldLabel);
            GUILayout.Label($"ID: {item.GetEffectiveItemId()}", EditorStyles.miniLabel);
            item.targetColor = EditorGUILayout.ColorField(item.targetColor);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            int targetLvlNum = selectedLevel != null ? selectedLevel.levelNumber : 1;
            
            EditorGUILayout.BeginHorizontal();
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button($"➕ Hedef (S{targetLvlNum})", GUILayout.Height(24)))
            {
                AddItemToSelectedLevelGoals(item);
            }
            GUI.backgroundColor = new Color(0.95f, 0.7f, 0.2f);
            if (GUILayout.Button($"📦 Dolgu", GUILayout.Height(24)))
            {
                AddItemToSelectedLevelFillers(item);
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void AddItemToSelectedLevelGoals(ItemDataSO item)
        {
            if (item == null) return;
            if (selectedLevel == null)
            {
                LevelDataSO[] levels = FindAllAssets<LevelDataSO>();
                if (levels != null && levels.Length > 0) selectedLevel = levels[0];
                else CreateNewLevelAsset();
            }

            if (selectedLevel == null) return;

            bool found = false;
            foreach (var req in selectedLevel.targetGoals)
            {
                if (req.itemData == item)
                {
                    req.requiredCount += 3;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                selectedLevel.targetGoals.Add(new LevelGoalRequirement
                {
                    itemData = item,
                    requiredCount = 6
                });
            }

            EditorUtility.SetDirty(selectedLevel);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent($"🎯 {item.displayName} Seviye {selectedLevel.levelNumber} Hedeflerine Eklendi!"));
            Debug.Log($"🎯 {item.displayName} Seviye {selectedLevel.levelNumber} hedeflerine eklendi.");
        }

        private void AddItemToSelectedLevelFillers(ItemDataSO item)
        {
            if (item == null) return;
            if (selectedLevel == null)
            {
                LevelDataSO[] levels = FindAllAssets<LevelDataSO>();
                if (levels != null && levels.Length > 0) selectedLevel = levels[0];
                else CreateNewLevelAsset();
            }

            if (selectedLevel == null) return;

            if (!selectedLevel.fillerItems.Contains(item))
            {
                selectedLevel.fillerItems.Add(item);
            }

            EditorUtility.SetDirty(selectedLevel);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent($"📦 {item.displayName} Seviye {selectedLevel.levelNumber} Dolgularına Eklendi!"));
            Debug.Log($"📦 {item.displayName} Seviye {selectedLevel.levelNumber} dolgularına eklendi.");
        }

        // ====================================================================
        // TAB 3: PREFAB BATCH IMPORTER
        // ====================================================================
        private void DrawPrefabBatchImporterTab()
        {
            GUILayout.Label("🚀 Toplu Prefab Yükleyici (Batch Importer)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Kendi 3D prefablarınızı (.prefab, .fbx) aşağıdaki alana sürükleyip bırakarak veya klasör seçerek saniyeler içinde oyun nesnesine dönüştürebilirsiniz.", MessageType.Info);

            EditorGUILayout.Space(10);

            // Drag and drop dropzone
            Rect dropArea = GUILayoutUtility.GetRect(0f, 100f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "📥 Prefab Dosyalarını Buraya Sürükleyin ve Bırakın\n(veya aşağıdaki butonu kullanın)", GUI.skin.box);

            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (dropArea.Contains(evt.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is GameObject go)
                            {
                                CreateItemDataFromPrefab(go);
                            }
                        }
                    }
                    evt.Use();
                }
            }

            EditorGUILayout.Space(15);
            if (GUILayout.Button("📂 Projedeki Tüm Prefabları Tara & Otomatik Dönüştür (Food Kit vb.)", GUILayout.Height(35)))
            {
                ScanAndImportFoodKitPrefabs();
            }
        }

        private static void CreateItemDataFromPrefab(GameObject prefab)
        {
            if (prefab == null) return;

            string folderPath = "Assets/LevelData/Items";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            string assetPath = $"{folderPath}/Item_{prefab.name}.asset";
            ItemDataSO existing = AssetDatabase.LoadAssetAtPath<ItemDataSO>(assetPath);
            if (existing != null)
            {
                existing.prefab = prefab;
                existing.itemId = prefab.name.ToLowerInvariant();
                existing.displayName = prefab.name;
                EditorUtility.SetDirty(existing);
                Debug.Log($"🔄 Güncellendi: {assetPath}");
                return;
            }

            ItemDataSO newItem = ScriptableObject.CreateInstance<ItemDataSO>();
            newItem.itemId = prefab.name.ToLowerInvariant();
            newItem.displayName = prefab.name;
            newItem.prefab = prefab;
            newItem.targetColor = GetRandomPaletteColor();

            AssetDatabase.CreateAsset(newItem, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"✨ Yeni Obje Oluşturuldu: {assetPath}");
        }

        private static void ScanAndImportFoodKitPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/kenney_food-kit", "Assets/Prefabs" });
            int createdCount = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    CreateItemDataFromPrefab(prefab);
                    createdCount++;
                }
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Başarılı", $"{createdCount} adet prefab başarıyla Obje Kütüphanesine eklendi!", "Tamam");
        }

        /// <summary>
        /// Previews ONE mecha entry in the Scene view. <paramref name="mechaIndex"/> (0 = the level's own
        /// primary mecha, 1+ = additionalMechas) names the preview object uniquely per mecha, so previewing
        /// mecha #2 does not destroy mecha #1's preview - clicking through every mecha's own button leaves
        /// all of them standing in the scene at once, next to each other, exactly what "mechaları nasıl
        /// duruyor görmek" (see how the mechas are positioned) needs.
        /// </summary>
        private static void GenerateScene3DPreview(LevelDataSO level, MechaSpawnEntry entry, int mechaIndex)
        {
            if (level == null || entry == null || entry.hostItemSO == null || entry.hostItemSO.prefab == null)
            {
                EditorUtility.DisplayDialog("Uyarı", "Lütfen önce bu mecha için geçerli bir Hedef Obje (ItemData) seçin!", "Tamam");
                return;
            }

            string previewName = mechaIndex == 0 ? "Mecha_3D_Preview_Instance" : $"Mecha_3D_Preview_Instance_{mechaIndex}";

            GameObject oldPreview = GameObject.Find(previewName);
            if (oldPreview != null) DestroyImmediate(oldPreview);

            GameObject hostInstance = Instantiate(entry.hostItemSO.prefab);
            hostInstance.name = previewName;
            // Additional mechas' previews spread out along X so they don't all stack on top of each other -
            // mecha #1 keeps world origin (matches its pre-multi-mecha position exactly), #2/#3/... offset.
            hostInstance.transform.position = new Vector3(mechaIndex * 1.5f, 0f, 0f);
            hostInstance.transform.rotation = Quaternion.identity;

            // Load mecha model robustly
            GameObject mechaPrefab = entry.customMechaPrefab;
            if (mechaPrefab == null)
            {
                mechaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/meccha chameleon.glb");
            }
            if (mechaPrefab == null)
            {
                string[] guids = AssetDatabase.FindAssets("meccha");
                if (guids != null && guids.Length > 0)
                {
                    mechaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (mechaPrefab == null)
            {
                EditorUtility.DisplayDialog("Hata", "Mecha modeli 'Assets/Prefabs/meccha chameleon.glb' bulunamadı!", "Tamam");
                return;
            }

            GameObject mechaInst = Instantiate(mechaPrefab);
            mechaInst.name = $"Preview_Mecha_Silhouette_{mechaIndex}";
            // Match play EXACTLY: PhysicsObjectSpawner normalizes each food to maxDim = Max(1.10, foodTargetSize)
            // (there's a hard 1.10 floor), NOT raw foodTargetSize. Scale the preview host to that same size so
            // the mecha (sized absolutely below) reads identically in preview and gameplay.
            {
                float targetHostMax = Mathf.Max(1.10f, level.foodTargetSize);
                Renderer[] hr = hostInstance.GetComponentsInChildren<Renderer>();
                if (hr.Length > 0)
                {
                    Bounds hb = hr[0].bounds;
                    for (int i = 1; i < hr.Length; i++) hb.Encapsulate(hr[i].bounds);
                    float maxDim = Mathf.Max(hb.size.x, Mathf.Max(hb.size.y, hb.size.z));
                    if (maxDim > 1e-4f)
                        hostInstance.transform.localScale *= (targetHostMax / maxDim);
                }
            }

            ChameleonCamouflage.EmbedMechaInHostObject(
                mechaInst,
                hostInstance,
                entry.mechaScaleRatio,
                entry.mechaOpacity,
                entry.mechaLocalOffset,
                entry.mechaRotationOffset,
                entry.mechaWorldSize,
                entry.targetPivot,
                entry.mechaWrapAmount
            );

            Selection.activeGameObject = hostInstance;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            Debug.Log($"👁️ Mecha #{mechaIndex + 1} 3D Canlı Önizlemesi Sahnede (Scene View) Odaklandı! (Mecha: {mechaInst.name})");
        }

        private static void LiveUpdateScene3DPreview(LevelDataSO level, MechaSpawnEntry entry, int mechaIndex)
        {
            if (level == null || entry == null || entry.hostItemSO == null || entry.hostItemSO.prefab == null) return;

            // Regenerate through the single source of truth (ChameleonCamouflage.EmbedMechaInHostObject)
            // instead of duplicating its scale/pose/material math here — keeps the editor preview and the
            // runtime result from ever drifting apart.
            GenerateScene3DPreview(level, entry, mechaIndex);
            SceneView.RepaintAll();
        }

        private static void GenerateAllScene3DPreviews(LevelDataSO level)
        {
            if (level == null) return;
            var entries = level.GetAllMechaEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].hostItemSO != null)
                {
                    GenerateScene3DPreview(level, entries[i], i);
                }
            }
        }

        private void DrawMechaSizeInfoBox(MechaSpawnEntry mechaEntry)
        {
            if (mechaEntry == null || mechaEntry.hostItemSO == null)
            {
                EditorGUILayout.HelpBox("Lütfen bu mecha için geçerli bir Hedef Obje (ItemData) seçin.", MessageType.Info);
                return;
            }

            ItemDataSO item = mechaEntry.hostItemSO;
            GameObject prefab = item.prefab;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            // 1. 2D Asset Preview Thumbnail (matching screenshot)
            Texture2D thumb = prefab != null ? AssetPreview.GetAssetPreview(prefab) : null;
            if (thumb != null)
            {
                GUILayout.Label(thumb, GUILayout.Width(64), GUILayout.Height(64));
            }
            else
            {
                GUILayout.Box("3D", GUILayout.Width(64), GUILayout.Height(64));
            }

            // 2. Mesh Dimensions & Geometrical Size (matching screenshot)
            EditorGUILayout.BeginVertical();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUILayout.Label($"📐 {item.displayName} Boyut Bilgisi & Önizleme", titleStyle);

            if (prefab != null)
            {
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
                if (renderers != null && renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);

                    Vector3 size = b.size;
                    float maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

                    GUILayout.Label($"Obje Mesh Boyutları (X, Y, Z): {size.x:F2}m x {size.y:F2}m x {size.z:F2}m", EditorStyles.miniLabel);
                    GUILayout.Label($"Maksimum Obje Çapı: {maxDim:F2} birim", EditorStyles.miniLabel);

                    GUIStyle greenStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        normal = { textColor = new Color(0.2f, 0.95f, 0.4f) }
                    };

                    if (mechaEntry.mechaWorldSize > 0f)
                    {
                        GUILayout.Label($"📐 Sabit Mecha Boyutu: {mechaEntry.mechaWorldSize:F2} birim (Sabit Dünya Ölçeği)", greenStyle);
                    }
                    else
                    {
                        float calculatedSize = maxDim * mechaEntry.mechaScaleRatio;
                        GUILayout.Label($"📐 Geometrik Mecha Boyutu: {calculatedSize:F2} birim (%{mechaEntry.mechaScaleRatio * 100f:F0} oranında)", greenStyle);
                    }
                }
                else
                {
                    GUILayout.Label("Objede Renderer component'ı bulunamadı.", EditorStyles.miniLabel);
                }
            }
            else
            {
                GUILayout.Label("ItemDataSO içerisinde Prefab atanmamış.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private static void CreateNewLevelAsset()
        {
            string folderPath = "Assets/LevelData";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            LevelDataSO[] existingLevels = FindAllAssets<LevelDataSO>();
            int nextNumber = existingLevels.Length + 1;

            string assetPath = $"{folderPath}/Level_{nextNumber:D2}.asset";
            LevelDataSO newLevel = ScriptableObject.CreateInstance<LevelDataSO>();
            newLevel.levelNumber = nextNumber;
            newLevel.levelTitle = $"Seviye {nextNumber}";
            newLevel.enableCamouflageMecha = true;

            ItemDataSO[] allItems = FindAllAssets<ItemDataSO>();
            if (allItems != null && allItems.Length > 0)
            {
                ItemDataSO defaultHost = allItems[0];
                newLevel.hostItemSO = defaultHost;
                newLevel.mechaHostKeyword = defaultHost.GetEffectiveItemId();
                newLevel.targetGoals.Add(new LevelGoalRequirement
                {
                    itemData = defaultHost,
                    requiredCount = 6
                });
            }

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = newLevel;
            Debug.Log($"🎮 Yeni Seviye Varlığı Oluşturuldu: {assetPath}");
        }

        private static void ApplyLevelToActiveScene(LevelDataSO level)
        {
            if (level == null) return;

            GameObject sceneController = GameObject.Find("Physics_Scene_Controller");
            if (sceneController == null)
            {
                ScenePhysicsSetup.CreateOrSetupScene();
                sceneController = GameObject.Find("Physics_Scene_Controller");
            }

            LevelManager manager = sceneController.GetComponent<LevelManager>();
            if (manager == null) manager = sceneController.AddComponent<LevelManager>();

            // Previewing a level makes it the CURRENT one, it does not pin it as a debug override.
            // Setting debugLevelOverride here meant every preview permanently froze the game on that level -
            // ActiveLevelData returns the override ahead of currentLevelIndex, so finishing a level appeared
            // to advance while the same one kept loading. The override is cleared for the same reason.
            manager.debugLevelOverride = null;
            manager.AutoFindLevelsIfEmpty();

            int targetIdx = manager.levels != null ? manager.levels.IndexOf(level) : 0;
            if (targetIdx < 0) targetIdx = 0;
            manager.currentLevelIndex = targetIdx;
            manager.LoadLevel(targetIdx);

            ScenePhysicsSetup setup = sceneController.GetComponent<ScenePhysicsSetup>();
            if (setup != null) setup.SetupSceneEnvironment();

            Debug.Log($"✅ Seviye {level.levelNumber} sahnede aktifleştirildi ve başarıyla yüklendi!");
        }

        private static T[] FindAllAssets<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            T[] assets = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return assets;
        }

        private static Color GetRandomPaletteColor()
        {
            Color[] colors = new Color[]
            {
                new Color(0.95f, 0.20f, 0.20f),
                new Color(0.20f, 0.55f, 0.95f),
                new Color(0.20f, 0.85f, 0.35f),
                new Color(0.98f, 0.85f, 0.15f),
                new Color(0.65f, 0.25f, 0.90f),
                new Color(0.98f, 0.50f, 0.15f)
            };
            return colors[Random.Range(0, colors.Length)];
        }
    }
}
