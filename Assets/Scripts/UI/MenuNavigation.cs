using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("Behaviour")]
    [Tooltip("Highlight a button as soon as this object - or its panel - is enabled.")]
    public bool selectOnEnable = true;

    [Tooltip("Put the highlight back when something clears it, e.g. a click on empty space or a button " +
             "being disabled. Turn this off if the selection should be free to go away.")]
    public bool keepSelectionAlive = true;

    [Tooltip("Return to the button that was highlighted the last time this panel closed.")]
    public bool rememberLast = true;

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

    // The axes Unity's own input module navigates with. They carry W/A/S/D, the thumbs tick and the D-pad,
    // which is also everything the player drives with - that overlap is the whole reason for the grace below.
    private const string NavHorizontalAxis = "Horizontal";
    private const string NavVerticalAxis = "Vertical";

    private Coroutine inputGrace;
    private bool navigationHeld;

    // ---------------------------------------------------------------- lifecycle

    private void OnEnable()
    {
        Refresh();

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
        if (!keepSelectionAlive) return;

        // Nothing can move the selection while the grace is running, so there is nothing to restore either.
        if (navigationHeld) return;

        EventSystem events = EventSystem.current;
        if (events == null) return;

        GameObject selected = events.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy && managed.Contains(selected.GetComponent<Selectable>()))
            return;

        // The selection was cleared (or points at something that is no longer usable) - take it back.
        Refresh();
        SelectDefault();
    }

    // ---------------------------------------------------------------- selection

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

        Selectable target = FindByName(firstButtonName) ?? TopMost() ?? managed[0];
        events.SetSelectedGameObject(target.gameObject);
    }

    private void Refresh()
    {
        managed.Clear();

        if (buttons != null && buttons.Length > 0)
        {
            foreach (Selectable button in buttons)
                if (IsUsable(button)) managed.Add(button);
            return;
        }

        foreach (Selectable child in GetComponentsInChildren<Selectable>(true))
            if (IsUsable(child)) managed.Add(child);

        if (managed.Count > 0) return;

        // A manager object with no UI of its own: fall back to the buttons of the scene.
        foreach (Selectable sceneButton in FindObjectsOfType<Selectable>(true))
            if (IsUsable(sceneButton)) managed.Add(sceneButton);
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
