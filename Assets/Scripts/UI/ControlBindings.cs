using System;

/// <summary>
/// One line of a controls table: what it does, and what to press on each device.
/// </summary>
[Serializable]
public struct ControlBinding
{
    public string action;
    public string keyboard;
    public string gamepad;

    public ControlBinding(string action, string keyboard, string gamepad)
    {
        this.action = action;
        this.keyboard = keyboard;
        this.gamepad = gamepad;
    }
}

/// <summary>A titled group of bindings, so a long table reads as two short ones.</summary>
[Serializable]
public struct ControlGroup
{
    public string title;
    public ControlBinding[] bindings;

    public ControlGroup(string title, params ControlBinding[] bindings)
    {
        this.title = title;
        this.bindings = bindings;
    }
}

/// <summary>
/// The controls the game reads today, written down once.
///
/// Both places that list them - the options screen of the main menu, and the options inside the pause menu -
/// read this, so the two can never drift apart or from what the truck actually does. It was assembled from
/// <see cref="GameInput"/> rather than from an old diagram: W/A/S/D and the arrow keys both drive, focus is
/// held rather than tapped (it locks the truck onto a target ahead), and the pad's face buttons are named by
/// position - A bottom, B right, X left, Y top - so one list covers every pad.
///
/// There are two tables because the gearbox setting moves two controls. Driving manually, the face buttons
/// become the gear lever and the horn and the lights step onto the D-pad (see <see cref="GameInput"/>), so a
/// list that did not move with them would be telling the player to shift gear with the headlights. The two
/// tables hold the same number of rows on purpose: the screens lay the notes out under the table, and a
/// manual table one row longer would push them off the bottom of the screen.
///
/// Reword a row by editing it here; a screen that wants different words can still override the table with its
/// own array in the Inspector, which is why the screens keep their own serialized copy.
/// </summary>
public static class ControlBindings
{
    /// <summary>The automatic gearbox's table - the game as it was built.</summary>
    public static readonly ControlGroup[] Automatic =
    {
        new ControlGroup("DRIVING",
            new ControlBinding("Accelerate", "W   or   Up arrow", "Right trigger  RT"),
            new ControlBinding("Brake / Reverse", "S   or   Down arrow", "Left trigger  LT"),
            new ControlBinding("Steer", "A  D   or   Left / Right arrow", "Left stick"),
            new ControlBinding("Focus  (hold)", "Space", "LB"),
            new ControlBinding("Boost", "Left Shift", "A"),
            new ControlBinding("Horn", "H", "X"),
            new ControlBinding("Headlights", "L", "B"),
            new ControlBinding("Change camera", "C", "Y"),
            new ControlBinding("Reset vehicle", "R", "D-pad up"),
            new ControlBinding("Pause", "Escape", "Start")),

        new ControlGroup("MENUS",
            new ControlBinding("Move the highlight", "Arrow keys   or   W A S D", "D-pad   or   Left stick"),
            new ControlBinding("Confirm", "Enter", "A"),
            new ControlBinding("Back", "Escape", "B")),
    };

    /// <summary>
    /// The same table for a truck the player changes gear in.
    ///
    /// The lever takes the two face buttons the horn and the lights had, and those two move one place inwards
    /// onto the D-pad, so the table lists the shifts and the horn and the lights together rather than
    /// duplicated - which also keeps it the same height as the automatic one.
    /// </summary>
    public static readonly ControlGroup[] Manual =
    {
        new ControlGroup("DRIVING",
            new ControlBinding("Accelerate", "W   or   Up arrow", "Right trigger  RT"),
            new ControlBinding("Brake", "S   or   Down arrow", "Left trigger  LT"),
            new ControlBinding("Steer", "A  D   or   Left / Right arrow", "Left stick"),
            new ControlBinding("Focus  (hold)", "Space", "LB"),
            new ControlBinding("Boost", "Left Shift", "A"),
            new ControlBinding("Shift up / down", "Page Up / Page Down  (E / Q)", "B  /  X"),
            new ControlBinding("Horn / Lights", "H  /  L", "D-pad left / right"),
            new ControlBinding("Change camera", "C", "Y"),
            new ControlBinding("Reset vehicle", "R", "D-pad up"),
            new ControlBinding("Pause", "Escape", "Start")),

        new ControlGroup("MENUS",
            new ControlBinding("Move the highlight", "Arrow keys   or   W A S D", "D-pad   or   Left stick"),
            new ControlBinding("Confirm", "Enter", "A"),
            new ControlBinding("Back", "Escape", "B")),
    };

    /// <summary>The notes under a table, one row each.</summary>
    private static readonly string[] AutomaticNotes =
    {
        "Hold FOCUS to lock onto a target in front of the truck and steer onto it.",
        "Any controller Unity recognises works - the pad column is named the way an Xbox pad is.",
    };

    /// <summary>
    /// The manual table's notes. The second one is spent on the thing about driving by hand that the table
    /// cannot show: that the gear is the only way backwards, so the brake really is only a brake.
    /// </summary>
    private static readonly string[] ManualNotes =
    {
        "Hold FOCUS to lock onto a target in front of the truck and steer onto it.",
        "In MANUAL the truck reverses in the R gear only - the brake just brakes, and R goes in from a stop.",
    };

    /// <summary>The table for a gearbox.</summary>
    public static ControlGroup[] For(GearboxMode gearbox)
    {
        return gearbox == GearboxMode.Manual ? Manual : Automatic;
    }

    /// <summary>The table for the gearbox in force.</summary>
    public static ControlGroup[] ForCurrent
    {
        get { return For(DriveSettings.Mode); }
    }

    /// <summary>The notes that go with a table.</summary>
    public static string[] NotesFor(GearboxMode gearbox)
    {
        return gearbox == GearboxMode.Manual ? ManualNotes : AutomaticNotes;
    }

    /// <summary>The notes for the gearbox in force.</summary>
    public static string[] NotesForCurrent
    {
        get { return NotesFor(DriveSettings.Mode); }
    }
}
