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
        [Tooltip("Show only every Nth arrow group. ConveyorTile.fbx ships 10 groups of 5 stripes across a tile that is already the full belt width, so 1 is the authored density; the old squashed Conveyor.fbx needed 4. Hidden groups still take part in the path, so the loop keeps its full fidelity.")]
        [Min(1)]
        [SerializeField] private int arrowGroupStride = 1;

        [Tooltip("Uniform scale on each visible stripe. LEAVE AT 1 for ConveyorTile.fbx: its stripes are modelled at final size with the gaps that make the chevron read, and scaling each one about its own pivot closes those gaps and smears the arrow into blobs. It exists for the old Conveyor.fbx, whose stripes were authored too small.")]
        [Min(0.1f)]
        [SerializeField] private float arrowScale = 1f;

        [Tooltip("Lifts each stripe off the pallet surface, as a fraction of the loop length. The stripes are zero-thickness quads sitting exactly ON the surface, so with no lift the depth buffer cannot tell which is in front and the arrows break up into speckle. Raise if they still shimmer, lower if they look detached.")]
        [SerializeField] private float arrowSurfaceLift = 0.006f;

        [Tooltip("Shrinks a stripe away as it wraps around an end cap. An arrow bending over the cap is what a real belt does, but on the tile's rounded end it reads as a skewed, broken arrow. 0 shows the bend; 1 hides the whole cap. Only the very ends of the tile are affected either way.")]
        [Range(0f, 1f)]
        [SerializeField] private float capFadeStrength = 1f;

        // The loop is now four analytic segments rather than a knotted polyline, so there are no path
        // lists left to survive a domain reload - but the arrays below still have to, and a recompile
        // during Play used to bring them back empty while loopLength and dashes survived. Advance()
        // therefore re-checks all of them and rebuilds rather than indexing into nothing.
        private Transform[] dashes;
        private int[] dashGroup;
        private Vector3[] dashLocalPos;
        private Quaternion[] dashLocalRot;
        private float[] groupRestArc;
        private float loopLength;
        private float loopRadius;
        private float loopCentreZ;
        private float loopCapX;
        private float loopStraight;
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

            // Belt and braces for the domain-reload hazard above: if the state ever comes back out of step,
            // rebuild it rather than index into nothing.
            if (loopLength <= 1e-5f
                || dashGroup == null || dashGroup.Length != dashes.Length
                || dashLocalPos == null || dashLocalRot == null || groupRestArc == null)
            {
                BuildPathFromRestPose();
                offset = 0f;
                if (dashes == null) return;
            }

            if (loopLength <= 1e-5f) return;

            offset = Mathf.Repeat(offset + distance, loopLength);

            for (int i = 0; i < dashes.Length; i++)
            {
                if (dashes[i] == null) continue;

                // Each stripe rides the loop at its OWN arc - its offset along the belt, measured inside its
                // arrow's frame - so the arrow behaves like paint on the belt: rigid along the straights
                // (where the frame never changes, so the chevron is reproduced exactly) and bending round
                // the drum at the caps. Carrying the whole arrow rigidly instead kept it flat over the cap
                // and the arrow at the tile's edge came out visibly skewed.
                //
                // In frame coordinates x is the outward normal, y runs across the belt's width and z runs
                // along travel, because the frame is LookRotation(tangent, Vector3.up) and the loop lies in
                // the tile's local XZ plane.
                int g = dashGroup[i];
                Vector3 local = dashLocalPos[i];
                float groupArc = groupRestArc[g] + offset;

                // Faded by the ARROW's arc, and its offsets shrink with it, so the whole chevron scales
                // about its own centre. Scaling only the stripes pulls them apart instead - each shrinks
                // about its own pivot and the gaps between them stay put, which is the same thing that
                // wrecked the arrows back when arrowScale was 1.8.
                float fade = CapFade(groupArc);
                float arc = groupArc + local.z * fade;
                SamplePath(arc, out Vector3 onLoop, out Quaternion frame);

                dashes[i].localPosition = onLoop
                                          + frame * new Vector3(local.x, local.y * fade, 0f)
                                          + (frame * Vector3.right) * (loopLength * arrowSurfaceLift);
                dashes[i].localRotation = frame * dashLocalRot[i];
                dashes[i].localScale = Vector3.one * (arrowScale * fade);
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

            // Group each stripe by the ROW in its Arrow_row_col name: one row is one complete arrow.
            dashGroup = new int[dashes.Length];
            var rowIds = new List<int>();
            for (int i = 0; i < dashes.Length; i++)
            {
                int row = int.Parse(dashes[i].name.Split('_')[1]);
                int g = rowIds.IndexOf(row);
                if (g < 0) { rowIds.Add(row); g = rowIds.Count - 1; }
                dashGroup[i] = g;
            }
            int groupCount = rowIds.Count;

            // The loop is measured from the PALLET, not from the arrows: it is the stadium outline of the
            // tile's own mesh in the local XZ plane (the arrows only ride on it).
            if (!TryMeasureLoop())
            {
                Debug.LogWarning($"🎞️ ConveyorBelt '{name}': palet mesh'i ölçülemedi, kayış durağan kalacak.");
                dashes = null;
                return;
            }

            // Each arrow's centre, projected onto the loop, gives the frame it was authored in; every
            // stripe's pose is stored inside that frame so the arrow travels as one rigid body.
            var centre = new Vector3[groupCount];
            var members = new int[groupCount];
            for (int i = 0; i < dashes.Length; i++)
            {
                centre[dashGroup[i]] += dashes[i].localPosition;
                members[dashGroup[i]]++;
            }

            var restFrame = new Quaternion[groupCount];
            for (int g = 0; g < groupCount; g++)
            {
                if (members[g] > 0) centre[g] /= members[g];
                SamplePath(ProjectOntoLoop(centre[g]), out Vector3 onLoop, out restFrame[g]);
                centre[g] = onLoop;
            }

            dashLocalPos = new Vector3[dashes.Length];
            dashLocalRot = new Quaternion[dashes.Length];
            for (int i = 0; i < dashes.Length; i++)
            {
                Quaternion inv = Quaternion.Inverse(restFrame[dashGroup[i]]);
                dashLocalPos[i] = inv * (dashes[i].localPosition - centre[dashGroup[i]]);
                dashLocalRot[i] = inv * dashes[i].localRotation;
            }

            // Seeded at EVEN arc intervals. The model spaces its arrows evenly, and with a true arc length
            // to seed against, even spacing is preserved everywhere including across the caps.
            float spacing = loopLength / groupCount;
            groupRestArc = new float[groupCount];
            for (int g = 0; g < groupCount; g++)
            {
                float arc = g * spacing;
                // Mirroring reverses the order the tapering motif is read in, turning the heads round.
                groupRestArc[g] = flipStripes ? Mathf.Repeat(loopLength - arc, loopLength) : arc;
            }

            ApplyArrowStyling();
        }

        /// <summary>
        /// Reads the stadium loop off the PALLET's own mesh - its local XZ footprint, with caps whose
        /// radius is half the belt's thickness. Measuring the pallet rather than the arrows is what makes
        /// the arc length true, and it also means the loop is correct even if every arrow is hidden.
        /// </summary>
        private bool TryMeasureLoop()
        {
            var filter = GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            Bounds local = filter.sharedMesh.bounds;
            loopRadius = (local.max.z - local.min.z) * 0.5f;
            loopCentreZ = (local.max.z + local.min.z) * 0.5f;
            loopCapX = (local.max.x - local.min.x) * 0.5f - loopRadius;
            loopStraight = loopCapX * 2f;
            loopLength = loopStraight * 2f + 2f * Mathf.PI * loopRadius;

            return loopRadius > 1e-5f && loopStraight > 1e-5f;
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

                // Group index comes from the stripe's own name rather than i / 5: the tile no longer ships a
                // fixed five stripes per arrow.
                bool visible = dashGroup == null || (dashGroup[i] % stride == 0);

                foreach (Renderer r in dash.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = visible;
                }

                dash.localScale = Vector3.one * Mathf.Max(0.1f, arrowScale);
            }
        }

        /// <summary>
        /// Position and frame at an arc length around the loop, evaluated ANALYTICALLY as a stadium: the
        /// top run, a true half-circle cap, the bottom run, the other cap.
        ///
        /// The path used to be a polyline knotted at the arrows themselves. Its cap was two chords cutting
        /// the corner, so the arc across a cap came out far shorter than the real curve, and an arrow
        /// coming round it closed right up on the one ahead - the run bunched every few seconds no matter
        /// how the arrows were seeded (measured gaps 0.63 / 0.64 / 0.35 / 0.64). With true arc length,
        /// evenly seeded arrows stay evenly spaced everywhere.
        /// </summary>
        private void SamplePath(float arc, out Vector3 position, out Quaternion rotation)
        {
            arc = Mathf.Repeat(arc, loopLength);

            float capLen = Mathf.PI * loopRadius;
            Vector3 tangent;

            if (arc < loopStraight)                                   // top run, travelling -X
            {
                position = new Vector3(loopCapX - arc, 0f, loopCentreZ + loopRadius);
                tangent = Vector3.left;
            }
            else if (arc < loopStraight + capLen)                     // left cap
            {
                float a = (arc - loopStraight) / loopRadius;          // 0 -> PI, from top round to bottom
                position = new Vector3(-loopCapX - Mathf.Sin(a) * loopRadius, 0f,
                                       loopCentreZ + Mathf.Cos(a) * loopRadius);
                tangent = new Vector3(-Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            else if (arc < loopStraight * 2f + capLen)                // bottom run, travelling +X
            {
                position = new Vector3(-loopCapX + (arc - loopStraight - capLen), 0f, loopCentreZ - loopRadius);
                tangent = Vector3.right;
            }
            else                                                      // right cap
            {
                float a = (arc - loopStraight * 2f - capLen) / loopRadius;
                position = new Vector3(loopCapX + Mathf.Sin(a) * loopRadius, 0f,
                                       loopCentreZ - Mathf.Cos(a) * loopRadius);
                tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
            }

            rotation = Quaternion.LookRotation(tangent, Vector3.up);
        }

        /// <summary>
        /// 1 along the straight runs, easing to <c>1 - capFadeStrength</c> across an end cap.
        ///
        /// The stripes are painted flat on the belt, so over a cap they genuinely bend round the drum -
        /// correct, but on a tile whose rounded end sits in the middle of the screen it reads as a skewed,
        /// half-crumpled arrow. Shrinking them away over the cap and back on the straight trades that for a
        /// gap at the very end of the tile, where the belt visibly turns anyway.
        /// </summary>
        private float CapFade(float arc)
        {
            if (capFadeStrength <= 0f) return 1f;

            arc = Mathf.Repeat(arc, loopLength);
            float capLen = Mathf.PI * loopRadius;
            float ease = Mathf.Min(capLen, loopStraight * 0.25f);   // ramp in over the tail of the straight

            // Distance to the nearest point that is safely on a straight run.
            float intoCap;
            if (arc < loopStraight) intoCap = -Mathf.Min(arc, loopStraight - arc);
            else if (arc < loopStraight + capLen) intoCap = Mathf.Min(arc - loopStraight, loopStraight + capLen - arc);
            else if (arc < loopStraight * 2f + capLen) intoCap = -Mathf.Min(arc - loopStraight - capLen, loopStraight * 2f + capLen - arc);
            else intoCap = Mathf.Min(arc - loopStraight * 2f - capLen, loopLength - arc);

            float t = Mathf.Clamp01((intoCap + ease) / (ease * 2f));   // 0 on the straight, 1 inside the cap
            return Mathf.Lerp(1f, 1f - capFadeStrength, Mathf.SmoothStep(0f, 1f, t));
        }

        /// <summary>Arc length whose point on the loop is closest to <paramref name="local"/>.</summary>
        private float ProjectOntoLoop(Vector3 local)
        {
            const int samples = 512;
            float best = 0f, bestSqr = float.MaxValue;
            for (int i = 0; i < samples; i++)
            {
                float arc = loopLength * i / samples;
                SamplePath(arc, out Vector3 p, out _);
                float d = (p - local).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = arc; }
            }
            return best;
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
                                          int tileCount, int arrowGroupStride, float arrowScale,
                                          float yScaleMultiplier = 1f)
        {
            if (tilePrefab == null || cam == null) return null;

            var row = new GameObject("Conveyor_Belt_3D");
            row.transform.SetParent(parent, false);
            row.transform.SetPositionAndRotation(topSurfaceCentre, rotation);

            MeasureTile(tilePrefab, rotation, cam, out float unitWidth, out float unitHeight);
            if (unitWidth < 1e-6f || unitHeight < 1e-6f) return row;

            int count;
            float scaleX;
            float scaleYZ;

            if (tileCount > 0)
            {
                // Pallet count driven directly: width scales to fit the run across the count.
                // Height and depth scale independently from tileHeight so asking for fewer tiles doesn't blow up the belt height.
                count = tileCount;
                scaleX = (rowWidth / count) / unitWidth;
                scaleYZ = (tileHeight / unitHeight) * yScaleMultiplier;
            }
            else
            {
                // Auto: size a pallet from the wanted belt thickness, then fit as many as the run takes.
                scaleYZ = (tileHeight / unitHeight) * yScaleMultiplier;
                scaleX = scaleYZ;
                count = Mathf.Max(1, Mathf.CeilToInt(rowWidth / (unitWidth * scaleX)));
            }

            float tileWorldWidth = unitWidth * scaleX;

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
                tile.transform.localScale = new Vector3(scaleX, scaleYZ, scaleYZ);
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
