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
///   LT / 9th axis     brake, and reverse once the car has stopped (in manual, brake only - see below)
///   Left stick        steering
///   LB / L1           focus (hold)        Space             focus (hold)
///   A / cross         boost               Left Shift        boost
///   B / circle        headlights          L                 headlights
///   X / square        horn                H                 horn
///   Y / triangle      change camera       C                 change camera
///   D-pad up          reset the vehicle   R                 reset the vehicle
///   Start / Options   pause               Escape            pause
///
/// With the gearbox set to manual the truck changes gear by hand, so two controls have to give way to
/// make room on the pad - and they move rather than being given up:
///
///   B / circle        shift up            Page Up or E       shift up
///   X / square        shift down          Page Down or Q     shift down
///   D-pad left        horn                H                 horn
///   D-pad right       headlights          L                 headlights
///
/// The D-pad is also what navigates the menus on a pad - see <see cref="MenuDirection"/> - and with the
/// gearbox on manual that is the same four directions the truck uses for its reset, its horn and its
/// lights. The two never overlap in practice: a menu is up only while the truck is paused, and a paused
/// truck reads none of these controls (see <see cref="CarController"/>).
///
/// That is the only difference between the two layouts: the horn and the lights keep their own button
/// names on the keyboard, and on the pad they simply step one place inwards - the horn was X, the left
/// face button, and it becomes D-pad left; the lights were B, the right one, and they become D-pad right.
/// Nothing else moves, and switching back to automatic puts the pad exactly as it was.
///
/// The face buttons are read by position - bottom, right, left, top - through the generic
/// <c>KeyCode.JoystickButton</c> values, which is how a controller reports them on every platform: an
/// Xbox A, a PlayStation cross and a Switch B are all button 0, so one mapping covers all of them.
///
/// The axes are named in Project Settings > Input Manager, together with the axis number that suits
/// each device, so a pad that reports its triggers or D-pad somewhere else can be fixed there without
/// touching code. <c>KeyboardSteer</c> and <c>KeyboardThrottle</c> exist because the car must no
/// longer read the built-in <c>Horizontal</c>/<c>Vertical</c> axes: those now also carry the D-pad,
/// which the menus need, and a D-pad nudge must never steer the truck. The D-pad is read on its own
/// two axes (<c>ControllerDPadVertical</c>, <c>ControllerDPadHorizontal</c>) for the same reason.
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
    private const string ControllerDPadHorizontalAxis = "ControllerDPadHorizontal";

    // ---------------------------------------------------------------- menu

    /// <summary>
    /// Left shoulder: LB on an Xbox pad, L1 on a PlayStation pad. It is the focus control while the truck is
    /// being driven and the tab-previous control in a menu, which is the one place the two can be told apart:
    /// a menu has no truck to focus with.
    /// </summary>
    public const KeyCode TabPreviousButton = KeyCode.JoystickButton4;

    /// <summary>Right shoulder: RB on an Xbox pad, R1 on a PlayStation pad. Tab-next, and nothing else.</summary>
    public const KeyCode TabNextButton = KeyCode.JoystickButton5;

    // ---------------------------------------------------------------- buttons

    /// <summary>
    /// Left shoulder: LB on an Xbox pad, L1 on a PlayStation pad. Focus is held down, so it belongs on a
    /// button a finger rests on rather than one it has to travel to - which is what leaves the bottom face
    /// button free for the boost, a tap that is meant to be found instantly.
    /// </summary>
    public const KeyCode FocusButton = KeyCode.JoystickButton4;

    /// <summary>Bottom face button: A on an Xbox pad, cross on a PlayStation pad.</summary>
    public const KeyCode BoostButton = KeyCode.JoystickButton0;

    /// <summary>
    /// Right face button: B / circle. The headlights in automatic, and shift up in manual - the gearbox
    /// setting is what decides which (see <see cref="LightsPressed"/> and <see cref="GearUpPressed"/>).
    /// </summary>
    public const KeyCode LightsButton = KeyCode.JoystickButton1;

    /// <summary>
    /// Left face button: X / square. The horn in automatic, and shift down in manual.
    /// </summary>
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

    /// <summary>
    /// Dead zone of the D-pad, whose axes report a direction as a full-scale value. The same number the
    /// Input Manager uses for the two D-pad axes, so a press is read exactly the way the menus read it.
    /// </summary>
    public const float DPadDeadZone = 0.5f;

    // ---------------------------------------------------------------- directions

    /// <summary>
    /// Which way each D-pad direction reports on its own axis.
    ///
    /// Up is <em>positive</em> on <c>ControllerDPadVertical</c> and right is positive on
    /// <c>ControllerDPadHorizontal</c>. Which way a pad reports its own vertical is not something the project
    /// can know for it: an Xbox pad reports the D-pad's up on the positive end, so that entry carries no
    /// Invert - and the Invert flag is the single place to put a pad that arrives reporting it the other way
    /// round, which is where this was wrong before. Up being positive is also what every menu in the game
    /// treats as "up", so one D-pad press means the same thing to the truck and to a menu.
    /// </summary>
    private const float DPadUp = 1f;
    private const float DPadDown = -1f;
    private const float DPadLeft = -1f;
    private const float DPadRight = 1f;

    // ---------------------------------------------------------------- driving

    /// <summary>
    /// Throttle: +1 is full gas, -1 is full brake and then reverse, 0 is coasting.
    ///
    /// The two pedals are read separately rather than as one signed axis, so holding both simply
    /// cancels out instead of whichever the driver happened to press first winning.
    ///
    /// What the brake does depends on the gearbox, and it is the truck that decides: in automatic this is
    /// brake-then-reverse, and in manual it is a brake and nothing else, because the way to go backwards
    /// there is to select the reverse gear and use the gas (see <see cref="WheelPhysics"/>).
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

    /// <summary>Focus is held, not tapped - space, or the left shoulder button.</summary>
    public static bool FocusHeld()
    {
        return Input.GetKey(KeyCode.Space) || Input.GetKey(FocusButton);
    }

    /// <summary>
    /// Spend one of the boosts the truck is carrying: Left Shift, or the bottom face button. An edge
    /// rather than a hold, so leaning on it does not spend them one after another.
    /// </summary>
    public static bool BoostPressed()
    {
        return Input.GetKeyDown(KeyCode.LeftShift)
            || Input.GetKeyDown(KeyCode.RightShift)
            || Input.GetKeyDown(BoostButton);
    }

    /// <summary>
    /// Headlights: L, or B / circle in automatic and right on the D-pad in manual - where that button is
    /// the gear lever instead.
    /// </summary>
    public static bool LightsPressed()
    {
        if (Input.GetKeyDown(KeyCode.L))
            return true;

        if (DriveSettings.Manual)
            return DPadPressed(ControllerDPadHorizontalAxis, DPadRight, ref dpadRightWasHeld);

        return Input.GetKeyDown(LightsButton);
    }

    /// <summary>
    /// Horn: H, or X / square in automatic and left on the D-pad in manual. The horn and the lights are
    /// the two controls the pad's face buttons give up when the player drives manually, and they take the
    /// two directions the D-pad has left over.
    /// </summary>
    public static bool HornPressed()
    {
        if (Input.GetKeyDown(KeyCode.H))
            return true;

        if (DriveSettings.Manual)
            return DPadPressed(ControllerDPadHorizontalAxis, DPadLeft, ref dpadLeftWasHeld);

        return Input.GetKeyDown(HornButton);
    }

    /// <summary>
    /// Shift up a gear: B / circle, which is free in manual because the lights have moved to the D-pad.
    /// Page Up is the keyboard's gear lever, and E is the second one a player coming from another racing
    /// game is likely to reach for - both work, and neither is used for anything else.
    ///
    /// Nothing at all in automatic: there the box shifts itself and this button is still the headlights.
    /// </summary>
    public static bool GearUpPressed()
    {
        if (!DriveSettings.Manual)
            return false;

        return Input.GetKeyDown(KeyCode.PageUp)
            || Input.GetKeyDown(KeyCode.E)
            || Input.GetKeyDown(LightsButton);
    }

    /// <summary>
    /// Shift down a gear: X / square, Page Down, or Q - the mirror of <see cref="GearUpPressed"/>, and
    /// nothing in automatic, where the button is still the horn.
    ///
    /// Pressed again in first, this selects reverse, which is the one gear with no ratio of its own (see
    /// <see cref="EngineAudio"/>).
    /// </summary>
    public static bool GearDownPressed()
    {
        if (!DriveSettings.Manual)
            return false;

        return Input.GetKeyDown(KeyCode.PageDown)
            || Input.GetKeyDown(KeyCode.Q)
            || Input.GetKeyDown(HornButton);
    }

    /// <summary>
    /// The pad's own direction buttons as a menu direction: -1, 0 or 1 on each axis, and 0 when nothing is
    /// held. +y is up and +x is right, the way the menus read them.
    ///
    /// The menus are navigated through Unity's own input module, which reads the built-in
    /// <c>Horizontal</c>/<c>Vertical</c> axes, and that is where this used to be left - a copy of each of
    /// those axes was added to the Input Manager on the D-pad's own axis numbers. It did not work on a real
    /// pad, and neither did the truck's own D-pad controls, which is what this replaces: the D-pad is read
    /// here, on the two axes it is known to report on (the same ones the truck's controls use), and the menu
    /// navigation is moved by <see cref="MenuNavigation"/> with the answer. Adding the D-pad to the built-in
    /// axes as well would move a menu twice for one press, so it is deliberately not done.
    /// </summary>
    public static void MenuDirection(out int horizontal, out int vertical)
    {
        float x = Input.GetAxisRaw(ControllerDPadHorizontalAxis);
        float y = Input.GetAxisRaw(ControllerDPadVerticalAxis);

        horizontal = x > DPadDeadZone ? 1 : (x < -DPadDeadZone ? -1 : 0);
        vertical = y > DPadDeadZone ? 1 : (y < -DPadDeadZone ? -1 : 0);
    }

    /// <summary>
    /// Whether the pad's D-pad is being held at all, whatever direction. Used to keep the menus' highlight
    /// following the selection while a pad is used, the way the built-in axes do when the stick or a key is.
    /// </summary>
    public static bool MenuDirectionHeld()
    {
        MenuDirection(out int x, out int y);

        return x != 0 || y != 0;
    }

    /// <summary>Next tab in a menu: E, or the right shoulder button.</summary>
    public static bool TabNextPressed()
    {
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(TabNextButton);
    }

    /// <summary>Previous tab in a menu: Q, or the left shoulder button.</summary>
    public static bool TabPreviousPressed()
    {
        return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(TabPreviousButton);
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
    /// Reset the vehicle: R, or up on the D-pad. It puts the truck back on the road, facing the way the
    /// level goes - it does not mend the damage the truck is carrying.
    ///
    /// The D-pad is an axis rather than a button, so it is turned into a press here - holding it down
    /// must not reset the car again every time the cooldown runs out. Up rather than down, which is where
    /// the controls tables have always listed it, and which leaves the two directions a driving game
    /// expects to find free - left and right, for the horn and the lights - free.
    /// </summary>
    public static bool ResetPressed()
    {
        if (Input.GetKeyDown(KeyCode.R))
            return true;

        return DPadPressed(ControllerDPadVerticalAxis, DPadUp, ref dpadUpWasHeld);
    }

    /// <summary>
    /// One direction of the D-pad as a press rather than as an axis: true on the frame it is pushed, and
    /// not again until it has been let go.
    ///
    /// The axis is named by the caller rather than worked out from <paramref name="direction"/>, and that is
    /// the whole point of the parameter: up and right are both +1 on their own axes, so a direction cannot
    /// say which axis it belongs to. It used to be guessed that way, and every +1 was therefore read off the
    /// horizontal axis - which put the reset on D-pad right, alongside the lights, and left D-pad up doing
    /// nothing at all.
    /// </summary>
    private static bool DPadPressed(string axis, float direction, ref bool wasHeld)
    {
        bool held = DPadHeld(axis, direction);
        bool pressed = held && !wasHeld;

        wasHeld = held;

        return pressed;
    }

    /// <summary>Whether one direction of the D-pad is down right now.</summary>
    private static bool DPadHeld(string axis, float direction)
    {
        float raw = Input.GetAxis(axis);

        return direction > 0f ? raw > DPadDeadZone : raw < -DPadDeadZone;
    }

    // One flag per direction, because each is asked for on its own: the reset, the horn and the lights
    // are all separate controls and one being held must not count as the others having been pressed.
    private static bool dpadUpWasHeld;
    private static bool dpadLeftWasHeld;
    private static bool dpadRightWasHeld;

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
