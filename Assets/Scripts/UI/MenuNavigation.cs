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

    private readonly List<Selectable> managed = new List<Selectable>();
    private Selectable lastSelected;

    // ---------------------------------------------------------------- lifecycle

    private void OnEnable()
    {
        Refresh();
        if (selectOnEnable) SelectDefault();
    }

    private void OnDisable()
    {
        // Remember where the player was, so reopening this panel lands on the same button.
        Selectable current = CurrentSelection();
        if (current != null && managed.Contains(current)) lastSelected = current;
    }

    private void Update()
    {
        if (!keepSelectionAlive) return;

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
