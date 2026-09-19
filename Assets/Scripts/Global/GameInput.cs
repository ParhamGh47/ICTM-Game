using UnityEngine;

/// <summary>
/// Every control the player drives with, read in one place so a keyboard and a gamepad behave the
/// same way.
///
/// The two devices do not naturally agree - a key ramps to full throttle in a third of a second
/// while a trigger is analogue - so both are read here and normalised into the same ranges the car
/// already expects (<see cref="CarController.throttleInput"/>, <see cref="CarController.steerInput"/>).
/// Full deflection of the stick, or the trigger squeezed all the way, therefore means exactly what a
/// held key means: a gamepad is never faster or slower, only finer.
///
/// Gamepad layout (Xbox naming, and the same physical buttons on any pad Unity recognises):
///
///   RT / 10th axis    gas, forward
///   LT / 9th axis     brake, and reverse once the car has stopped
///   Left stick        steering
///   A / cross         focus               B / circle        headlights
///   X / square        horn                Y / triangle      change camera
///   D-pad up          reset the vehicle   Start / Options   pause
///
/// The face buttons are read by position - bottom, right, left, top - through the generic
/// <c>KeyCode.JoystickButton</c> values, which is how a controller reports them on every platform: an
/// Xbox A, a PlayStation cross and a Switch B are all button 0, so one mapping covers all of them.
///
/// The axes are named in Project Settings > Input Manager, together with the axis number that suits
/// each device, so a pad that reports its triggers or D-pad somewhere else can be fixed there without
/// touching code. <c>KeyboardSteer</c> and <c>KeyboardThrottle</c> exist because the car must no
/// longer read the built-in <c>Horizontal</c>/<c>Vertical</c> axes: those now also carry the D-pad,
/// which the menus need, and a D-pad nudge must never steer the truck.
/// </summary>
public static class GameInput
{
    // ---------------------------------------------------------------- axis names

    private const string KeyboardSteerAxis = "KeyboardSteer";
    private const string KeyboardThrottleAxis = "KeyboardThrottle";
    private const string ControllerSteerAxis = "ControllerSteer";
    private const string ControllerGasAxis = "ControllerGas";
    private const string ControllerBrakeAxis = "ControllerBrake";
    private const string ControllerDPadVerticalAxis = "ControllerDPadVertical";

    // ---------------------------------------------------------------- buttons

    /// <summary>Bottom face button: A on an Xbox pad, cross on a PlayStation pad.</summary>
    public const KeyCode FocusButton = KeyCode.JoystickButton0;

    /// <summary>Right face button: B / circle.</summary>
    public const KeyCode LightsButton = KeyCode.JoystickButton1;

    /// <summary>Left face button: X / square.</summary>
    public const KeyCode HornButton = KeyCode.JoystickButton2;

    /// <summary>Top face button: Y / triangle.</summary>
    public const KeyCode CameraButton = KeyCode.JoystickButton3;

    /// <summary>Start on an Xbox pad, Menu on a newer one, Options on a PlayStation pad.</summary>
    public const KeyCode PauseButton = KeyCode.JoystickButton7;

    // ---------------------------------------------------------------- feel

    /// <summary>
    /// Stick movement below this is ignored, so a stick that rests slightly off centre can never pull
    /// the truck sideways while the player's hands are off it.
    /// </summary>
    public const float SteerDeadZone = 0.15f;

    /// <summary>
    /// How the stick is shaped between the dead zone and full lock. 1 would be a straight line; this
    /// is a little calmer around the centre, where a car is most sensitive, while a stick held all the
    /// way over still steers as hard as a held key.
    /// </summary>
    public const float SteerResponse = 1.35f;

    /// <summary>Trigger travel below this counts as released.</summary>
    public const float TriggerDeadZone = 0.06f;

    // ---------------------------------------------------------------- driving

    /// <summary>
    /// Throttle: +1 is full gas, -1 is full brake and then reverse, 0 is coasting.
    ///
    /// The two pedals are read separately rather than as one signed axis, so holding both simply
    /// cancels out instead of whichever the driver happened to press first winning.
    /// </summary>
    public static float Throttle()
    {
        float keys = Input.GetAxis(KeyboardThrottleAxis);

        float gas = Mathf.Max(0f, keys);
        float brake = Mathf.Max(0f, -keys);

        gas = Mathf.Max(gas, Trigger(ControllerGasAxis));
        brake = Mathf.Max(brake, Trigger(ControllerBrakeAxis));

        return Mathf.Clamp(gas - brake, -1f, 1f);
    }

    /// <summary>Steering: -1 is full left, +1 is full right.</summary>
    public static float Steer()
    {
        float keys = Input.GetAxis(KeyboardSteerAxis);
        float stick = ShapeStick(Input.GetAxis(ControllerSteerAxis));

        // Whichever device is being pushed further is the one the player means. The two are never
        // added together, so a stick left off centre cannot add to a held key and double the steering.
        return Mathf.Abs(stick) > Mathf.Abs(keys) ? stick : keys;
    }

    // ---------------------------------------------------------------- actions

    /// <summary>Focus is held, not tapped - space, or the bottom face button.</summary>
    public static bool FocusHeld()
    {
        return Input.GetKey(KeyCode.Space) || Input.GetKey(FocusButton);
    }

    /// <summary>Headlights: L, or B / circle.</summary>
    public static bool LightsPressed()
    {
        return Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(LightsButton);
    }

    /// <summary>Horn: H, or X / square.</summary>
    public static bool HornPressed()
    {
        return Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(HornButton);
    }

    /// <summary>Change camera: C, or Y / triangle.</summary>
    public static bool CameraPressed()
    {
        return Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(CameraButton);
    }

    /// <summary>Pause: Escape, or Start / Options.</summary>
    public static bool PausePressed()
    {
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(PauseButton);
    }

    /// <summary>
    /// Reset the vehicle: R, or up on the D-pad.
    ///
    /// The D-pad is an axis rather than a button, so it is turned into a press here - holding it down
    /// must not reset the car again every time the cooldown runs out.
    /// </summary>
    public static bool ResetPressed()
    {
        if (Input.GetKeyDown(KeyCode.R))
            return true;

        bool up = Input.GetAxis(ControllerDPadVerticalAxis) > 0.5f;
        bool pressed = up && !dpadUpWasHeld;

        dpadUpWasHeld = up;

        return pressed;
    }

    private static bool dpadUpWasHeld;

    // ---------------------------------------------------------------- shaping

    /// <summary>
    /// Takes the stick out of its dead zone and applies the response curve, keeping the ends exactly
    /// at -1 and 1 so the lock available on a pad matches the lock available on the keys.
    /// </summary>
    private static float ShapeStick(float raw)
    {
        float magnitude = Mathf.Abs(raw);

        if (magnitude <= SteerDeadZone)
            return 0f;

        float travel = Mathf.Clamp01((magnitude - SteerDeadZone) / (1f - SteerDeadZone));

        return Mathf.Sign(raw) * Mathf.Pow(travel, SteerResponse);
    }

    /// <summary>
    /// A trigger as 0..1. Drivers disagree about a released trigger - the newer ones report 0, some
    /// report -1 - so anything at or below the dead zone reads as released, and neither pedal can
    /// sneak a little throttle or braking into a corner while it is not being touched.
    /// </summary>
    private static float Trigger(string axis)
    {
        float raw = Input.GetAxis(axis);

        return Mathf.Clamp01((raw - TriggerDeadZone) / (1f - TriggerDeadZone));
    }
}
