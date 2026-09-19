using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>The parts of the player truck that can be repainted, in the order the UI shows them.</summary>
public enum TruckPart
{
    Body = 0,
    Windows = 1,
    Wheels = 2,
    IceCream = 3,
    Cone = 4,
    BackDoor = 5,
    Bumper = 6,

    /// <summary>
    /// The front lamps: the glass the player picks the colour of, and the beam the truck shines, which
    /// is tinted to match it.
    /// </summary>
    Headlights = 7,

    /// <summary>
    /// The rear lamps - the tail and brake lights. A separate part from the headlights, so a truck can
    /// have white lamps in front and red ones behind, or any other pair.
    /// </summary>
    BrakeLights = 8,
}

/// <summary>Which of a mesh's material slots a part's colour is written to.</summary>
public enum SlotRule
{
    /// <summary>Every slot of the mesh.</summary>
    All = 0,

    /// <summary>Only the mesh's first slot - the wheels' hub, with the tyre left as it is.</summary>
    First = 1,

    /// <summary>Only the slots painted with the model's window material - the glass in the body.</summary>
    Windows = 2,

    /// <summary>Every slot except the window ones, so the body is painted but its glass is not.</summary>
    ExceptWindows = 3,

    /// <summary>
    /// Only the lens slots at the front of the truck - the lamps and the rings around them. Which end a
    /// lens is on is decided by where it sits on the model, not by its name, so a ring counts as part
    /// of the lamp it surrounds.
    /// </summary>
    Headlights = 4,

    /// <summary>
    /// Only the lens slots at the back of the truck, plus the brake lens itself - the one the car
    /// controller switches on when the driver brakes.
    /// </summary>
    BrakeLights = 5,
}

/// <summary>How a painted part is shaded. The colour is the same for all of them.</summary>
public enum PaintStyle
{
    Paint = 0,
    Gloss = 1,
    Matte = 2,
    Metallic = 3,
    Chrome = 4,
    Glass = 5,
}

/// <summary>
/// The player truck's paint job: which parts can change, which colours and finishes they can take,
/// where the choice is remembered and how it reaches the truck.
///
/// Parts are found by the name of the model's own objects, which the art gives us for free - the truck
/// FBX names its meshes <c>body</c>, <c>backWindow</c>, <c>backDoor</c>, <c>gelgir</c>,
/// <c>wheelsFront</c> and <c>wheelsBack</c>, and the ice cream model its <c>cone</c> and
/// <c>iceCream</c>. Matching is case-insensitive and by substring, so a part is still found if a name
/// gains a suffix.
///
/// Slots matter: a mesh can carry several materials, and which of them belong to a part is what
/// <see cref="SlotRule"/> decides. The body is the interesting one - it is painted with its own colour
/// plus a window material for the glass, so the windows are their own part and the body leaves them
/// alone. The wheels carry the hub and the tyre in separate slots, and only the hub takes the wheel
/// colour. The lights are the exception to the object name rule: the model spreads its lenses over
/// several meshes (<c>lightFront</c>, <c>lights</c>, <c>lightRings</c>, <c>lightBack</c>), so they are
/// found by their material and by which end of the truck they sit on instead - see
/// <see cref="SlotRule.Headlights"/> and <see cref="SlotRule.BrakeLights"/> - and the truck's own
/// <c>Light</c> components are tinted to match, so the beam is the colour the player picked too. The two
/// ends are separate parts on purpose: white lamps in front and red ones behind is the point.
///
/// Painting is stored as a colour per part, not as an index into <see cref="Palette"/>: the palette is
/// only a set of presets, and the player is free to pick any colour they like with the picker. A part
/// that has never been painted keeps the model's own colour.
/// </summary>
public static class TruckPaint
{
    public const int PartCount = 9;

    // ---------------------------------------------------------------- the parts

    public static readonly string[] PartLabels =
    {
        "Body",
        "Windows",
        "Wheels",
        "Ice Cream",
        "Cone",
        "Back Door",
        "Bumper",
        "Headlights",
        "Brake Lights",
    };

    // Model object names per part.
    private static readonly string[][] PartObjectNames =
    {
        new[] { "body" },
        new[] { "body" },                                     // the same mesh, its window slots
        new[] { "wheelsFront", "wheelsBack" },
        new[] { "iceCream" },
        new[] { "cone" },
        new[] { "backDoor", "backWindow" },                   // the rear door and the glass in it
        new[] { "gelgir" },                                   // the front grille and its surround
        new string[0],                                        // headlights: found by material and position
        new string[0],                                        // brake lights: the same, at the other end
    };

    private static readonly SlotRule[] PartSlotRules =
    {
        SlotRule.ExceptWindows,   // body: its paint, not its glass
        SlotRule.Windows,         // windows: that glass, wherever the model keeps it
        SlotRule.First,           // wheels: the hub
        SlotRule.All,             // ice cream
        SlotRule.All,             // cone
        SlotRule.All,             // back door and its window
        SlotRule.All,             // bumper
        SlotRule.Headlights,      // the lamps at the front
        SlotRule.BrakeLights,     // the lamps at the back
    };

    /// <summary>
    /// The model objects a part lives on. Empty for a part that has no mesh of its own - each end of the
    /// truck's lights is spread across the model, so the applier looks for their material anywhere on the
    /// truck and then works out which end of it they are on.
    /// </summary>
    public static string[] ObjectNamesOf(TruckPart part) { return PartObjectNames[(int)part]; }
    public static SlotRule SlotRuleOf(TruckPart part) { return PartSlotRules[(int)part]; }

    // ---------------------------------------------------------------- the palette

    /// <summary>
    /// Presets for the picker rather than the whole vocabulary of colours: enough to tell the trucks
    /// apart at a glance, all of them legible under the game's lighting, and each one an honest
    /// representation of its name when the body wears it.
    /// </summary>
    public static readonly string[] PaletteNames =
    {
        "Red", "Orange", "Amber", "Yellow", "Lime",
        "Green", "Teal", "Sky", "Blue", "Purple",
        "Magenta", "Pink", "Brown", "Cream", "White",
        "Silver", "Grey", "Graphite", "Black",
    };

    /// <summary>
    /// The colours themselves. These are written to the truck as they are - the game's own paint is
    /// flat, so what the swatch shows is what the truck gets.
    /// </summary>
    public static readonly Color[] Palette =
    {
        new Color(0.82f, 0.14f, 0.13f),   // Red
        new Color(0.95f, 0.45f, 0.10f),   // Orange
        new Color(0.97f, 0.70f, 0.14f),   // Amber
        new Color(0.96f, 0.87f, 0.22f),   // Yellow
        new Color(0.62f, 0.86f, 0.17f),   // Lime
        new Color(0.19f, 0.63f, 0.28f),   // Green
        new Color(0.06f, 0.60f, 0.60f),   // Teal
        new Color(0.24f, 0.62f, 0.92f),   // Sky
        new Color(0.13f, 0.30f, 0.78f),   // Blue
        new Color(0.52f, 0.26f, 0.74f),   // Purple
        new Color(0.82f, 0.22f, 0.66f),   // Magenta
        new Color(0.96f, 0.55f, 0.72f),   // Pink
        new Color(0.42f, 0.27f, 0.16f),   // Brown
        new Color(0.95f, 0.90f, 0.76f),   // Cream
        new Color(0.93f, 0.95f, 0.97f),   // White
        new Color(0.73f, 0.76f, 0.81f),   // Silver
        new Color(0.44f, 0.46f, 0.50f),   // Grey
        new Color(0.21f, 0.22f, 0.25f),   // Graphite
        new Color(0.06f, 0.06f, 0.07f),   // Black
    };

    public static int PaletteCount { get { return Palette.Length; } }

    /// <summary>The colour the picker starts from before anything has been chosen anywhere.</summary>
    public static readonly Color FallbackColour = new Color(0.82f, 0.14f, 0.13f);

    // ---------------------------------------------------------------- the finishes

    public static readonly string[] StyleLabels =
    {
        "PAINT", "GLOSS", "MATTE", "METALLIC", "CHROME", "GLASS",
    };

    public static int StyleCount { get { return StyleLabels.Length; } }

    public const int DefaultStyle = (int)PaintStyle.Paint;

    // ---------------------------------------------------------------- storage

    private const string KeyPrefix = "TruckPaint.";
    private const string PaintedKey = KeyPrefix + "Painted.";
    private const string ColourKey = KeyPrefix + "Colour.";
    private const string StyleKey = KeyPrefix + "Style.";

    private static readonly bool[] painted = new bool[PartCount];
    private static readonly Color[] colours = new Color[PartCount];
    private static readonly int[] styles = new int[PartCount];

    private static bool loaded;
    private static bool dirty;

    /// <summary>True when this part has a colour of its own instead of the model's.</summary>
    public static bool IsPainted(TruckPart part)
    {
        EnsureLoaded();
        return painted[(int)part];
    }

    /// <summary>The colour of a painted part, or <see cref="FallbackColour"/> when it has none.</summary>
    public static Color ColourOf(TruckPart part)
    {
        EnsureLoaded();
        return painted[(int)part] ? colours[(int)part] : FallbackColour;
    }

    /// <summary>The finish of a part. Meaningful whether or not it has been painted.</summary>
    public static int StyleOf(TruckPart part)
    {
        EnsureLoaded();
        return styles[(int)part];
    }

    /// <summary>True when at least one part has been repainted.</summary>
    public static bool HasCustomisation
    {
        get
        {
            EnsureLoaded();

            for (int i = 0; i < PartCount; i++)
                if (painted[i]) return true;

            return false;
        }
    }

    /// <summary>
    /// Paints a part. The colour is stored as it is given, so the picker can hand over any colour
    /// rather than an index into <see cref="Palette"/>.
    /// </summary>
    public static void SetColour(TruckPart part, Color colour)
    {
        EnsureLoaded();

        colour.a = 1f;

        int index = (int)part;
        if (painted[index] && colours[index] == colour) return;

        painted[index] = true;
        colours[index] = colour;

        PlayerPrefs.SetInt(PaintedKey + part, 1);
        PlayerPrefs.SetString(ColourKey + part, Encode(colour));
        dirty = true;
    }

    /// <summary>Changes a part's finish. Painting a part that has not been painted yet does not.</summary>
    public static void SetStyle(TruckPart part, int style)
    {
        EnsureLoaded();

        int clamped = Mathf.Clamp(style, 0, StyleCount - 1);
        if (styles[(int)part] == clamped) return;

        styles[(int)part] = clamped;
        PlayerPrefs.SetInt(StyleKey + part, clamped);
        dirty = true;
    }

    /// <summary>Puts a part back to the colour the model came with.</summary>
    public static void Clear(TruckPart part)
    {
        EnsureLoaded();

        int index = (int)part;
        if (!painted[index]) return;

        painted[index] = false;
        PlayerPrefs.DeleteKey(PaintedKey + part);
        PlayerPrefs.DeleteKey(ColourKey + part);
        dirty = true;
    }

    /// <summary>Puts every part back to the colour the model came with.</summary>
    public static void ResetAll()
    {
        EnsureLoaded();

        for (int i = 0; i < PartCount; i++)
        {
            painted[i] = false;
            styles[i] = DefaultStyle;

            TruckPart part = (TruckPart)i;
            PlayerPrefs.DeleteKey(PaintedKey + part);
            PlayerPrefs.DeleteKey(ColourKey + part);
            PlayerPrefs.DeleteKey(StyleKey + part);
        }

        dirty = true;
        Save();
    }

    /// <summary>
    /// Writes the paint job to disk. The player changes colours many times a second while dragging
    /// through the picker, so the values are only held in memory until something settles; Unity saves
    /// them on quit as well.
    /// </summary>
    public static void Save()
    {
        if (!dirty) return;

        dirty = false;
        PlayerPrefs.Save();
    }

    private static string Encode(Color colour)
    {
        return ColorUtility.ToHtmlStringRGB(colour);
    }

    private static Color Decode(string text, Color fallback)
    {
        Color colour;
        if (!string.IsNullOrEmpty(text) && ColorUtility.TryParseHtmlString("#" + text, out colour)) return colour;

        return fallback;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;

        for (int i = 0; i < PartCount; i++)
        {
            TruckPart part = (TruckPart)i;

            painted[i] = PlayerPrefs.GetInt(PaintedKey + part, 0) != 0;
            colours[i] = painted[i]
                ? Decode(PlayerPrefs.GetString(ColourKey + part, ""), Palette[i % Palette.Length])
                : FallbackColour;
            styles[i] = Mathf.Clamp(PlayerPrefs.GetInt(StyleKey + part, DefaultStyle), 0, StyleCount - 1);
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
