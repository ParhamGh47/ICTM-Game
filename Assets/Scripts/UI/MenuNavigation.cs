using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes a menu usable with a keyboard or a gamepad.
///
/// Unity already works out which button an arrow key moves to (every button in the project uses
/// automatic navigation) and the project's input axes already cover the arrow keys, WASD and a gamepad
/// stick. The one thing missing by default is a starting point: with nothing selected the first key
/// press lands on nothing and the menu looks dead.
///
/// This component supplies that starting point. It highlights a button when the menu - or the panel it
/// sits on - becomes visible, and puts the highlight back if something clears it.
///
/// It also decides who wins when a panel opens in the middle of play. The truck is driven with W/A/S/D and
/// the left stick, which are the very axes Unity navigates menus with, so a panel that appears while the
/// player is still holding a control - the game over panel opens on a crash, and a crash happens mid-turn -
/// would be dragged off its default button before it could be read. Navigation is therefore held back until
/// the controls are released (see <see cref="ignoreHeldInput"/>), and the default button is shown as the
/// highlighted one even if the pointer happens to be resting on another (see <see cref="highlightHoldTime"/>).
/// Only navigation is held back: the pointer keeps working, so a click is never swallowed.
///
/// Attach it to the menu root, or to a panel that is switched on and off (the pause panel), and leave
/// <see cref="buttons"/> empty: it then picks up every selectable underneath, or - when the object has
/// no UI children of its own, like a manager object - every selectable in the scene. That keeps it
/// working as buttons are added, renamed or removed.
///
/// It also moves the highlight with the pad's own direction buttons (<see cref="dPadNavigation"/>). That is
/// here rather than left to Unity because a pad's left stick feeds the built-in axes the input module
/// navigates with and its D-pad does not: as far as the menus were concerned a gamepad had a stick and no
/// D-pad at all. The D-pad is read on the two axes the truck already reads it on, so a direction that turns
/// the truck's lights off also moves a menu, and it is only ever applied by the manager that is actually
/// holding the highlight - so a pause menu and the options inside it cannot both answer one press.
///
/// A menu scene with no navigator of its own is given one as it loads (<see cref="Bootstrap"/>), which is how
/// the credits screen - built in code, one button - is navigable on a pad like the rest of them.
/// </summary>
[DisallowMultipleComponent]
public class MenuNavigation : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("Buttons to manage. Leave empty to find them automatically.")]
    public Selectable[] buttons;

    [Tooltip("Button highlighted first, matched by GameObject name (case-insensitive). " +
             "Leave empty for the top-most button.")]
    public string firstButtonName;

    [Header("Out of navigation")]
    [Tooltip("Selectables the highlight must never come to rest on, however they are reached: the options " +
             "tab row is the reason for it, since a tab is switched by its own controls - the shoulder buttons " +
             "or Q/E - and is deliberately not part of the page's button navigation. A tab named here can " +
             "still be clicked; it simply cannot be arrived at with a stick, a D-pad or the arrow keys, and " +
             "the default highlight never lands on one either.")]
    public Selectable[] ignore;

    [Header("Behaviour")]
    [Tooltip("Highlight a button as soon as this object - or its panel - is enabled.")]
    public bool selectOnEnable = true;

    [Tooltip("Put the highlight back when something clears it, e.g. a click on empty space or a button " +
             "being disabled. Turn this off if the selection should be free to go away.")]
    public bool keepSelectionAlive = true;

    [Tooltip("Return to the button that was highlighted the last time this panel closed.")]
    public bool rememberLast = true;

    [Header("D-pad")]
    [Tooltip("Move the highlight with the pad's own direction buttons. Unity's input module moves a menu " +
             "with the built-in axes, which a pad's left stick feeds but its D-pad does not, so the D-pad is " +
             "read and applied here instead. Off leaves the menus to the stick, the keys and the pointer.")]
    public bool dPadNavigation = true;

    [Tooltip("How long a direction is held before it starts stepping again.")]
    public float dPadRepeatDelay = 0.45f;

    [Tooltip("How fast the highlight then steps while the direction is held.")]
    public float dPadRepeatRate = 0.12f;

    [Header("Input")]
    [Tooltip("Hold navigation back while a control the game is also driven with is still held as this panel " +
             "opens. Without it the throttle and steering keys - the same keys Unity moves a menu with - " +
             "drag the highlight off the default button the moment the panel appears.")]
    public bool ignoreHeldInput = true;

    [Tooltip("The shortest time navigation stays held back, even with nothing pressed: long enough that the " +
             "press which opened the panel cannot also move it.")]
    public float minimumInputGrace = 0.2f;

    [Tooltip("The longest navigation can stay held back, so a control that is stuck or resting off centre " +
             "can never leave the menu unusable.")]
    public float maximumInputGrace = 2f;

    [Tooltip("How long the default button is shown as the highlighted one, even if the pointer happens to be " +
             "resting on another button as the panel opens.")]
    public float highlightHoldTime = 0.35f;

    private readonly List<Selectable> managed = new List<Selectable>();
    private Selectable lastSelected;

    // The axes Unity's own input module navigates with. They carry W/A/S/D, the thumbstick and the D-pad,
    // which is also everything the player drives with - that overlap is the whole reason for the grace below.
    private const string NavHorizontalAxis = "Horizontal";
    private const string NavVerticalAxis = "Vertical";

    private Coroutine inputGrace;
    private bool navigationHeld;

    // The D-pad direction being held, so a hold steps through the buttons rather than moving one place.
    private int dPadX;
    private int dPadY;
    private float dPadNextStep;

    // Set while a direction that was already down as the panel opened is still down: it is not acted on until
    // it has been released and pressed again.
    private bool dPadHeldAlready;

    // When the list of buttons may be looked for again after finding none - see <see cref="KeepSelection"/>.
    private float nextListSearch;

    // When a named default stops being waited for - see <see cref="SelectDefault"/>. Zero means it has not
    // started waiting. Short on purpose: it is there to cover a button that arrives a frame or two after the
    // menu object enables, not to leave a menu unhighlighted while something is hoped for.
    private float defaultSearchDeadline;

    private const float DefaultSearchTime = 0.4f;

    // ---------------------------------------------------------------- lifecycle

    private void OnEnable()
    {
        Refresh();

        // Whatever the D-pad is doing as this panel opens counts as already held, so it has to be let go before
        // it moves anything: the direction that was being leaned on when the panel opened itself - the reset a
        // player is holding as a game over panel comes up, say - must not drag the highlight with it. The same
        // idea as ignoreHeldInput, for the D-pad rather than for the axes.
        GameInput.MenuDirection(out dPadX, out dPadY);
        dPadHeldAlready = dPadX != 0 || dPadY != 0;
        dPadNextStep = Time.unscaledTime + Mathf.Max(0.05f, dPadRepeatDelay);

        if (!selectOnEnable) return;

        SelectDefault();

        // The default button is the one thing the player should be able to read before the panel moves -
        // seen, not merely selected, so the pointer is held off it for a moment too.
        ButtonFocusEffect.HoldHighlightOnSelection(highlightHoldTime);

        BeginInputGrace();
    }

    private void OnDisable()
    {
        // Remember where the player was, so reopening this panel lands on the same button.
        Selectable current = CurrentSelection();
        if (current != null && managed.Contains(current)) lastSelected = current;

        // A panel can be closed while the grace is still running - pausing and resuming quickly, say - and
        // the guard must never outlive the object that set it.
        if (inputGrace != null) StopCoroutine(inputGrace);
        ResumeNavigation();
    }

    // ---------------------------------------------------------------- input grace

    private void BeginInputGrace()
    {
        if (!ignoreHeldInput) return;

        if (inputGrace != null) StopCoroutine(inputGrace);
        inputGrace = StartCoroutine(InputGraceRoutine());
    }

    /// <summary>
    /// Switches Unity's navigation events off, and waits for the controls to come back to rest before
    /// switching them on again.
    ///
    /// A time alone would not be enough: the input module repeats a held direction every half second, so a
    /// key held when the panel opened would keep pulling the highlight away for as long as it was held. The
    /// guard therefore ends on release - with a floor so nothing is missed on the way in, and a ceiling so a
    /// stuck control cannot lock the menu.
    ///
    /// Only movement and submit are suspended; the module still processes pointer input, so the mouse and
    /// every click keep working throughout.
    /// </summary>
    private IEnumerator InputGraceRoutine()
    {
        EventSystem events = EventSystem.current;

        // Nothing to guard, or another panel is already holding navigation - in which case it owns the
        // switch, and we must not turn it back on behind its back.
        if (events == null || !events.sendNavigationEvents) yield break;

        events.sendNavigationEvents = false;
        navigationHeld = true;

        float startedAt = Time.unscaledTime;

        while (true)
        {
            // Unscaled, because the panels that need this most - the game over one - stop time as they open.
            float elapsed = Time.unscaledTime - startedAt;

            bool atRest = Mathf.Abs(Input.GetAxisRaw(NavHorizontalAxis)) < 0.5f &&
                          Mathf.Abs(Input.GetAxisRaw(NavVerticalAxis)) < 0.5f;

            if ((elapsed >= minimumInputGrace && atRest) || elapsed >= maximumInputGrace) break;

            yield return null;
        }

        ResumeNavigation();
    }

    private void ResumeNavigation()
    {
        inputGrace = null;

        if (!navigationHeld) return;
        navigationHeld = false;

        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
    }

    private void Update()
    {
        UpdateDPad();

        KeepSelection();
    }

    // ---------------------------------------------------------------- the D-pad

    /// <summary>
    /// Steps the highlight when the pad's direction buttons are used, and steps it again, more slowly, while
    /// one is held - the same shape a stick held over has, which is what Unity's own navigation does for the
    /// axes it reads.
    ///
    /// Unscaled time throughout, because the panels that most need this are the ones that stop the clock.
    /// </summary>
    private void UpdateDPad()
    {
        if (!dPadNavigation || navigationHeld)
        {
            ForgetDPad();
            return;
        }

        GameInput.MenuDirection(out int x, out int y);

        if (x == 0 && y == 0)
        {
            dPadHeldAlready = false;
            ForgetDPad();
            return;
        }

        // One that was already down as the panel opened waits to be let go.
        if (dPadHeldAlready)
        {
            dPadX = x;
            dPadY = y;
            return;
        }

        // A new direction moves at once; holding the same one keeps moving on the repeat's own clock.
        if (x != dPadX || y != dPadY)
        {
            dPadX = x;
            dPadY = y;
            dPadNextStep = Time.unscaledTime + Mathf.Max(0.05f, dPadRepeatDelay);

            MoveSelection(x, y);
            return;
        }

        if (Time.unscaledTime < dPadNextStep) return;

        dPadNextStep = Time.unscaledTime + Mathf.Max(0.02f, dPadRepeatRate);

        MoveSelection(x, y);
    }

    private void ForgetDPad()
    {
        dPadX = 0;
        dPadY = 0;
    }

    /// <summary>
    /// Moves the highlight one step in the direction the pad is being held.
    ///
    /// Only the manager that is holding the highlight answers: the selection has to be one of this object's
    /// own buttons, so when the options are open inside the pause window - one manager between them - the
    /// press is answered once, and a menu that is up behind another cannot answer it at all.
    /// </summary>
    private void MoveSelection(int x, int y)
    {
        EventSystem events = EventSystem.current;
        if (events == null) return;

        Selectable current = CurrentSelection();
        if (current == null || !managed.Contains(current)) return;

        Selectable target = null;

        if (y > 0) target = current.FindSelectableOnUp();
        else if (y < 0) target = current.FindSelectableOnDown();
        else if (x < 0) target = current.FindSelectableOnLeft();
        else if (x > 0) target = current.FindSelectableOnRight();

        // The target has to be one of this menu's own buttons and not one of the selectables that are kept
        // out of navigation: Unity's own directional search does skip them, but a tab is exactly the thing a
        // press must never land on, so it is checked here rather than trusted to.
        if (target == null || !IsUsable(target) || !managed.Contains(target)) return;

        events.SetSelectedGameObject(target.gameObject);
    }

    // ---------------------------------------------------------------- keeping a selection

    private void KeepSelection()
    {
        if (!keepSelectionAlive) return;

        // Nothing can move the selection while the grace is running, so there is nothing to restore either.
        if (navigationHeld) return;

        EventSystem events = EventSystem.current;
        if (events == null) return;

        GameObject selected = events.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && managed.Contains(selected.GetComponent<Selectable>()))
            return;

        // A menu with nothing to highlight at all - a screen whose buttons are built a frame or two after it
        // loads - would otherwise re-read every selectable in the scene every frame while it waits. Looking
        // four times a second is fast enough for buttons that are on their way, and costs almost nothing when
        // there are none coming.
        if (managed.Count == 0 && Time.unscaledTime < nextListSearch) return;

        nextListSearch = Time.unscaledTime + 0.25f;

        // The selection was cleared (or points at something that is no longer usable) - take it back.
        Refresh();
        SelectDefault();
    }

    // ---------------------------------------------------------------- selection

    /// <summary>
    /// Re-reads the buttons after the screen has changed what is on offer - a page switched on, a tab added to
    /// <see cref="ignore"/> - so the highlight cannot be left on a button that is no longer part of the menu.
    /// </summary>
    public void RefreshButtons()
    {
        Refresh();
    }

    /// <summary>Highlights the remembered button, or the configured first one.</summary>
    public void SelectDefault()
    {
        EventSystem events = EventSystem.current;
        if (events == null) return;

        if (managed.Count == 0) return;

        if (rememberLast && IsUsable(lastSelected) && managed.Contains(lastSelected))
        {
            events.SetSelectedGameObject(lastSelected.gameObject);
            return;
        }

        Selectable target = FindByName(firstButtonName);

        // A default named on this component is worth a moment's wait when the button is not there yet: the
        // name is set in the scene while the buttons it names can arrive a frame or two later - a canvas
        // switched on after this object enables, a screen built in code. Falling back to the top-most button
        // at once is what makes a menu open on the wrong button and stay there, because by the time the named
        // one turns up the fallback is a perfectly good selection and nothing ever moves it. The wait is short
        // (see DefaultSearchTime): a name that is not on the screen at all - a level list whose first button
        // is still locked, say - falls back to the top-most button almost at once.
        if (target == null && !string.IsNullOrEmpty(firstButtonName))
        {
            if (defaultSearchDeadline <= 0f) defaultSearchDeadline = Time.unscaledTime + DefaultSearchTime;

            if (Time.unscaledTime < defaultSearchDeadline) return;
        }

        defaultSearchDeadline = 0f;

        target = target != null ? target : (TopMost() ?? managed[0]);

        events.SetSelectedGameObject(target.gameObject);
    }

    // ---------------------------------------------------------------- install

    /// <summary>
    /// Gives a menu scene a navigator if it has none. Every menu in the project already carries this
    /// component except the credits screen, which is built in code and holds one button - and now that the
    /// D-pad is read here rather than through the built-in axes, a menu scene without one is a menu a gamepad
    /// cannot move at all.
    ///
    /// A scene with a player truck in it is a level rather than a menu and is left alone, so nothing is ever
    /// added beside the pause menu or the game over panel, both of which have their own. The rule is the same
    /// one the speed blur uses to decide whether a scene is worth costing anything.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        Consider(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Consider(scene);
    }

    private static void Consider(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        if (Object.FindObjectsOfType<CarController>().Length > 0) return;
        if (Object.FindObjectOfType<MenuNavigation>() != null) return;

        new GameObject("Menu Navigation").AddComponent<MenuNavigation>();
    }

    private void Refresh()
    {
        managed.Clear();

        if (buttons != null && buttons.Length > 0)
        {
            foreach (Selectable button in buttons)
                if (CanBeHighlighted(button)) managed.Add(button);
            return;
        }

        foreach (Selectable child in GetComponentsInChildren<Selectable>(true))
            if (CanBeHighlighted(child)) managed.Add(child);

        if (managed.Count > 0) return;

        // A manager object with no UI of its own: fall back to the buttons of the scene.
        foreach (Selectable sceneButton in FindObjectsOfType<Selectable>(true))
            if (CanBeHighlighted(sceneButton)) managed.Add(sceneButton);
    }

    /// <summary>
    /// Whether the highlight may come to rest on this one: usable, and not one of the selectables that are
    /// deliberately outside the button navigation - see <see cref="ignore"/>.
    /// </summary>
    private bool CanBeHighlighted(Selectable selectable)
    {
        if (!IsUsable(selectable)) return false;
        if (ignore == null) return true;

        for (int i = 0; i < ignore.Length; i++)
            if (ignore[i] == selectable) return false;

        return true;
    }

    private static bool IsUsable(Selectable selectable)
    {
        // isActiveAndEnabled already covers buttons inside a hidden panel, which must not be selectable.
        return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable();
    }

    private static Selectable CurrentSelection()
    {
        EventSystem events = EventSystem.current;
        if (events == null || events.currentSelectedGameObject == null) return null;
        return events.currentSelectedGameObject.GetComponent<Selectable>();
    }

    private Selectable FindByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        foreach (Selectable selectable in managed)
            if (string.Equals(selectable.gameObject.name, name, System.StringComparison.OrdinalIgnoreCase))
                return selectable;

        return null;
    }

    private Selectable TopMost()
    {
        Selectable best = null;
        float bestY = float.NegativeInfinity;

        foreach (Selectable selectable in managed)
        {
            float y = selectable.transform.position.y;
            if (best == null || y > bestY)
            {
                best = selectable;
                bestY = y;
            }
        }

        return best;
    }
}
