using System.Collections.Generic;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    /// <summary>
    /// Scrolls the stripe dashes of Conveyor.fbx around the belt loop.
    ///
    /// Conveyor.fbx ships NO animation, but its 40 `Arrow_R_C` dashes are already placed around a closed
    /// belt loop (top run, rounded right cap, bottom return, rounded left cap) with each dash's rotation
    /// already matching the loop tangent. Those 40 rest poses are therefore used directly as the path -
    /// nothing is recomputed, so the motion follows exactly what was authored.
    ///
    /// Each dash simply advances along that path by the same arc length; dashes are never reassigned to
    /// each other's slots. That matters because a dash's LENGTH is baked per column (0.320, 0.320, 0.274,
    /// 0.183, 0.091) and that 5-dash tapering motif repeats identically in all 8 rows. Advancing everything
    /// together keeps every dash at its own size, and the whole arrangement repeats every 1/8 of the loop,
    /// so the scroll is seamless.
    /// </summary>
    public class ConveyorBelt : MonoBehaviour
    {
        [Header("Scroll")]
        [Tooltip("Belt speed in LOOPS per second. Scale-independent on purpose: the loop's local perimeter depends entirely on how the FBX was scaled on import (~0.026 units here), so a raw units-per-second speed silently became ~13 loops/sec. Negative reverses direction.")]
        [SerializeField] private float speed = 0.2f;

        [Tooltip("Run the scroll automatically. Turn off to drive it manually via Advance().")]
        [SerializeField] private bool autoScroll = true;

        [Tooltip("Mirrors the stripe layout around the loop, which flips which way the arrow heads point. The arrow shape comes from the 5-stripe taper (0.320, 0.320, 0.274, 0.183, 0.091), so the tip is the SHORT end - reversing the order is the only way to turn the arrows round.")]
        [SerializeField] private bool flipStripes;

        [Header("Arrow Density")]
        [Tooltip("Show only every Nth arrow group. The tile ships 8 groups of 5 stripes, which is far too dense once several tiles are lined up - at 1 they end up about 3px apart. Hidden groups still take part in the path, so the loop keeps its full fidelity.")]
        [Min(1)]
        [SerializeField] private int arrowGroupStride = 4;

        [Tooltip("Uniform scale on each visible stripe. Enlarges the arrows without changing the belt's own thickness or how many tiles fit.")]
        [Min(0.1f)]
        [SerializeField] private float arrowScale = 1.6f;

        [Tooltip("Lifts each stripe off the pallet surface, as a fraction of the loop length. The stripes are zero-thickness quads sitting exactly ON the surface, so with no lift the depth buffer cannot tell which is in front and the arrows break up into speckle. Raise if they still shimmer, lower if they look detached.")]
        [SerializeField] private float arrowSurfaceLift = 0.006f;

        // Deliberately NOT readonly: Unity skips readonly fields when it serializes component state across
        // a domain reload, so on a script recompile during Play these three came back empty while
        // loopLength and dashes survived. Advance() then sailed past its guard and indexed an empty list,
        // throwing every frame and silently freezing the belt.
        private List<Vector3> pathPoints = new List<Vector3>();
        private List<Quaternion> pathRotations = new List<Quaternion>();
        private List<float> pathArc = new List<float>();
        private Transform[] dashes;
        private float[] dashRestArc;
        private float loopLength;
        private float offset;

        public float Speed { get => speed; set => speed = value; }

        /// <summary>Show only every Nth arrow group. 1 shows all eight the tile ships.</summary>
        public int ArrowGroupStride
        {
            get => arrowGroupStride;
            set { arrowGroupStride = Mathf.Max(1, value); ApplyArrowStyling(); }
        }

        /// <summary>Uniform scale on each visible stripe.</summary>
        public float ArrowScale
        {
            get => arrowScale;
            set { arrowScale = Mathf.Max(0.1f, value); ApplyArrowStyling(); }
        }

        /// <summary>Mirrors the stripe layout so the arrow heads point the other way. Rebuilds the path.</summary>
        public bool FlipStripes
        {
            get => flipStripes;
            set
            {
                if (flipStripes == value) return;
                flipStripes = value;
                BuildPathFromRestPose();
                offset = 0f;
            }
        }

        private void Awake()
        {
            BuildPathFromRestPose();
        }

        private void Update()
        {
            if (autoScroll) Advance(speed * loopLength * Time.deltaTime);
        }

        /// <summary>Moves every dash forward along the loop by <paramref name="distance"/> local units.</summary>
        public void Advance(float distance)
        {
            if (dashes == null) return;

            // Belt and braces for the same hazard: if the path ever comes back out of step with the rest of
            // the state, rebuild it from the dashes' current poses rather than index into nothing. They are
            // still sitting on the loop, so the rebuilt path describes the same shape.
            if (pathPoints.Count < 2 || pathArc.Count != pathPoints.Count)
            {
                BuildPathFromRestPose();
                offset = 0f;
                if (dashes == null || pathPoints.Count < 2) return;
            }

            if (loopLength <= 1e-5f) return;

            offset = Mathf.Repeat(offset + distance, loopLength);

            for (int i = 0; i < dashes.Length; i++)
            {
                if (dashes[i] == null) continue;

                float arc = Mathf.Repeat(dashRestArc[i] + offset, loopLength);
                SamplePath(arc, out Vector3 pos, out Quaternion rot);

                // A stripe is a flat quad whose normal is its local +Z, and it is authored exactly ON the
                // pallet surface. Nudging it out along that normal is what stops the two coplanar surfaces
                // from z-fighting into speckle; the normal rotates with the loop, so this stays correct
                // around the end caps as well as along the straights.
                dashes[i].localPosition = pos + (rot * Vector3.forward) * (loopLength * arrowSurfaceLift);
                dashes[i].localRotation = rot;
            }
        }

        /// <summary>
        /// Captures the dashes' imported rest poses as the loop path. They are gathered in Arrow_R_C order,
        /// which is already the order they sit around the loop.
        /// </summary>
        private void BuildPathFromRestPose()
        {
            var found = new SortedList<int, Transform>();

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Arrow_")) continue;

                string[] parts = t.name.Split('_');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[1], out int row) || !int.TryParse(parts[2], out int col)) continue;

                int key = row * 100 + col;
                if (!found.ContainsKey(key)) found.Add(key, t);
            }

            if (found.Count < 2)
            {
                Debug.LogWarning($"🎞️ ConveyorBelt '{name}': Arrow_R_C parçaları bulunamadı, kayış durağan kalacak.");
                dashes = null;
                return;
            }

            dashes = new Transform[found.Count];
            found.Values.CopyTo(dashes, 0);

            pathPoints.Clear();
            pathRotations.Clear();
            pathArc.Clear();

            foreach (Transform t in dashes)
            {
                pathPoints.Add(t.localPosition);
                pathRotations.Add(t.localRotation);
            }

            // Cumulative arc length around the CLOSED loop, so the last segment wraps back to point 0.
            float total = 0f;
            for (int i = 0; i < pathPoints.Count; i++)
            {
                pathArc.Add(total);
                Vector3 next = pathPoints[(i + 1) % pathPoints.Count];
                total += Vector3.Distance(pathPoints[i], next);
            }
            loopLength = total;

            // Mirroring the arc each stripe is seeded at reverses the order the tapering 5-stripe motif is
            // read in, so the arrow heads point the other way. The stripes still land on the loop, because
            // SamplePath returns the pose authored for wherever they end up.
            dashRestArc = new float[dashes.Length];
            for (int i = 0; i < dashes.Length; i++)
            {
                dashRestArc[i] = flipStripes ? Mathf.Repeat(loopLength - pathArc[i], loopLength) : pathArc[i];
            }

            ApplyArrowStyling();
        }

        /// <summary>
        /// Thins the arrows out and resizes them.
        ///
        /// Hidden groups are only switched off at the RENDERER - they stay in the path and keep scrolling,
        /// so thinning the arrows never coarsens the loop the visible ones ride on. The group is the row
        /// index of Arrow_R_C: each row is one complete 5-stripe arrow.
        /// </summary>
        private void ApplyArrowStyling()
        {
            if (dashes == null) return;

            int stride = Mathf.Max(1, arrowGroupStride);

            for (int i = 0; i < dashes.Length; i++)
            {
                Transform dash = dashes[i];
                if (dash == null) continue;

                // dashes[] is gathered in Arrow_R_C order, five stripes per row, so the row is i / 5.
                bool visible = (i / 5) % stride == 0;

                foreach (Renderer r in dash.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = visible;
                }

                dash.localScale = Vector3.one * Mathf.Max(0.1f, arrowScale);
            }
        }

        /// <summary>Position and rotation at an arc length around the loop, interpolated between rest samples.</summary>
        private void SamplePath(float arc, out Vector3 position, out Quaternion rotation)
        {
            int count = pathPoints.Count;

            int i = 0;
            while (i < count - 1 && pathArc[i + 1] <= arc) i++;

            float segStart = pathArc[i];
            float segEnd = (i + 1 < count) ? pathArc[i + 1] : loopLength;
            float segLen = segEnd - segStart;
            float t = segLen > 1e-6f ? (arc - segStart) / segLen : 0f;

            int next = (i + 1) % count;
            position = Vector3.Lerp(pathPoints[i], pathPoints[next], t);
            rotation = Quaternion.Slerp(pathRotations[i], pathRotations[next], t);
        }

        /// <summary>
        /// Places the belt so it covers <paramref name="uiRect"/>'s on-screen footprint at
        /// <paramref name="depth"/> units in front of the camera, with the loop face turned toward the
        /// camera so the dashes are actually visible.
        ///
        /// The prefab root carries Conveyor.fbx's baked Euler(-90,0,0) from its Z-up authoring, which puts
        /// the loop in the world XY plane. That rest rotation is composed with - never replaced by - the
        /// camera-facing rotation; assigning an absolute rotation here would lay the belt over on its side,
        /// the same trap the packaging box hit.
        /// </summary>
        /// <summary>
        /// Builds a row of belt tiles laid edge to edge across <paramref name="uiRect"/>'s screen footprint,
        /// and returns the parent holding them.
        ///
        /// The model is called ConveyorTile and is authored as one repeatable 1 x 1 segment, so the belt is
        /// TILED rather than one segment stretched across the dock - stretching would smear the stripe
        /// pattern and give a single lonely loop instead of a running belt.
        ///
        /// Nothing here assumes how big the imported tile is. Conveyor.fbx and Box.fbx both carry a x100
        /// node scale but are modelled at wildly different vertex ranges (±0.5 vs ±12), so they import at
        /// utterly different world sizes - an earlier version assumed "1 tile = 1 unit" and came out 100x
        /// too small. The tile is measured instead.
        /// </summary>
        public static GameObject BuildRow(GameObject tilePrefab, Transform parent, Camera cam,
                                          Vector3 topSurfaceCentre, Quaternion rotation,
                                          float rowWidth, float tileHeight, float speed, bool flipStripes,
                                          int tileCount, int arrowGroupStride, float arrowScale)
        {
            if (tilePrefab == null || cam == null) return null;

            var row = new GameObject("Conveyor_Belt_3D");
            row.transform.SetParent(parent, false);
            row.transform.SetPositionAndRotation(topSurfaceCentre, rotation);

            MeasureTile(tilePrefab, rotation, cam, out float unitWidth, out float unitHeight);
            if (unitWidth < 1e-6f || unitHeight < 1e-6f) return row;

            int count;
            float scale;

            if (tileCount > 0)
            {
                // Pallet count driven directly: they divide the run between them and are scaled to fill it,
                // so asking for fewer makes each one bigger. Height follows from the pallet's own aspect.
                count = tileCount;
                scale = (rowWidth / count) / unitWidth;
            }
            else
            {
                // Auto: size a pallet from the wanted belt thickness, then fit as many as the run takes.
                scale = tileHeight / unitHeight;
                count = Mathf.Max(1, Mathf.CeilToInt(rowWidth / (unitWidth * scale)));
            }

            float tileWorldWidth = unitWidth * scale;

            // The belt runs along the tile's own local X, so the row is laid out along that axis rather
            // than along the camera's right - which is what keeps the tiles in the same perspective as the
            // dock boxes instead of billboarding flat to the lens.
            Vector3 across = rotation * Vector3.right;
            float span = count * tileWorldWidth;

            for (int i = 0; i < count; i++)
            {
                GameObject tile = Instantiate(tilePrefab, row.transform);
                tile.name = $"ConveyorTile_{i}";
                tile.transform.rotation = rotation;
                tile.transform.localScale = Vector3.one * scale;
                tile.transform.position = topSurfaceCentre
                                          + across * (-span * 0.5f + (i + 0.5f) * tileWorldWidth);

                var belt = tile.GetComponent<ConveyorBelt>();
                if (belt == null) belt = tile.AddComponent<ConveyorBelt>();
                belt.Speed = speed;
                belt.FlipStripes = flipStripes;
                belt.ArrowGroupStride = arrowGroupStride;
                belt.ArrowScale = arrowScale;
            }

            // The tile's pivot is at the BOTTOM of its loop (the model runs z = 0 up to 0.30), so seeding
            // the row at the wanted surface height leaves the whole belt sitting on top of the boxes
            // instead of under them. Measuring the assembled row and dropping it by however far it
            // overshoots puts the belt's top face exactly at topSurfaceCentre, whatever the tile's pivot.
            if (TryGetWorldBounds(row, out Bounds bounds))
            {
                row.transform.position += Vector3.up * (topSurfaceCentre.y - bounds.max.y);
            }

            return row;
        }

        private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        /// <summary>
        /// Size of one unscaled tile along the camera's right and up axes, once rotated into place.
        ///
        /// Measured from each renderer's LOCAL mesh bounds pushed through its own transform, never from
        /// Renderer.bounds. Renderer.bounds is a world axis-aligned box, so for a rotated tile it is
        /// inflated by the parts of the model that stick out along other axes - and this tile is a solid
        /// box as deep as it is wide, so that inflation made it measure nearly as tall as it is long and
        /// threw both the tile scale and the tile count out.
        /// </summary>
        private static void MeasureTile(GameObject tilePrefab, Quaternion rotation, Camera cam,
                                        out float width, out float height)
        {
            GameObject probe = Instantiate(tilePrefab);
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.transform.SetPositionAndRotation(Vector3.zero, rotation);
            probe.transform.localScale = Vector3.one;

            width = OrientedExtent(probe, cam.transform.right);
            height = OrientedExtent(probe, cam.transform.up);

            DestroyImmediate(probe);
        }

        /// <summary>Extent of the object's true oriented geometry along a world axis.</summary>
        private static float OrientedExtent(GameObject go, Vector3 axis)
        {
            float min = float.MaxValue;
            float max = float.MinValue;

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;

                Bounds local = GetLocalBounds(r);
                Vector3 c = local.center;
                Vector3 e = local.extents;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        c.x + ((i & 1) == 0 ? -e.x : e.x),
                        c.y + ((i & 2) == 0 ? -e.y : e.y),
                        c.z + ((i & 4) == 0 ? -e.z : e.z));

                    float p = Vector3.Dot(r.localToWorldMatrix.MultiplyPoint3x4(corner), axis);
                    if (p < min) min = p;
                    if (p > max) max = p;
                }
            }

            return max > min ? max - min : 0f;
        }

        private static Bounds GetLocalBounds(Renderer r)
        {
            if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
            {
                return mf.sharedMesh.bounds;
            }
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                return smr.sharedMesh.bounds;
            }
            return new Bounds(Vector3.zero, Vector3.zero);
        }
    }
}
