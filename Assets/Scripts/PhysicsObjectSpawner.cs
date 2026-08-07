using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Spawns a rich 3D pile of plastic toys for Match Factory style Search & Find gameplay.
    /// Configured with low friction (0.18) and smooth drag (2.2) so toys part effortlessly.
    /// </summary>
    public class PhysicsObjectSpawner : MonoBehaviour
    {
        [Header("Match Factory Toy Pile Config")]
        [Tooltip("Total number of physics objects to spawn in the pile.")]
        [SerializeField] private int totalObjectCount = 65;

        [Tooltip("Dimensions of the spawn area plate surface (inside container tray).")]
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(5.8f, 5.8f);

        [Tooltip("Drop height range to create a stacked 3D pile.")]
        [SerializeField] private float spawnHeightMin = 0.1f;
        [SerializeField] private float spawnHeightMax = 1.5f;

        [Tooltip("Scale range for spawned objects.")]
        [SerializeField] private float minScale = 0.65f;
        [SerializeField] private float maxScale = 0.95f;

        [Header("Match Factory Smooth Toy Physics")]
        [SerializeField] private float objectMass = 1.0f;
        [SerializeField] private float linearDrag = 2.2f;
        [SerializeField] private float angularDrag = 2.5f;
        [SerializeField] private float bounciness = 0.12f;
        [SerializeField] private float friction = 0.18f;

        [Header("Material & Visuals")]
        [SerializeField] private Shader objectShader;

        private struct NamedColor
        {
            public string name;
            public Color color;

            public NamedColor(string n, Color c)
            {
                name = n;
                color = c;
            }
        }

        private readonly List<GameObject> spawnedObjects = new List<GameObject>();
        private PhysicsMaterial physicsMaterial;
        private List<NamedColor> namedColors;
        private List<Material> colorMaterials;

        private void Awake()
        {
            InitializeNamedColors();
            InitializePhysicsMaterial();
            CreateColorMaterials();
        }

        private void Start()
        {
            SpawnObjects();
        }

        private void InitializeNamedColors()
        {
            namedColors = new List<NamedColor>
            {
                new NamedColor("Kırmızı", new Color(0.95f, 0.2f, 0.2f)),
                new NamedColor("Mavi", new Color(0.2f, 0.55f, 0.95f)),
                new NamedColor("Yeşil", new Color(0.2f, 0.85f, 0.35f)),
                new NamedColor("Sarı", new Color(0.98f, 0.85f, 0.15f)),
                new NamedColor("Mor", new Color(0.65f, 0.25f, 0.9f)),
                new NamedColor("Turuncu", new Color(0.98f, 0.5f, 0.15f)),
                new NamedColor("Turkuaz", new Color(0.15f, 0.85f, 0.85f)),
                new NamedColor("Pembe", new Color(0.95f, 0.4f, 0.7f))
            };
        }

        private void InitializePhysicsMaterial()
        {
            physicsMaterial = new PhysicsMaterial("MatchFactoryToyPhysics")
            {
                dynamicFriction = friction,
                staticFriction = friction * 1.2f,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }

        private void CreateColorMaterials()
        {
            colorMaterials = new List<Material>();
            Shader shaderToUse = objectShader;
            if (shaderToUse == null)
            {
                shaderToUse = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }

            for (int i = 0; i < namedColors.Count; i++)
            {
                Material mat = new Material(shaderToUse)
                {
                    name = $"ToyMaterial_{namedColors[i].name}"
                };

                Color col = namedColors[i].color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.65f);

                colorMaterials.Add(mat);
            }
        }

        public void SpawnObjects()
        {
            ClearObjects();

            Transform container = new GameObject("Spawned_Physics_Objects").transform;
            container.SetParent(transform);

            for (int i = 0; i < totalObjectCount; i++)
            {
                bool isCube = (i % 2 == 0);
                PrimitiveType type = isCube ? PrimitiveType.Cube : PrimitiveType.Sphere;

                GameObject obj = GameObject.CreatePrimitive(type);
                obj.name = isCube ? $"PhysicsCube_{i}" : $"PhysicsSphere_{i}";
                obj.transform.SetParent(container);

                float posX = Random.Range(-spawnAreaSize.x * 0.45f, spawnAreaSize.x * 0.45f);
                float posZ = Random.Range(-spawnAreaSize.y * 0.45f, spawnAreaSize.y * 0.45f);
                float posY = Random.Range(spawnHeightMin, spawnHeightMax);
                obj.transform.position = transform.position + new Vector3(posX, posY, posZ);

                obj.transform.rotation = Random.rotation;

                float scale = Random.Range(minScale, maxScale);
                obj.transform.localScale = Vector3.one * scale;

                int colorIdx = Random.Range(0, namedColors.Count);
                NamedColor chosenColor = namedColors[colorIdx];

                Renderer rend = obj.GetComponent<Renderer>();
                if (rend != null && colorIdx < colorMaterials.Count)
                {
                    rend.sharedMaterial = colorMaterials[colorIdx];
                }

                Collider col = obj.GetComponent<Collider>();
                if (col != null)
                {
                    col.sharedMaterial = physicsMaterial;
                }

                ObjectShapeType shapeType = isCube ? ObjectShapeType.Cube : ObjectShapeType.Sphere;
                FindTargetObject targetComp = obj.AddComponent<FindTargetObject>();
                targetComp.Initialize(shapeType, chosenColor.color, chosenColor.name);

                Rigidbody rb = obj.AddComponent<Rigidbody>();
                rb.mass = objectMass * Mathf.Pow(scale, 3);
                rb.linearDamping = linearDrag;
                rb.angularDamping = angularDrag;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                spawnedObjects.Add(obj);
            }
        }

        /// Gathers the remaining (not-yet-docked) pile objects back toward the tray's center with
        /// a smooth tween instead of destroying/respawning, so leftover pieces stuck near the
        /// walls or corners get pulled back into easy reach and rejumbled.
        public void GatherAndReshuffleRemaining()
        {
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj == null) continue;

                FindTargetObject targetComp = obj.GetComponent<FindTargetObject>();
                if (targetComp != null && targetComp.isDocked) continue;

                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb == null) continue;

                float posX = Random.Range(-spawnAreaSize.x * 0.3f, spawnAreaSize.x * 0.3f);
                float posZ = Random.Range(-spawnAreaSize.y * 0.3f, spawnAreaSize.y * 0.3f);
                float posY = Random.Range(spawnHeightMin, spawnHeightMax * 0.6f);
                Vector3 targetPos = transform.position + new Vector3(posX, posY, posZ);
                Quaternion targetRot = Random.rotation;

                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;

                obj.transform.DOKill();

                Sequence seq = DOTween.Sequence();
                seq.Join(obj.transform.DOMove(targetPos, 0.5f).SetEase(Ease.InOutSine));
                seq.Join(obj.transform.DORotateQuaternion(targetRot, 0.5f).SetEase(Ease.InOutSine));
                seq.OnComplete(() =>
                {
                    if (rb != null) rb.isKinematic = false;
                });
            }
        }

        public void ClearObjects()
        {
            // Items already collected into a dock slot must survive a reshuffle: rescue them out
            // of this pile's container before it gets destroyed below.
            foreach (GameObject obj in spawnedObjects)
            {
                if (obj == null) continue;

                FindTargetObject targetComp = obj.GetComponent<FindTargetObject>();
                if (targetComp != null && targetComp.isDocked)
                {
                    obj.transform.SetParent(null, true);
                    continue;
                }

                Destroy(obj);
            }
            spawnedObjects.Clear();

            Transform oldContainer = transform.Find("Spawned_Physics_Objects");
            if (oldContainer != null)
            {
                Destroy(oldContainer.gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 center = transform.position + Vector3.up * ((spawnHeightMin + spawnHeightMax) * 0.5f);
            Vector3 size = new Vector3(spawnAreaSize.x, spawnHeightMax - spawnHeightMin, spawnAreaSize.y);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
