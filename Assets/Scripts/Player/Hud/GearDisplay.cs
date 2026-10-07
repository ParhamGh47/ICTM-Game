using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The HUD's gear readout: the gear the truck is in, printed as R or its number.
///
/// In manual the number is also the thing the player is being asked to manage, and the one cue the readout
/// has for that is its colour: when the gearbox says a change is due - the engine is near the top of its rev
/// range and there is a gear left to go up to, or it is labouring low down in a gear that has one below it -
/// the reading blinks between its own colour and the cue colour, and back to its own colour when there is
/// nothing to do. See <see cref="EngineAudio.ShiftCue"/>.
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
    [Tooltip("The colour the readout blinks to when a change is due. The reading's own authored colour is " +
             "what it blinks back to, so the prefab stays the place the resting look is set.")]
    public Color shiftCueColor = new Color(1f, 0.92f, 0.45f, 1f);

    [Tooltip("How many times a second the readout alternates while a change is due. Fast enough to read as a " +
             "blink rather than a fade, slow enough not to strobe.")]
    public float flashRate = 4f;

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

        if (!engineAudio.ShiftCue)
        {
            gearText.color = restingColor;
            flashTimer = 0f;
            return;
        }

        // A square blink rather than a sine: the readout is on or off, not dimming.
        flashTimer += Time.deltaTime * Mathf.Max(0.01f, flashRate);

        bool lit = (Mathf.FloorToInt(flashTimer) & 1) == 0;

        gearText.color = lit ? shiftCueColor : restingColor;
    }
}
