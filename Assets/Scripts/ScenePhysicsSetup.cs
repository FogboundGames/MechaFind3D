using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MechaFind3D.PhysicsInteraction
{
    public enum BackdropMode
    {
        FloorMapped,
        CameraFacing
    }

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
        [Header("Background Settings")]
        [SerializeField] private bool showBackgroundOnFloor = true;
        [SerializeField] private string backgroundTextureName = "GameBackground";
        [SerializeField] private BackdropMode backdropMode = BackdropMode.CameraFacing;

        [Header("Play Area Line Boundary (Adjustable in Inspector)")]
        [Tooltip("Adjustable boundary line dimensions. Objects are strictly kept inside this line by code constraint.")]
        [SerializeField] private Vector2 boundaryAreaSize = new Vector2(6.35f, 6.35f);
        [SerializeField] private Color boundaryLineColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private float boundaryLineWidth = 0.0f;

        [Header("Match Factory Floor Tray Dimensions")]
        [SerializeField] private Vector3 containerSize = new Vector3(8.5f, 0.1f, 8.5f);
        [SerializeField] private Color trayFloorColor = new Color(0.12f, 0.15f, 0.20f);

        private void Start()
        {
            if (Application.isPlaying)
            {
                SetupSceneEnvironment();
            }
        }

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
            }

            cam.transform.position = new Vector3(0f, 11.2f, -7.6f);
            cam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            cam.depth = 0;
            cam.clearFlags = CameraClearFlags.Depth;
            cam.fieldOfView = 58f;
        }

        private void SetupLighting()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l != null) l.shadows = LightShadows.None;
            }

            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            dirLight.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            dirLight.color = new Color(1f, 0.97f, 0.92f);
            dirLight.intensity = 1.4f;
            dirLight.shadows = LightShadows.None;
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

            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "Container_Tray_Floor";
            floorObj.transform.SetParent(transform);
            floorObj.transform.position = new Vector3(0f, -containerSize.y * 0.5f, 0f);
            floorObj.transform.localScale = containerSize;

            // The floor stays invisible - CanvasUIDesignManager.EnsureBackgroundCanvas draws the
            // actual background (image or fallback color) via a dedicated screen-space camera/canvas
            // instead, so this cube only needs to exist for its BoxCollider.
            Renderer rend = floorObj.GetComponent<Renderer>();
            if (rend != null) DestroyImmediate(rend);
            MeshFilter mf = floorObj.GetComponent<MeshFilter>();
            if (mf != null) DestroyImmediate(mf);

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
            // Remove previous visual backgrounds if any
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

            if (showBackgroundOnFloor)
            {
                // Create Visual Background Quad
                GameObject bgVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bgVisual.name = "Visual_Background";
                
                if (backdropMode == BackdropMode.CameraFacing)
                {
                    // Parent directly to controller so it isn't squashed by floor bounds
                    bgVisual.transform.SetParent(transform);
                    // Find the actual Main Camera, not the Background Camera
                    Camera cam = null;
                    GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera");
                    if (mainCamObj != null) cam = mainCamObj.GetComponent<Camera>();
                    if (cam == null) cam = Camera.main;
                    
                    if (cam != null)
                    {
                        float distance = 20f; // Push it back so shadows and physics items fit in front
                        bgVisual.transform.position = cam.transform.position + cam.transform.forward * distance;
                        bgVisual.transform.rotation = cam.transform.rotation;
                        
                        float frustumHeight = 2.0f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        // Force the quad to exactly fill the screen frustum, regardless of image aspect ratio
                        float frustumWidth = frustumHeight * cam.aspect;
                        bgVisual.transform.localScale = new Vector3(frustumWidth, frustumHeight, 1f);
                    }
                }
                else
                {
                    // Floor mapped
                    bgVisual.transform.SetParent(floorObj.transform);
                    bgVisual.transform.localPosition = new Vector3(0f, 0.51f, 0f); // Just above the cube's top face
                    bgVisual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    bgVisual.transform.localScale = new Vector3(1.5f, 1.5f * (1024f / 682f), 1f);
                }

                Renderer bgRend = bgVisual.GetComponent<Renderer>();
                if (bgRend != null)
                {
                    bgRend.receiveShadows = false;
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
                    Material mat = new Material(shader);
                    Texture2D bgTex = Resources.Load<Texture2D>(backgroundTextureName);
                    if (bgTex != null)
                    {
                        mat.mainTexture = bgTex;
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                    }
                    else
                    {
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", trayFloorColor);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", trayFloorColor);
                    }
                    bgRend.sharedMaterial = mat;
                }
            }
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
            if (existingFrame != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(existingFrame.gameObject);
#else
                Destroy(existingFrame.gameObject);
#endif
            }

            GameObject lineObj = new GameObject("Boundary_Line_Frame");
            lineObj.transform.SetParent(transform);
            lineObj.transform.position = Vector3.zero;

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = boundaryLineWidth;
            line.endWidth = boundaryLineWidth;

            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material lineMat = new Material(lineShader);
            if (lineMat.HasProperty("_BaseColor")) lineMat.SetColor("_BaseColor", boundaryLineColor);
            if (lineMat.HasProperty("_Color")) lineMat.SetColor("_Color", boundaryLineColor);
            line.sharedMaterial = lineMat;

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
            canvasUIDesign.EnsureCanvasStructure();

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
