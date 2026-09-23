using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Gives buttons an obvious hover / focus / press reaction.
///
/// Unity's built-in feedback is a colour tint (normal white, highlighted 0.96, selected 0.96), which is
/// almost invisible - and on this project's buttons it cannot be made much better, because their plates
/// are already tinted pure red, so a tint can only ever darken them and hover ends up looking like
/// pressed. Size is what actually reads, so this enlarges the button that has the pointer or the
/// selection, lifts its brightness, and eases back when it loses focus.
///
/// It installs itself once (the same way <see cref="LoadingScreen"/> does) and lives for the whole
/// session, so every button in the game reacts without anything being wired up in any scene. Only the
/// button that is currently hovered or selected is ever changed.
///
/// Turn it off through <see cref="Enabled"/>, or tune the fields on the "Button Focus Effect" object it
/// creates, to change how buttons feel everywhere at once.
/// </summary>
[DisallowMultipleComponent]
public class ButtonFocusEffect : MonoBehaviour
{
    // ---------------------------------------------------------------- configuration

    [Header("Size")]
    [Tooltip("How much bigger a button gets while it is hovered or selected.")]
    public float highlightedScale = 1.10f;

    [Tooltip("How much a button shrinks while it is held down.")]
    public float pressedScale = 0.96f;

    [Tooltip("How quickly the effect catches up. Higher is snappier.")]
    public float response = 18f;

    [Header("Brightness")]
    [Tooltip("How far the button's colour is pulled toward white while highlighted. " +
             "0 leaves only the size change.")]
    [Range(0f, 1f)] public float highlightedBrightness = 0.35f;

    [Tooltip("How far the button's colour is pulled toward black while pressed.")]
    [Range(0f, 1f)] public float pressedDarkening = 0.25f;

    [Header("Input")]
    [Tooltip("Follow the selection while a key or gamepad is used and the pointer while the mouse is " +
             "used, so the highlight never sticks to a button the mouse happens to be resting on.")]
    public bool switchWithInputDevice = true;

    // ---------------------------------------------------------------- state

    /// <summary>Global switch, so a settings screen can turn the effect off.</summary>
    public static bool Enabled = true;

    /// <summary>
    /// Until this time the pointer is ignored and the selection keeps the highlight.
    ///
    /// See <see cref="HoldHighlightOnSelection"/> - it is what stops a mouse left resting on one button
    /// from making it look like that button is the one a freshly opened panel defaults to.
    /// </summary>
    private static float holdHoverUntil;

    private static ButtonFocusEffect instance;

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Entry> stale = new List<Entry>();
    private readonly List<RaycastResult> hits = new List<RaycastResult>();

    private PointerEventData pointerData;
    private Vector3 lastPointerPosition;
    private bool usingMouse;

    private sealed class Entry
    {
        public RectTransform rect;
        public Graphic graphic;
        public Vector3 baseScale;
        public Color baseColor;
        public float scale = 1f;
        public Color color;
        public bool settled;
    }

    // ---------------------------------------------------------------- bootstrap

    /// <summary>
    /// Creates the effect once the game starts. Menus and levels both get it, and nothing has to be
    /// placed in a scene for buttons to react.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;
        Ensure();
    }

    public static ButtonFocusEffect Ensure()
    {
        if (instance != null) return instance;

        GameObject go = new GameObject("Button Focus Effect");
        return go.AddComponent<ButtonFocusEffect>();
    }

    /// <summary>
    /// Keeps the highlight on the selection for a moment, however the pointer is placed.
    ///
    /// Called when a menu hands its highlight to a default button as it opens. A pointer that happens to be
    /// resting on another button would otherwise be followed instead - the panel would open looking as if it
    /// defaulted to that button, and a player who clicked without moving the mouse would get it - so the
    /// hover is held off until <paramref name="seconds"/> have passed, or until the pointer is moved, which
    /// is checked against a stale position rather than a timer and so hands the highlight over at once.
    /// </summary>
    public static void HoldHighlightOnSelection(float seconds)
    {
        if (seconds <= 0f) return;

        Ensure();

        float until = Time.unscaledTime + seconds;
        if (until > holdHoverUntil) holdHoverUntil = until;

        // A pointer that has not moved since this call must not count as the mouse being *used*, or the very
        // position it is resting on would be followed the moment the hold expires. So the frame it was last
        // seen at is forgotten - any real movement is measured against it and hands the highlight over as it
        // should, while a pointer left alone keeps the highlight where the panel put it.
        if (instance != null)
        {
            instance.lastPointerPosition = Input.mousePosition;
            instance.usingMouse = false;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------------------------------------------------------------- per frame

    private void Update()
    {
        if (!Enabled || instance != this) return;

        EventSystem events = EventSystem.current;
        if (events == null) return;

        TrackInputDevice();

        // The pointer only leads the highlight when it is allowed to, which is everywhere except the moment
        // a panel has just handed its highlight to a default button.
        bool hoverAllowed = Time.unscaledTime >= holdHoverUntil;
        bool pointerMode = (usingMouse || !switchWithInputDevice) && hoverAllowed;

        Button hovered = pointerMode ? FindHovered(events) : null;

        Button selected = null;
        if (events.currentSelectedGameObject != null)
        {
            Button candidate = events.currentSelectedGameObject.GetComponent<Button>();
            if (IsUsable(candidate)) selected = candidate;
        }

        // Keep one button highlighted at all times, so it is always clear what Submit would activate.
        Button focus = pointerMode ? (hovered != null ? hovered : selected) : selected;
        bool pressed = focus != null && (Input.GetMouseButton(0) || Input.GetButton("Submit"));

        if (focus != null) EnsureEntry(focus);

        RectTransform focusRect = focus != null ? focus.transform as RectTransform : null;
        float blend = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);

        stale.Clear();
        foreach (Entry entry in entries)
        {
            if (entry.rect == null || entry.graphic == null)
            {
                stale.Add(entry);
                continue;
            }

            bool isFocus = entry.rect == focusRect;

            // Buttons that are already back to normal are left completely alone, so a menu full of
            // untouched buttons costs nothing per frame.
            if (entry.settled)
            {
                if (!isFocus) continue;
                entry.settled = false;
            }

            float targetScale = isFocus ? (pressed ? pressedScale : highlightedScale) : 1f;
            Color targetColor = isFocus
                ? Pull(entry.baseColor, pressed ? Color.black : Color.white,
                       pressed ? pressedDarkening : highlightedBrightness)
                : entry.baseColor;

            entry.scale = Mathf.Lerp(entry.scale, targetScale, blend);
            entry.color = Color.Lerp(entry.color, targetColor, blend);

            entry.rect.localScale = entry.baseScale * entry.scale;
            entry.graphic.color = entry.color;

            if (!isFocus && Mathf.Approximately(entry.scale, 1f) && entry.color == entry.baseColor)
                entry.settled = true;
        }

        foreach (Entry entry in stale) entries.Remove(entry);
    }

    // ---------------------------------------------------------------- input

    /// <summary>
    /// Remembers whether the player last used the mouse or a key, so the highlight follows what they are
    /// actually using instead of a pointer they left parked on a button.
    /// </summary>
    private void TrackInputDevice()
    {
        Vector3 position = Input.mousePosition;
        if (position != lastPointerPosition)
        {
            lastPointerPosition = position;
            usingMouse = true;
        }

        if (Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f ||
            Input.GetButtonDown("Submit") || Input.GetButtonDown("Cancel"))
        {
            usingMouse = false;
        }

        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) usingMouse = true;
    }

    private Button FindHovered(EventSystem events)
    {
        if (!events.IsPointerOverGameObject()) return null;

        if (pointerData == null) pointerData = new PointerEventData(events);
        pointerData.position = Input.mousePosition;

        // The first hit that belongs to a button wins: a button's own label sits on top of its plate, and
        // the parent search resolves both of them to the same button.
        hits.Clear();
        events.RaycastAll(pointerData, hits);

        for (int i = 0; i < hits.Count; i++)
        {
            Button button = hits[i].gameObject.GetComponentInParent<Button>();
            if (IsUsable(button)) return button;
        }

        return null;
    }

    private static bool IsUsable(Button button)
    {
        return button != null && button.isActiveAndEnabled && button.IsInteractable();
    }

    // ---------------------------------------------------------------- helpers

    private Entry EnsureEntry(Button button)
    {
        RectTransform rect = button.transform as RectTransform;
        if (rect == null) return null;

        foreach (Entry existing in entries)
            if (existing.rect == rect) return existing;

        Graphic graphic = button.targetGraphic;

        Entry entry = new Entry
        {
            rect = rect,
            graphic = graphic,
            baseScale = rect.localScale,
            baseColor = graphic != null ? graphic.color : Color.white,
        };

        entry.scale = 1f;
        entry.color = entry.baseColor;

        entries.Add(entry);
        return entry;
    }

    private static Color Pull(Color baseColor, Color towards, float amount)
    {
        Color result = Color.Lerp(baseColor, towards, Mathf.Clamp01(amount));
        result.a = baseColor.a;
        return result;
    }
}
