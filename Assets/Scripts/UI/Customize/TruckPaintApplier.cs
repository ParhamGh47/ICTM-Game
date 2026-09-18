using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Repaints one truck. Added at runtime to the truck instance that needs it - the level's player truck,
/// or the preview in the customize scene.
///
/// Painting is authoritative rather than a tint. Writing the chosen colour into the model's material
/// would only multiply it: the truck's body is baked dark teal and its wheel rims are baked black, so a
/// red truck would come out black and a lime one green. The albedo texture is therefore taken off the
/// painted slots and the colour is written straight in - which is honest, because the model's own
/// colours are flat, and its shape, panel lines and windows are geometry rather than texture.
///
/// Two rules keep this safe to run on a shared model:
///
///   * The prefab's materials are shared assets (the truck's are embedded in the FBX, the ice cream's
///     are <c>Cone.mat</c> / <c>IceCream.mat</c>). Writing to them would repaint every truck in the game,
///     and permanently, so each slot that is actually painted gets its own copy created from the
///     original. A part that is back on "Default" hands the original material back to the renderer, so
///     an untouched truck looks exactly like the prefab and allocates nothing at all.
///   * Those copies belong to this component, so it is this component's job to destroy them. That keeps
///     a level's worth of copies from surviving the scene that created them.
/// </summary>
[DisallowMultipleComponent]
public class TruckPaintApplier : MonoBehaviour
{
    private class Target
    {
        public TruckPart part;
        public Renderer renderer;
        public int slot;
        public Material original;        // the shared material the model came with
        public Material instance;        // our own copy, created the first time this slot is painted
        public bool showingInstance;     // whether the renderer currently points at that copy
        public Color modelColour;        // the colour the model has for this slot
    }

    private readonly List<Target> targets = new List<Target>();

    /// <summary>True once <see cref="Capture"/> has run and at least one slot was found.</summary>
    public bool IsReady { get { return targets.Count > 0; } }

    private void Awake()
    {
        Capture();
    }

    // ---------------------------------------------------------------- discovery

    /// <summary>
    /// Finds the renderer slots that make up each part. Runs once; calling it again would read the
    /// materials this component has already replaced.
    /// </summary>
    public void Capture()
    {
        if (targets.Count > 0) return;

        // Mesh renderers only: a ParticleSystemRenderer and a TrailRenderer are Renderers too, and
        // neither should ever be repainted.
        MeshRenderer[] meshes = GetComponentsInChildren<MeshRenderer>(true);
        SkinnedMeshRenderer[] skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);

        List<Renderer> renderers = new List<Renderer>(meshes.Length + skinned.Length);
        for (int i = 0; i < meshes.Length; i++) renderers.Add(meshes[i]);
        for (int i = 0; i < skinned.Length; i++) renderers.Add(skinned[i]);

        for (int p = 0; p < TruckPaint.PartCount; p++)
        {
            TruckPart part = (TruckPart)p;
            string[] names = TruckPaint.ObjectNamesOf(part);
            SlotRule rule = TruckPaint.SlotRuleOf(part);

            List<Target> found = new List<Target>();
            List<string> seen = new List<string>();          // for the message, if the search comes up empty

            for (int r = 0; r < renderers.Count; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null || !Matches(renderer.gameObject.name, names)) continue;

                Material[] materials = renderer.sharedMaterials;
                for (int s = 0; s < materials.Length; s++)
                {
                    if (materials[s] == null) continue;

                    seen.Add(string.Format("{0}[{1}]={2}", renderer.gameObject.name, s, Label(materials[s])));

                    if (!Allows(rule, s, materials[s])) continue;

                    found.Add(new Target
                    {
                        part = part,
                        renderer = renderer,
                        slot = s,
                        original = materials[s],
                        modelColour = ReadColour(materials[s]),
                    });
                }
            }

            if (found.Count == 0)
            {
                Debug.LogWarning(string.Format(
                    "[TruckPaint] Found no slot to paint for the '{0}' part on '{1}': looked for {2}, saw {3}. " +
                    "That part will not be repainted - check the model's object and material names.",
                    TruckPaint.PartLabels[p], name, string.Join(", ", names),
                    seen.Count > 0 ? string.Join(", ", seen) : "no matching mesh"));
            }

            targets.AddRange(found);
        }
    }

    private static bool Matches(string objectName, string[] names)
    {
        if (string.IsNullOrEmpty(objectName)) return false;

        for (int i = 0; i < names.Length; i++)
            if (objectName.IndexOf(names[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        return false;
    }

    private static bool Allows(SlotRule rule, int slot, Material material)
    {
        switch (rule)
        {
            case SlotRule.All: return true;
            case SlotRule.First: return slot == 0;
            case SlotRule.Windows: return IsWindow(material);
            case SlotRule.ExceptWindows: return !IsWindow(material);
        }

        return true;
    }

    /// <summary>
    /// Whether a slot is the model's glass. The body carries its windows in a slot of its own, and the
    /// imported material and its texture are both named after the window art, so the name is what tells
    /// the paint from the glass without hard-coding which slot it is.
    /// </summary>
    private static bool IsWindow(Material material)
    {
        return Label(material).IndexOf("window", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// What a slot calls itself. The texture is the better guide, because the art's texture names are the
    /// names the model was built with; the material name is only a fallback for a slot with no texture.
    /// </summary>
    private static string Label(Material material)
    {
        Texture texture = material.mainTexture;

        return texture != null ? texture.name : material.name;
    }

    // ---------------------------------------------------------------- applying

    /// <summary>Writes the saved paint job onto this truck. Does nothing where nothing was chosen.</summary>
    public void Apply()
    {
        Capture();

        for (int p = 0; p < TruckPaint.PartCount; p++)
        {
            TruckPart part = (TruckPart)p;

            bool painted = TruckPaint.IsPainted(part);
            Color colour = TruckPaint.ColourOf(part);
            int style = TruckPaint.StyleOf(part);

            for (int i = 0; i < targets.Count; i++)
            {
                Target target = targets[i];
                if (target.part != part) continue;

                if (painted) Paint(target, colour, style);
                else Restore(target);
            }
        }
    }

    /// <summary>The colour the model itself uses for a part, for the picker's starting point.</summary>
    public Color ModelColourOf(TruckPart part)
    {
        Capture();

        for (int i = 0; i < targets.Count; i++)
            if (targets[i].part == part)
                return targets[i].modelColour;

        return TruckPaint.FallbackColour;
    }

    private void Paint(Target target, Color colour, int style)
    {
        if (target.instance == null)
        {
            // A private copy of the shared material. Created once and reused from then on, so dragging
            // through colours in the customize scene does not allocate a material per frame.
            target.instance = new Material(target.original);
            target.instance.name = target.original.name + " (Painted)";
        }

        Write(target.instance, colour, style);

        if (target.showingInstance) return;

        Material[] materials = target.renderer.sharedMaterials;
        if (target.slot >= materials.Length) return;

        materials[target.slot] = target.instance;
        target.renderer.sharedMaterials = materials;
        target.showingInstance = true;
    }

    private static void Restore(Target target)
    {
        if (!target.showingInstance) return;

        Material[] materials = target.renderer.sharedMaterials;
        if (target.slot < materials.Length)
        {
            materials[target.slot] = target.original;
            target.renderer.sharedMaterials = materials;
        }

        target.showingInstance = false;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].instance != null) Destroy(targets[i].instance);

        targets.Clear();
    }

    // ---------------------------------------------------------------- finishes

    private struct Finish
    {
        public float metallic;
        public float smoothness;
        public float alpha;
        public bool transparent;
    }

    /// <summary>
    /// One entry per <see cref="PaintStyle"/>. A flat colour with a roughness change is enough to read
    /// as a different material, which keeps the finishes useful on the truck's simple, untextured
    /// shapes: a matte finish scatters, chrome catches the light, and glass is tinted.
    ///
    /// Metallic stays well short of 1 on purpose. At full metal the shader drops the diffuse completely,
    /// so a part is left showing nothing but what it reflects - which, anywhere the reflections are dim,
    /// is a black slab with a hint of the chosen colour in it. Chrome is the shiniest of the finishes
    /// rather than a true mirror, so the colour a player picked is always legible.
    ///
    /// Glass is a tinted window rather than a sheet of clear glass: it stays see-through enough to read
    /// as glass, without turning the back of the doors into a display case.
    /// </summary>
    private static readonly Finish[] Finishes =
    {
        new Finish { metallic = 0.00f, smoothness = 0.35f, alpha = 1.00f, transparent = false },  // Paint
        new Finish { metallic = 0.00f, smoothness = 0.75f, alpha = 1.00f, transparent = false },  // Gloss
        new Finish { metallic = 0.00f, smoothness = 0.06f, alpha = 1.00f, transparent = false },  // Matte
        new Finish { metallic = 0.55f, smoothness = 0.45f, alpha = 1.00f, transparent = false },  // Metallic
        new Finish { metallic = 0.65f, smoothness = 0.90f, alpha = 1.00f, transparent = false },  // Chrome
        new Finish { metallic = 0.10f, smoothness = 0.80f, alpha = 0.80f, transparent = true },   // Glass
    };

    // ---------------------------------------------------------------- shader plumbing

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int SourceBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DestinationBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    private static Color ReadColour(Material material)
    {
        if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
        if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);

        return Color.white;
    }

    private static void Write(Material material, Color colour, int style)
    {
        Finish finish = Finishes[Mathf.Clamp(style, 0, Finishes.Length - 1)];

        colour.a = finish.alpha;

        // The project renders with the built-in pipeline, so the Standard shader's albedo is _Color.
        // _BaseColor is written too when a shader has it, so a material imported from a different
        // pipeline still repaints rather than silently doing nothing.
        if (material.HasProperty(ColorId)) material.SetColor(ColorId, colour);
        if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, colour);

        // The model's own colour is baked into its texture, so the texture has to go for the paint to
        // show. White leaves the colour exactly as it was picked.
        if (material.HasProperty(MainTexId)) material.SetTexture(MainTexId, Texture2D.whiteTexture);
        if (material.HasProperty(BaseMapId)) material.SetTexture(BaseMapId, Texture2D.whiteTexture);

        if (material.HasProperty(MetallicId)) material.SetFloat(MetallicId, finish.metallic);
        if (material.HasProperty(GlossinessId)) material.SetFloat(GlossinessId, finish.smoothness);
        if (material.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, finish.smoothness);

        SetTransparent(material, finish.transparent);
    }

    /// <summary>
    /// Switches a material between the Standard shader's opaque and transparent modes. Unity keeps this
    /// spread over several properties and shader keywords rather than one flag, and the mode has to be
    /// set by hand because the material is assembled at runtime.
    /// </summary>
    private static void SetTransparent(Material material, bool transparent)
    {
        if (material.HasProperty(ModeId)) material.SetFloat(ModeId, transparent ? 3f : 0f);
        if (material.HasProperty(SourceBlendId))
            material.SetInt(SourceBlendId, (int)(transparent ? UnityEngine.Rendering.BlendMode.SrcAlpha
                                                              : UnityEngine.Rendering.BlendMode.One));
        if (material.HasProperty(DestinationBlendId))
            material.SetInt(DestinationBlendId, (int)(transparent ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
                                                                  : UnityEngine.Rendering.BlendMode.Zero));
        if (material.HasProperty(ZWriteId)) material.SetInt(ZWriteId, transparent ? 0 : 1);

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        if (transparent) material.EnableKeyword("_ALPHABLEND_ON");
        else material.DisableKeyword("_ALPHABLEND_ON");

        material.renderQueue = transparent ? (int)UnityEngine.Rendering.RenderQueue.Transparent : -1;
    }
}
