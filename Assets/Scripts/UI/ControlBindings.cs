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
/// Reword a row by editing it here; a screen that wants different words can still override the table with its
/// own array in the Inspector, which is why the screens keep their own serialized copy.
/// </summary>
public static class ControlBindings
{
    public static readonly ControlGroup[] All =
    {
        new ControlGroup("DRIVING",
            new ControlBinding("Accelerate", "W   or   Up arrow", "Right trigger  RT"),
            new ControlBinding("Brake / Reverse", "S   or   Down arrow", "Left trigger  LT"),
            new ControlBinding("Steer", "A  D   or   Left / Right arrow", "Left stick"),
            new ControlBinding("Focus  (hold)", "Space", "A"),
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

    public static readonly string[] Notes =
    {
        "Hold FOCUS to lock onto a target in front of the truck and steer onto it.",
        "Any controller Unity recognises works - the pad column is named the way an Xbox pad is.",
    };
}
