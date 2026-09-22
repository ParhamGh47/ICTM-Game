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
///
/// The lights are painted like anything else - the lens keeps the colour, and gets an emission colour to
/// glow in, so the tail and brake lights light up in it too - and because a lens is only half of a light,
/// the truck's own <c>Light</c> components are tinted to match, so the beam the headlights throw is the
/// colour the player picked as well. The front and the back are separate parts, so a truck can wear white
/// lamps in front and red ones behind.
///
/// A lamp is not the whole mesh it sits in, though. The headlight is one mesh carrying three materials -
/// the lens that lights up, the rings around it and a plain part - so painting every light-ish slot would
/// turn the chrome around the lamp into body colour. The lens is taken to be the slot the truck's own
/// <see cref="LightToggle"/> glows, which is the same slot the player sees light up when they press the
/// key; the rings and bezel are left as the model has them. The brake light works the same way from the
/// other side: it is the slot the car controller flashes when the driver brakes.
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
        public Color modelEmission;      // the glow the model has for this slot, for a lens
    }

    private class LightTarget
    {
        public Light light;
        public Color modelColour;
        public bool front;              // which end of the truck it shines from
    }

    private readonly List<Target> targets = new List<Target>();
    private readonly List<LightTarget> lights = new List<LightTarget>();

    // The headlight mesh the truck lights up, and which of its slots is the lamp: the rest of that mesh is
    // the trim around the lamp and is left alone.
    private Renderer lensRenderer;
    private int lensSlot = -1;

    // The brake lamp is the material the car controller switches on when the driver brakes, which is the
    // mesh's first material - <c>Renderer.material</c>. It is identified by the renderer rather than by a
    // position on the model, so a model oriented the other way round still lights its brake lens.
    private const int BrakeSlot = 0;

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

        CaptureLens();

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

            // A part with no object names of its own is found by its material instead, wherever on the
            // model it lives: the lights are spread over several meshes rather than sitting on one.
            bool anyObject = names == null || names.Length == 0;

            // The brake lens is the one lens the model may not name like a lamp, so the rear part covers
            // it by identity as well.
            Renderer brakeLens = part == TruckPart.BrakeLights ? FindBrakeLens() : null;

            List<Target> found = new List<Target>();
            List<string> seen = new List<string>();          // for the message, if the search comes up empty

            for (int r = 0; r < renderers.Count; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;
                if (!anyObject && !Matches(renderer.gameObject.name, names)) continue;

                // Which half of the truck this mesh is in: a lens at the front is a headlight, a lens at
                // the back is a tail light, whatever the art called it.
                bool front = IsFrontOfTruck(renderer);

                Material[] materials = renderer.sharedMaterials;
                for (int s = 0; s < materials.Length; s++)
                {
                    if (materials[s] == null) continue;

                    seen.Add(string.Format("{0}[{1}]={2}", renderer.gameObject.name, s, Label(materials[s])));

                    if (!Allows(rule, s, materials[s], renderer, brakeLens, front)) continue;

                    found.Add(new Target
                    {
                        part = part,
                        renderer = renderer,
                        slot = s,
                        original = materials[s],
                        modelColour = ReadColour(materials[s]),
                        modelEmission = ReadEmission(materials[s]),
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

        CaptureLights();
    }

    /// <summary>
    /// Finds the lamp itself: the mesh the truck's light toggle glows, and which of that mesh's slots it
    /// glows. The toggle already knows both - it is the thing that switches the lens on - so the paint
    /// system simply agrees with it, and a headlight mesh that carries its rings in another slot does not
    /// end up with chrome painted body colour.
    ///
    /// A truck with no toggle (or one whose emission object is unset) leaves this empty, and the lights
    /// fall back to being found by their material name.
    /// </summary>
    private void CaptureLens()
    {
        lensRenderer = null;
        lensSlot = -1;

        LightToggle toggle = GetComponentInChildren<LightToggle>(true);
        if (toggle == null || toggle.emissionObject == null) return;

        lensRenderer = toggle.emissionObject;
        lensSlot = Mathf.Max(0, toggle.LensMaterialIndex);
    }

    /// <summary>
    /// The lights whose colour follows the paint job. The truck's own headlights are the ones the
    /// player means, so they are used when the truck declares them; a truck without a light toggle
    /// falls back to every light it has.
    /// </summary>
    private void CaptureLights()
    {
        lights.Clear();

        LightToggle toggle = GetComponentInChildren<LightToggle>(true);
        if (toggle != null && toggle.headlights != null)
            for (int i = 0; i < toggle.headlights.Length; i++)
                AddLight(toggle.headlights[i]);

        if (lights.Count > 0) return;

        Light[] found = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < found.Length; i++)
            AddLight(found[i]);
    }

    private void AddLight(Light light)
    {
        if (light == null) return;

        LightTarget target = new LightTarget();
        target.light = light;
        target.modelColour = light.color;

        // The beam belongs to the same end of the truck as the lamp: the headlights are at the front,
        // and a truck with a rear light (a reversing lamp, say) would follow the rear part instead.
        Vector3 local = transform.InverseTransformPoint(light.transform.position);
        target.front = local.z >= 0f;

        lights.Add(target);
    }

    private static bool Matches(string objectName, string[] names)
    {
        if (string.IsNullOrEmpty(objectName)) return false;

        for (int i = 0; i < names.Length; i++)
            if (objectName.IndexOf(names[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        return false;
    }

    private bool Allows(SlotRule rule, int slot, Material material, Renderer renderer,
                        Renderer brakeLens, bool front)
    {
        switch (rule)
        {
            case SlotRule.All: return true;
            case SlotRule.First: return slot == 0;
            case SlotRule.Windows: return IsWindow(material);
            case SlotRule.ExceptWindows: return !IsWindow(material);

            // The two ends of the truck are painted separately. A lens counts for the end it is on, and
            // the brake lens counts whatever the art called it - it is the lamp the car controller
            // switches on, so it has to follow the rear part's colour.
            case SlotRule.Headlights: return front && IsHeadlightLens(slot, material, renderer);
            // The brake lamp itself is the slot the car controller switches on, and it is taken by
            // identity rather than by where it sits: a model authored the other way round, or one whose
            // rear lamps sit near the middle, still gets its brake lens painted - and lit - instead of
            // being mistaken for a headlight. The trim around it (the rings) is left as the model has it,
            // the same way the headlight's bezel is.
            case SlotRule.BrakeLights:
                if (renderer == brakeLens && slot == BrakeSlot) return true;
                return !front && IsLight(material) && !IsTrim(material);
        }

        return true;
    }

    /// <summary>
    /// Whether a slot of the headlight mesh is the lamp rather than the trim around it.
    ///
    /// The truck's headlight is a single mesh with the lens, the rings around it and a plain part in three
    /// slots, and the toggle glows one of them: that one is the lamp, and the rest is chrome and bezel that
    /// the model should keep. Where the truck declares no toggle or no emission object, the material's own
    /// name is the only guide left, and the trim is then told apart by name.
    /// </summary>
    private bool IsHeadlightLens(int slot, Material material, Renderer renderer)
    {
        if (lensRenderer != null && renderer == lensRenderer) return slot == lensSlot;

        return IsLight(material) && !IsTrim(material);
    }

    /// <summary>
    /// Whether a material is the trim around a lamp rather than a lamp: the rings and bezel that surround
    /// one. The model calls its bezel <c>lightRings</c>, which would otherwise read as a lens.
    /// </summary>
    private static bool IsTrim(Material material)
    {
        string label = Label(material);

        return label.IndexOf("ring", System.StringComparison.OrdinalIgnoreCase) >= 0
            || label.IndexOf("bezel", System.StringComparison.OrdinalIgnoreCase) >= 0
            || label.IndexOf("trim", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Whether a renderer sits in the front half of the truck. The model's own lenses are named
    /// <c>lightFront</c>, <c>lightRings</c> and <c>lightBack</c>, but a ring belongs to whichever lamp it
    /// surrounds rather than to a name of its own, so the position is what decides - which also means a
    /// model that never names its lenses at all still splits into two sensible parts.
    /// </summary>
    private bool IsFrontOfTruck(Renderer renderer)
    {
        Vector3 local = transform.InverseTransformPoint(renderer.bounds.center);
        return local.z >= 0f;
    }

    /// <summary>
    /// The renderer the car controller lights up when the driver brakes. It is covered by identity as
    /// well as by its material, because the back of the truck is lit by lenses rather than by lamps -
    /// there are no rear Light components on the truck at all, so the lens is the brake light.
    /// </summary>
    private Renderer FindBrakeLens()
    {
        CarController car = GetComponentInChildren<CarController>(true);
        return car != null ? car.brakeLightRenderer : null;
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
    /// Whether a slot is one of the model's lenses. The truck names them <c>lightFront</c>,
    /// <c>lights</c>, <c>lightRings</c> and <c>lightBack</c>, and both the material and its texture are
    /// checked because either one can be the unnamed half of the pair.
    /// </summary>
    private static bool IsLight(Material material)
    {
        if (material == null) return false;

        Texture texture = material.mainTexture;
        string textureName = texture != null ? texture.name : string.Empty;

        return ContainsLightWord(material.name) || ContainsLightWord(textureName);
    }

    private static bool ContainsLightWord(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        return text.IndexOf("light", System.StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

        // The light the truck actually shines is part of the paint job too, so the lamps are tinted to
        // match the lenses rather than staying white behind a coloured cover.
        ApplyLightColour(TruckPart.Headlights);
        ApplyLightColour(TruckPart.BrakeLights);
    }

    private void ApplyLightColour(TruckPart part)
    {
        bool front = part == TruckPart.Headlights;
        bool painted = TruckPaint.IsPainted(part);
        Color colour = TruckPaint.ColourOf(part);

        for (int i = 0; i < lights.Count; i++)
        {
            // Only the lamps at that end of the truck: painting the headlights must not touch the lights
            // behind, and the other way round.
            if (lights[i].front != front) continue;

            Light light = lights[i].light;
            if (light == null) continue;

            light.color = painted ? colour : lights[i].modelColour;
        }

        // The lens material was just swapped for this component's own copy, and the light toggle holds
        // the material it switches the glow on: tell it to look at the truck again, or it would be
        // switching a material nothing is drawn with any more.
        LightToggle toggle = GetComponentInChildren<LightToggle>(true);
        if (toggle != null) toggle.RefreshEmissionMaterial();
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

        bool lens = target.part == TruckPart.Headlights || target.part == TruckPart.BrakeLights;

        Write(target.instance, colour, style, lens, target.modelEmission);

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
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");

    /// <summary>
    /// How much brighter than the chosen colour a lens glows. Above 1 so it reads as a lamp that is on
    /// rather than as paint, low enough that it is still the colour the player picked.
    /// </summary>
    private const float EmissionBoost = 1.45f;

    private static Color ReadColour(Material material)
    {
        if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
        if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);

        return Color.white;
    }

    /// <summary>
    /// The glow the model itself gives this slot, for a lens. Read while the slot is still the model's own
    /// material, and used later so a painted lamp glows at least as brightly as the lamp it replaces.
    /// </summary>
    private static Color ReadEmission(Material material)
    {
        if (material.HasProperty(EmissionColorId)) return material.GetColor(EmissionColorId);

        return Color.black;
    }

    private static void Write(Material material, Color colour, int style, bool lens, Color modelEmission)
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

        if (lens) WriteGlow(material, colour, modelEmission);

        SetTransparent(material, finish.transparent);
    }

    /// <summary>
    /// Gives a lens the colour it should glow with. The light toggle owns the keyword from here - it is
    /// what switches the glow off with the headlights - so this only says what colour the glow is, and
    /// it is re-applied by the toggle every time the lights go on.
    /// </summary>
    private static void WriteGlow(Material material, Color colour, Color modelEmission)
    {
        // A lamp only as bright as its albedo does not read as a lamp. The model's own glow is the
        // reference - the default brake and head lamp emission colour - so the painted lens is at least
        // that bright, and the colour the player picks glows exactly the way the model's own lamps did.
        float modelGlow = Mathf.Max(modelEmission.r, Mathf.Max(modelEmission.g, modelEmission.b));
        float boost = Mathf.Max(EmissionBoost, modelGlow);

        Color glow = colour * boost;
        glow.a = 1f;

        if (material.HasProperty(EmissionColorId)) material.SetColor(EmissionColorId, glow);

        // A lens with an emission texture would otherwise glow in the texture's pattern rather than in
        // the chosen colour, the same way the model's baked albedo did.
        if (material.HasProperty(EmissionMapId)) material.SetTexture(EmissionMapId, Texture2D.whiteTexture);

        material.EnableKeyword("_EMISSION");
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
