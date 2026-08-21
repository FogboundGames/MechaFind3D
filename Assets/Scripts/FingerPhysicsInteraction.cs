using System.Collections.Generic;
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

        [Tooltip("Radius around touch point that lights an item's outline up. Kept far tighter than interactionRadius on purpose - that one is tuned for pushing a whole cluster aside, this one is meant to read as 'the one thing your finger is actually on', not the whole push radius.")]
        [SerializeField] private float touchGlowRadius = 0.45f;

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
        [SerializeField] private bool showTouchIndicator = false;

        private Camera mainCamera;
        private GameObject indicatorObject;

        private Vector3 lastTouchWorldPos;
        private Vector3 currentTouchVelocity;
        private bool isTouching = false;
        private bool isDragging = false;
        private Vector2 touchStartScreenPos;
        private Plane groundPlane;

        // Match Factory style: the yellow outline is a live "currently under your finger" indicator, not
        // a "docked" one. These track which items are glowing right now so the set can be diffed each
        // drag frame - only what changed gets an outline call, and nothing is ever left glowing once the
        // finger moves off it or lifts.
        private readonly HashSet<FindTargetObject> touchGlowActive = new HashSet<FindTargetObject>();
        private readonly HashSet<FindTargetObject> touchGlowFrameSet = new HashSet<FindTargetObject>();

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
            // Clean up any existing indicator object
            Transform existing = transform.Find("Touch_Visual_Indicator");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            if (!showTouchIndicator)
            {
                indicatorObject = null;
                return;
            }

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
            return GetTouchWorldPosition(screenPos, out hitTargetObject, out _);
        }

        private Vector3 GetTouchWorldPosition(Vector2 screenPos, out FindTargetObject hitTargetObject, out Collider hitCollider)
        {
            hitTargetObject = null;
            hitCollider = null;
            if (mainCamera == null) return Vector3.zero;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);

            // 1. Direct RaycastAll for exact 2D screen pixel hits
            RaycastHit[] rayHits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);

            // 2. Tighter SphereCastAll (0.15f radius) for finger tap tolerance when missing exact mesh edges
            RaycastHit[] sphereHits = Physics.SphereCastAll(ray.origin, 0.15f, ray.direction, 200f, ~0, QueryTriggerInteraction.Collide);

            List<RaycastHit> allHits = new List<RaycastHit>(rayHits);
            allHits.AddRange(sphereHits);

            // HIGH-SENSITIVITY FIRST PRIORITY: Check if ANY hit touches an active Mecha collider!
            foreach (RaycastHit h in allHits)
            {
                if (h.collider == null) continue;
                FindTargetObject item = h.collider.GetComponentInParent<FindTargetObject>();
                if (item == null || item.isDocked) continue;

                if (CanvasUIDesignManager.IsHitOnMechaCollider(item, h.collider))
                {
                    hitTargetObject = item;
                    hitCollider = h.collider;
                    return h.point;
                }
            }

            // SECOND PRIORITY: Select candidate from hits using screen-space proximity
            // Eliminates depth misclicks when items overlap or rest on top of each other
            FindTargetObject bestItem = null;
            Collider bestCollider = null;
            Vector3 bestHitPoint = Vector3.zero;
            float bestScore = float.MaxValue;

            // A) Direct pixel hits (highest accuracy)
            foreach (RaycastHit h in rayHits)
            {
                if (h.collider == null) continue;
                FindTargetObject item = h.collider.GetComponentInParent<FindTargetObject>();
                if (item == null || item.isDocked) continue;

                Vector3 itemCenter = GetObjectBoundsCenter(item.gameObject);
                Vector2 itemScreenPos = mainCamera.WorldToScreenPoint(itemCenter);
                float screenDist = Vector2.Distance(screenPos, itemScreenPos);

                float score = screenDist + (h.distance * 0.05f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestItem = item;
                    bestCollider = h.collider;
                    bestHitPoint = h.point;
                }
            }

            // B) Fallback to sphere hits only if no direct pixel hit was found
            if (bestItem == null)
            {
                foreach (RaycastHit h in sphereHits)
                {
                    if (h.collider == null) continue;
                    FindTargetObject item = h.collider.GetComponentInParent<FindTargetObject>();
                    if (item == null || item.isDocked) continue;

                    Vector3 itemCenter = GetObjectBoundsCenter(item.gameObject);
                    Vector2 itemScreenPos = mainCamera.WorldToScreenPoint(itemCenter);
                    float screenDist = Vector2.Distance(screenPos, itemScreenPos);

                    float score = screenDist + (h.distance * 0.05f);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestItem = item;
                        bestCollider = h.collider;
                        bestHitPoint = h.point;
                    }
                }
            }

            if (bestItem != null)
            {
                hitTargetObject = bestItem;
                hitCollider = bestCollider;
                return bestHitPoint;
            }

            if (groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return Vector3.zero;
        }

        private Vector3 GetObjectBoundsCenter(GameObject obj)
        {
            if (obj == null) return Vector3.zero;
            Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return obj.transform.position;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null && rends[i].enabled) b.Encapsulate(rends[i].bounds);
            }
            return b.center;
        }

        private void OnTouchBegan(Vector2 screenPos)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() ||
                    (Input.touchCount > 0 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))
                {
                    return;
                }
            }

            ClearAllTouchGlow();

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
        }

        private void OnTouchMoved(Vector2 screenPos)
        {
            Vector3 newWorldPos = GetTouchWorldPosition(screenPos, out _);

            Vector3 rawVelocity = (newWorldPos - lastTouchWorldPos) / Mathf.Max(Time.deltaTime, 0.001f);
            currentTouchVelocity = Vector3.ClampMagnitude(rawVelocity, 18.0f);
            lastTouchWorldPos = newWorldPos;

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

            if (isDragging)
            {
                ApplyMatchFactoryFluidForces(newWorldPos, currentTouchVelocity);
            }
        }

        /// Continuous drag push: applies strictly horizontal force at offset contact points to rotate/flip
        /// objects flat on the tray table without launching them upward into the air.
        private void ApplyMatchFactoryFluidForces(Vector3 centerPoint, Vector3 swipeVelocity)
        {
            Collider[] hits = Physics.OverlapSphere(centerPoint, interactionRadius, interactableLayer);

            // Deliberately a SEPARATE, much tighter query than `hits` above - interactionRadius is sized to
            // push a whole cluster of items aside, and reusing it here was lighting up 8-10 items at once
            // any time the pile was dense, instead of just the one or two the finger is actually on.
            Collider[] glowHits = Physics.OverlapSphere(centerPoint, touchGlowRadius, interactableLayer);
            UpdateTouchGlow(glowHits);

            Vector3 dragDir = new Vector3(swipeVelocity.x, 0f, swipeVelocity.z);
            float speed = dragDir.magnitude;
            if (speed > 1e-4f) dragDir /= speed;
            else dragDir = Vector3.forward;

            float forceMagnitude = Mathf.Clamp(speed * pushForceMultiplier * 0.035f, 0.05f, maxForceClamp * 0.35f);

            foreach (Collider col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 com = rb.worldCenterOfMass;
                    Vector3 diff = com - centerPoint;
                    diff.y = 0f;

                    float dist = diff.magnitude;
                    float proximity = Mathf.Clamp01(1.0f - (dist / interactionRadius));

                    Vector3 radialDir = dist > 1e-4f ? diff / dist : dragDir;
                    Vector3 pushDir = (radialDir * 0.5f + dragDir * 0.5f).normalized;

                    // Strictly horizontal push force - ZERO Y component so objects stay grounded flat on the table
                    pushDir.y = 0f;
                    pushDir.Normalize();

                    Vector3 force = pushDir * forceMagnitude * proximity;

                    // Get impact point on object surface nearest to finger swipe
                    Vector3 contactPoint = GetImpactPointOnCollider(col, centerPoint, com);

                    // Controlled contact point offset for smooth ground tilting/flipping
                    Vector3 pushPoint = Vector3.Lerp(com, contactPoint, 0.50f);

                    // Smooth angular speed limit and damping so items settle quickly
                    rb.maxAngularVelocity = 10f;

#if UNITY_6000_0_OR_NEWER
                    rb.angularDamping = 2.0f;
#else
                    rb.angularDrag = 2.0f;
#endif

                    // Strictly clamp upward vertical velocity so objects never launch into the air
                    Vector3 curVel = rb.linearVelocity;
                    if (curVel.y > 0.1f)
                    {
                        curVel.y = 0.1f;
                        rb.linearVelocity = curVel;
                    }

                    // Apply horizontal force at offset contact position
                    rb.AddForceAtPosition(force, pushPoint, ForceMode.Impulse);
                }
            }
        }

        /// <summary>
        /// Diffs this frame's OverlapSphere hits against the currently-glowing set: turns the outline off
        /// for anything the finger has moved away from, on for anything newly under it. Docked items are
        /// skipped - they've already left the pile and the glow means "in the pile, under your finger".
        /// </summary>
        private void UpdateTouchGlow(Collider[] hits)
        {
            touchGlowFrameSet.Clear();
            foreach (Collider col in hits)
            {
                FindTargetObject item = col.GetComponentInParent<FindTargetObject>();
                if (item == null || item.isDocked) continue;
                touchGlowFrameSet.Add(item);
            }

            touchGlowActive.RemoveWhere(item =>
            {
                if (item != null && touchGlowFrameSet.Contains(item)) return false;
                if (item != null) item.SetYellowOutlineActive(false);
                return true;
            });

            foreach (FindTargetObject item in touchGlowFrameSet)
            {
                if (touchGlowActive.Add(item)) item.SetYellowOutlineActive(true);
            }
        }

        private void ClearAllTouchGlow()
        {
            foreach (FindTargetObject item in touchGlowActive)
            {
                if (item != null) item.SetYellowOutlineActive(false);
            }
            touchGlowActive.Clear();
        }

        private Vector3 GetImpactPointOnCollider(Collider col, Vector3 centerPoint, Vector3 fallbackCom)
        {
            if (col == null) return fallbackCom;
            try
            {
                return col.ClosestPoint(centerPoint);
            }
            catch
            {
                return col.bounds.ClosestPoint(centerPoint);
            }
        }

        private void ClampObjectsInsideContainer()
        {
        }

        private void OnTouchEnded(Vector2 screenPos)
        {
            bool wasTap = isTouching && !isDragging;

            isTouching = false;
            isDragging = false;
            ClearAllTouchGlow();

            if (indicatorObject != null)
            {
                indicatorObject.SetActive(false);
            }

            if (!wasTap) return;

            // A running/vanishing mecha has already unparented itself from its host item
            // (see MechaRunnerBehavior.StartRunningMode), so it no longer sits under any
            // FindTargetObject in the hierarchy and the lookup below would never find it.
            // Route taps on it directly instead of through the FindTargetObject pile flow.
            if (TryHandleStandaloneMechaTap(screenPos)) return;

            // When a Mecha is running loose in the scene, block tapping all other objects!
            if (MechaRunnerBehavior.IsAnyMechaRunning()) return;

            GetTouchWorldPosition(screenPos, out FindTargetObject targetItem, out Collider hitCollider);
            if (targetItem != null)
            {
                if (VFXManager.Instance != null)
                {
                    VFXManager.Instance.PlayTouchRippleVFX(targetItem.transform.position);
                }
                if (CanvasUIDesignManager.Instance != null)
                {
                    CanvasUIDesignManager.Instance.TryCollectItemToDock(targetItem, hitCollider);
                }
            }
        }

        private bool TryHandleStandaloneMechaTap(Vector2 screenPos)
        {
            if (mainCamera == null) return false;

            Ray ray = mainCamera.ScreenPointToRay(screenPos);
            RaycastHit[] rayHits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit h in rayHits)
            {
                if (TryDispatchStandaloneMechaHit(h.collider)) return true;
            }

            RaycastHit[] sphereHits = Physics.SphereCastAll(ray.origin, 0.35f, ray.direction, 200f, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit h in sphereHits)
            {
                if (TryDispatchStandaloneMechaHit(h.collider)) return true;
            }

            return false;
        }

        private bool TryDispatchStandaloneMechaHit(Collider col)
        {
            if (col == null) return false;
            MechaRunnerBehavior runner = col.GetComponentInParent<MechaRunnerBehavior>();
            if (runner == null) return false;

            // CamouflagedOnHost mecha is still parented under a host FindTargetObject, so let
            // the normal pile-tap flow below handle that first tap. Only intercept once it's
            // running loose (2nd tap = vanish) or already vanishing (swallow stray taps).
            if (runner.currentState == MechaRunnerBehavior.MechaState.RunningInArea)
            {
                runner.VanishAndDisappear();
                return true;
            }

            return runner.currentState == MechaRunnerBehavior.MechaState.Vanishing;
        }
    }
}
