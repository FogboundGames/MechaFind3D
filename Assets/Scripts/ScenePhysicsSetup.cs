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
        [Header("Elle Tasarım Modu")]
        [Tooltip("Açıkken kod sahnede ZATEN VAR OLAN hiçbir şeyi ezmez: zemin objesi silinip yeniden yaratılmaz, " +
                 "materyali değiştirilmez, ışık/ambient/post-process değerleri elle ayarladığın gibi kalır. " +
                 "Kod yalnızca sahnede eksik olan objeleri kurar. Kapatırsan eski davranış geri gelir ve " +
                 "her Play'de aşağıdaki varsayılan değerler sahneye yazılır.")]
        [SerializeField] private bool respectSceneSetup = true;

        [Header("Play Area Line Boundary (Adjustable in Inspector)")]
        [Tooltip("Adjustable boundary line dimensions. Objects are strictly kept inside this line by code constraint.")]
        [SerializeField] private Vector2 boundaryAreaSize = new Vector2(6.35f, 6.35f);
        [Tooltip("Line colour drawn around the play area so its edge reads clearly against the solid navy background. " +
                 "Boundary Line Material atanmışsa bu renk kullanılmaz.")]
        [SerializeField] private Color boundaryLineColor = new Color(0.85f, 0.92f, 1f, 0.9f);
        [SerializeField] private float boundaryLineWidth = 0.06f;
        [Tooltip("Sınır çizgisi için kendi materyalin. Atarsan kod ne materyali ne de rengini değiştirir.")]
        [SerializeField] private Material boundaryLineMaterial;

        [Header("Match Factory Floor Tray Dimensions")]
        [SerializeField] private Vector3 containerSize = new Vector3(32f, 0.1f, 32f);
        [Tooltip("Zemin tepsisi için kendi materyalin. Atarsan kod sıfırdan materyal üretmez ve rengine dokunmaz.")]
        [SerializeField] private Material trayFloorMaterial;
        [Tooltip("Sadece Tray Floor Material boşken ve zemin sıfırdan yaratılırken kullanılır.")]
        [SerializeField] private Color trayFloorColor = new Color(0.12f, 0.16f, 0.24f, 0.95f);
        [SerializeField, Range(0f, 1f)] private float trayFloorSmoothness = 0.45f;
        [Tooltip("Zeminin gölge alıp almayacağı. Sadece zemin sıfırdan yaratılırken uygulanır.")]
        [SerializeField] private bool trayFloorReceivesShadows = true;

        [Header("Eski Duvar/Tavan Temizliği")]
        [Tooltip("Açıkken Container_Border_Walls ve Ceiling_Barrier objeleri silinir (eski davranış). " +
                 "Elle duvar/tavan eklediysen KAPALI bırak.")]
        [SerializeField] private bool removeLegacyWallsAndCeiling = false;

        [Header("Global Fizik Ayarları")]
        [Tooltip("Açıkken Project Settings yerine aşağıdaki değerler her Play'de uygulanır.")]
        [SerializeField] private bool applyGlobalPhysicsSettings = true;
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private int vSyncCount = 0;
        [SerializeField] private Vector3 gravity = new Vector3(0f, -15.0f, 0f);
        [SerializeField] private int solverIterations = 8;
        [SerializeField] private int solverVelocityIterations = 2;
        [SerializeField] private float contactOffset = 0.008f;

        [Header("Işıklandırma")]
        [Tooltip("Açıkken sahnedeki ışıkların TÜM değerleri her Play'de aşağıdakilerle ezilir. " +
                 "Işıkları elle ayarlıyorsan KAPALI bırak - kod o zaman yalnızca hiç ışık yoksa yenisini kurar.")]
        [SerializeField] private bool overrideSceneLighting = false;
        [SerializeField] private Vector3 keyLightRotation = new Vector3(50f, -30f, 0f);
        [SerializeField] private Color keyLightColor = new Color(1.0f, 0.96f, 0.88f);
        [SerializeField] private float keyLightIntensity = 1.35f;
        [SerializeField] private LightShadows keyLightShadows = LightShadows.Soft;
        [SerializeField, Range(0f, 1f)] private float keyLightShadowStrength = 0.65f;
        [SerializeField] private float keyLightShadowBias = 0.05f;
        [SerializeField] private float keyLightShadowNormalBias = 0.4f;
        [SerializeField] private Vector3 rimLightRotation = new Vector3(25f, 150f, 0f);
        [SerializeField] private Color rimLightColor = new Color(0.60f, 0.85f, 1.0f);
        [SerializeField] private float rimLightIntensity = 0.55f;

        [Header("Ambient / Environment")]
        [Tooltip("Açıkken Lighting penceresindeki ambient ayarların her Play'de aşağıdakilerle ezilir.")]
        [SerializeField] private bool overrideAmbientLighting = false;
        [SerializeField] private Color ambientSkyColor = new Color(0.85f, 0.90f, 0.98f);
        [SerializeField] private Color ambientEquatorColor = new Color(0.65f, 0.72f, 0.85f);
        [SerializeField] private Color ambientGroundColor = new Color(0.40f, 0.45f, 0.55f);
        [SerializeField] private float ambientIntensity = 1.1f;

        [Header("Post Process (yalnızca profil boşken kurulur)")]
        [SerializeField] private float bloomThreshold = 0.85f;
        [SerializeField] private float bloomIntensity = 0.35f;
        [SerializeField] private float bloomScatter = 0.7f;
        [SerializeField] private float postExposure = 0.15f;
        [SerializeField] private float contrast = 12f;
        [SerializeField] private float saturation = 18f;
        [SerializeField] private float vignetteIntensity = 0.22f;
        [SerializeField] private float vignetteSmoothness = 0.4f;

        [Header("Kamera (yalnızca sahnede kamera yoksa kurulur)")]
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 11.2f, -7.6f);
        [SerializeField] private Vector3 cameraRotation = new Vector3(58f, 0f, 0f);
        [SerializeField] private float cameraFieldOfView = 58f;

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
            if (applyGlobalPhysicsSettings)
            {
                Application.targetFrameRate = targetFrameRate;
                QualitySettings.vSyncCount = vSyncCount;
                Physics.gravity = gravity;
                Physics.defaultSolverIterations = solverIterations;
                Physics.defaultSolverVelocityIterations = solverVelocityIterations;
                Physics.defaultContactOffset = contactOffset;
            }

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

                cam.transform.position = cameraPosition;
                cam.transform.rotation = Quaternion.Euler(cameraRotation);
                cam.depth = 0;
                cam.clearFlags = CameraClearFlags.Depth;
                cam.fieldOfView = cameraFieldOfView;
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

            // A light the designer already placed keeps every value it was given; only a light this code
            // has to invent from nothing gets the defaults below. `overrideSceneLighting` opts back into
            // the old "rewrite everything each Play" behaviour.
            bool keyLightIsNew = mainLight == null;
            if (keyLightIsNew)
            {
                GameObject lightObj = new GameObject("Main Directional Light");
                mainLight = lightObj.AddComponent<Light>();
                mainLight.type = LightType.Directional;
                mainLight.name = "Main Directional Light";
            }

            if (keyLightIsNew || overrideSceneLighting)
            {
                mainLight.transform.rotation = Quaternion.Euler(keyLightRotation);
                mainLight.color = keyLightColor; // Warm Champagne Sunlight
                mainLight.intensity = keyLightIntensity;
                mainLight.shadows = keyLightShadows; // Enable Soft Shadows!
                mainLight.shadowStrength = keyLightShadowStrength;
                mainLight.shadowBias = keyLightShadowBias;
                mainLight.shadowNormalBias = keyLightShadowNormalBias;
            }

            // 2. Rim / Fill Backlight (Cool cyan backlight for crisp 3D object separation)
            Light rimLight = GameObject.Find("Rim Backlight")?.GetComponent<Light>();
            bool rimLightIsNew = rimLight == null;
            if (rimLightIsNew)
            {
                GameObject rimObj = new GameObject("Rim Backlight");
                rimLight = rimObj.AddComponent<Light>();
                rimLight.type = LightType.Directional;
                rimLight.name = "Rim Backlight";
            }

            if (rimLightIsNew || overrideSceneLighting)
            {
                rimLight.transform.rotation = Quaternion.Euler(rimLightRotation);
                rimLight.color = rimLightColor; // Cool Cyan Rim
                rimLight.intensity = rimLightIntensity;
                rimLight.shadows = LightShadows.None;
            }

            // 3. Environment Ambient Lighting (Trilight Skybox Ambient)
            if (overrideAmbientLighting)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                RenderSettings.ambientIntensity = ambientIntensity;
            }

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
                bloom.threshold.Override(bloomThreshold);
                bloom.intensity.Override(bloomIntensity);
                bloom.scatter.Override(bloomScatter);

                // Add Color Adjustments
                var colorAdj = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
                colorAdj.postExposure.Override(postExposure);
                colorAdj.contrast.Override(contrast);
                colorAdj.saturation.Override(saturation);

                // Add Vignette
                var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
                vignette.intensity.Override(vignetteIntensity);
                vignette.smoothness.Override(vignetteSmoothness);

                volume.profile = profile;
            }
        }

        /// <summary>
        /// Returns the tray floor, building one only when the scene has none.
        ///
        /// The floor used to be destroyed and rebuilt from scratch on every Play, which threw away the
        /// hand-authored object along with its material - the reason edits to TrayShadowReceiverMat
        /// silently reverted. With <see cref="respectSceneSetup"/> on, an existing floor is handed back
        /// untouched: its transform, material, colliders and shadow flags are whatever the scene says.
        /// </summary>
        private GameObject CreateContainerTrayFloor()
        {
            Transform existingFloor = transform.Find("Container_Tray_Floor");

            if (existingFloor != null && respectSceneSetup)
            {
                return existingFloor.gameObject;
            }

            if (existingFloor != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingFloor.gameObject);
#else
                Destroy(existingFloor.gameObject);
#endif
            }

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

            if (trayFloorMaterial != null)
            {
                // Designer-supplied material: used exactly as authored, never recoloured here.
                rend.sharedMaterial = trayFloorMaterial;
            }
            else
            {
                Shader floorShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material floorMat = new Material(floorShader) { name = "TrayShadowReceiverMat" };
                if (floorMat.HasProperty("_BaseColor")) floorMat.SetColor("_BaseColor", trayFloorColor);
                if (floorMat.HasProperty("_Color")) floorMat.SetColor("_Color", trayFloorColor);
                if (floorMat.HasProperty("_Smoothness")) floorMat.SetFloat("_Smoothness", trayFloorSmoothness);
                rend.sharedMaterial = floorMat;
            }

            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = trayFloorReceivesShadows; // RECEIVE SOFT SHADOWS!

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

            return floorObj;
        }

        private void RemoveOldWallsAndCeiling()
        {
            // Opt-in now: this used to delete these objects unconditionally on every Play, so a wall or
            // ceiling added by hand could never survive.
            if (!removeLegacyWallsAndCeiling) return;

            DestroyIfPresent(transform.Find("Visual_Background"));

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

        private static void DestroyIfPresent(Transform target)
        {
            if (target == null) return;
#if UNITY_EDITOR
            DestroyImmediate(target.gameObject);
#else
            Destroy(target.gameObject);
#endif
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

                if (boundaryLineMaterial != null)
                {
                    line.sharedMaterial = boundaryLineMaterial;
                }
                else
                {
                    Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    Material lineMat = new Material(lineShader);
                    if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", boundaryLineColor);
                    if (lineMat.HasProperty("_Color")) lineMat.SetColor("_Color", boundaryLineColor);
                    line.sharedMaterial = lineMat;
                }
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

            // Only the material this script generated is recoloured from the Inspector field. Writing into
            // `sharedMaterial` in edit mode edits the asset itself, so a material the designer assigned is
            // left strictly alone - its colour is theirs to set in the material.
            if (boundaryLineMaterial == null && line.sharedMaterial != null)
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
