using DG.Tweening;
using UnityEngine;

namespace MechaFind3D.PhysicsInteraction
{
    public enum ObjectShapeType
    {
        Cube,
        Sphere
    }

    /// <summary>
    /// Component attached to each interactable physics object in the pile.
    /// Stores shape type, color, and handles click/tap selection for the Search Game.
    /// </summary>
    public class FindTargetObject : MonoBehaviour
    {
        public ObjectShapeType shapeType;
        public Color objectColor;
        public string colorName;
        [Tooltip("Set true once this item has been collected into a dock slot, so a pile reshuffle leaves it alone.")]
        public bool isDocked;

        private Renderer objectRenderer;
        private Vector3 originalScale;

        private ItemOutlineHighlighter outlineHighlighter;

        // Whatever the renderers were set to before docking hid their shadows. Restoring these exactly is
        // what keeps a hand-authored shadow setup intact: the dock used to blanket-restore ShadowCastingMode.On
        // and receiveShadows = true, so any item that had been docked once came back with different shadow
        // settings from an identical item still sitting in the pile.
        private Renderer[] shadowRenderers;
        private UnityEngine.Rendering.ShadowCastingMode[] savedShadowCasting;
        private bool[] savedReceiveShadows;

        public Vector3 OriginalScale => (originalScale != Vector3.zero ? originalScale : Vector3.one);

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
            originalScale = transform.localScale;
            outlineHighlighter = GetComponent<ItemOutlineHighlighter>();
        }

        public void Initialize(ObjectShapeType shape, Color color, string nameStr)
        {
            shapeType = shape;
            objectColor = color;
            colorName = nameStr;
            originalScale = transform.localScale;
        }

        public bool MatchesTarget(ObjectShapeType targetShape, string targetColorName)
        {
            return shapeType == targetShape && colorName.Equals(targetColorName, System.StringComparison.OrdinalIgnoreCase);
        }

        public void PlayFoundAnimation()
        {
            transform.DOKill();
            transform.localScale = originalScale;
            transform.DOPunchScale(originalScale * 0.4f, 0.5f, 8, 1f);
        }

        /// <summary>
        /// Records every renderer's current shadow settings, then turns shadows off while the item sits in
        /// the dock. Safe to call twice - a second call will not overwrite the saved state.
        /// </summary>
        public void SuppressShadowsForDock()
        {
            if (shadowRenderers != null) return;

            shadowRenderers = GetComponentsInChildren<Renderer>(true);
            savedShadowCasting = new UnityEngine.Rendering.ShadowCastingMode[shadowRenderers.Length];
            savedReceiveShadows = new bool[shadowRenderers.Length];

            for (int i = 0; i < shadowRenderers.Length; i++)
            {
                Renderer r = shadowRenderers[i];
                if (r == null) continue;

                savedShadowCasting[i] = r.shadowCastingMode;
                savedReceiveShadows[i] = r.receiveShadows;

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Puts back exactly the shadow settings <see cref="SuppressShadowsForDock"/> recorded.</summary>
        public void RestoreShadowsAfterDock()
        {
            if (shadowRenderers == null) return;

            for (int i = 0; i < shadowRenderers.Length; i++)
            {
                Renderer r = shadowRenderers[i];
                if (r == null) continue;

                r.shadowCastingMode = savedShadowCasting[i];
                r.receiveShadows = savedReceiveShadows[i];
            }

            shadowRenderers = null;
            savedShadowCasting = null;
            savedReceiveShadows = null;
        }

        /// <summary>
        /// Activates or deactivates the vibrant yellow outline pass around this object.
        /// </summary>
        public void SetYellowOutlineActive(bool active, Color? customColor = null)
        {
            if (outlineHighlighter == null)
            {
                outlineHighlighter = gameObject.AddComponent<ItemOutlineHighlighter>();
            }
            outlineHighlighter.SetOutlineActive(active, customColor);
        }
    }
}
