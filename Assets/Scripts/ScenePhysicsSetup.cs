using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Automatic scene builder for Match Factory Style 3D Search & Canvas UI Game.
    /// Configures high-angle camera, clean tray container, CanvasUIDesignManager, MatchGoalManager,
    /// PhysicsObjectSpawner, and FingerPhysicsInteraction.
    /// Includes 1-click Editor Menu items:
    /// - Tools > Setup 3D Physics Scene
    /// - Tools > Build Canvas UI Design
    /// </summary>
    [ExecuteAlways]
    public class ScenePhysicsSetup : MonoBehaviour
    {
        [Header("Play Area Line Boundary (Adjustable in Inspector)")]
        [Tooltip("Adjustable boundary line dimensions. Objects are strictly kept inside this line by code constraint.")]
        [SerializeField] private Vector2 boundaryAreaSize = new Vector2(6.35f, 6.35f);
        [Tooltip("Line colour drawn around the play area so its edge reads clearly against the solid navy background.")]
        [SerializeField] private Color boundaryLineColor = new Color(0.85f, 0.92f, 1f, 0.9f);
        [SerializeField] private float boundaryLineWidth = 0.06f;

        [Header("Match Factory Floor Tray Dimensions")]
        [SerializeField] private Vector3 containerSize = new Vector3(32f, 0.1f, 32f);

        private void Start()
        {
            if (Application.isPlaying)
            {
                SetupSceneEnvironment();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            UpdateBoundaryLineFromInspector();
        }
#endif

#if UNITY_EDITOR
        [MenuItem("Tools/Setup 3D Physics Scene")]
        public static void CreateOrSetupScene()
        {
            GameObject setupObj = GameObject.Find("Physics_Scene_Controller");
            if (setupObj == null)
            {
                setupObj = new GameObject("Physics_Scene_Controller");
            }

            ScenePhysicsSetup setup = setupObj.GetComponent<ScenePhysicsSetup>();
            if (setup == null)
            {
                setup = setupObj.AddComponent<ScenePhysicsSetup>();
            }

            setup.SetupSceneEnvironment();
            Selection.activeGameObject = setupObj;
            Debug.Log("✅ Match Factory Canvas UI & Scene Setup Completed Successfully!");
        }
#endif

        [ContextMenu("Build Scene Environment Now")]
        public void SetupSceneEnvironment()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Physics.gravity = new Vector3(0f, -15.0f, 0f);
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            Physics.defaultContactOffset = 0.008f;

            SetupCamera();
            SetupLighting();
            GameObject floorObj = CreateContainerTrayFloor();
            RemoveOldWallsAndCeiling();
            CreateVisualBoundaryLineFrame(floorObj.transform);
            SetupInteractionAndSpawner();
        }

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = Object.FindFirstObjectByType<Camera>();
            }
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();

                cam.transform.position = new Vector3(0f, 11.2f, -7.6f);
                cam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
                cam.depth = 0;
                cam.clearFlags = CameraClearFlags.Depth;
                cam.fieldOfView = 58f;
            }
        }

        private void SetupLighting()
        {
            // 1. Main Key Sunlight (Warm studio key light with soft shadows)
            Light mainLight = GameObject.Find("Main Directional Light")?.GetComponent<Light>();
            if (mainLight == null)
            {
                Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                if (lights != null && lights.Length > 0) mainLight = lights[0];
            }

            if (mainLight == null)
            {
                GameObject lightObj = new GameObject("Main Directional Light");
                mainLight = lightObj.AddComponent<Light>();
                mainLight.type = LightType.Directional;
            }

            mainLight.name = "Main Directional Light";
            mainLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            mainLight.color = new Color(1.0f, 0.96f, 0.88f); // Warm Champagne Sunlight
            mainLight.intensity = 1.35f;
            mainLight.shadows = LightShadows.Soft; // Enable Soft Shadows!
            mainLight.shadowStrength = 0.65f;
            mainLight.shadowBias = 0.05f;
            mainLight.shadowNormalBias = 0.4f;

            // 2. Rim / Fill Backlight (Cool cyan backlight for crisp 3D object separation)
            Light rimLight = GameObject.Find("Rim Backlight")?.GetComponent<Light>();
            if (rimLight == null)
            {
                GameObject rimObj = new GameObject("Rim Backlight");
                rimLight = rimObj.AddComponent<Light>();
                rimLight.type = LightType.Directional;
            }

            rimLight.name = "Rim Backlight";
            rimLight.transform.rotation = Quaternion.Euler(25f, 150f, 0f);
            rimLight.color = new Color(0.60f, 0.85f, 1.0f); // Cool Cyan Rim
            rimLight.intensity = 0.55f;
            rimLight.shadows = LightShadows.None;

            // 3. Environment Ambient Lighting (Trilight Skybox Ambient)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.85f, 0.90f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.65f, 0.72f, 0.85f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.45f, 0.55f);
            RenderSettings.ambientIntensity = 1.1f;

            SetupPostProcessingVolume();
        }

        private void SetupPostProcessingVolume()
        {
            // Setup URP Post Processing Volume
            GameObject volumeObj = GameObject.Find("Global_PostProcess_Volume");
            if (volumeObj == null)
            {
                volumeObj = new GameObject("Global_PostProcess_Volume");
            }

            var volume = volumeObj.GetComponent<UnityEngine.Rendering.Volume>();
            if (volume == null) volume = volumeObj.AddComponent<UnityEngine.Rendering.Volume>();

            volume.isGlobal = true;
            volume.priority = 1f;

            if (volume.profile == null)
            {
                var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                profile.name = "Global_PostProcess_Profile";

                // Add Bloom
                var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
                bloom.threshold.Override(0.85f);
                bloom.intensity.Override(0.35f);
                bloom.scatter.Override(0.7f);

                // Add Color Adjustments
                var colorAdj = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
                colorAdj.postExposure.Override(0.15f);
                colorAdj.contrast.Override(12f);
                colorAdj.saturation.Override(18f);

                // Add Vignette
                var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
                vignette.intensity.Override(0.22f);
                vignette.smoothness.Override(0.4f);

                volume.profile = profile;
            }
        }

        private GameObject CreateContainerTrayFloor()
        {
            Transform existingFloor = transform.Find("Container_Tray_Floor");
            if (existingFloor != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingFloor.gameObject);
#else
                Destroy(existingFloor.gameObject);
#endif
            }

            containerSize = new Vector3(32f, 0.1f, 32f);

            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "Container_Tray_Floor";
            floorObj.transform.SetParent(transform);
            floorObj.transform.position = new Vector3(0f, -containerSize.y * 0.5f, 0f);
            floorObj.transform.localScale = containerSize;

            // Stylized shadow-receiving tray floor
            Renderer rend = floorObj.GetComponent<Renderer>();
            if (rend == null) rend = floorObj.AddComponent<MeshRenderer>();
            MeshFilter mf = floorObj.GetComponent<MeshFilter>();
            if (mf == null) mf = floorObj.AddComponent<MeshFilter>();

            Shader floorShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material floorMat = new Material(floorShader) { name = "TrayShadowReceiverMat" };
            Color trayColor = new Color(0.12f, 0.16f, 0.24f, 0.95f); // Deep pastel navy tray
            if (floorMat.HasProperty("_BaseColor")) floorMat.SetColor("_BaseColor", trayColor);
            if (floorMat.HasProperty("_Color")) floorMat.SetColor("_Color", trayColor);
            if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", 0.45f);

            rend.sharedMaterial = floorMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = true; // RECEIVE SOFT SHADOWS!

            BoxCollider boxCol = floorObj.GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.sharedMaterial = new PhysicsMaterial("TrayFloorPhysics")
                {
                    dynamicFriction = 0.35f,
                    staticFriction = 0.40f,
                    bounciness = 0.0f,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            // Drop any old visual background quad
            Transform existingVisual = floorObj.transform.Find("Visual_Background");
            if (existingVisual != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingVisual.gameObject);
#else
                Destroy(existingVisual.gameObject);
#endif
            }

            Transform existingVisual2 = transform.Find("Visual_Background");
            if (existingVisual2 != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingVisual2.gameObject);
#else
                Destroy(existingVisual2.gameObject);
#endif
            }

            return floorObj;
        }

        private void RemoveOldWallsAndCeiling()
        {
            Transform walls = transform.Find("Container_Border_Walls");
            if (walls != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(walls.gameObject);
#else
                Destroy(walls.gameObject);
#endif
            }

            Transform ceiling = transform.Find("Ceiling_Barrier");
            if (ceiling != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(ceiling.gameObject);
#else
                Destroy(ceiling.gameObject);
#endif
            }
        }

        private void CreateVisualBoundaryLineFrame(Transform parent)
        {
            Transform existingFrame = transform.Find("Boundary_Line_Frame");
            LineRenderer line;
            if (existingFrame != null)
            {
                line = existingFrame.GetComponent<LineRenderer>();
                if (line == null) line = existingFrame.gameObject.AddComponent<LineRenderer>();
            }
            else
            {
                GameObject lineObj = new GameObject("Boundary_Line_Frame");
                lineObj.transform.SetParent(transform);
                lineObj.transform.position = Vector3.zero;

                line = lineObj.AddComponent<LineRenderer>();
                line.enabled = true;
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = 4;
                line.numCornerVertices = 4;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;

                Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                Material lineMat = new Material(lineShader);
                if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", boundaryLineColor);
                if (lineMat.HasProperty("_Color")) lineMat.SetColor("_Color", boundaryLineColor);
                line.sharedMaterial = lineMat;
            }

            line.startWidth = boundaryLineWidth;
            line.endWidth = boundaryLineWidth;

            float halfX = boundaryAreaSize.x * 0.5f;
            float halfZ = boundaryAreaSize.y * 0.5f;
            float yPos = 0.02f;

            line.SetPosition(0, new Vector3(-halfX, yPos, -halfZ));
            line.SetPosition(1, new Vector3(halfX, yPos, -halfZ));
            line.SetPosition(2, new Vector3(halfX, yPos, halfZ));
            line.SetPosition(3, new Vector3(-halfX, yPos, halfZ));
        }

        public void UpdateBoundaryLineFromInspector()
        {
            Transform existingFrame = transform.Find("Boundary_Line_Frame");
            if (existingFrame == null) return;
            LineRenderer line = existingFrame.GetComponent<LineRenderer>();
            if (line == null) return;

            line.startWidth = boundaryLineWidth;
            line.endWidth = boundaryLineWidth;

            if (line.sharedMaterial != null)
            {
                if (line.sharedMaterial.HasProperty("_BaseColor")) line.sharedMaterial.SetColor("_BaseColor", boundaryLineColor);
                if (line.sharedMaterial.HasProperty("_Color")) line.sharedMaterial.SetColor("_Color", boundaryLineColor);
            }

            float halfX = boundaryAreaSize.x * 0.5f;
            float halfZ = boundaryAreaSize.y * 0.5f;
            float yPos = 0.02f;

            line.SetPosition(0, new Vector3(-halfX, yPos, -halfZ));
            line.SetPosition(1, new Vector3(halfX, yPos, -halfZ));
            line.SetPosition(2, new Vector3(halfX, yPos, halfZ));
            line.SetPosition(3, new Vector3(-halfX, yPos, halfZ));
        }



        private void SetupInteractionAndSpawner()
        {
            LevelManager levelManager = GetComponent<LevelManager>();
            if (levelManager == null)
            {
                levelManager = gameObject.AddComponent<LevelManager>();
            }
            levelManager.AutoFindLevelsIfEmpty();

            MatchGoalManager goalManager = GetComponent<MatchGoalManager>();
            if (goalManager == null)
            {
                goalManager = gameObject.AddComponent<MatchGoalManager>();
            }

            CanvasUIDesignManager canvasUIDesign = GetComponent<CanvasUIDesignManager>();
            if (canvasUIDesign == null)
            {
                canvasUIDesign = gameObject.AddComponent<CanvasUIDesignManager>();
            }

            PhysicsObjectSpawner spawner = GetComponent<PhysicsObjectSpawner>();
            if (spawner == null)
            {
                spawner = gameObject.AddComponent<PhysicsObjectSpawner>();
            }

            FingerPhysicsInteraction interaction = GetComponent<FingerPhysicsInteraction>();
            if (interaction == null)
            {
                interaction = gameObject.AddComponent<FingerPhysicsInteraction>();
            }

            MechaRagdollSpawner mechaSpawner = GetComponent<MechaRagdollSpawner>();
            if (mechaSpawner == null)
            {
                mechaSpawner = gameObject.AddComponent<MechaRagdollSpawner>();
            }
            mechaSpawner.AutoFindCharacterModelsIfEmpty();
        }
    }
}
