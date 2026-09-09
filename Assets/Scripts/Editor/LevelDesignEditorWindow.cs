using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    public class LevelDesignEditorWindow : EditorWindow
    {
        private enum Tab { LevelManager, Templates, ItemLibrary, PrefabBatchImporter }
        private Tab selectedTab = Tab.LevelManager;

        private Vector2 scrollPos;
        private LevelDataSO selectedLevel;
        private string itemSearchQuery = "";

        // Template authoring state. Session-only: which template the "+ Yeni Seviye" button seeds from,
        // which one the Templates tab is editing, and which one filters the item library grid.
        private LevelTemplateSO newLevelTemplate;
        private LevelTemplateSO selectedTemplate;
        private LevelTemplateSO libraryFilterTemplate;

        // ---- Asset lookup caches ----
        // OnGUI runs for every repaint AND every input event, so anything it calls runs dozens of times
        // per keystroke. An AssetDatabase.FindAssets sweep per call - times 100+ item cards, each also
        // rebuilding a template's pool list and rescanning every pose preset - is what made this window
        // lag behind typing. These caches turn all of that into dictionary lookups; they are dropped
        // whenever the project changes or the window regains focus, so they cannot go stale.
        private static readonly Dictionary<System.Type, UnityEngine.Object[]> assetCache =
            new Dictionary<System.Type, UnityEngine.Object[]>();
        private static readonly Dictionary<LevelTemplateSO, HashSet<ItemDataSO>> poolCache =
            new Dictionary<LevelTemplateSO, HashSet<ItemDataSO>>();
        private static HashSet<ItemDataSO> posePresetHosts;

        // FindFirstObjectByType walks the whole scene, and OnGUI asked for the manager on every event.
        // Unity's overloaded null check makes a destroyed/unloaded manager compare equal to null, so this
        // re-finds itself automatically instead of handing back a dead reference.
        private LevelManager cachedLevelManager;

        private LevelManager GetLevelManager()
        {
            if (cachedLevelManager == null) cachedLevelManager = Object.FindFirstObjectByType<LevelManager>();
            return cachedLevelManager;
        }

        private static void InvalidateAssetCaches()
        {
            assetCache.Clear();
            poolCache.Clear();
            posePresetHosts = null;
        }

        private void OnProjectChange() => InvalidateAssetCaches();
        private void OnFocus() => InvalidateAssetCaches();

        /// <summary>Cached membership test for a template's item pool.</summary>
        private static bool PoolContains(LevelTemplateSO template, ItemDataSO item)
        {
            if (template == null || item == null) return false;
            if (!poolCache.TryGetValue(template, out HashSet<ItemDataSO> set))
            {
                set = new HashSet<ItemDataSO>(template.GetValidPool());
                poolCache[template] = set;
            }
            return set.Contains(item);
        }

        private static int PoolCount(LevelTemplateSO template)
        {
            if (template == null) return 0;
            if (!poolCache.TryGetValue(template, out HashSet<ItemDataSO> set))
            {
                set = new HashSet<ItemDataSO>(template.GetValidPool());
                poolCache[template] = set;
            }
            return set.Count;
        }

        // Expand/collapse state per additionalMechas entry, keyed by list index. Session-only (not saved
        // with the asset) - purely so re-drawing the same OnGUI frame doesn't reset every foldout shut.
        private readonly Dictionary<int, bool> mechaEntryFoldouts = new Dictionary<int, bool>();

        // Pivot is a re-basing switch, not a positioning knob, so it lives behind its own foldout.
        private readonly Dictionary<int, bool> pivotFoldouts = new Dictionary<int, bool>();

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
                case Tab.Templates:
                    DrawTemplatesTab();
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
            if (GUILayout.Toggle(selectedTab == Tab.Templates, "🍝 Şablonlar (Templates)", "LargeButton", GUILayout.Height(35)))
                selectedTab = Tab.Templates;
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

            // Seed picker for the create button. The level is filled from the template once and then goes
            // its own way - it keeps no live link, so editing the template later leaves it alone.
            LevelTemplateSO[] allTemplates = FindAllAssets<LevelTemplateSO>();
            if (allTemplates != null && allTemplates.Length > 0)
            {
                var names = new List<string> { "Şablonsuz (boş seviye)" };
                int chosenIdx = 0;
                for (int t = 0; t < allTemplates.Length; t++)
                {
                    if (allTemplates[t] == null) continue;
                    names.Add(allTemplates[t].GetDisplayName());
                    if (allTemplates[t] == newLevelTemplate) chosenIdx = names.Count - 1;
                }

                int picked = EditorGUILayout.Popup("Şablon:", chosenIdx, names.ToArray());
                newLevelTemplate = picked <= 0 ? null : allTemplates[picked - 1];
            }
            else
            {
                newLevelTemplate = null;
                EditorGUILayout.HelpBox("Henüz şablon yok. 🍝 Şablonlar sekmesinden tema oluşturabilirsin.", MessageType.None);
            }

            string createLabel = newLevelTemplate != null
                ? $"+ Yeni Seviye ({newLevelTemplate.GetDisplayName()})"
                : "+ Yeni Seviye Oluştur";
            if (GUILayout.Button(createLabel, GUILayout.Height(30)))
            {
                LevelDataSO created = CreateNewLevelAsset(newLevelTemplate);
                if (created != null) selectedLevel = created;
            }

            EditorGUILayout.Space(5);

            LevelDataSO[] levels = FindAllAssets<LevelDataSO>();
            System.Array.Sort(levels, (a, b) => a.levelNumber.CompareTo(b.levelNumber));

            LevelManager manager = GetLevelManager();
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
            LevelManager manager = GetLevelManager();
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

            DrawLevelTemplateBox(so, level);

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
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🪙 Booster & Coin Sistemi", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Booster butonları (Trash / Undo ve Reveal) artık seviye kilitleri yerine Coin ile çalışmaktadır.\n" +
                $"Mevcut Oyuncu Coini: {CoinManager.Coins}",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("➕ 10 Coin Ekle"))
            {
                CoinManager.AddCoins(10);
                var uiMgr = Object.FindFirstObjectByType<CanvasUIDesignManager>();
                if (uiMgr != null) uiMgr.RefreshCoinUI();
            }
            if (GUILayout.Button("0️⃣ Coin Sıfırla"))
            {
                CoinManager.Coins = 0;
                var uiMgr = Object.FindFirstObjectByType<CanvasUIDesignManager>();
                if (uiMgr != null) uiMgr.RefreshCoinUI();
            }
            if (GUILayout.Button("🔄 10 Yap"))
            {
                CoinManager.ResetCoins(10);
                var uiMgr = Object.FindFirstObjectByType<CanvasUIDesignManager>();
                if (uiMgr != null) uiMgr.RefreshCoinUI();
            }
            EditorGUILayout.EndHorizontal();

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
                    DrawPivotField(so.FindProperty("targetPivot"), mechaIdx);
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(so.FindProperty("hostItemSO"), new GUIContent("Yapışacağı Hedef Obje (ItemData):"));
                    if (EditorGUI.EndChangeCheck()) AutoApplyHostPreset(so, level, mechaIdx);
                    EditorGUILayout.PropertyField(so.FindProperty("mechaHostKeyword"), new GUIContent("Hedef Obje Arama İnce Ayarı:"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaWorldSize"), new GUIContent("Mecha Boyu (dünya birimi, 0=oran kullan):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaScaleRatio"), new GUIContent("Mecha Ölçek Oranı (yalnızca Boy=0 ise):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaWrapAmount"), new GUIContent("Objeye Sarılma (0=düz, 1=tam sarılır):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaOpacity"), new GUIContent("Mecha Saydamlığı (0.55 = %55):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaLocalOffset"), new GUIContent("Mecha Konum Öteleme (Offset):"));
                    EditorGUILayout.PropertyField(so.FindProperty("mechaRotationOffset"), new GUIContent("Mecha Dönüş Açısı (Euler):"));
                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(so.FindProperty("isStickyMecha"), new GUIContent("Yapışkan Mecha:"));
                    if (so.FindProperty("isStickyMecha").boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(so.FindProperty("stickyJumpInterval"), new GUIContent("Atlama Aralığı (sn):"));
                        EditorGUILayout.PropertyField(so.FindProperty("stickyMaxJumps"), new GUIContent("Maks Atlama Sayısı:"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(so.FindProperty("isMagnetMecha"), new GUIContent("Mıknatıs Mecha:"));
                    if (so.FindProperty("isMagnetMecha").boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(so.FindProperty("magnetRadius"), new GUIContent("Çekim Yarıçapı:"));
                        EditorGUILayout.PropertyField(so.FindProperty("magnetForce"), new GUIContent("Çekim Kuvveti:"));
                        EditorGUILayout.PropertyField(so.FindProperty("magnetMaxObjects"), new GUIContent("Maks Çekilen Obje:"));
                        EditorGUI.indentLevel--;
                    }
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
                        DrawPivotField(elem.FindPropertyRelative("targetPivot"), mechaIdx);
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("hostItemSO"), new GUIContent("Yapışacağı Hedef Obje (ItemData):"));
                        if (EditorGUI.EndChangeCheck()) AutoApplyHostPreset(so, level, mechaIdx);
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaHostKeyword"), new GUIContent("Hedef Obje Arama İnce Ayarı:"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaWorldSize"), new GUIContent("Mecha Boyu (dünya birimi, 0=oran kullan):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaScaleRatio"), new GUIContent("Mecha Ölçek Oranı (yalnızca Boy=0 ise):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaWrapAmount"), new GUIContent("Objeye Sarılma (0=düz, 1=tam sarılır):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaOpacity"), new GUIContent("Mecha Saydamlığı (0.55 = %55):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaLocalOffset"), new GUIContent("Mecha Konum Öteleme (Offset):"));
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("mechaRotationOffset"), new GUIContent("Mecha Dönüş Açısı (Euler):"));
                        EditorGUILayout.Space(4);
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("isStickyMecha"), new GUIContent("Yapışkan Mecha:"));
                        if (elem.FindPropertyRelative("isStickyMecha").boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(elem.FindPropertyRelative("stickyJumpInterval"), new GUIContent("Atlama Aralığı (sn):"));
                            EditorGUILayout.PropertyField(elem.FindPropertyRelative("stickyMaxJumps"), new GUIContent("Maks Atlama Sayısı:"));
                            EditorGUI.indentLevel--;
                        }
                        EditorGUILayout.PropertyField(elem.FindPropertyRelative("isMagnetMecha"), new GUIContent("Mıknatıs Mecha:"));
                        if (elem.FindPropertyRelative("isMagnetMecha").boolValue)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(elem.FindPropertyRelative("magnetRadius"), new GUIContent("Çekim Yarıçapı:"));
                            EditorGUILayout.PropertyField(elem.FindPropertyRelative("magnetForce"), new GUIContent("Çekim Kuvveti:"));
                            EditorGUILayout.PropertyField(elem.FindPropertyRelative("magnetMaxObjects"), new GUIContent("Maks Çekilen Obje:"));
                            EditorGUI.indentLevel--;
                        }
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

                // Bone Override UI
                DrawBoneOverrideSection(so, level, mechaIdx);

                EditorGUILayout.Space(6);

                // Pose Preset UI
                DrawPosePresetSection(so, level, mechaIdx);

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
            newElem.FindPropertyRelative("mechaWorldSize").floatValue = 1f;
            newElem.FindPropertyRelative("mechaOpacity").floatValue = 0.22f;
            newElem.FindPropertyRelative("mechaLocalOffset").vector3Value = Vector3.zero;
            newElem.FindPropertyRelative("mechaRotationOffset").vector3Value = new Vector3(90f, 0f, 0f);

            int newMechaIndex = mechasProp.arraySize; // index 0 is Primary Mecha, index arraySize is the new entry
            mechaEntryFoldouts[newMechaIndex] = true;
            so.ApplyModifiedProperties();
        }

        // ====================================================================
        // TAB 2: LEVEL TEMPLATES (THEMES)
        // ====================================================================
        private void DrawTemplatesTab()
        {
            EditorGUILayout.BeginHorizontal();

            // Left sidebar: template list
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(240));
            GUILayout.Label("Şablonlar", EditorStyles.boldLabel);

            if (GUILayout.Button("+ Yeni Şablon Oluştur", GUILayout.Height(30)))
            {
                LevelTemplateSO created = CreateNewTemplateAsset();
                if (created != null) selectedTemplate = created;
            }

            EditorGUILayout.Space(5);

            LevelTemplateSO[] templates = FindAllAssets<LevelTemplateSO>();
            System.Array.Sort(templates, (a, b) => string.Compare(
                a != null ? a.GetDisplayName() : "", b != null ? b.GetDisplayName() : "", System.StringComparison.OrdinalIgnoreCase));

            foreach (LevelTemplateSO template in templates)
            {
                if (template == null) continue;

                EditorGUILayout.BeginHorizontal();
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = (selectedTemplate == template) ? new Color(0.3f, 0.8f, 1.0f) : template.themeColor;
                if (GUILayout.Button($"{template.GetDisplayName()}  ({PoolCount(template)})", GUILayout.Height(28)))
                {
                    selectedTemplate = template;
                }
                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();
            }

            if (templates.Length == 0)
            {
                EditorGUILayout.HelpBox("Henüz şablon yok. Yukarıdaki butonla ilk temanı oluştur (ör. İtalyan Mutfağı).", MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            // Right panel: selected template editor
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (selectedTemplate != null)
            {
                DrawSelectedTemplateEditor(selectedTemplate);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Şablon = bir temanın obje havuzu (ör. İtalyan Mutfağı, Abur Cubur). Yeni seviye " +
                    "oluştururken şablonu seçersin, seviye o havuzdan hazır dolu gelir.\n\n" +
                    "Şablon sadece BAŞLANGIÇ değeri verir: seviye oluştuktan sonra şablona bağlı kalmaz, " +
                    "şablonu değiştirmek eski seviyeleri bozmaz.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedTemplateEditor(LevelTemplateSO template)
        {
            SerializedObject so = new SerializedObject(template);
            so.Update();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"🍝 {template.GetDisplayName()}", EditorStyles.boldLabel);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
            if (GUILayout.Button("🎮 Bu Şablondan Seviye Oluştur", GUILayout.Width(240), GUILayout.Height(28)))
            {
                LevelDataSO created = CreateNewLevelAsset(template);
                if (created != null)
                {
                    selectedLevel = created;
                    newLevelTemplate = template;
                    selectedTab = Tab.LevelManager;
                    ShowNotification(new GUIContent($"🎮 Seviye {created.levelNumber} oluşturuldu!"));
                }
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(so.FindProperty("templateName"), new GUIContent("Şablon Adı"));
            EditorGUILayout.PropertyField(so.FindProperty("themeColor"), new GUIContent("Tema Rengi"));
            EditorGUILayout.PropertyField(so.FindProperty("icon"), new GUIContent("Tema İkonu (opsiyonel)"));

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("⚙️ Tema Varsayılanları", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bu değerler yeni seviyeye bir kez kopyalanır. Süre, siyah kilitli obje, booster, yapışkan/mıknatıs " +
                "mecha gibi ZORLUK ayarları bilerek burada yok - onlar temaya değil, seviye ilerleyişine ait.",
                MessageType.None);
            EditorGUILayout.PropertyField(so.FindProperty("foodTargetSize"), new GUIContent("Obje Hedef Ölçeği"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultGoalVariety"), new GUIContent("Kaç Çeşit Hedef"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultCountPerGoal"), new GUIContent("Hedef Başına Adet (3'ün katı)"));
            EditorGUILayout.PropertyField(so.FindProperty("defaultFillerCount"), new GUIContent("Dolgu Obje Sayısı"));
            EditorGUILayout.EndVertical();

            so.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            List<ItemDataSO> pool = template.GetValidPool();
            GUILayout.Label($"📦 Obje Havuzu ({pool.Count})", EditorStyles.boldLabel);

            int needed = Mathf.Max(1, template.defaultGoalVariety) + Mathf.Max(0, template.defaultFillerCount);
            if (pool.Count < needed)
            {
                EditorGUILayout.HelpBox(
                    $"Havuzda {pool.Count} obje var ama bu ayarlarla bir seviye {needed} obje istiyor. " +
                    "Eksik kalırsa seviye daha az hedefle oluşur.", MessageType.Warning);
            }
            else if (pool.Count == needed)
            {
                EditorGUILayout.HelpBox(
                    "Havuz tam yeterli - bu şablondan üretilen her seviye aynı objelerden oluşur. " +
                    "Çeşitlilik istiyorsan havuza birkaç obje daha ekle.", MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            ItemDataSO toAdd = (ItemDataSO)EditorGUILayout.ObjectField("Havuza Ekle:", null, typeof(ItemDataSO), false);
            if (toAdd != null) AddItemToTemplatePool(template, toAdd);
            if (GUILayout.Button("📚 Kütüphaneden Seç", GUILayout.Width(160), GUILayout.Height(18)))
            {
                selectedTab = Tab.ItemLibrary;
                libraryFilterTemplate = null;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (pool.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Havuz boş. Obje Kütüphanesi sekmesinden 🍝 butonuyla toplu ekleyebilir veya yukarıdaki " +
                    "alandan tek tek sürükleyebilirsin.", MessageType.Info);
            }
            else
            {
                const int columns = 4;
                for (int i = 0; i < pool.Count; i += columns)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int c = 0; c < columns; c++)
                    {
                        int idx = i + c;
                        if (idx >= pool.Count)
                        {
                            GUILayout.Box("", GUIStyle.none, GUILayout.Width(150), GUILayout.Height(70));
                            continue;
                        }
                        DrawTemplatePoolCard(template, pool[idx]);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTemplatePoolCard(LevelTemplateSO template, ItemDataSO item)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(150), GUILayout.Height(70));
            EditorGUILayout.BeginHorizontal();

            Texture2D preview = item.prefab != null ? AssetPreview.GetAssetPreview(item.prefab) : null;
            if (preview != null) GUILayout.Label(preview, GUILayout.Width(40), GUILayout.Height(40));
            else GUILayout.Label("3D", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(40), GUILayout.Height(40));

            EditorGUILayout.BeginVertical();
            GUILayout.Label(item.displayName, EditorStyles.miniBoldLabel);

            // A host with a saved pose preset is worth flagging: seeding a level off this template picks
            // such an item as the mecha host, so its camouflage lands already dialed in.
            if (HasPosePreset(item)) GUILayout.Label("🤖 poz var", EditorStyles.miniLabel);

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("✖ Çıkar", GUILayout.Height(18)))
            {
                Undo.RecordObject(template, "Havuzdan çıkar");
                template.itemPool.Remove(item);
                EditorUtility.SetDirty(template);
                AssetDatabase.SaveAssets();
                InvalidateAssetCaches();
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static bool HasPosePreset(ItemDataSO item)
        {
            if (item == null) return false;
            if (posePresetHosts == null)
            {
                posePresetHosts = new HashSet<ItemDataSO>();
                MechaPosePresetSO[] presets = FindAllAssets<MechaPosePresetSO>();
                if (presets != null)
                {
                    foreach (MechaPosePresetSO preset in presets)
                    {
                        if (preset != null && preset.targetHostItem != null) posePresetHosts.Add(preset.targetHostItem);
                    }
                }
            }
            return posePresetHosts.Contains(item);
        }

        private void AddItemToTemplatePool(LevelTemplateSO template, ItemDataSO item)
        {
            if (template == null || item == null) return;
            if (template.itemPool == null) template.itemPool = new List<ItemDataSO>();
            if (template.itemPool.Contains(item)) return;

            Undo.RecordObject(template, "Havuza obje ekle");
            template.itemPool.Add(item);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();
            InvalidateAssetCaches();
            ShowNotification(new GUIContent($"🍝 {item.displayName} → {template.GetDisplayName()}"));
        }

        private static LevelTemplateSO CreateNewTemplateAsset()
        {
            string folderPath = "Assets/LevelData/Templates";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            int nextNumber = FindAllAssets<LevelTemplateSO>().Length + 1;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/Template_{nextNumber:D2}.asset");

            LevelTemplateSO template = ScriptableObject.CreateInstance<LevelTemplateSO>();
            template.templateName = $"Yeni Şablon {nextNumber}";
            template.themeColor = GetRandomPaletteColor();

            AssetDatabase.CreateAsset(template, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            InvalidateAssetCaches();
            Selection.activeObject = template;
            Debug.Log($"🍝 Yeni Şablon Oluşturuldu: {assetPath}");
            return template;
        }

        /// <summary>
        /// Theme row on the level editor: which template seeded this level, plus re-seeding controls.
        ///
        /// Re-seeding is destructive on purpose - it replaces goals and fillers outright, which is the
        /// point of a re-roll - so it asks first.
        /// </summary>
        private void DrawLevelTemplateBox(SerializedObject so, LevelDataSO level)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🍝 Şablon (Tema)", EditorStyles.boldLabel, GUILayout.Width(110));
            EditorGUILayout.PropertyField(so.FindProperty("sourceTemplate"), GUIContent.none);
            EditorGUILayout.EndHorizontal();
            so.ApplyModifiedProperties();

            LevelTemplateSO template = level.sourceTemplate;
            if (template == null)
            {
                EditorGUILayout.HelpBox(
                    "Bu seviye bir şablondan üretilmemiş. Yukarıdan bir şablon seçersen hedefleri o temanın " +
                    "havuzundan yeniden doldurabilirsin.", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"Havuz: {PoolCount(template)} obje  •  {template.defaultGoalVariety} çeşit hedef  •  hedef başına {template.defaultCountPerGoal}",
                    EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.95f, 0.75f, 0.25f);
                if (GUILayout.Button("🎲 Şablondan Yeniden Karıştır", GUILayout.Height(24)))
                {
                    ReseedLevelFromTemplate(so, level, template, true);
                }
                GUI.backgroundColor = new Color(0.75f, 0.75f, 0.75f);
                if (GUILayout.Button("📋 Havuz Sırasıyla Doldur", GUILayout.Width(180), GUILayout.Height(24)))
                {
                    ReseedLevelFromTemplate(so, level, template, false);
                }
                GUI.backgroundColor = oldBg;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(
                    "Şablon sadece başlangıç değeri verir; seviye şablona bağlı kalmaz. Süre, siyah kilit, " +
                    "booster gibi zorluk ayarları şablondan gelmez.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void ReseedLevelFromTemplate(SerializedObject so, LevelDataSO level, LevelTemplateSO template, bool shuffle)
        {
            if (level == null || template == null) return;

            if (template.GetValidPool().Count == 0)
            {
                EditorUtility.DisplayDialog("Boş havuz",
                    $"'{template.GetDisplayName()}' şablonunun obje havuzu boş. Önce Şablonlar sekmesinden obje ekle.", "Tamam");
                return;
            }

            bool hasContent = (level.targetGoals != null && level.targetGoals.Count > 0)
                              || (level.fillerItems != null && level.fillerItems.Count > 0);
            if (hasContent && !EditorUtility.DisplayDialog(
                    "Hedefler değiştirilecek",
                    $"Seviye {level.levelNumber} hedefleri ve dolguları '{template.GetDisplayName()}' havuzundan " +
                    "yeniden doldurulacak. Mevcut hedef listesi silinecek. Devam edilsin mi?",
                    "Evet, yeniden doldur", "Vazgeç"))
            {
                return;
            }

            Undo.RecordObject(level, "Şablondan yeniden doldur");
            template.ApplyTo(level, shuffle);
            AssignHostFromPresets(level);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            so.Update();
            ShowNotification(new GUIContent($"🎲 '{template.GetDisplayName()}' şablonundan yeniden dolduruldu!"));
        }

        // ====================================================================
        // TAB 3: ITEM LIBRARY
        // ====================================================================
        private void DrawItemLibraryTab()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Kayıtlı Obje Prefab Kütüphanesi", EditorStyles.boldLabel);
            itemSearchQuery = EditorGUILayout.TextField("Ara:", itemSearchQuery, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();

            // Theme filter. With 100+ items in one flat grid, authoring an Italian level means scrolling
            // past every snack in the project; filtering to a template's pool is the whole point of themes.
            LevelTemplateSO[] templates = FindAllAssets<LevelTemplateSO>();
            if (templates != null && templates.Length > 0)
            {
                var filterNames = new List<string> { "Tüm objeler" };
                int filterIdx = 0;
                for (int t = 0; t < templates.Length; t++)
                {
                    if (templates[t] == null) continue;
                    filterNames.Add($"{templates[t].GetDisplayName()} havuzu");
                    if (templates[t] == libraryFilterTemplate) filterIdx = filterNames.Count - 1;
                }

                EditorGUILayout.BeginHorizontal();
                int pickedFilter = EditorGUILayout.Popup("Şablon Filtresi:", filterIdx, filterNames.ToArray(), GUILayout.Width(380));
                libraryFilterTemplate = pickedFilter <= 0 ? null : templates[pickedFilter - 1];

                GUILayout.Label("Havuza eklenecek şablon:", GUILayout.Width(160));
                selectedTemplate = (LevelTemplateSO)EditorGUILayout.ObjectField(selectedTemplate, typeof(LevelTemplateSO), false, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();
            }

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
                if (libraryFilterTemplate != null && !PoolContains(libraryFilterTemplate, item))
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

            if (selectedTemplate != null)
            {
                bool alreadyInPool = PoolContains(selectedTemplate, item);
                GUI.backgroundColor = alreadyInPool ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.75f, 0.45f, 0.95f);
                var poolContent = new GUIContent(alreadyInPool ? "✓" : "🍝",
                    alreadyInPool
                        ? $"Zaten '{selectedTemplate.GetDisplayName()}' havuzunda"
                        : $"'{selectedTemplate.GetDisplayName()}' havuzuna ekle");
                using (new EditorGUI.DisabledScope(alreadyInPool))
                {
                    if (GUILayout.Button(poolContent, GUILayout.Width(34), GUILayout.Height(24)))
                    {
                        AddItemToTemplatePool(selectedTemplate, item);
                    }
                }
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
        // TAB 4: PREFAB BATCH IMPORTER
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
        /// <summary>
        /// Finds the mecha model to preview with.
        ///
        /// The old lookup hardcoded "Assets/Prefabs/meccha chameleon.glb" and, when that missed, took the
        /// first hit of an unscoped FindAssets("meccha"). The GLB has since been converted to FBX, so the
        /// path always missed - and the first unscoped hit is "Assets/meccha chameleon@Running (1).fbx",
        /// an ANIMATION-only file with a skeleton and no SkinnedMeshRenderer. Levels that left the custom
        /// prefab empty therefore previewed as bare bone gizmos with no body. Runtime never had this
        /// problem: MechaRagdollSpawner scopes its own search to Assets/Prefabs.
        ///
        /// So: scope the search the same way, and reject any candidate that carries no mesh, which keeps
        /// this working through the next rename too.
        /// </summary>
        private static GameObject ResolveMechaModel(GameObject preferred)
        {
            if (preferred != null) return preferred;

            foreach (string path in new[] { "Assets/Prefabs/meccha chameleon.fbx", "Assets/Prefabs/meccha chameleon.glb" })
            {
                GameObject direct = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (HasVisibleMesh(direct)) return direct;
            }

            foreach (string searchFolder in new[] { "Assets/Prefabs", "Assets" })
            {
                string[] guids = AssetDatabase.FindAssets("meccha t:Model", new[] { searchFolder });
                if (guids == null) continue;

                foreach (string guid in guids)
                {
                    GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (HasVisibleMesh(candidate)) return candidate;
                }
            }

            return null;
        }

        /// <summary>True when the model actually has geometry to draw, not just a rig.</summary>
        private static bool HasVisibleMesh(GameObject model)
        {
            if (model == null) return false;

            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr != null && smr.sharedMesh != null && smr.sharedMesh.vertexCount > 0) return true;
            }
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 0) return true;
            }
            return false;
        }

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

            GameObject mechaPrefab = ResolveMechaModel(entry.customMechaPrefab);
            if (mechaPrefab == null)
            {
                EditorUtility.DisplayDialog("Hata",
                    "Mecha modeli bulunamadı. 'Assets/Prefabs' altında gövdesi (mesh'i) olan bir meccha " +
                    "modeli yok - ya da mecha kartındaki 'Özel Mecha Model Prefab'ı' alanına elle bir model seç.",
                    "Tamam");
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
                entry.mechaWrapAmount,
                entry.boneOverrides
            );

            MechaBonePreviewTag tag = hostInstance.GetComponent<MechaBonePreviewTag>();
            if (tag == null) tag = hostInstance.AddComponent<MechaBonePreviewTag>();
            tag.Initialize(level, mechaIndex, mechaInst);

            Selection.activeGameObject = hostInstance;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            Debug.Log($"👁️ Mecha #{mechaIndex + 1} 3D Canlı Önizlemesi Sahnede (Scene View) Odaklandı! (Mecha: {mechaInst.name})");
        }

        public static void RepaintWindow()
        {
            if (HasOpenInstances<LevelDesignEditorWindow>())
            {
                var window = GetWindow<LevelDesignEditorWindow>("Level Design Manager", false);
                if (window != null) window.Repaint();
            }
        }

        public static LevelDataSO GetSelectedLevelData()
        {
            if (HasOpenInstances<LevelDesignEditorWindow>())
            {
                var window = GetWindow<LevelDesignEditorWindow>("Level Design Manager", false);
                if (window != null) return window.selectedLevel;
            }
            return null;
        }

        /// <summary>
        /// Rebuilds one mecha's scene preview from the level asset. Entry point for code outside this
        /// window (the bone/root preview inspector) so the preview is always produced by the same
        /// placement path as gameplay rather than by poking the preview's transform by hand.
        /// </summary>
        public static void RefreshScene3DPreview(LevelDataSO level, int mechaIndex)
        {
            if (level == null) return;
            List<MechaSpawnEntry> entries = level.GetAllMechaEntries();
            if (mechaIndex < 0 || mechaIndex >= entries.Count) return;
            LiveUpdateScene3DPreview(level, entries[mechaIndex], mechaIndex);
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

        public static readonly string[] BonePresets = new string[]
        {
            "upper_arm_L", "upper_arm_R",
            "forearm_L", "forearm_R",
            "hand_L", "hand_R",
            "shoulder_L", "shoulder_R",
            "thigh_L", "thigh_R",
            "shin_L", "shin_R",
            "foot_L", "foot_R",
            "spine", "spine_001", "spine_002", "spine_003", "spine_004", "spine_005", "spine_006",
            "pelvis_L", "pelvis_R",
            "toe_L", "toe_R",
        };

        private void DrawBoneOverrideSection(SerializedObject so, LevelDataSO level, int mechaIdx)
        {
            SerializedProperty bonesProp;
            if (mechaIdx == 0)
            {
                bonesProp = so.FindProperty("boneOverrides");
            }
            else
            {
                SerializedProperty mechasProp = so.FindProperty("additionalMechas");
                int addIdx = mechaIdx - 1;
                if (addIdx >= mechasProp.arraySize) return;
                bonesProp = mechasProp.GetArrayElementAtIndex(addIdx).FindPropertyRelative("boneOverrides");
            }

            if (bonesProp == null) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🦴 Kemik Bazlı Poz Ayarları", EditorStyles.boldLabel);

            int removeIdx = -1;
            for (int i = 0; i < bonesProp.arraySize; i++)
            {
                SerializedProperty elem = bonesProp.GetArrayElementAtIndex(i);
                SerializedProperty kwProp = elem.FindPropertyRelative("boneKeyword");
                SerializedProperty rotProp = elem.FindPropertyRelative("rotationOffset");

                EditorGUILayout.BeginHorizontal();

                int currentPresetIdx = System.Array.IndexOf(BonePresets, kwProp.stringValue);
                int selected = EditorGUILayout.Popup(currentPresetIdx >= 0 ? currentPresetIdx : 0, BonePresets, GUILayout.Width(130));
                if (selected != currentPresetIdx)
                {
                    kwProp.stringValue = BonePresets[selected];
                }

                EditorGUILayout.PropertyField(rotProp, GUIContent.none);

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    removeIdx = i;
                }
                GUI.backgroundColor = prevBg;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIdx >= 0) bonesProp.DeleteArrayElementAtIndex(removeIdx);

            EditorGUILayout.BeginHorizontal();
            Color addBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.85f, 1f);
            if (GUILayout.Button("➕ Kemik Ekle", GUILayout.Height(24)))
            {
                bonesProp.arraySize++;
                var newElem = bonesProp.GetArrayElementAtIndex(bonesProp.arraySize - 1);
                newElem.FindPropertyRelative("boneKeyword").stringValue = "upper_arm_L";
                newElem.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
            }

            if (bonesProp.arraySize > 0)
            {
                GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
                if (GUILayout.Button("🗑️ Tümünü Temizle", GUILayout.Width(130), GUILayout.Height(24)))
                {
                    bonesProp.ClearArray();
                }
            }
            GUI.backgroundColor = addBg;
            EditorGUILayout.EndHorizontal();

            if (bonesProp.serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(level);
                var entries = level.GetAllMechaEntries();
                if (mechaIdx < entries.Count)
                {
                    LiveUpdateScene3DPreview(level, entries[mechaIdx], mechaIdx);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPosePresetSection(SerializedObject so, LevelDataSO level, int mechaIdx)
        {
            var entries = level.GetAllMechaEntries();
            if (mechaIdx >= entries.Count) return;
            MechaSpawnEntry mechaEntry = entries[mechaIdx];
            if (mechaEntry == null) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("📦 MECHA POZ ŞABLONLARI (PRESETS)", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Find all MechaPosePresetSO assets in project
            MechaPosePresetSO[] allPresets = FindAllAssets<MechaPosePresetSO>();
            ItemDataSO currentHost = mechaEntry.hostItemSO;

            List<MechaPosePresetSO> matchingPresets = new List<MechaPosePresetSO>();

            if (allPresets != null)
            {
                foreach (var p in allPresets)
                {
                    if (p == null) continue;

                    // Strictly filter: show only presets matching current host item or general presets
                    if (p.targetHostItem == null || (currentHost != null && p.targetHostItem == currentHost))
                    {
                        matchingPresets.Add(p);
                    }
                }
            }

            SortPresets(matchingPresets);

            string hostName = currentHost != null ? currentHost.displayName : "Objesiz";

            // The pose lives on the LEVEL, not on the item, so swapping the host object leaves the old
            // object's pose in place - a mecha tuned to curl around an avocado stays curled that way on a
            // cookie. Nothing auto-corrects it (a host can have several saved poses, so there is no single
            // right one to pick), but the mismatch is worth pointing at.
            bool hostHasPreset = false;
            bool poseMatchesHost = false;
            foreach (MechaPosePresetSO p in matchingPresets)
            {
                if (p.targetHostItem == null) continue;   // generic presets say nothing about this host
                hostHasPreset = true;
                if (PoseMatchesPreset(mechaEntry, p, level.GetHostWorldSize())) { poseMatchesHost = true; break; }
            }

            if (hostHasPreset && !poseMatchesHost)
            {
                EditorGUILayout.HelpBox(
                    $"⚠️ Mevcut poz '{hostName}' için kayıtlı şablonların hiçbiriyle eşleşmiyor - büyük " +
                    $"ihtimalle başka bir objeden kalma. Bu mecha şu an '{PivotLabel(mechaEntry.targetPivot)}' " +
                    "pivotunda; aşağıdan bu objeye ait bir şablonu uygula.",
                    MessageType.Warning);
            }

            if (matchingPresets.Count > 0)
            {
                EditorGUILayout.HelpBox($"💡 '{hostName}' için kayıtlı {matchingPresets.Count} poz şablonu:", MessageType.Info);
                foreach (var preset in matchingPresets)
                {
                    DrawPresetRow(preset, so, level, mechaIdx, mechaEntry);
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"'{hostName}' objesi için henüz poz şablonu oluşturulmamış. Aşağıdaki butonla kaydedebilirsiniz.", MessageType.None);
            }

            EditorGUILayout.Space(6);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button($"💾 Mevcut Pozu Şablon Olarak Kaydet ({hostName})", GUILayout.Height(26)))
            {
                SaveCurrentPoseAsPreset(mechaEntry, currentHost, level.GetHostWorldSize());
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Applies the new host's top pose preset the moment the host object is swapped.
        ///
        /// The pose lives on the LEVEL, so changing the host used to leave the previous object's pose
        /// behind - a mecha curled around an avocado stayed curled that way on a cookie. Only presets
        /// bound to this exact host are considered: a generic preset says nothing about how to sit on
        /// this particular object, and letting one win just because it sorted first would overwrite a
        /// tuned pose with a meaningless one.
        /// </summary>
        private void AutoApplyHostPreset(SerializedObject so, LevelDataSO level, int mechaIdx)
        {
            if (so == null || level == null) return;

            so.ApplyModifiedProperties();   // commit the new host before reading it back

            List<MechaSpawnEntry> entries = level.GetAllMechaEntries();
            if (mechaIdx < 0 || mechaIdx >= entries.Count) return;

            MechaSpawnEntry entry = entries[mechaIdx];
            ItemDataSO host = entry != null ? entry.hostItemSO : null;
            if (host == null) return;

            MechaPosePresetSO preset = FindTopPresetForHost(host);
            if (preset == null)
            {
                ShowNotification(new GUIContent($"'{host.displayName}' için kayıtlı poz yok - poz olduğu gibi kaldı"));
                return;
            }

            Undo.RecordObject(level, "Host değişti - poz şablonu uygula");
            preset.ApplyTo(entry, level.GetHostWorldSize());
            if (mechaIdx == 0) level.WritePrimaryEntryBack();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            so.Update();

            // Only refresh a preview that is already on screen - never conjure scene objects the user
            // did not ask for.
            string previewName = mechaIdx == 0 ? "Mecha_3D_Preview_Instance" : $"Mecha_3D_Preview_Instance_{mechaIdx}";
            if (GameObject.Find(previewName) != null) RefreshScene3DPreview(level, mechaIdx);

            ShowNotification(new GUIContent($"📋 '{preset.presetName}' otomatik uygulandı ({host.displayName})"));
        }

        /// <summary>
        /// The preset that sits at the top of the list for this host - the one auto-applied on a host
        /// swap. Sorted by name rather than by AssetDatabase search order, so which preset counts as
        /// "the top one" is something the author controls by naming, not a coincidence of GUIDs.
        /// </summary>
        private static MechaPosePresetSO FindTopPresetForHost(ItemDataSO host)
        {
            if (host == null) return null;

            List<MechaPosePresetSO> matches = new List<MechaPosePresetSO>();
            foreach (MechaPosePresetSO preset in FindAllAssets<MechaPosePresetSO>())
            {
                if (preset != null && preset.targetHostItem == host) matches.Add(preset);
            }
            if (matches.Count == 0) return null;

            SortPresets(matches);
            return matches[0];
        }

        /// <summary>Host-specific presets first (by name), generic ones after.</summary>
        private static void SortPresets(List<MechaPosePresetSO> presets)
        {
            presets.Sort((a, b) =>
            {
                bool aGeneric = a == null || a.targetHostItem == null;
                bool bGeneric = b == null || b.targetHostItem == null;
                if (aGeneric != bGeneric) return aGeneric ? 1 : -1;

                string an = a != null ? (!string.IsNullOrEmpty(a.presetName) ? a.presetName : a.name) : "";
                string bn = b != null ? (!string.IsNullOrEmpty(b.presetName) ? b.presetName : b.name) : "";
                return string.Compare(an, bn, System.StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Whether an entry's pose is the one this preset stores.
        ///
        /// Opacity is deliberately not compared: it is a per-level visibility tweak, not part of how the
        /// mecha is folded onto the object, and comparing it would report a pose mismatch every time
        /// someone dimmed a mecha.
        /// </summary>
        private static bool PoseMatchesPreset(MechaSpawnEntry entry, MechaPosePresetSO preset, float hostSize)
        {
            if (entry == null || preset == null) return false;

            if (entry.targetPivot != preset.targetPivot) return false;

            // Compare against what this preset WOULD produce here, not its raw offset: on a level whose
            // host is a different size the applied offset is scaled, and an unscaled comparison would
            // report every such pose as a mismatch.
            float scale = preset.GetHostSizeScale(hostSize);
            Vector3 presetOffset = preset.mechaLocalOffset * scale;

            const float eps = 0.001f;
            if (Mathf.Abs(entry.mechaWrapAmount - preset.mechaWrapAmount) > eps) return false;
            if (Mathf.Abs(entry.mechaWorldSize - preset.mechaWorldSize) > eps) return false;
            if (Mathf.Abs(entry.mechaScaleRatio - preset.mechaScaleRatio) > eps) return false;
            if ((entry.mechaLocalOffset - presetOffset).sqrMagnitude > eps * eps) return false;
            if ((entry.mechaRotationOffset - preset.mechaRotationOffset).sqrMagnitude > eps * eps) return false;

            int entryBones = entry.boneOverrides != null ? entry.boneOverrides.Count : 0;
            int presetBones = preset.boneOverrides != null ? preset.boneOverrides.Count : 0;
            if (entryBones != presetBones) return false;

            for (int i = 0; i < entryBones; i++)
            {
                MechaBoneOverride a = entry.boneOverrides[i];
                MechaBoneOverride b = preset.boneOverrides[i];
                if (a == null || b == null) return false;
                if (a.boneKeyword != b.boneKeyword) return false;
                if ((a.rotationOffset - b.rotationOffset).sqrMagnitude > eps * eps) return false;
            }

            return true;
        }

        /// <summary>
        /// Draws the pivot picker folded away.
        ///
        /// The pivot decides which face the mecha lies against, and every offset/rotation below it is
        /// measured from that face - so touching it silently invalidates a pose that was already dialed
        /// in. Everything reachable by changing it is also reachable from the offset and rotation fields,
        /// which is why it is no longer part of the normal authoring flow.
        /// </summary>
        private void DrawPivotField(SerializedProperty pivotProp, int mechaIdx)
        {
            if (pivotProp == null) return;

            bool open;
            if (!pivotFoldouts.TryGetValue(mechaIdx, out open)) open = false;

            string current = PivotLabel((MechaPivotSelection)pivotProp.enumValueIndex);
            open = EditorGUILayout.Foldout(open, $"⚙️ Gelişmiş — Yerleşim Yüzeyi: {current}", true);
            pivotFoldouts[mechaIdx] = open;
            if (!open) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(
                "Mecha'nın objenin hangi yüzeyine yaslanacağı. Offset ve dönüş açısı bu yüzeye GÖRE " +
                "ölçülür - burayı değiştirirsen ayarlı poz başka bir yere kayar ve baştan ayarlaman " +
                "gerekir. Normalde dokunma: konumu Offset ve Dönüş Açısı ile ayarla.\n\n" +
                "Auto = en geniş yüzeyi kendisi seçer (her seferinde aynı sonucu verir).",
                MessageType.None);
            EditorGUILayout.PropertyField(pivotProp, new GUIContent("Yerleşeceği Pivot Noktası:"));
            EditorGUI.indentLevel--;
        }

        private static string PivotLabel(MechaPivotSelection pivot)
        {
            switch (pivot)
            {
                case MechaPivotSelection.PivotTop: return "Üst";
                case MechaPivotSelection.PivotBottom: return "Alt";
                case MechaPivotSelection.PivotLeft: return "Sol";
                case MechaPivotSelection.PivotRight: return "Sağ";
                case MechaPivotSelection.PivotFront: return "Ön";
                case MechaPivotSelection.PivotBack: return "Arka";
                case MechaPivotSelection.MechaAnchor: return "Anchor";
                default: return "Auto";
            }
        }

        private void DrawPresetRow(MechaPosePresetSO preset, SerializedObject so, LevelDataSO level, int mechaIdx, MechaSpawnEntry mechaEntry)
        {
            if (preset == null) return;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            string hostTag = preset.targetHostItem != null ? $"[{preset.targetHostItem.displayName}] " : "[Genel] ";
            GUILayout.Label($"{hostTag}{preset.presetName}", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            // The pivot decides which face the mecha lies on, so a preset carrying the wrong one puts the
            // mecha somewhere unexpected. Show it, and let it be corrected in place - older presets were
            // saved before the pivot was stored at all and all default to "Üst".
            using (new EditorGUI.DisabledScope(mechaEntry == null))
            {
                EditorGUI.BeginChangeCheck();
                var newPivot = (MechaPivotSelection)EditorGUILayout.EnumPopup(
                    new GUIContent(PivotLabel(preset.targetPivot), "Pozun yaslandığı yüzey. Yanlışsa mecha başka yere oturur."),
                    preset.targetPivot, GUILayout.Width(110));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(preset, "Poz şablonu pivotu");
                    preset.targetPivot = newPivot;
                    EditorUtility.SetDirty(preset);
                    AssetDatabase.SaveAssets();
                }
            }

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
            if (GUILayout.Button("📋 Uygula", GUILayout.Width(75), GUILayout.Height(20)))
            {
                Undo.RecordObject(level, "Apply Mecha Pose Preset");
                preset.ApplyTo(mechaEntry, level.GetHostWorldSize());

                // Mecha #1's entry is LevelDataSO._primaryEntry - a [NonSerialized] scratch copy that
                // GetAllMechaEntries() refills from the level's own fields on every call. Without this
                // write-back the preset landed only in that copy, and the GetAllMechaEntries() call four
                // lines down re-synced it from the unchanged asset, so applying a preset to the level's
                // main mecha silently did nothing (Mecha #2+ were fine - those entries are real
                // serialized list elements).
                if (mechaIdx == 0) level.WritePrimaryEntryBack();

                so.Update();
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
                var updatedEntries = level.GetAllMechaEntries();
                if (mechaIdx < updatedEntries.Count)
                {
                    LiveUpdateScene3DPreview(level, updatedEntries[mechaIdx], mechaIdx);
                }
                ShowNotification(new GUIContent($"📋 '{preset.presetName}' Poz Şablonu Uygulandı!"));
            }

            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("🗑️", GUILayout.Width(25), GUILayout.Height(20)))
            {
                if (EditorUtility.DisplayDialog("Şablonu Sil", $"'{preset.presetName}' şablonunu projeden silmek istediğinize emin misiniz?", "Evet, Sil", "İptal"))
                {
                    string path = AssetDatabase.GetAssetPath(preset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.DeleteAsset(path);
                        AssetDatabase.Refresh();
                        InvalidateAssetCaches();
                    }
                }
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.EndHorizontal();
        }

        public static void SaveCurrentPoseAsPreset(MechaSpawnEntry entry, ItemDataSO hostItem, float hostSize = 0f)
        {
            if (entry == null) return;

            string defaultName = hostItem != null ? $"{hostItem.displayName}_Poz_1" : "Yeni_Mecha_Pozu";
            string folderPath = "Assets/ScriptableObjects/MechaPosePresets";

            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Mecha Poz Şablonu Kaydet",
                defaultName,
                "asset",
                "Lütfen şablon için bir dosya adı belirleyin.",
                folderPath
            );

            if (string.IsNullOrEmpty(path)) return;

            MechaPosePresetSO preset = ScriptableObject.CreateInstance<MechaPosePresetSO>();
            preset.presetName = System.IO.Path.GetFileNameWithoutExtension(path);
            preset.CopyFrom(entry, hostItem, hostSize);

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            InvalidateAssetCaches();

            Debug.Log($"💾 Yeni Mecha Poz Şablonu Oluşturuldu: {path}");
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

        /// <summary>
        /// Creates the next Level_NN asset, optionally seeded from a theme template.
        ///
        /// The template is a SNAPSHOT source: its values are copied in here and the level never reads it
        /// again at runtime. <see cref="LevelDataSO.sourceTemplate"/> is recorded purely so the editor can
        /// offer a re-roll later.
        /// </summary>
        private static LevelDataSO CreateNewLevelAsset(LevelTemplateSO template = null)
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

            bool seeded = template != null && template.ApplyTo(newLevel, true);
            if (seeded)
            {
                newLevel.sourceTemplate = template;
                newLevel.levelTitle = $"Seviye {nextNumber} - {template.GetDisplayName()}";
                AssignHostFromPresets(newLevel);
            }
            else
            {
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
            }

            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            InvalidateAssetCaches();
            Selection.activeObject = newLevel;
            Debug.Log(seeded
                ? $"🍝 '{template.GetDisplayName()}' şablonundan yeni seviye oluşturuldu: {assetPath}"
                : $"🎮 Yeni Seviye Varlığı Oluşturuldu: {assetPath}");
            return newLevel;
        }

        /// <summary>
        /// Picks the level's mecha host from its own goal items and applies that host's pose preset.
        ///
        /// The host has to be a goal item: the mecha hides inside something that is actually in the pile.
        /// Among those, a host with a saved <see cref="MechaPosePresetSO"/> wins - a pose is tuned per
        /// object shape (a watermelon's spine bend is nothing like an avocado's curled limbs), so seeding
        /// a host that already has one is the difference between a level that looks right immediately and
        /// one that needs the pose dialed in by hand.
        /// </summary>
        private static void AssignHostFromPresets(LevelDataSO level)
        {
            if (level == null || level.targetGoals == null || level.targetGoals.Count == 0) return;

            MechaPosePresetSO[] presets = FindAllAssets<MechaPosePresetSO>();
            ItemDataSO chosenHost = null;
            MechaPosePresetSO chosenPreset = null;

            foreach (LevelGoalRequirement goal in level.targetGoals)
            {
                if (goal == null || goal.itemData == null) continue;
                if (presets != null)
                {
                    foreach (MechaPosePresetSO preset in presets)
                    {
                        if (preset != null && preset.targetHostItem == goal.itemData)
                        {
                            chosenHost = goal.itemData;
                            chosenPreset = preset;
                            break;
                        }
                    }
                }
                if (chosenHost != null) break;
            }

            if (chosenHost == null)
            {
                foreach (LevelGoalRequirement goal in level.targetGoals)
                {
                    if (goal != null && goal.itemData != null) { chosenHost = goal.itemData; break; }
                }
            }
            if (chosenHost == null) return;

            level.hostItemSO = chosenHost;
            level.mechaHostKeyword = chosenHost.GetEffectiveItemId();

            if (chosenPreset != null)
            {
                List<MechaSpawnEntry> entries = level.GetAllMechaEntries();
                if (entries.Count > 0)
                {
                    chosenPreset.ApplyTo(entries[0], level.GetHostWorldSize());
                    // Entry 0 is a scratch copy of the level's own singular mecha fields, so the pose only
                    // survives once it is written back onto the asset.
                    level.WritePrimaryEntryBack();
                }
            }
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

            CanvasUIDesignManager uiMgr = sceneController.GetComponent<CanvasUIDesignManager>() ?? Object.FindFirstObjectByType<CanvasUIDesignManager>();
            if (uiMgr != null) uiMgr.UpdateBoosterLockStates();

            Debug.Log($"✅ Seviye {level.levelNumber} sahnede aktifleştirildi ve başarıyla yüklendi!");
        }

        private static T[] FindAllAssets<T>() where T : UnityEngine.Object
        {
            if (assetCache.TryGetValue(typeof(T), out UnityEngine.Object[] cached) && cached != null)
            {
                // A deleted asset leaves a null behind, which is the one thing the cache cannot notice on
                // its own if the project-change callback was missed - rescan instead of handing it out.
                bool stale = false;
                foreach (UnityEngine.Object obj in cached)
                {
                    if (obj == null) { stale = true; break; }
                }
                if (!stale) return (T[])cached;
            }

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            T[] assets = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }
            assetCache[typeof(T)] = assets;
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
