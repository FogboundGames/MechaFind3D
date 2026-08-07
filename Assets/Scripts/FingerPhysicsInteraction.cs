using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Match Factory Style Fluid Touch Physics Interaction & Canvas UI Item Collection.
    /// Handles fluid rummaging AND tapping items to collect into CanvasUIDesignManager dock slots.
    /// </summary>
    public class FingerPhysicsInteraction : MonoBehaviour
    {
        [Header("Match Factory Fluid Touch Settings")]
        [Tooltip("Fluid force multiplier for effortless Match Factory style rummaging.")]
        [SerializeField] private float pushForceMultiplier = 1.8f;

        [Tooltip("Radius around touch point that pushes nearby items aside.")]
        [SerializeField] private float interactionRadius = 1.6f;

        [Tooltip("Force clamp to keep movement smooth and contained.")]
        [SerializeField] private float maxForceClamp = 3.5f;

        [Tooltip("Instant push strength for a single stationary tap (frame-rate independent, unlike the drag force).")]
        [SerializeField] private float tapPushImpulse = 2.2f;

        [Tooltip("Layer mask for physics objects.")]
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("Container Bounds Safety")]
        [Tooltip("Safety-net bounds only, kept outside the real container walls so it doesn't fight normal physics containment (which is what actually keeps items off the corners).")]
        [SerializeField] private Vector2 boxInnerBounds = new Vector2(8.2f, 8.2f);

        [Header("Visual Feedback")]
        [Tooltip("Create a visible touch indicator sphere at touch point.")]
        [SerializeField] private bool showTouchIndicator = true;

        private Camera mainCamera;
        private GameObject indicatorObject;

        private Vector3 lastTouchWorldPos;
        private Vector3 currentTouchVelocity;
        private bool isTouching = false;
        private Plane groundPlane;

        private void Awake()
        {
            FindCamera();
            groundPlane = new Plane(Vector3.up, Vector3.zero);
            CreateTouchIndicator();
        }

        private void FindCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = Object.FindFirstObjectByType<Camera>();
            }
        }

        private void CreateTouchIndicator()
        {
            if (!showTouchIndicator) return;

            indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicatorObject.name = "Touch_Visual_Indicator";
            indicatorObject.transform.SetParent(transform);
            indicatorObject.transform.localScale = Vector3.one * 0.4f;

            Collider col = indicatorObject.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = indicatorObject.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                Material mat = new Material(shader);
                Color color = new Color(0.2f, 0.75f, 1.0f, 0.45f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                rend.sharedMaterial = mat;
            }

            indicatorObject.SetActive(false);
        }

        private void Update()
        {
            if (mainCamera == null) FindCamera();
            HandleUniversalInput();
        }

        private void FixedUpdate()
        {
            if (isTouching)
            {
                ApplyMatchFactoryFluidForces(lastTouchWorldPos, currentTouchVelocity);
            }

            ClampObjectsInsideContainer();
        }

        private void HandleUniversalInput()
        {
            Vector2 screenPos = Vector2.zero;
            bool activeInput = false;
            bool inputDown = false;
            bool inputUp = false;

#if ENABLE_INPUT_SYSTEM
            if (Pointer.current != null)
            {
                activeInput = Pointer.current.press.isPressed;
                inputDown = Pointer.current.press.wasPressedThisFrame;
                inputUp = Pointer.current.press.wasReleasedThisFrame;
                screenPos = Pointer.current.position.ReadValue();
            }
            else
#endif
            {
                if (Input.touchCount > 0)
                {
                    Touch touch = Input.GetTouch(0);
                    screenPos = touch.position;
                    activeInput = true;
                    if (touch.phase == UnityEngine.TouchPhase.Began) inputDown = true;
                    if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled) inputUp = true;
                }
                else
                {
                    screenPos = Input.mousePosition;
                    activeInput = Input.GetMouseButton(0);
                    inputDown = Input.GetMouseButtonDown(0);
                    inputUp = Input.GetMouseButtonUp(0);
                }
            }

            if (inputDown)
            {
                OnTouchBegan(screenPos);
            }
            else if (activeInput && isTouching)
            {
                OnTouchMoved(screenPos);
            }
            else if (inputUp || (!activeInput && isTouching))
            {
                OnTouchEnded();
            }
        }

        private Vector3 GetTouchWorldPosition(Vector2 screenPos, out FindTargetObject hitTargetObject)
        {
            hitTargetObject = null;
            if (mainCamera == null) return Vector3.zero;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 200f, interactableLayer))
            {
                hitTargetObject = hit.collider.GetComponentInParent<FindTargetObject>();
                return hit.point;
            }

            if (groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return Vector3.zero;
        }

        private void OnTouchBegan(Vector2 screenPos)
        {
            isTouching = true;
            Vector3 worldPos = GetTouchWorldPosition(screenPos, out FindTargetObject targetItem);
            lastTouchWorldPos = worldPos;
            currentTouchVelocity = Vector3.zero;

            if (indicatorObject != null)
            {
                indicatorObject.transform.position = worldPos + Vector3.up * 0.05f;
                indicatorObject.SetActive(true);
            }

            // Collect item to CanvasUIDesignManager Dock! Fall back to a push if it couldn't be
            // docked (e.g. dock full or mid-match), so the tap still feels responsive.
            bool docked = targetItem != null && CanvasUIDesignManager.Instance != null
                && CanvasUIDesignManager.Instance.TryCollectItemToDock(targetItem);

            if (!docked)
            {
                ApplyInstantTapPush(worldPos);
            }
        }

        private void OnTouchMoved(Vector2 screenPos)
        {
            Vector3 newWorldPos = GetTouchWorldPosition(screenPos, out _);

            Vector3 rawVelocity = (newWorldPos - lastTouchWorldPos) / Mathf.Max(Time.deltaTime, 0.001f);
            currentTouchVelocity = Vector3.ClampMagnitude(rawVelocity, 18.0f);
            lastTouchWorldPos = newWorldPos;

            if (indicatorObject != null)
            {
                indicatorObject.transform.position = newWorldPos + Vector3.up * 0.05f;
            }

            ApplyMatchFactoryFluidForces(newWorldPos, currentTouchVelocity);
        }

        /// Continuous drag push, called every FixedUpdate while the touch is held. Uses
        /// ForceMode.Force so Unity integrates it over each physics step correctly.
        private void ApplyMatchFactoryFluidForces(Vector3 centerPoint, Vector3 swipeVelocity)
        {
            Collider[] hits = Physics.OverlapSphere(centerPoint, interactionRadius, interactableLayer);

            foreach (Collider col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 diff = rb.transform.position - centerPoint;
                    diff.y = 0.02f;

                    float dist = diff.magnitude;
                    float proximityFactor = Mathf.Clamp01(1.0f - (dist / interactionRadius));

                    Vector3 pushDir = diff.normalized * 0.65f + swipeVelocity.normalized * 0.35f;
                    pushDir.y = 0f;

                    float velocityMag = Mathf.Max(swipeVelocity.magnitude, 1.5f);
                    Vector3 force = pushDir * velocityMag * pushForceMultiplier * proximityFactor;
                    force.y = 0f;

                    force = Vector3.ClampMagnitude(force, maxForceClamp);

                    rb.AddForce(force, ForceMode.Force);
                    rb.AddTorque(Random.insideUnitSphere * (force.magnitude * 0.15f), ForceMode.Force);
                }
            }
        }

        /// One-shot nudge for a stationary tap. Needs ForceMode.Impulse (frame-rate independent)
        /// instead of the drag path's ForceMode.Force, which only integrates a meaningful amount
        /// of motion when called every FixedUpdate over a sustained drag.
        private void ApplyInstantTapPush(Vector3 centerPoint)
        {
            Collider[] hits = Physics.OverlapSphere(centerPoint, interactionRadius, interactableLayer);

            foreach (Collider col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 diff = rb.transform.position - centerPoint;
                    diff.y = 0f;

                    float dist = diff.magnitude;
                    float proximityFactor = Mathf.Clamp01(1.0f - (dist / interactionRadius));

                    Vector3 impulse = diff.normalized * tapPushImpulse * proximityFactor;
                    impulse.y = 0f;

                    rb.AddForce(impulse, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * (impulse.magnitude * 0.15f), ForceMode.Impulse);
                }
            }
        }

        private void ClampObjectsInsideContainer()
        {
            float halfX = boxInnerBounds.x * 0.5f;
            float halfZ = boxInnerBounds.y * 0.5f;

            Collider[] objects = Physics.OverlapBox(transform.position + Vector3.up * 1f, new Vector3(20f, 10f, 20f), Quaternion.identity, interactableLayer);

            foreach (Collider col in objects)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 pos = rb.position - transform.position;
                    bool clamped = false;

                    if (Mathf.Abs(pos.x) > halfX)
                    {
                        pos.x = Mathf.Sign(pos.x) * halfX;
                        clamped = true;
                    }
                    if (Mathf.Abs(pos.z) > halfZ)
                    {
                        pos.z = Mathf.Sign(pos.z) * halfZ;
                        clamped = true;
                    }
                    if (pos.y < 0.0f || pos.y > 2.5f)
                    {
                        pos.y = Mathf.Clamp(pos.y, 0.1f, 2.2f);
                        clamped = true;
                    }

                    if (clamped)
                    {
                        // Only snap position back inside; don't kill velocity, or objects that
                        // reach this safety-net boundary lose momentum and pile up at the edges.
                        rb.position = transform.position + pos;
                    }
                }
            }
        }

        private void OnTouchEnded()
        {
            isTouching = false;

            if (indicatorObject != null)
            {
                indicatorObject.SetActive(false);
            }
        }
    }
}
