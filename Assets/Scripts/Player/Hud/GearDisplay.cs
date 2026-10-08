using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The HUD's gear readout: the gear the truck is in, printed as R or its number.
///
/// In manual the number is also the thing the player is being asked to manage, and the one cue the readout
/// has for that is its colour: when the gearbox says a change is due - the engine is near the top of its rev
/// range and there is a gear left to go up to, or it is labouring low down in a gear that has one below it -
/// the reading blinks between its own colour and a cue colour, and back to its own colour when there is
/// nothing to do. See <see cref="EngineAudio.ShiftCue"/>.
///
/// Which way it wants to go shows itself two ways, and they are meant to look different from each other: a
/// change up is a <em>blink</em> to blue, and a change down is the reading going <em>steady</em> amber with no
/// blinking at all. Blinking means there is something to gain - the driver is being asked to act - while a
/// steady colour is the engine telling them where it is, which is what a gear that is too low is: nothing is
/// lost by leaving it, only by labouring in it. A single cue colour left the player reading the number to
/// know which way the change was, which is the one thing they have no time for while driving.
///
/// Nothing here changes in automatic: the box changes for the player, so the cue is never raised and the
/// readout stays the steady colour it has always been.
/// </summary>
public class GearDisplay : MonoBehaviour
{
    [Header("References")]
    public EngineAudio engineAudio;
    public Text gearText;

    [Header("Manual shift cue")]
    [Tooltip("The colour the readout blinks to when there is a gear above to change into. The reading's own " +
             "authored colour is what it blinks back to, so the prefab stays the place the resting look is " +
             "set.")]
    public Color shiftUpColor = new Color(0.42f, 0.79f, 1f, 1f);

    [Tooltip("The colour the reading goes when the engine wants a gear below - steady, not blinking. The " +
             "amber a shift cue has always been in this game.")]
    public Color shiftDownColor = new Color(1f, 0.92f, 0.45f, 1f);

    [Tooltip("How many times a second the readout alternates while a change up is due. Fast enough to read " +
             "as a blink rather than a fade, slow enough not to strobe - and quick enough that the cue is " +
             "still ahead of the gear the player has to change into. A change down does not blink.")]
    public float flashRate = 6.5f;

    // The colour the text was authored with, captured once so the flash has something to return to. Taken on
    // the first frame it is needed rather than in Start, so it is the prefab's own colour whatever it is that
    // wires the readout up and whenever it does so.
    private Color restingColor;
    private bool colourCaptured;
    private float flashTimer;

    private void Update()
    {
        if (engineAudio == null || gearText == null) return;

        if (!colourCaptured)
        {
            restingColor = gearText.color;
            colourCaptured = true;
        }

        int gear = engineAudio.CurrentGear;

        gearText.text = gear == 0 ? "R" : gear.ToString();

        bool up = engineAudio.ShiftUpSuggested;
        bool down = engineAudio.ShiftDownSuggested;

        if (!up && !down)
        {
            gearText.color = restingColor;
            flashTimer = 0f;
            return;
        }

        // A change below is the reading simply being amber, with no blinking: the eye reads a steady colour
        // as a state and a blinking one as something to do, so the two hints can never be taken for each
        // other. Never both at once either - the engine cannot be at the top of its range and labouring at
        // the bottom of it at the same time.
        if (!up)
        {
            gearText.color = shiftDownColor;
            flashTimer = 0f;
            return;
        }

        // A square blink rather than a sine: the readout is on or off, not dimming.
        flashTimer += Time.deltaTime * Mathf.Max(0.01f, flashRate);

        bool lit = (Mathf.FloorToInt(flashTimer) & 1) == 0;

        gearText.color = lit ? shiftUpColor : restingColor;
    }
}
