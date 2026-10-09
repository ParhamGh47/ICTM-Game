using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The boost counter in the corner of the HUD: how many milkshakes the truck is still carrying.
///
/// It reads the truck's own <see cref="BoostManager"/> rather than counting pickups itself, so the number on
/// screen is exactly the number the driver is about to spend, and the same count is shown whenever the
/// counter is placed. A level that puts a truck in the scene needs nothing wired for it: the manager is
/// found, a few times a second if need be, because the canvas and the truck are placed independently.
///
/// It is drawn in the same hand as the rest of the HUD - the same font, the same white drop shadow and the
/// same dark blue as the HUD's own paper - and it is printed on a small sheet of that paper: the count is a
/// child of the plate, so it is always drawn over the paper rather than under it, and the whole note moves
/// and scales as one thing. Beside the count sits the boost's own icon, the milkshake the truck collects, so
/// the number says what it is counting without needing a label.
///
/// It is deliberately quiet when there is nothing to spend: an empty counter is faded rather than hidden, so
/// the driver can see at a glance whether the last milkshake was used or merely missed. While a boost is
/// running the counter stays at full strength even if that boost was the last one, so the number dimming is
/// never mistaken for the boost itself failing.
///
/// The icon and the count share the note's centreline. The icon is simply centred on it; the count sits a
/// little below it, by the part of its line the font keeps for descenders - which digits never use, and which
/// would otherwise leave the number floating a couple of units above the middle of the milkshake beside it.
/// </summary>
public class BoostDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The text the count is written into.")]
    public Text countText;

    [Tooltip("The truck's boost manager. Left empty - which is how the levels use it, the truck being placed " +
             "by the scene rather than by the canvas - the manager in the scene is found instead.")]
    public BoostManager boostManager;

    [Tooltip("The icon beside the count: the milkshake, the thing the truck actually collects. Optional, so a " +
             "counter can be a bare number if a level wants one. It fades with the count when there is " +
             "nothing to spend.")]
    public Image iconImage;

    [Header("What It Says")]
    [Tooltip("Written in front of the count. Left empty, because the icon beside it already says what is " +
             "being counted; set it to 'x' - or anything else - for a counter that reads 'x3'.")]
    public string label = "";

    [Header("Colours")]
    [Tooltip("The colour the counter is written in, while the truck is carrying at least one boost - and while " +
             "one is being spent, even if that was the last. This is the dark blue the HUD's paper notes are " +
             "printed in.")]
    public Color carriedColour = new Color(0.24705882f, 0.52156866f, 0.6117647f, 1f);

    [Tooltip("The same colour, faded, while the truck is carrying none: the counter reads as empty without " +
             "disappearing, so it never looks like it has gone missing.")]
    public Color emptyColour = new Color(0.24705882f, 0.52156866f, 0.6117647f, 0.5f);

    // When the manager was last looked for, so a level whose truck is spawned late is picked up without the
    // scene being searched every frame.
    private float nextSearch;

    private void Update()
    {
        if (countText == null)
            return;

        if (boostManager == null)
        {
            if (Time.unscaledTime < nextSearch)
                return;

            nextSearch = Time.unscaledTime + 0.5f;

            boostManager = BoostManager.Instance;

            if (boostManager == null)
                return;
        }

        int carried = boostManager.Boosts;

        // Carrying one counts as much as spending one: a counter that dimmed at the moment the last boost
        // was spent would read as the boost having failed.
        bool live = carried > 0 || boostManager.IsBoosting;

        countText.text = string.IsNullOrEmpty(label) ? carried.ToString() : label + " " + carried;

        countText.color = live ? carriedColour : emptyColour;

        // The icon fades with the count, and only in alpha - the artwork keeps its own colours while the
        // whole note dims as one thing.
        if (iconImage != null)
            iconImage.color = new Color(1f, 1f, 1f, live ? 1f : emptyColour.a);
    }
}
