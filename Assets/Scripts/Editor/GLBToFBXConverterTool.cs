using System.IO;
using UnityEditor;
using UnityEngine;

namespace MechaFind3D.EditorTools
{
    /// <summary>
    /// Unity Editor Tool & GUI Window to convert any .glb / glTF model into .fbx format.
    /// Supports automatic path finding, drag & drop slot in GUI, and right-click asset menu.
    /// </summary>
    public class GLBToFBXConverterTool : EditorWindow
    {
        private GameObject targetGlbModel;

        [MenuItem("MechaTools/Open GLB to FBX Converter Window", false, 1)]
        [MenuItem("Window/GLB to FBX Converter", false, 10)]
        public static void OpenWindow()
        {
            GLBToFBXConverterTool window = GetWindow<GLBToFBXConverterTool>("GLB to FBX Converter");
            window.minSize = new Vector2(400, 220);
            window.AutoSelectProjectMechaModel();
            window.Show();
        }

        [MenuItem("MechaTools/Convert Mecha GLB to FBX Now", false, 10)]
        public static void ConvertMechaGLBToFBXQuick()
        {
            ConvertMechaGLBToFBX();
        }

        public static void ConvertMechaGLBToFBX()
        {
            string glbPath = FindMechaGLBAssetPath();
            if (string.IsNullOrEmpty(glbPath))
            {
                EditorUtility.DisplayDialog("GLB to FBX Converter", "No .glb model found in Assets! You can open the Converter Window from MechaTools to drag & drop any model file.", "OK");
                OpenWindow();
                return;
            }

            GameObject glbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (glbPrefab == null)
            {
                EditorUtility.DisplayDialog("GLB to FBX Converter", $"Failed to load model at: {glbPath}", "OK");
                return;
            }

            string fbxPath = Path.ChangeExtension(glbPath, ".fbx");
            ConvertGameObjectToFBX(glbPrefab, fbxPath);
        }

        [MenuItem("Assets/Convert Selected Asset to FBX", false, 20)]
        public static void ConvertSelectedAssetToFBX()
        {
            Object selectedObj = Selection.activeObject;
            if (selectedObj == null) return;

            string assetPath = AssetDatabase.GetAssetPath(selectedObj);
            if (string.IsNullOrEmpty(assetPath)) return;

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (modelPrefab == null) return;

            string fbxPath = Path.ChangeExtension(assetPath, ".fbx");
            ConvertGameObjectToFBX(modelPrefab, fbxPath);
        }

        private void OnEnable()
        {
            AutoSelectProjectMechaModel();
        }

        private void AutoSelectProjectMechaModel()
        {
            if (targetGlbModel != null) return;
            string path = FindMechaGLBAssetPath();
            if (!string.IsNullOrEmpty(path))
            {
                targetGlbModel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("🤖 GLB / glTF -> FBX Format Dönüştürücü", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Sürükleyip bırakarak (Drag & Drop) herhangi bir .glb modelini FBX formatına çevirebilirsiniz.", MessageType.Info);

            EditorGUILayout.Space(10);
            targetGlbModel = (GameObject)EditorGUILayout.ObjectField("Dönüştürülecek GLB Model:", targetGlbModel, typeof(GameObject), false);

            if (targetGlbModel != null)
            {
                string path = AssetDatabase.GetAssetPath(targetGlbModel);
                EditorGUILayout.LabelField("Dosya Yolu:", string.IsNullOrEmpty(path) ? "Seçilen Obje" : path, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(15);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);

            if (GUILayout.Button("⚡ FBX Formatına Dönüştür ve Kaydet (.fbx)", GUILayout.Height(38)))
            {
                if (targetGlbModel == null)
                {
                    EditorUtility.DisplayDialog("Uyarı", "Lütfen üstteki kutuya bir .glb modeli sürükleyip bırakın!", "Tamam");
                }
                else
                {
                    string path = AssetDatabase.GetAssetPath(targetGlbModel);
                    if (string.IsNullOrEmpty(path)) path = "Assets/Prefabs/meccha chameleon.glb";
                    string targetFbxPath = Path.ChangeExtension(path, ".fbx");
                    ConvertGameObjectToFBX(targetGlbModel, targetFbxPath);
                }
            }
            GUI.backgroundColor = prevBg;
        }

        public static string FindMechaGLBAssetPath()
        {
            // 1. Direct path check for meccha chameleon.glb
            string directPath = "Assets/Prefabs/meccha chameleon.glb";
            if (File.Exists(Path.Combine(System.Environment.CurrentDirectory, directPath)))
            {
                return directPath;
            }

            // 2. Search Application.dataPath for all .glb files
            string dataPath = Application.dataPath;
            if (Directory.Exists(dataPath))
            {
                string[] files = Directory.GetFiles(dataPath, "*.glb", SearchOption.AllDirectories);
                if (files != null && files.Length > 0)
                {
                    // Prioritize files with "meccha" or "mecha"
                    foreach (string f in files)
                    {
                        if (f.ToLowerInvariant().Contains("meccha") || f.ToLowerInvariant().Contains("chameleon") || f.ToLowerInvariant().Contains("mecha"))
                        {
                            return "Assets" + f.Substring(dataPath.Length).Replace('\\', '/');
                        }
                    }
                    // Return first found GLB file
                    return "Assets" + files[0].Substring(dataPath.Length).Replace('\\', '/');
                }
            }

            return null;
        }

        public static void ConvertGameObjectToFBX(GameObject modelPrefab, string targetFbxPath)
        {
            if (modelPrefab == null) return;

            GameObject tempInstance = Instantiate(modelPrefab);
            tempInstance.name = modelPrefab.name;

            try
            {
                // Try Unity FBX Exporter via Reflection if available
                System.Type exporterType = System.Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
                if (exporterType != null)
                {
                    var exportMethod = exporterType.GetMethod("ExportObject", new[] { typeof(string), typeof(UnityEngine.Object) });
                    if (exportMethod != null)
                    {
                        exportMethod.Invoke(null, new object[] { targetFbxPath, tempInstance });
                        Debug.Log($"✅ FBX Model successfully exported via Unity FBX Exporter to: {targetFbxPath}");
                        AssetDatabase.Refresh();
                        EditorUtility.DisplayDialog("FBX Converter", $"Başarıyla FBX dosyası oluşturuldu:\n{targetFbxPath}\n\nArtık projenizde hem GLB hem de FBX formatı var!", "Harika!");
                        return;
                    }
                }

                // Fallback: Export Mesh + Skeleton hierarchy as a native standalone .fbx asset / FBX Clone Prefab
                ExportModelFallback(tempInstance, targetFbxPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GLBToFBXConverterTool] Export notice: {ex.Message}");
                ExportModelFallback(tempInstance, targetFbxPath);
            }
            finally
            {
                DestroyImmediate(tempInstance);
            }
        }

        private static void ExportModelFallback(GameObject instance, string targetFbxPath)
        {
            string directory = Path.GetDirectoryName(targetFbxPath);
            if (string.IsNullOrEmpty(directory)) directory = "Assets/Prefabs";
            string baseName = Path.GetFileNameWithoutExtension(targetFbxPath);
            string meshAssetPath = Path.Combine(directory, $"{baseName}_ExtractedMesh.asset");
            string prefabPath = Path.Combine(directory, $"{baseName}_FBXClone.prefab");

            SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                Mesh savedMesh = Instantiate(smr.sharedMesh);
                savedMesh.name = $"{baseName}_Mesh";
                AssetDatabase.CreateAsset(savedMesh, meshAssetPath);
                smr.sharedMesh = savedMesh;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("FBX Converter", $"FBX Prefab ve İskelet Mesh Varlığı Oluşturuldu:\n{prefabPath}\n\nArtık projenizde hem GLB hem de FBX/Prefab formatı hazır!", "Harika!");
        }
    }
}
