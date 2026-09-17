using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The parts of the player truck that can be repainted, in the order the UI shows them.</summary>
public enum TruckPart
{
    Body = 0,
    Windows = 1,
    Wheels = 2,
    IceCream = 3,
    BackDoor = 4,
}

/// <summary>
/// The player truck's paint job: which parts can change, which colours they can take, where the choice
/// is remembered and how it reaches the truck.
///
/// Parts are found by the name of the model's own objects, which the art gives us for free - the truck
/// FBX names its meshes <c>body</c>, <c>backWindow</c>, <c>wheelsFront</c>, <c>wheelsBack</c>,
/// <c>backDoor</c>, and the ice cream model names its two <c>cone</c> and <c>iceCream</c>. Matching is
/// case-insensitive and by substring, so a part is still found if a name gains a suffix.
///
/// Slots matter: a part is repainted on the slots it names, and an empty slot list means "every slot of
/// that mesh". The wheels are the only multi-slot mesh here - five slots, of which slot 0 is the hub
/// and slot 4 is the tyre rubber (the middle three are unused). A wheel colour is expected to change
/// the wheel, so the whole thing is repainted rather than just the hub, and Black / Graphite in the
/// palette put the tyres back.
///
/// The choice is stored as an index into <see cref="Palette"/>, or -1 for "leave the model's own
/// colour alone". Storing the index rather than a colour keeps the palette authoritative and makes the
/// reset exact.
///
/// Nothing is applied unless something has actually been chosen, so an untouched game renders the truck
/// exactly as the prefab does.
/// </summary>
public static class TruckPaint
{
    public const int PartCount = 5;

    /// <summary>An index of <see cref="ChoiceNone"/> means the part keeps the colour the model came with.</summary>
    public const int ChoiceNone = -1;

    // ---------------------------------------------------------------- the parts

    public static readonly string[] PartLabels =
    {
        "Body",
        "Windows",
        "Wheels",
        "Ice Cream",
        "Back Door",
    };

    // Model object names per part.
    private static readonly string[][] PartObjectNames =
    {
        new[] { "body" },
        new[] { "backWindow" },
        new[] { "wheelsFront", "wheelsBack" },
        new[] { "cone", "iceCream" },
        new[] { "backDoor" },
    };

    // Material slots to repaint. Empty means every slot of that mesh.
    private static readonly int[][] PartSlots =
    {
        new int[0],          // body: the main paint plus its two trim shades
        new[] { 0 },         // back window: the glass, its only material
        new int[0],          // wheels: the hub and the tyre rubber
        new int[0],          // cone and ice cream each carry a single material
        new[] { 0 },         // back door: its only material
    };

    // The body is painted with three related shades; keeping their relative brightness means a repaint
    // still reads as a shaded body rather than one flat slab of colour.
    private static readonly bool[] PartPreserveShading =
    {
        true,
        false,
        false,
        false,
        false,
    };

    public static string[] ObjectNamesOf(TruckPart part) { return PartObjectNames[(int)part]; }
    public static int[] SlotsOf(TruckPart part) { return PartSlots[(int)part]; }
    public static bool PreservesShading(TruckPart part) { return PartPreserveShading[(int)part]; }

    // ---------------------------------------------------------------- the palette

    public static readonly string[] PaletteNames =
    {
        "Red", "Orange", "Yellow", "Lime", "Green",
        "Teal", "Sky", "Blue", "Purple", "Pink",
        "Brown", "Cream", "Silver", "Graphite", "Black",
    };

    /// <summary>
    /// A spread of plain, readable vehicle colours rather than a full picker: enough to tell the trucks
    /// apart at a glance, and all of them stay legible under the game's lighting.
    /// </summary>
    public static readonly Color[] Palette =
    {
        new Color(0.80f, 0.15f, 0.13f),   // Red
        new Color(0.92f, 0.45f, 0.11f),   // Orange
        new Color(0.95f, 0.78f, 0.15f),   // Yellow
        new Color(0.42f, 0.72f, 0.15f),   // Lime
        new Color(0.13f, 0.55f, 0.25f),   // Green
        new Color(0.08f, 0.60f, 0.62f),   // Teal
        new Color(0.15f, 0.55f, 0.88f),   // Sky
        new Color(0.12f, 0.25f, 0.72f),   // Blue
        new Color(0.48f, 0.24f, 0.70f),   // Purple
        new Color(0.90f, 0.32f, 0.60f),   // Pink
        new Color(0.40f, 0.26f, 0.16f),   // Brown
        new Color(0.93f, 0.90f, 0.80f),   // Cream
        new Color(0.72f, 0.74f, 0.78f),   // Silver
        new Color(0.22f, 0.23f, 0.25f),   // Graphite
        new Color(0.07f, 0.07f, 0.08f),   // Black
    };

    public static int PaletteCount { get { return Palette.Length; } }

    // ---------------------------------------------------------------- storage

    private const string KeyPrefix = "TruckPaint.";

    private static int[] choices;
    private static bool loaded;

    public static int GetChoice(TruckPart part)
    {
        EnsureLoaded();
        return choices[(int)part];
    }

    /// <summary>Index into <see cref="Palette"/>, or <see cref="ChoiceNone"/> for the model's own colour.</summary>
    public static void SetChoice(TruckPart part, int paletteIndex)
    {
        EnsureLoaded();

        int clamped = (paletteIndex < 0 || paletteIndex >= Palette.Length) ? ChoiceNone : paletteIndex;
        if (choices[(int)part] == clamped) return;

        choices[(int)part] = clamped;
        PlayerPrefs.SetInt(KeyPrefix + part, clamped);
        PlayerPrefs.Save();
    }

    /// <summary>The colour a part should be, or null when it should keep the model's own colour.</summary>
    public static Color? ColorOf(TruckPart part)
    {
        int index = GetChoice(part);
        if (index < 0 || index >= Palette.Length) return null;
        return Palette[index];
    }

    /// <summary>True when at least one part has been repainted.</summary>
    public static bool HasCustomisation
    {
        get
        {
            EnsureLoaded();
            for (int i = 0; i < PartCount; i++)
                if (choices[i] != ChoiceNone) return true;
            return false;
        }
    }

    /// <summary>Puts every part back to the colour the model came with.</summary>
    public static void ResetAll()
    {
        EnsureLoaded();

        for (int i = 0; i < PartCount; i++)
        {
            choices[i] = ChoiceNone;
            PlayerPrefs.DeleteKey(KeyPrefix + (TruckPart)i);
        }

        PlayerPrefs.Save();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;

        choices = new int[PartCount];
        for (int i = 0; i < PartCount; i++)
        {
            string key = KeyPrefix + (TruckPart)i;
            int stored = PlayerPrefs.GetInt(key, ChoiceNone);
            choices[i] = (stored < 0 || stored >= Palette.Length) ? ChoiceNone : stored;
        }

        loaded = true;
    }

    // ---------------------------------------------------------------- reaching the truck

    /// <summary>
    /// Adds a <see cref="TruckPaintApplier"/> to a truck and applies the saved paint job to it. Safe to
    /// call more than once: the same applier is reused and simply re-applied.
    /// </summary>
    public static TruckPaintApplier AttachAndApply(GameObject truckRoot)
    {
        if (truckRoot == null) return null;

        TruckPaintApplier applier = truckRoot.GetComponent<TruckPaintApplier>();
        if (applier == null) applier = truckRoot.AddComponent<TruckPaintApplier>();

        applier.Apply();
        return applier;
    }

    /// <summary>
    /// Applies the saved paint job to the player truck - or trucks - in the active scene, if there are
    /// any. Returns how many were repainted.
    /// </summary>
    public static int ApplyToActiveScene()
    {
        if (!HasCustomisation) return 0;

        // CarController is what the player's truck runs; the traffic runs a different controller, so a
        // passing car can never be picked up by mistake. Every match is painted, because a scene is free
        // to hold more than one player truck (the playground does).
        CarController[] cars = Object.FindObjectsOfType<CarController>();
        if (cars != null && cars.Length > 0)
        {
            for (int i = 0; i < cars.Length; i++)
                if (cars[i] != null) AttachAndApply(cars[i].gameObject);

            return cars.Length;
        }

        // Fallback, in case the controller is ever swapped for something else.
        GameObject tagged = GameObject.FindWithTag("Player");
        if (tagged == null) return 0;

        AttachAndApply(tagged);
        return 1;
    }

    // ---------------------------------------------------------------- bootstrap

    /// <summary>
    /// Reapplies the paint job in every scene, so a level always starts with the player's own truck.
    /// The truck is placed in the level by hand and is a fresh instance of the prefab on each load, which
    /// is exactly why the colours have to be applied again that often.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        // The first scene is already loaded by the time this runs.
        ApplyToActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToActiveScene();
    }
}
