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

            foreach (var lvl in levels)
            {
                if (lvl == null) continue;
                GUI.backgroundColor = (selectedLevel == lvl) ? new Color(0.3f, 0.8f, 1.0f) : Color.white;
                if (GUILayout.Button($"Seviye {lvl.levelNumber}: {lvl.levelTitle}", GUILayout.Height(28)))
                {
                    selectedLevel = lvl;
                }
                GUI.backgroundColor = Color.white;
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
            EditorGUILayout.PropertyField(so.FindProperty("foodTargetSize"), new GUIContent("Obje Hedef Ölçeği (Varsayılan 0.55):"));

            EditorGUILayout.Space(10);
            GUILayout.Label("🤖 Bukalemun Mecha & Kamuflaj Ayarları", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("enableCamouflageMecha"), new GUIContent("Mecha Karakteri Olsun Mu?"));

            if (so.FindProperty("enableCamouflageMecha").boolValue)
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.PropertyField(so.FindProperty("customMechaPrefab"), new GUIContent("Özel Mecha Model Prefab'ı:"));
                EditorGUILayout.PropertyField(so.FindProperty("hostItemSO"), new GUIContent("Yapışacağı Hedef Obje (ItemData):"));
                EditorGUILayout.PropertyField(so.FindProperty("mechaHostKeyword"), new GUIContent("Hedef Obje Arama İnce Ayarı:"));
                EditorGUILayout.PropertyField(so.FindProperty("mechaWorldSize"), new GUIContent("Mecha Boyu (dünya birimi, 0=oran kullan):"));
                EditorGUILayout.PropertyField(so.FindProperty("mechaScaleRatio"), new GUIContent("Mecha Ölçek Oranı (yalnızca Boy=0 ise):"));
                EditorGUILayout.PropertyField(so.FindProperty("mechaOpacity"), new GUIContent("Mecha Saydamlığı (0.55 = %55):"));
                EditorGUILayout.PropertyField(so.FindProperty("mechaLocalOffset"), new GUIContent("Mecha Konum Öteleme (Offset):"));
                EditorGUILayout.PropertyField(so.FindProperty("mechaRotationOffset"), new GUIContent("Mecha Dönüş Açısı (Euler):"));

                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                    LiveUpdateScene3DPreview(level);
                }

                EditorGUILayout.Space(8);
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.8f, 1.0f);
                if (GUILayout.Button("🔍 3D Canlı Önizlemeyi Sahnede Göster ve Odaklan (Scene View)", GUILayout.Height(34)))
                {
                    GenerateScene3DPreview(level);
                }
                GUI.backgroundColor = prevBg;

                // Visual Preview & Dimension Stats Box
                ItemDataSO hostSO = level.hostItemSO;
                if (hostSO != null && hostSO.prefab != null)
                {
                    EditorGUILayout.Space(8);
                    EditorGUILayout.BeginHorizontal(GUI.skin.box);

                    // 2D Preview Thumbnail of Host Object
                    Texture2D hostPreview = AssetPreview.GetAssetPreview(hostSO.prefab);
                    if (hostPreview != null)
                    {
                        GUILayout.Label(hostPreview, GUILayout.Width(70), GUILayout.Height(70));
                    }

                    EditorGUILayout.BeginVertical();
                    GUILayout.Label($"📐 {hostSO.displayName} Boyut Bilgisi & Önizleme", EditorStyles.boldLabel);

                    Renderer[] rends = hostSO.prefab.GetComponentsInChildren<Renderer>();
                    if (rends != null && rends.Length > 0)
                    {
                        Bounds b = rends[0].bounds;
                        for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);

                        float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
                        float ratio = level.mechaScaleRatio;
                        float mechaSize = maxDim * ratio;

                        GUILayout.Label($"Obje Mesh Boyutları (X, Y, Z): {b.size.x:F2}m x {b.size.y:F2}m x {b.size.z:F2}m", EditorStyles.miniLabel);
                        GUILayout.Label($"Maksimum Obje Çapı: {maxDim:F2} birim", EditorStyles.miniLabel);

                        GUIStyle highlightStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal = { textColor = new Color(0.2f, 0.8f, 0.4f) }
                        };
                        GUILayout.Label($"📏 Geometrik Mecha Boyutu: {mechaSize:F2} birim (%{(ratio * 100):F0} oranında)", highlightStyle);
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
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

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
            }
        }

        // ====================================================================
        // TAB 2: ITEM LIBRARY
        // ====================================================================
        private void DrawItemLibraryTab()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Kayıtlı Obje Prefab Kütüphanesi", EditorStyles.boldLabel);
            itemSearchQuery = EditorGUILayout.TextField("Ara:", itemSearchQuery, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            ItemDataSO[] items = FindAllAssets<ItemDataSO>();
            if (items.Length == 0)
            {
                EditorGUILayout.HelpBox("Henüz hiçbir ItemDataSO varlığı bulunamadı. Toplu Prefab Yükleyici sekmesinden 3D prefablarınızı tek tıkla yükleyebilirsiniz!", MessageType.Info);
                return;
            }

            int columns = 3;
            int colCount = 0;
            EditorGUILayout.BeginHorizontal();

            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(itemSearchQuery) && !item.displayName.ToLower().Contains(itemSearchQuery.ToLower()))
                    continue;

                EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(220), GUILayout.Height(130));

                EditorGUILayout.BeginHorizontal();
                Texture2D preview = item.prefab != null ? AssetPreview.GetAssetPreview(item.prefab) : null;
                if (preview != null)
                {
                    GUILayout.Label(preview, GUILayout.Width(50), GUILayout.Height(50));
                }
                else
                {
                    GUILayout.Box("3D", GUILayout.Width(50), GUILayout.Height(50));
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

                colCount++;
                if (colCount >= columns)
                {
                    colCount = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }

            EditorGUILayout.EndHorizontal();
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

        private static void GenerateScene3DPreview(LevelDataSO level)
        {
            if (level == null || level.hostItemSO == null || level.hostItemSO.prefab == null)
            {
                EditorUtility.DisplayDialog("Uyarı", "Lütfen önce geçerli bir Hedef Obje (ItemDataSO) seçin!", "Tamam");
                return;
            }

            GameObject oldPreview = GameObject.Find("Mecha_3D_Preview_Instance");
            if (oldPreview != null) DestroyImmediate(oldPreview);

            GameObject hostInstance = Instantiate(level.hostItemSO.prefab);
            hostInstance.name = "Mecha_3D_Preview_Instance";
            hostInstance.transform.position = Vector3.zero;
            hostInstance.transform.rotation = Quaternion.identity;

            // Load mecha model robustly
            GameObject mechaPrefab = level.customMechaPrefab;
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
            mechaInst.name = "Preview_Mecha_Silhouette";
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
                level.mechaScaleRatio,
                level.mechaOpacity,
                level.mechaLocalOffset,
                level.mechaRotationOffset,
                level.mechaWorldSize
            );

            Selection.activeGameObject = hostInstance;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
            Debug.Log($"👁️ Mecha 3D Canlı Önizlemesi Sahnede (Scene View) Odaklandı! (Mecha: {mechaInst.name})");
        }

        private static void LiveUpdateScene3DPreview(LevelDataSO level)
        {
            if (level == null || level.hostItemSO == null || level.hostItemSO.prefab == null) return;

            // Regenerate through the single source of truth (ChameleonCamouflage.EmbedMechaInHostObject)
            // instead of duplicating its scale/pose/material math here — keeps the editor preview and the
            // runtime result from ever drifting apart.
            GenerateScene3DPreview(level);
            SceneView.RepaintAll();
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

            manager.debugLevelOverride = level;
            manager.AutoFindLevelsIfEmpty();

            int targetIdx = manager.levels != null ? manager.levels.IndexOf(level) : 0;
            if (targetIdx < 0) targetIdx = 0;
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
