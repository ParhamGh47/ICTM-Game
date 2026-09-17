using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Repaints one truck. Added at runtime to the truck instance that needs it - the level's player truck,
/// or the preview in the customize scene.
///
/// Two rules keep this safe to run on a shared model:
///
///   * The prefab's materials are shared assets (the truck's are embedded in the FBX, the ice cream's
///     are <c>Cone.mat</c> / <c>IceCream.mat</c>). Writing to them would repaint every truck in the game,
///     and permanently, so each slot that is actually repainted gets its own copy created from the
///     original. Untouched slots keep pointing at the shared material, so a truck with no customization
///     allocates nothing at all.
///   * Those copies belong to this component, so it is this component's job to destroy them. That keeps
///     a level's worth of copies from surviving the scene that created them.
///
/// The original colour of each slot is read before anything is written, which is what lets "Default"
/// put a part back exactly as the model had it, and what gives the body its brightness variation.
/// </summary>
[DisallowMultipleComponent]
public class TruckPaintApplier : MonoBehaviour
{
    private class Target
    {
        public TruckPart part;
        public Renderer renderer;
        public int slot;
        public Material original;
        public Material instance;
        public Color originalColor;
        public float shade = 1f;
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
    /// Finds the renderer slots that make up each part and remembers their original colours. Runs once;
    /// calling it again would read the colours this component has already written.
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
            int[] slots = TruckPaint.SlotsOf(part);

            List<Target> found = new List<Target>();
            float reference = 0f;

            for (int r = 0; r < renderers.Count; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null || !Matches(renderer.gameObject.name, names)) continue;

                Material[] materials = renderer.sharedMaterials;
                for (int s = 0; s < materials.Length; s++)
                {
                    if (!Allows(s, slots)) continue;
                    if (materials[s] == null) continue;

                    Color colour = ReadColor(materials[s]);
                    reference = Mathf.Max(reference, Brightness(colour));

                    found.Add(new Target
                    {
                        part = part,
                        renderer = renderer,
                        slot = s,
                        original = materials[s],
                        originalColor = colour,
                    });
                }
            }

            if (found.Count == 0)
            {
                Debug.LogWarning(string.Format(
                    "[TruckPaint] Found no mesh for the '{0}' part (looked for {1}) on '{2}'. " +
                    "That part will not be repainted; check the model's object names.",
                    TruckPaint.PartLabels[p], string.Join(", ", names), name));
                continue;
            }

            // Relative brightness within the part, so a repaint keeps the model's own shading.
            if (TruckPaint.PreservesShading(part) && reference > 0.0001f)
                for (int i = 0; i < found.Count; i++)
                    found[i].shade = Mathf.Clamp(Brightness(found[i].originalColor) / reference, 0.35f, 1f);

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

    private static bool Allows(int slot, int[] slots)
    {
        if (slots == null || slots.Length == 0) return true;

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == slot) return true;

        return false;
    }

    // ---------------------------------------------------------------- applying

    /// <summary>Writes the saved paint job onto this truck. Does nothing where nothing was chosen.</summary>
    public void Apply()
    {
        Capture();

        for (int p = 0; p < TruckPaint.PartCount; p++)
        {
            TruckPart part = (TruckPart)p;
            Color? chosen = TruckPaint.ColorOf(part);

            for (int i = 0; i < targets.Count; i++)
            {
                Target target = targets[i];
                if ((int)target.part != p) continue;

                Color colour = chosen.HasValue ? chosen.Value * target.shade : target.originalColor;
                colour.a = target.originalColor.a;

                // Nothing to do when the part is back on Default and was never repainted.
                if (!chosen.HasValue && target.instance == null) continue;

                Write(target, colour);
            }
        }
    }

    /// <summary>The colour the model itself uses for a part, for the UI's "Default" swatch.</summary>
    public Color OriginalColorOf(TruckPart part)
    {
        Capture();

        for (int i = 0; i < targets.Count; i++)
            if (targets[i].part == part)
                return targets[i].originalColor;

        return new Color(0.5f, 0.5f, 0.5f);
    }

    private void Write(Target target, Color colour)
    {
        if (target.instance == null)
        {
            // A private copy of the shared material. Created once and reused from then on, so dragging
            // through colours in the customize scene does not allocate a material per frame.
            target.instance = new Material(target.original);
            target.instance.name = target.original.name + " (Painted)";

            Material[] materials = target.renderer.sharedMaterials;
            if (target.slot < materials.Length)
            {
                materials[target.slot] = target.instance;
                target.renderer.sharedMaterials = materials;
            }
        }

        SetColor(target.instance, colour);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].instance != null) Destroy(targets[i].instance);

        targets.Clear();
    }

    // ---------------------------------------------------------------- shader plumbing

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static Color ReadColor(Material material)
    {
        if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
        if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
        return Color.white;
    }

    private static void SetColor(Material material, Color colour)
    {
        // The project renders with the built-in pipeline, so the Standard shader's albedo is _Color.
        // _BaseColor is written too when a shader has it, so a material imported from a different
        // pipeline still repaints rather than silently doing nothing.
        if (material.HasProperty(ColorId)) material.SetColor(ColorId, colour);
        if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, colour);
    }

    private static float Brightness(Color colour)
    {
        return Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
    }
}
