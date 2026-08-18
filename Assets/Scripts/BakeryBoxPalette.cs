using UnityEngine;

/// <summary>
/// Recolours the BakeryBox prefab. Drop this on the imported root
/// (the object with BoxBody, BoxLid, Ribbon_Left, Ribbon_Right,
/// Ribbon_Bow underneath it) and pick a variant in the inspector.
///
/// Four independent colour zones, so it reads as wrapped gift paper:
///   Base    - the wrapping paper, shared by lid and body so they always match
///   Pattern - the little motifs printed on the paper
///   Ribbon  - both straps and the bow
///   Card    - the note panel on the lid
///
/// Material slot layout coming out of the FBX:
///   BoxBody       slot 0 = Wrap_Base, slot 1 = Wrap_Pattern
///   BoxLid        slot 0 = Wrap_Base, slot 1 = Wrap_Pattern, slot 2 = Card
///   Ribbon_Left   slot 0 = Ribbon
///   Ribbon_Right  slot 0 = Ribbon
///   Ribbon_Bow    slot 0 = Ribbon
///
/// Colours go through MaterialPropertyBlock, so every box can use a different
/// palette while sharing one material asset: no runtime material instances,
/// and GPU instancing keeps working.
/// </summary>
[ExecuteAlways]
public class BakeryBoxPalette : MonoBehaviour
{
    public enum Variant
    {
        GreenRed,
        RedGreen,
        PinkGold,
        NavyGold,
        KraftRed,
        MintCream,
        Custom
    }

    [System.Serializable]
    public struct Palette
    {
        public Color baseColor;
        public Color pattern;
        public Color ribbon;
        public Color card;

        public Palette(string baseHex, string patternHex, string ribbonHex, string cardHex)
        {
            baseColor = Hex(baseHex);
            pattern   = Hex(patternHex);
            ribbon    = Hex(ribbonHex);
            card      = Hex(cardHex);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out var c);
            return c;
        }
    }

    static readonly Palette[] Presets =
    {                //  base      pattern   ribbon    card
        new Palette("0E7A54", "FFFFFF", "D8231F", "FFF3DC"),  // GreenRed
        new Palette("C0392B", "FFFFFF", "1E7A4C", "FFF7E8"),  // RedGreen
        new Palette("F2A8BC", "FFFFFF", "B3123F", "FFE9C9"),  // PinkGold
        new Palette("1F3E6E", "F2D8A0", "D4A437", "FFF6E0"),  // NavyGold
        new Palette("A8794F", "F2E8D8", "B3261E", "F3EFE7"),  // KraftRed
        new Palette("6FC2AE", "FFFFFF", "E2455F", "FFE0AE"),  // MintCream
    };

    [Header("Palette")]
    public Variant variant = Variant.GreenRed;

    [Tooltip("Only used when Variant is set to Custom.")]
    public Palette custom = new Palette("0E7A54", "FFFFFF", "D8231F", "FFF3DC");

    // Both names are written, so this works on URP/HDRP (_BaseColor)
    // and the Built-in pipeline (_Color) with no changes.
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    MaterialPropertyBlock _mpb;

    void OnEnable()   { Apply(); }
    void OnValidate() { Apply(); }

    public void Apply()
    {
        Palette p = variant == Variant.Custom ? custom : Presets[(int)variant];

        Paint("BoxBody",      0, p.baseColor);
        Paint("BoxBody",      1, p.pattern);
        Paint("BoxLid",       0, p.baseColor);
        Paint("BoxLid",       1, p.pattern);
        Paint("BoxLid",       2, p.card);
        Paint("Ribbon_Left",  0, p.ribbon);
        Paint("Ribbon_Right", 0, p.ribbon);
        Paint("Ribbon_Bow",   0, p.ribbon);
    }

    void Paint(string childName, int materialIndex, Color color)
    {
        Transform t = FindDeep(transform, childName);
        if (t == null) return;

        var renderer = t.GetComponent<Renderer>();
        if (renderer == null) return;
        if (materialIndex >= renderer.sharedMaterials.Length) return;

        _mpb ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(_mpb, materialIndex);
        _mpb.SetColor(BaseColorId, color);
        _mpb.SetColor(ColorId, color);
        renderer.SetPropertyBlock(_mpb, materialIndex);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
