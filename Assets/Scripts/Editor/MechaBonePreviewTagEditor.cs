using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MechaFind3D.PhysicsInteraction.EditorTools
{
    [CustomEditor(typeof(MechaBonePreviewTag))]
    public class MechaBonePreviewTagEditor : Editor
    {
        private MechaBonePreviewTag tagComponent;

        private static readonly string[] ArmBonesL = new string[] { "shoulder_L", "upper_arm_L", "forearm_L", "hand_L" };
        private static readonly string[] ArmBonesR = new string[] { "shoulder_R", "upper_arm_R", "forearm_R", "hand_R" };
        private static readonly string[] LegBonesL = new string[] { "pelvis_L", "thigh_L", "shin_L", "foot_L", "toe_L" };
        private static readonly string[] LegBonesR = new string[] { "pelvis_R", "thigh_R", "shin_R", "foot_R", "toe_R" };
        private static readonly string[] SpineBones = new string[] { "spine", "spine_001", "spine_002", "spine_003", "spine_004", "spine_005", "spine_006" };

        private void OnEnable()
        {
            tagComponent = (MechaBonePreviewTag)target;
        }

        public override void OnInspectorGUI()
        {
            if (tagComponent == null) return;

            try
            {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Header Banner
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };
            GUILayout.Label("🦴 MECHA POZ EDITÖRÜ (INSPECTOR & SCENE)", headerStyle);

            if (tagComponent.levelData != null)
            {
                EditorGUILayout.HelpBox($"Level: {tagComponent.levelData.levelTitle} (Seviye {tagComponent.levelData.levelNumber}) | Mecha Index: #{tagComponent.mechaIndex + 1}", MessageType.Info);

                MechaSpawnEntry entry = MechaBonePreviewTag.GetMechaEntry(tagComponent.levelData, tagComponent.mechaIndex);
                if (entry != null)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label("🎯 MECHA GENEL KONUM & ROTASYON", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    Vector3 newOffset = EditorGUILayout.Vector3Field("📍 Local Offset (Konum)", entry.mechaLocalOffset);
                    Vector3 newRot = EditorGUILayout.Vector3Field("🔄 Rotation Offset (Açı)", entry.mechaRotationOffset);
                    float newWrap = EditorGUILayout.Slider("🌊 Sarılma Oranı (Wrap)", entry.mechaWrapAmount, 0f, 1f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(tagComponent.levelData, "Update Mecha Root Offset");
                        entry.mechaLocalOffset = newOffset;
                        entry.mechaRotationOffset = newRot;
                        entry.mechaWrapAmount = newWrap;
                        if (tagComponent.mechaIndex == 0) tagComponent.levelData.WritePrimaryEntryBack();
                        EditorUtility.SetDirty(tagComponent.levelData);

                        if (tagComponent.mechaInstance != null)
                        {
                            tagComponent.mechaInstance.transform.localPosition = newOffset;
                            tagComponent.mechaInstance.transform.localRotation = Quaternion.Euler(newRot);
                        }
                        LevelDesignEditorWindow.RepaintWindow();
                        SceneView.RepaintAll();
                    }

                    Color rootBtnBg = GUI.backgroundColor;
                    GUI.backgroundColor = tagComponent.isEditingRoot ? new Color(0.2f, 1f, 0.4f) : new Color(0.3f, 0.8f, 1f);
                    string rootBtnText = tagComponent.isEditingRoot ? "🎯 Sahne Kök Gizmo'su AKTİF (Move/Rotate)" : "🎯 Sahne Kök Gizmo'sunu Aç (Move/Rotate)";
                    if (GUILayout.Button(rootBtnText, GUILayout.Height(24)))
                    {
                        tagComponent.isEditingRoot = !tagComponent.isEditingRoot;
                        if (tagComponent.isEditingRoot) tagComponent.selectedBoneKeyword = "";
                        EditorUtility.SetDirty(tagComponent);
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = rootBtnBg;

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
            }

            EditorGUILayout.Space(5);
            GUILayout.Label("Sahneden mecha köküne veya kemik küresine tıklayarak düzenleyebilirsiniz:", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);

            // Bone Selector Buttons Grid
            DrawBoneCategoryGrid("🦾 Sol Kol", ArmBonesL, new Color(0.3f, 0.75f, 1f));
            DrawBoneCategoryGrid("🦾 Sağ Kol", ArmBonesR, new Color(0.3f, 0.75f, 1f));
            DrawBoneCategoryGrid("🦿 Sol Bacak", LegBonesL, new Color(0.3f, 0.9f, 0.5f));
            DrawBoneCategoryGrid("🦿 Sağ Bacak", LegBonesR, new Color(0.3f, 0.9f, 0.5f));
            DrawBoneCategoryGrid("🦴 Gövde / Omurga", SpineBones, new Color(1f, 0.75f, 0.3f));

            EditorGUILayout.Space(10);

            // Active Bone Selection Card
            if (!string.IsNullOrEmpty(tagComponent.selectedBoneKeyword))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUIStyle selectedStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(1f, 0.85f, 0.1f) }
                };
                GUILayout.Label($"🎯 Seçili Kemik: {tagComponent.selectedBoneKeyword}", selectedStyle);

                // Find active override in LevelDataSO
                List<MechaBoneOverride> overrides = MechaBonePreviewTag.GetBoneOverrides(tagComponent.levelData, tagComponent.mechaIndex);
                MechaBoneOverride activeOvr = null;
                if (overrides != null)
                {
                    activeOvr = overrides.Find(o => o != null && MechaBonePreviewTag.IsBoneMatch(o.boneKeyword, tagComponent.selectedBoneKeyword));
                }

                Vector3 currentRotOffset = activeOvr != null ? activeOvr.rotationOffset : Vector3.zero;

                EditorGUI.BeginChangeCheck();
                Vector3 newRotOffset = EditorGUILayout.Vector3Field("Ek Rotasyon (Euler Offset)", currentRotOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdateBoneOffset(tagComponent.selectedBoneKeyword, newRotOffset);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🎯 Sahne Kamerası Odaklan (Focus)", GUILayout.Height(24)))
                {
                    FocusSelectedBoneInScene();
                }

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("🗑️ Kemiği Sıfırla", GUILayout.Width(120), GUILayout.Height(24)))
                {
                    UpdateBoneOffset(tagComponent.selectedBoneKeyword, Vector3.zero);
                }
                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("Henüz bir kemik seçilmedi. Yukarıdaki butonlardan birine basın veya sahnede mecha eklemlerindeki mavi kürelere tıklayın.", MessageType.None);
            }

            EditorGUILayout.Space(10);

            // Quick Preset Save Section
            if (tagComponent.levelData != null)
            {
                var entries = tagComponent.levelData.GetAllMechaEntries();
                if (tagComponent.mechaIndex < entries.Count)
                {
                    MechaSpawnEntry entry = entries[tagComponent.mechaIndex];
                    ItemDataSO hostItem = entry != null ? entry.hostItemSO : tagComponent.levelData.hostItemSO;
                    string hostName = hostItem != null ? hostItem.displayName : "Objesiz";

                    Color saveBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
                    if (GUILayout.Button($"💾 Bu Pozu Şablon Olarak Kaydet ({hostName})", GUILayout.Height(28)))
                    {
                        LevelDesignEditorWindow.SaveCurrentPoseAsPreset(entry, hostItem);
                    }
                    GUI.backgroundColor = saveBg;
                }
            }

            EditorGUILayout.EndVertical();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void DrawBoneCategoryGrid(string categoryName, string[] boneKeywords, Color btnColor)
        {
            GUILayout.Label(categoryName, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            Color defaultBg = GUI.backgroundColor;

            foreach (string kw in boneKeywords)
            {
                bool isSelected = (tagComponent.selectedBoneKeyword == kw);
                GUI.backgroundColor = isSelected ? new Color(1f, 0.85f, 0.1f) : btnColor;

                if (GUILayout.Button(kw, GUILayout.Height(22)))
                {
                    tagComponent.selectedBoneKeyword = kw;
                    EditorUtility.SetDirty(tagComponent);
                    SceneView.RepaintAll();
                }
            }

            GUI.backgroundColor = defaultBg;
            EditorGUILayout.EndHorizontal();
        }

        private void FocusSelectedBoneInScene()
        {
            if (tagComponent == null || string.IsNullOrEmpty(tagComponent.selectedBoneKeyword)) return;
            var boneData = tagComponent.boneDataList.Find(b => b != null && b.boneTransform != null && MechaBonePreviewTag.IsBoneMatch(b.keyword, tagComponent.selectedBoneKeyword));
            if (boneData != null && boneData.boneTransform != null)
            {
                Selection.activeGameObject = boneData.boneTransform.gameObject;
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            }
        }

        private void UpdateBoneOffset(string keyword, Vector3 newOffset)
        {
            if (tagComponent == null || tagComponent.levelData == null) return;

            List<MechaBoneOverride> overrides = MechaBonePreviewTag.GetBoneOverrides(tagComponent.levelData, tagComponent.mechaIndex);
            if (overrides == null) return;

            Undo.RecordObject(tagComponent.levelData, "Update Mecha Bone Override");

            var ovr = overrides.Find(o => o != null && MechaBonePreviewTag.IsBoneMatch(o.boneKeyword, keyword));
            if (newOffset == Vector3.zero)
            {
                if (ovr != null) overrides.Remove(ovr);
            }
            else
            {
                if (ovr == null)
                {
                    ovr = new MechaBoneOverride { boneKeyword = keyword };
                    overrides.Add(ovr);
                }
                ovr.rotationOffset = newOffset;
            }

            // Apply immediately to bone transform in scene
            var boneData = tagComponent.boneDataList.Find(b => b != null && b.boneTransform != null && MechaBonePreviewTag.IsBoneMatch(b.keyword, keyword));
            if (boneData != null && boneData.boneTransform != null)
            {
                boneData.boneTransform.localRotation = boneData.baseLocalRotation * Quaternion.Euler(newOffset);
            }

            EditorUtility.SetDirty(tagComponent.levelData);
            LevelDesignEditorWindow.RepaintWindow();
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            if (tagComponent == null || tagComponent.boneDataList == null || tagComponent.boneDataList.Count == 0) return;

            CompareFunction prevZTest = Handles.zTest;
            Handles.zTest = CompareFunction.Always;

            try
            {
                // 0. Master Root Position & Rotation Gizmo (Green Root Marker)
                if (tagComponent.mechaInstance != null && tagComponent.levelData != null)
                {
                    MechaSpawnEntry entry = MechaBonePreviewTag.GetMechaEntry(tagComponent.levelData, tagComponent.mechaIndex);
                    if (entry != null)
                    {
                        Vector3 rootPos = tagComponent.mechaInstance.transform.position;
                        float rootHandleSize = HandleUtility.GetHandleSize(rootPos) * 0.18f;
                        rootHandleSize = Mathf.Clamp(rootHandleSize, 0.05f, 0.35f);

                        bool isRootSelected = tagComponent.isEditingRoot;

                        Handles.color = isRootSelected
                            ? new Color(0.2f, 1f, 0.4f, 0.95f) // Bright Green
                            : new Color(0.2f, 0.8f, 0.3f, 0.75f); // Medium Green

                        if (Handles.Button(rootPos, Quaternion.identity, rootHandleSize, rootHandleSize * 1.4f, Handles.CubeHandleCap))
                        {
                            tagComponent.isEditingRoot = !tagComponent.isEditingRoot;
                            if (tagComponent.isEditingRoot) tagComponent.selectedBoneKeyword = "";
                            EditorUtility.SetDirty(tagComponent);
                            Repaint();
                            SceneView.RepaintAll();
                        }

                        GUIStyle rootLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal = { textColor = isRootSelected ? new Color(0.3f, 1f, 0.5f) : new Color(0.7f, 0.9f, 0.7f) },
                            fontSize = 12,
                            alignment = TextAnchor.MiddleCenter
                        };
                        Handles.Label(rootPos + Vector3.down * (rootHandleSize * 1.5f), isRootSelected ? "🎯 [SEÇİLİ] Mecha Kök" : "🎯 Mecha Kök", rootLabelStyle);

                        if (isRootSelected)
                        {
                            // Position Handle for local offset
                            EditorGUI.BeginChangeCheck();
                            Vector3 newWorldPos = Handles.PositionHandle(rootPos, tagComponent.mechaInstance.transform.rotation);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Vector3 deltaWorld = newWorldPos - rootPos;
                                Transform parentTrans = tagComponent.mechaInstance.transform.parent;
                                Vector3 deltaLocal = parentTrans != null ? parentTrans.InverseTransformVector(deltaWorld) : deltaWorld;

                                Undo.RecordObject(tagComponent.levelData, "Move Mecha Root Position");
                                entry.mechaLocalOffset += deltaLocal;
                                if (tagComponent.mechaIndex == 0) tagComponent.levelData.WritePrimaryEntryBack();
                                EditorUtility.SetDirty(tagComponent.levelData);

                                tagComponent.mechaInstance.transform.position = newWorldPos;
                                LevelDesignEditorWindow.RepaintWindow();
                            }

                            // Rotation Handle for rotation offset
                            EditorGUI.BeginChangeCheck();
                            Quaternion currentRot = tagComponent.mechaInstance.transform.rotation;
                            Quaternion newRot = Handles.RotationHandle(currentRot, rootPos);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Quaternion deltaRot = newRot * Quaternion.Inverse(currentRot);
                                Undo.RecordObject(tagComponent.levelData, "Rotate Mecha Root Orientation");
                                entry.mechaRotationOffset = NormalizeEulerAngles(entry.mechaRotationOffset + deltaRot.eulerAngles);
                                if (tagComponent.mechaIndex == 0) tagComponent.levelData.WritePrimaryEntryBack();
                                EditorUtility.SetDirty(tagComponent.levelData);

                                tagComponent.mechaInstance.transform.rotation = newRot;
                                LevelDesignEditorWindow.RepaintWindow();
                            }
                        }
                    }
                }

                // 1. Draw skeleton connection lines
                Handles.color = new Color(0.2f, 0.9f, 1f, 0.5f);
                foreach (var boneData in tagComponent.boneDataList)
                {
                    if (boneData != null && boneData.boneTransform != null && boneData.boneTransform.parent != null)
                    {
                        if (tagComponent.mechaInstance != null &&
                            boneData.boneTransform.parent != tagComponent.mechaInstance.transform &&
                            boneData.boneTransform.parent != tagComponent.transform)
                        {
                            Handles.DrawLine(boneData.boneTransform.position, boneData.boneTransform.parent.position, 2.5f);
                        }
                    }
                }

                // 2. Draw handles for bones
                for (int i = 0; i < tagComponent.boneDataList.Count; i++)
                {
                    var boneData = tagComponent.boneDataList[i];
                    if (boneData == null || boneData.boneTransform == null) continue;

                    Vector3 bonePos = boneData.boneTransform.position;
                    bool isSelected = MechaBonePreviewTag.IsBoneMatch(boneData.keyword, tagComponent.selectedBoneKeyword);

                    float handleSize = HandleUtility.GetHandleSize(bonePos) * 0.12f;
                    handleSize = Mathf.Clamp(handleSize, 0.03f, 0.25f);

                    Handles.color = isSelected
                        ? new Color(1f, 0.85f, 0.1f, 0.95f) // Bright Gold/Yellow
                        : new Color(0.15f, 0.85f, 1f, 0.85f); // Bright Cyan/Blue

                    float buttonCapSize = isSelected ? handleSize * 1.35f : handleSize;

                    if (Handles.Button(bonePos, Quaternion.identity, buttonCapSize, buttonCapSize * 1.4f, Handles.SphereHandleCap))
                    {
                        tagComponent.selectedBoneKeyword = boneData.keyword;
                        EditorUtility.SetDirty(tagComponent);
                        Repaint();
                        SceneView.RepaintAll();
                    }

                    if (isSelected)
                    {
                        // Accent outer ring
                        Handles.color = new Color(1f, 0.95f, 0.3f, 0.85f);
                        Handles.DrawWireDisc(bonePos, SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform.forward : Vector3.forward, buttonCapSize * 1.25f);

                        // Label
                        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal = { textColor = new Color(1f, 0.95f, 0.2f) },
                            fontSize = 13,
                            alignment = TextAnchor.MiddleCenter
                        };
                        Handles.Label(bonePos + Vector3.up * (handleSize * 1.8f), $"🦴 {boneData.keyword}", labelStyle);

                        // Rotation Handle
                        EditorGUI.BeginChangeCheck();
                        Quaternion currentWorldRot = boneData.boneTransform.rotation;
                        Quaternion newWorldRot = Handles.RotationHandle(currentWorldRot, bonePos);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(boneData.boneTransform, "Rotate Mecha Bone");
                            boneData.boneTransform.rotation = newWorldRot;

                            Quaternion newLocalRot = boneData.boneTransform.localRotation;
                            Quaternion offsetQuat = Quaternion.Inverse(boneData.baseLocalRotation) * newLocalRot;
                            Vector3 rawEuler = offsetQuat.eulerAngles;

                            Vector3 normalizedOffset = new Vector3(
                                NormalizeAngle(rawEuler.x),
                                NormalizeAngle(rawEuler.y),
                                NormalizeAngle(rawEuler.z)
                            );

                            UpdateBoneOffset(boneData.keyword, normalizedOffset);
                        }
                    }
                }
            }
            finally
            {
                Handles.zTest = prevZTest;
            }
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return Mathf.Round(angle * 10f) / 10f;
        }

        private static Vector3 NormalizeEulerAngles(Vector3 euler)
        {
            return new Vector3(
                NormalizeAngle(euler.x),
                NormalizeAngle(euler.y),
                NormalizeAngle(euler.z)
            );
        }
    }
}
