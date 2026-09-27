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
/// and scales as one thing.
///
/// It is deliberately quiet when there is nothing to spend: an empty counter is faded rather than hidden, so
/// the driver can see at a glance whether the last milkshake was used or merely missed. While a boost is
/// running the counter stays at full strength even if that boost was the last one, so the number dimming is
/// never mistaken for the boost itself failing.
/// </summary>
public class BoostDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The text the count is written into.")]
    public Text countText;

    [Tooltip("The truck's boost manager. Left empty - which is how the levels use it, the truck being placed " +
             "by the scene rather than by the canvas - the manager in the scene is found instead.")]
    public BoostManager boostManager;

    [Header("What It Says")]
    [Tooltip("Written in front of the count, so the counter reads as 'x3' rather than as a bare number. Clear " +
             "it for the number on its own.")]
    public string label = "x";

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

        countText.text = string.IsNullOrEmpty(label) ? carried.ToString() : label + " " + carried;

        countText.color = carried > 0 || boostManager.IsBoosting ? carriedColour : emptyColour;
    }
}
