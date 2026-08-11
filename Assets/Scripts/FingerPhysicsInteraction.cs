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
        [SerializeField] private float pushForceMultiplier = 0.8f;

        [Tooltip("Radius around touch point that pushes nearby items aside.")]
        [SerializeField] private float interactionRadius = 1.4f;

        [Tooltip("Force clamp to keep movement smooth and contained.")]
        [SerializeField] private float maxForceClamp = 1.2f;

        [Header("Tap vs Rummage Classification")]
        [Tooltip("If the finger moves less than this many screen pixels before release, the gesture counts as a TAP (collect the item under it). Moving more than this turns the gesture into a DRAG (rummage the pile). This is what lets a single press-and-hold rummage AND a quick tap collect coexist.")]
        [SerializeField] private float tapMaxScreenMovePixels = 20f;

        [Tooltip("Layer mask for physics objects.")]
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("Custom Line Boundary Constraint (Editable in Inspector)")]
        [Tooltip("Adjustable boundary line dimensions. Objects cannot cross outside this area.")]
        [SerializeField] private Vector2 boundaryAreaSize = new Vector2(6.35f, 6.35f);

        [Header("Visual Feedback")]
        [Tooltip("Create a visible touch indicator sphere at touch point.")]
        [SerializeField] private bool showTouchIndicator = true;

        private Camera mainCamera;
        private GameObject indicatorObject;

        private Vector3 lastTouchWorldPos;
        private Vector3 currentTouchVelocity;
        private bool isTouching = false;
        private bool isDragging = false;
        private Vector2 touchStartScreenPos;
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
            if (isTouching && isDragging)
            {
                ApplyMatchFactoryFluidForces(lastTouchWorldPos, currentTouchVelocity);
            }

            EnforceLineBoundaryConstraint();
        }

        private void EnforceLineBoundaryConstraint()
        {
            float halfX = boundaryAreaSize.x * 0.5f;
            float halfZ = boundaryAreaSize.y * 0.5f;

            Collider[] cols = Physics.OverlapSphere(transform.position, 15f, interactableLayer);
            foreach (Collider c in cols)
            {
                FindTargetObject item = c.GetComponentInParent<FindTargetObject>();
                if (item == null || item.isDocked) continue;

                Rigidbody rb = item.GetComponent<Rigidbody>();
                if (rb == null || rb.isKinematic) continue;

                Vector3 pos = rb.position;
                Vector3 vel = rb.linearVelocity;
                bool clamped = false;

                if (pos.x < -halfX)
                {
                    pos.x = -halfX;
                    if (vel.x < 0f) vel.x = 0f;
                    clamped = true;
                }
                else if (pos.x > halfX)
                {
                    pos.x = halfX;
                    if (vel.x > 0f) vel.x = 0f;
                    clamped = true;
                }

                if (pos.z < -halfZ)
                {
                    pos.z = -halfZ;
                    if (vel.z < 0f) vel.z = 0f;
                    clamped = true;
                }
                else if (pos.z > halfZ)
                {
                    pos.z = halfZ;
                    if (vel.z > 0f) vel.z = 0f;
                    clamped = true;
                }

                if (pos.y < -0.05f)
                {
                    pos.y = 0.02f;
                    if (vel.y < 0f) vel.y = 0f;
                    clamped = true;
                }

                if (clamped)
                {
                    rb.position = pos;
                    rb.linearVelocity = vel;
                }
            }
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
                OnTouchEnded(screenPos);
            }
        }

        private Vector3 GetTouchWorldPosition(Vector2 screenPos, out FindTargetObject hitTargetObject)
        {
            hitTargetObject = null;
            if (mainCamera == null) return Vector3.zero;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            // RaycastAll instead of a single Raycast: the invisible Ceiling_Barrier collider (and the
            // tray walls/floor) sit between the top-down camera and the pile, so a plain Raycast keeps
            // returning one of THOSE — a collider with no FindTargetObject — and taps never resolve to
            // a block. We scan every hit and keep the NEAREST one that is an interactable, un-docked item.
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f, interactableLayer, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            Vector3 itemHitPoint = Vector3.zero;
            bool foundItem = false;

            foreach (RaycastHit h in hits)
            {
                FindTargetObject item = h.collider.GetComponentInParent<FindTargetObject>();
                if (item == null || item.isDocked) continue;

                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    hitTargetObject = item;
                    itemHitPoint = h.point;
                    foundItem = true;
                }
            }

            if (foundItem)
            {
                return itemHitPoint;
            }

            // No interactable item under the finger (e.g. an empty-space drag): fall back to the
            // ground plane so rummage forces still have a valid center point.
            if (groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return Vector3.zero;
        }

        private void OnTouchBegan(Vector2 screenPos)
        {
            isTouching = true;
            isDragging = false;
            touchStartScreenPos = screenPos;

            Vector3 worldPos = GetTouchWorldPosition(screenPos, out _);
            lastTouchWorldPos = worldPos;
            currentTouchVelocity = Vector3.zero;

            if (indicatorObject != null)
            {
                indicatorObject.transform.position = worldPos + Vector3.up * 0.05f;
                indicatorObject.SetActive(true);
            }

            // Deliberately do NOT collect or push on touch-down: at this instant we can't tell a
            // quick tap (collect) from the start of a rummage drag. The gesture is classified in
            // OnTouchMoved (becomes a drag past the movement threshold) and resolved in OnTouchEnded
            // (a tap collects the item under the finger).
        }

        private void OnTouchMoved(Vector2 screenPos)
        {
            Vector3 newWorldPos = GetTouchWorldPosition(screenPos, out _);

            Vector3 rawVelocity = (newWorldPos - lastTouchWorldPos) / Mathf.Max(Time.deltaTime, 0.001f);
            currentTouchVelocity = Vector3.ClampMagnitude(rawVelocity, 18.0f);
            lastTouchWorldPos = newWorldPos;

            // Once the finger has travelled past the tap threshold, this gesture is a rummage drag
            // for the rest of its lifetime (it can never revert to a collect-tap).
            if (!isDragging)
            {
                float movedPixels = Vector2.Distance(screenPos, touchStartScreenPos);
                float threshold = Mathf.Max(tapMaxScreenMovePixels, Screen.height * 0.02f);
                if (movedPixels > threshold)
                {
                    isDragging = true;
                }
            }

            if (indicatorObject != null)
            {
                indicatorObject.transform.position = newWorldPos + Vector3.up * 0.05f;
            }

            // Apply the rummage push right here on every move frame (in addition to the FixedUpdate
            // pass) once the gesture is a drag. This restores the punchy, responsive stir the tuned
            // scene values (pushForceMultiplier/maxForceClamp) were balanced around — applying it only
            // in FixedUpdate made rummaging feel nearly forceless.
            if (isDragging)
            {
                ApplyMatchFactoryFluidForces(newWorldPos, currentTouchVelocity);
            }
        }

        /// Continuous drag push, called every FixedUpdate while the touch is held. Uses
        /// ForceMode.Force so Unity integrates it over each physics step correctly.
        private void ApplyMatchFactoryFluidForces(Vector3 centerPoint, Vector3 swipeVelocity)
        {
            Collider[] hits = Physics.OverlapSphere(centerPoint, interactionRadius, interactableLayer);

            Vector3 dragDir = new Vector3(swipeVelocity.x, 0f, swipeVelocity.z);
            float speed = dragDir.magnitude;
            if (speed > 1e-4f) dragDir /= speed;
            else dragDir = Vector3.forward;

            float uniformForceMagnitude = Mathf.Clamp(speed * pushForceMultiplier * 0.04f, 0.08f, maxForceClamp * 0.4f);

            foreach (Collider col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 diff = rb.transform.position - centerPoint;
                    diff.y = 0f;

                    float dist = diff.magnitude;
                    float proximity = Mathf.Clamp01(1.0f - (dist / interactionRadius));

                    Vector3 radialDir = dist > 1e-4f ? diff / dist : dragDir;
                    Vector3 finalPushDir = (radialDir * 0.5f + dragDir * 0.5f).normalized;
                    finalPushDir.y = 0f;

                    Vector3 force = finalPushDir * uniformForceMagnitude * proximity;
                    rb.AddForce(force, ForceMode.Impulse);

                    Vector3 torque = Vector3.up * ((finalPushDir.x - finalPushDir.z) * 0.05f * proximity);
                    rb.AddTorque(torque, ForceMode.Impulse);
                }
            }
        }

        private void ClampObjectsInsideContainer()
        {
            // Disabled: Physical border walls (Border_North/South/East/West) handle containment cleanly.
            // Manual position clamping was forcing rigidbodies into wall colliders, causing violent physics jitter and floating.
        }

        private void OnTouchEnded(Vector2 screenPos)
        {
            // A gesture that never crossed the movement threshold is a TAP: collect the item under
            // the release point. A drag was a rummage and collects nothing.
            bool wasTap = isTouching && !isDragging;

            isTouching = false;
            isDragging = false;

            if (indicatorObject != null)
            {
                indicatorObject.SetActive(false);
            }

            if (!wasTap) return;

            GetTouchWorldPosition(screenPos, out FindTargetObject targetItem);
            if (targetItem != null && CanvasUIDesignManager.Instance != null)
            {
                // If the dock is full or a match is mid-flight the collect is refused. We intentionally
                // do nothing in that case rather than shoving the pile, which is exactly the "it scattered
                // instead of collecting" behaviour we're fixing.
                CanvasUIDesignManager.Instance.TryCollectItemToDock(targetItem);
            }
        }
    }
}
