using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The card a level opens on: a short, animated demonstration of what the level is about, shown once before
/// the truck is handed over, with a "don't show this again" switch for a player who has seen it.
///
/// It is dropped into a gameplay scene as a single object and needs nothing wired: the whole card - the frame,
/// the stage it animates on, the counter, the two controls - is built in code, the way the tips, credits and
/// loading screens are, so the scene file holds one object rather than a hundred. A level is given an intro by
/// placing one, and taken out of one by deleting it.
///
/// <b>What it teaches, and how.</b> The lesson is shown rather than described: a stage inside the card plays a
/// loop in which the level's own truck is let go a long way back, runs down a road at one of the game's own
/// targets, and the target is blown apart in the gap the truck stops short of - while the counter beside the
/// road ticks up and pops. The caption says what the picture is showing in as few words as it can ("SMASH THEM
/// ON THE ROAD", then "+1 KILL"), because the picture is doing the explaining. The loop runs until the player
/// starts, so a card glimpsed once can be watched as many times as it takes.
///
/// <b>While it is up.</b> The game is held still and quiet the way the pause menu holds it - the clock is
/// stopped, the listener is paused, so the engine and the level's soundtrack go quiet - and
/// <see cref="PauseTracker"/> says the game is paused, so the truck reads none of its controls. The pointer
/// is left visible (see <see cref="GameplayCursor"/>, which shows it whenever the clock is stopped), so the two
/// controls can be clicked as well as nudged, and the card's own two sounds stay audible over the silence. The
/// card fades in a beat after the level (see <see cref="showDelaySeconds"/>), so it lands on a scene that has
/// settled rather than on its first frame.
///
/// Escape and Start are deliberately not a way *into* anything here:
/// the pause menu refuses to open while a card is up (see <see cref="isShowing"/>), and any other button starts
/// the level outright, so leaning on the pad never leaves the player staring at a card.
///
/// <b>Being chosen again.</b> The card shows every time the level opens until the switch is used, and what it
/// remembers is one flag per tutorial, kept by <see cref="TutorialProgress"/> - so two levels' cards never hide
/// each other, and "show all intros again" is a single call away.
///
/// <b>Removing it.</b> Delete the object from the level. Set <see cref="alwaysShow"/> to see a card again
/// without touching the saved answer.
/// </summary>
[DisallowMultipleComponent]
public class TutorialIntro : MonoBehaviour
{
    /// <summary>
    /// The lessons this card can stage. One today - targets - and the natural place for the next: a lesson is a
    /// case in <see cref="BuildStage"/> and the words that go with it, while everything else about the card (the
    /// holding of the game, the switch, the leaving) is the same for all of them.
    /// </summary>
    public enum Lesson
    {
        KillTargets,
    }

    [Header("The lesson")]
    [Tooltip("What the card demonstrates. Each lesson has its own stage and its own words.")]
    public Lesson lesson = Lesson.KillTargets;

    [Tooltip("What the 'don't show this again' answer is remembered under. Left empty, the scene's own name " +
             "is used, so a card in another level never hides this one.")]
    public string tutorialName = "";

    [Tooltip("Show the card even when the player has switched it off. For looking at it again while working " +
             "on it - it does not touch the saved answer.")]
    public bool alwaysShow;

    [Tooltip("Upper case throughout, which is how the game's own headings read.")]
    public string titleText = "TARGETS & KILLS";

    [Tooltip("The line under the stage. It says what the picture is showing, and the picture does the rest.")]
    public string captionText = "SMASH THEM ON THE ROAD";

    [Tooltip("What the line says at the moment of the hit, while the counter is popping.")]
    public string hitCaptionText = "+1 KILL";

    [Header("The models on the stage")]
    [Tooltip("The level's own truck, copied onto the stage and stripped down to its model, so the card shows " +
             "the player the vehicle they are about to drive. Left empty, the card draws a stand-in instead.")]
    public GameObject truckModel;

    [Tooltip("The game's own Adamak - the figure the player is being told to hit - copied onto the stage and " +
             "stripped down to its model. Left empty, the card draws a stand-in instead.")]
    public GameObject targetModel;

    [Header("The counter on the stage")]
    [Tooltip("The number the level is asking for, shown beside the count.")]
    public int targetCount = 10;

    [Tooltip("The count the loop starts from, so the tick up reads as progress rather than as a first kill.")]
    public int startCount = 3;

    /// <summary>
    /// The card's grid, in the 1920x1080 the whole UI is laid out in.
    ///
    /// Everything on the card is placed on this rather than at a size or an offset of its own: two columns of
    /// set widths with an even margin around them and an even gap between them - the picture on the left, the
    /// count on the right - and the rows down the middle that the title, the columns, the caption and the two
    /// controls sit on. The widths follow from the card's inner rule, so the columns stay put if the card is
    /// resized, and both of them line up with each other by construction rather than by eye.
    /// </summary>
    private static class Layout
    {
        public const float Edge = 40f;             // margin from the card's inner rule, on every side
        public const float Gap = 40f;              // between the picture and the count
        public const float InnerHalfWidth = 602f;  // the rule's own half width (a 1240-wide card, inset 18)

        public const float PictureWidth = 764f;
        public const float NoteWidth = 320f;

        public const float StageHeight = 360f;
        public const float MiddleRow = 10f;
        public const float TitleRow = 252f;
        public const float CaptionRow = -200f;
        public const float ControlsRow = -272f;

        /// <summary>The middle of the picture column, which is the left one: the rule, then the margin, then
        /// half the column.</summary>
        public static float PictureCentre { get { return -InnerHalfWidth + Edge + PictureWidth * 0.5f; } }

        /// <summary>The middle of the count column, which is the right one, measured the same way in from the
        /// right-hand rule.</summary>
        public static float NoteCentre { get { return InnerHalfWidth - Edge - NoteWidth * 0.5f; } }
    }

    [Header("The card's look (the level HUD's own palette)")]
    [Tooltip("The card's own hand: the typewriter font the story scenes are written in (Saad).")]
    public TMP_FontAsset cardFont;

    [Tooltip("The font the HUD-like parts keep - the counter in the middle of the card - so the thing it is " +
             "explaining is drawn in exactly the hand the level draws it in.")]
    public Font hudFont;

    public Color backdropColor = new Color(0.02f, 0.03f, 0.06f, 0.72f);
    public Color paperColor = new Color(0.482f, 0.796f, 0.894f, 1f);
    public Color inkColor = new Color(0.078f, 0.412f, 0.541f, 1f);
    public Color accentColor = new Color(0.918f, 0.196f, 0.224f, 1f);
    public Color roadColor = new Color(0.184f, 0.204f, 0.243f, 1f);
    public Color lineColor = new Color(0.94f, 0.97f, 0.99f, 0.9f);

    [Tooltip("The card's size in the 1920x1080 the whole UI is laid out in.")]
    public Vector2 cardSize = new Vector2(1240f, 660f);

    [Header("Timing")]
    [Tooltip("One full pass of the loop: drive up, hit, count up, hold. The player starts whenever they like; " +
             "the loop simply keeps playing until then. The run itself is a fixed share of the cycle (see " +
             "HitPhase), so the loop is what sets how fast the truck comes in.")]
    public float cycleSeconds = 3.6f;
    public float fadeInSeconds = 0.4f;
    public float fadeOutSeconds = 0.3f;

    [Tooltip("How long a card may sit there with nothing pressed before it gets out of the player's way " +
             "regardless. A safety net, not a timer to read by.")]
    public float idleDismissSeconds = 30f;

    [Tooltip("How long the level is left running - held and quiet, with nothing drawn - before the card fades " +
             "in. A beat so the card arrives on a level that has settled rather than on top of its first frame.")]
    public float showDelaySeconds = 1f;

    [Header("The two controls")]
    public string startLabel = "START";
    public string hideLabel = "DON'T SHOW THIS AGAIN";

    // ---------------------------------------------------------------- state

    /// <summary>
    /// Whether a card is up anywhere - or on its way up, which is the same thing to everything outside here.
    /// The pause menu checks this so nothing can open behind the card, and it is the one piece of the card the
    /// rest of the game has to know about. It is set the moment the level starts rather than when the card is
    /// drawn, so the beat before it fades in (see <see cref="showDelaySeconds"/>), when the level is held and
    /// quiet with the card not yet on screen, is covered by it as well.
    /// </summary>
    public static bool isShowing;

    private CanvasGroup group;
    private RectTransform frame;
    private RectTransform stage;
    private CanvasGroup stageGroup;

    private RectTransform burst;
    private CanvasGroup burstGroup;

    // The stage: the real models, filmed somewhere below the world (see TutorialStage3D), and the picture of
    // them the card shows.
    private TutorialStage3D stage3D;
    private RawImage stageImage;

    private RectTransform counterNumberRect;
    private Text counterNumber;
    private RectTransform plusOne;
    private CanvasGroup plusOneGroup;

    private TextMeshProUGUI caption;

    private RectTransform startPlate;
    private RectTransform startRing;
    private RectTransform hideRow;
    private RectTransform hideRing;
    private RectTransform hideTick;

    private GameObject ownedEventSystem;   // only ever the one the card made itself, never the scene's

    private int focus;              // 0 - the start button, 1 - the switch
    private bool hideChecked;
    private bool leaving;
    private bool opening;           // on its way: held and quiet, but nothing drawn yet - see Start
    private bool stageFailed;
    private float shownAt;
    private float restoreTimeScale = 1f;

    private int lastNavX;
    private int lastNavY;

    // Where in the loop the hit lands, which is also how long the run takes: the truck covers the whole of the
    // road in this share of the cycle, so a smaller number is a faster truck. The rest of the cycle is the
    // knock, the counter and the hold before it loops.
    private const float HitPhase = 0.38f;

    // ---------------------------------------------------------------- life

    private void Awake()
    {
        // The scene's own name is the identity unless one is given, so a card moved between levels answers for
        // itself in each of them.
        if (string.IsNullOrEmpty(tutorialName))
            tutorialName = gameObject.scene.name + "." + lesson;
    }

    private void Start()
    {
        if (!alwaysShow && TutorialProgress.IsHidden(tutorialName))
        {
            // Nothing to do and nothing to draw: the level starts as it always would.
            enabled = false;
            return;
        }

        // The level is held and quiet from its own first frame, and the card is given a beat before it comes in.
        // It is an interruption, and one that arrives once the level has settled reads as one, where a card that
        // snapped up with the first frame would look like the rest of the loading.
        //
        // The card counts as up from here rather than from the moment it is drawn, so the beat is not a window
        // in which the level can be paused from under it: <see cref="isShowing"/> is what the pause menu asks.
        isShowing = true;
        opening = true;
        shownAt = Time.unscaledTime;

        HoldTheGame();

        StartCoroutine(Open());
    }

    private System.Collections.IEnumerator Open()
    {
        if (showDelaySeconds > 0f) yield return new WaitForSecondsRealtime(showDelaySeconds);

        // Unity stops a coroutine on the object that owns it, so nothing of this runs if the level was left
        // while the card was on its way - and OnDestroy has already let the game go.
        opening = false;

        Show();
    }

    private void Show()
    {
        if (cardFont == null) cardFont = TMP_Settings.defaultFontAsset;
        if (hudFont == null) hudFont = LegacyFont();

        Build();

        restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

        // The loop and the idle net both start from when the card is actually on screen, not from when the
        // level did.
        shownAt = Time.unscaledTime;

        HoldTheGame();
        StartCoroutine(FadeTo(1f, fadeInSeconds));
    }

    /// <summary>
    /// Keeps the game held for as long as the card is up, exactly the way the pause menu holds it: the clock
    /// stopped and the world quiet.
    ///
    /// The silence matters as much as the freeze. The engine, the road and the level's own soundtrack all go
    /// on playing through a stopped clock, so a card that only stopped time would play over the top of the
    /// level it is interrupting - and it is the pause menu's own trick, <c>AudioListener.pause</c>, with the
    /// card's own two sounds the only things left exempt from it (see <see cref="UiSounds"/>).
    ///
    /// Both are written every frame rather than set once, because the scene's other managers have their own
    /// ideas about the clock and the listener and the order their Start methods run in is not something to rely
    /// on: the pause menu, for one, sets the clock running and unpauses the listener in its own Start, which may
    /// well run after this one. A card that stopped them once and hoped would let the level drive away underneath
    /// it, at full volume.
    /// </summary>
    private void HoldTheGame()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = true;
    }
    private void LateUpdate()
    {
        if (!isShowing) return;

        HoldTheGame();

        if (opening) return;   // nothing drawn yet, and nothing to time out

        // The pointer is shown by the frozen clock (see GameplayCursor), so a click always has somewhere to
        // land. The idle net is there for the one case this cannot cover: input that never arrives at all.
        if (idleDismissSeconds > 0f && Time.unscaledTime - shownAt > idleDismissSeconds)
            Dismiss();
    }

    private void Update()
    {
        if (!isShowing || opening || leaving) return;

        // Read the controls first and draw the demonstration second. The player's way out of the card is
        // therefore taken before anything the stage does, so no fault in the film can ever hold the card up
        // in front of a player who has pressed start.
        ReadInput();

        if (stageFailed) return;

        // The loop runs on the clock the game is not using, because the game's clock is stopped.
        try
        {
            AnimateStage(cycleSeconds > 0.01f ? Mathf.Repeat((Time.unscaledTime - shownAt) / cycleSeconds, 1f) : 0f);
        }
        catch (System.Exception error)
        {
            // A demonstration is never worth a stuck card: if the stage throws, it is put down and said so
            // once, and the card carries on with its caption, its counter and its two controls.
            stageFailed = true;

            Debug.LogWarning("[TutorialIntro] The stage stopped and the card carries on without it: " + error, this);
        }
    }

    private void OnDestroy()
    {
        // The level was left, or the game quit, with the card up - or on its way: the clock and the silence are
        // not ours to keep, and the stage below the world is not worth leaving behind.
        if (isShowing) ReleaseTheGame();

        if (stageImage != null)
        {
            stageImage.texture = null;
            stageImage = null;
        }

        if (stage3D != null)
        {
            stage3D.Dispose();
            stage3D = null;
        }

        ReleaseEventSystem();
    }

    private void ReleaseTheGame()
    {
        isShowing = false;
        opening = false;

        Time.timeScale = restoreTimeScale > 0f ? restoreTimeScale : 1f;

        // The world is let back in. Everything the card quieted - the engine, the level's soundtrack, the rain
        // - picks up exactly where it stopped, because the listener was paused rather than the sources stopped.
        AudioListener.pause = false;

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = false;
    }

    // ---------------------------------------------------------------- input

    /// <summary>
    /// The two controls, on the keyboard and on a pad, with the pointer as a third way in.
    ///
    /// The pad's D-pad moves the highlight and its bottom face button works whatever is under it. The keyboard
    /// has no way to move the highlight - the direction the menus read is the pad's own D-pad - so its enter
    /// and space are not made to follow a highlight it cannot move: they always start the level. That matters
    /// on a machine with a pad plugged in, where a nudge of its D-pad would otherwise have left a keyboard
    /// player's enter switching a box they never selected. Any other button starts the level outright as well,
    /// so a player who does not care to read the card is never stuck looking at it. Nothing here is a way into
    /// a menu: the pause menu refuses to open while a card is up at all.
    /// </summary>
    private void ReadInput()
    {
        GameInput.MenuDirection(out int navX, out int navY);

        bool moved = (navX != 0 || navY != 0) && (navX != lastNavX || navY != lastNavY);

        lastNavX = navX;
        lastNavY = navY;

        if (moved)
        {
            focus = focus == 0 ? 1 : 0;
            ApplyFocus();
            UiSounds.PlayMove();
        }

        if (PointerPressed()) return;

        if (SubmitPressed())
        {
            UiSounds.PlayConfirm();

            if (focus == 0 || KeyboardSubmit()) Dismiss();
            else ToggleHide();

            return;
        }

        if (AnyOtherButtonPressed()) Dismiss();
    }

    /// <summary>
    /// A click on one of the two controls, hit-tested by the card itself against the two plates.
    ///
    /// It is read here rather than through Unity's buttons because a gameplay scene has no event system of its
    /// own, and the one the card makes for its press tints is machinery the card should not depend on: a
    /// control that only works while that machinery is healthy is a control that can strand a player on a card
    /// they cannot close. A click anywhere else - on the card or on the level behind it - is swallowed.
    /// </summary>
    private bool PointerPressed()
    {
        if (!Input.GetMouseButtonDown(0)) return false;

        if (startPlate != null && RectTransformUtility.RectangleContainsScreenPoint(startPlate, Input.mousePosition, null))
        {
            UiSounds.PlayConfirm();
            Dismiss();

            return true;
        }

        if (hideRow != null && RectTransformUtility.RectangleContainsScreenPoint(hideRow, Input.mousePosition, null))
        {
            UiSounds.PlayConfirm();
            ToggleHide();
        }

        return true;
    }

    private static bool SubmitPressed()
    {
        return KeyboardSubmit() || Input.GetKeyDown(GameInput.BoostButton);      // A / cross
    }

    /// <summary>
    /// Enter or space: the keyboard's own way in, which starts the level rather than following the highlight
    /// (see <see cref="ReadInput"/>).
    /// </summary>
    private static bool KeyboardSubmit()
    {
        return Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space);
    }

    /// <summary>
    /// Any other way out, including the pad's B, X and Y and the whole keyboard. The pointer is deliberately not
    /// one of them: a click on the dimmed level around the card should do nothing at all.
    /// </summary>
    private static bool AnyOtherButtonPressed()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) return true;

        for (int button = 1; button <= 3; button++)
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + button))) return true;

        if (Input.GetKeyDown(KeyCode.JoystickButton6)) return true;   // Back / share
        if (Input.GetKeyDown(KeyCode.JoystickButton7)) return true;   // Start / options

        return Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1);
    }

    private void ToggleHide()
    {
        hideChecked = !hideChecked;

        if (hideTick != null) hideTick.gameObject.SetActive(hideChecked);
    }

    private void Dismiss()
    {
        if (leaving) return;

        leaving = true;

        TutorialProgress.SetHidden(tutorialName, hideChecked);

        StartCoroutine(LeaveRoutine());
    }

    private System.Collections.IEnumerator LeaveRoutine()
    {
        yield return FadeTo(0f, fadeOutSeconds);

        ReleaseTheGame();

        if (group != null) group.gameObject.SetActive(false);

        ReleaseEventSystem();

        // The stage goes with the card. Its camera is a live camera with its own render texture, and left
        // standing it would go on filming the models the card was showing, every frame, for the rest of the
        // level - work nobody sees and the level pays for.
        if (stageImage != null)
        {
            stageImage.texture = null;
            stageImage = null;
        }

        if (stage3D != null)
        {
            stage3D.Dispose();
            stage3D = null;
        }
    }

    private System.Collections.IEnumerator FadeTo(float target, float duration)
    {
        if (group == null) yield break;

        float startAlpha = group.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, target, Mathf.Clamp01(time / Mathf.Max(0.0001f, duration)));
            yield return null;
        }

        group.alpha = target;
    }

    /// <summary>
    /// The highlight, written into the card's own colours: the control under it gets an accent ring and a touch
    /// of scale, and nothing else moves.
    /// </summary>
    private void ApplyFocus()
    {
        if (startRing != null) startRing.gameObject.SetActive(focus == 0);
        if (hideRing != null) hideRing.gameObject.SetActive(focus == 1);

        if (startPlate != null) startPlate.localScale = focus == 0 ? Vector3.one * 1.04f : Vector3.one;
    }

    // ---------------------------------------------------------------- the loop

    /// <summary>
    /// One frame of the demonstration, at <paramref name="t"/> through the cycle (0 to 1).
    ///
    /// The beats: the truck runs the road down (from the top of the loop to <see cref="HitPhase"/>), the hit
    /// lands there and the counter ticks up and pops while the target is thrown off and a +1 lifts off the
    /// counter, and then it all holds so the counter can be read before the loop starts again under a short
    /// fade over the seam.
    /// </summary>
    private void AnimateStage(float t)
    {
        bool hit = t >= HitPhase;
        float sinceHit = hit ? (t - HitPhase) : 0f;

        // The whole stage dips out and back over the seam, so the loop's reset is a fade rather than a jump.
        if (stageGroup != null)
        {
            float alpha = 1f;

            if (t > 0.92f) alpha = 1f - (t - 0.92f) / 0.08f;
            else if (t < 0.05f) alpha = t / 0.05f;

            stageGroup.alpha = Mathf.Clamp01(alpha);
        }

        // ---- the stage itself: the game's own truck running down the game's own road at one of the game's own
        // targets, and knocking it off its feet
        if (stage3D != null) stage3D.Animate(t, HitPhase);

        // ---- the burst of the hit, put where the hit actually is: the point of the struck target is projected
        // through the stage's own camera onto the card, so the flash lands on it wherever the stage has framed it
        if (burst != null)
        {
            burst.anchoredPosition = ProjectedHit();

            float burstLife = hit ? Mathf.Clamp01(sinceHit / 0.34f) : 0f;

            if (burstGroup != null) burstGroup.alpha = hit ? 1f - burstLife : 0f;

            burst.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.3f, burstLife);
            burst.localRotation = Quaternion.Euler(0f, 0f, burstLife * 55f);
        }

        // ---- the counter: the number ticks up at the hit and pops, in the accent colour for a moment so the
        // tick is seen and not merely read
        if (counterNumber != null)
        {
            counterNumber.text = (hit ? startCount + 1 : startCount).ToString();

            counterNumber.color = hit
                ? Color.Lerp(accentColor, inkColor, Mathf.Clamp01(sinceHit / 0.45f))
                : inkColor;
        }

        if (counterNumberRect != null)
        {
            float pop = hit ? Mathf.Clamp01(sinceHit / 0.4f) : 0f;
            counterNumberRect.localScale = Vector3.one * (hit ? Mathf.Lerp(1.3f, 1f, pop) : 1f);
        }

        // ---- the +1 lifting off the counter
        if (plusOne != null)
        {
            float life = hit ? Mathf.Clamp01(sinceHit / 0.45f) : 0f;

            if (plusOneGroup != null) plusOneGroup.alpha = hit ? 1f - life : 0f;

            plusOne.anchoredPosition = new Vector2(Layout.NoteCentre - Layout.PictureCentre,
                                                   Layout.StageHeight * 0.5f + 45f + 50f * life);
        }

        // ---- the caption
        if (caption != null)
        {
            bool shouting = hit && sinceHit < 0.32f;

            string wanted = shouting ? hitCaptionText : captionText;

            if (caption.text != wanted) caption.text = wanted;

            caption.color = shouting ? accentColor : inkColor;
        }
    }

    // ---------------------------------------------------------------- building the card

    private void Build()
    {
        // A gameplay level has no event system of its own - its buttons are driven by the game's own navigation
        // (see MenuNavigation) - so if one is wanted it is made here and taken away again when the card goes
        // (see ReleaseEventSystem), leaving the level exactly as it was found.
        if (EventSystem.current == null) ownedEventSystem = CreateEventSystem();

        GameObject canvasGo = new GameObject("Tutorial Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;              // above the HUD and the pause menu alike

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        Transform root = canvasGo.transform;

        // A dim over the level, which is also what stops a click reaching anything behind the card.
        Image backdrop = NewImage("Backdrop", root, backdropColor);
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        // The card: an ink frame with the paper inside it, and a printed rule just inside that.
        frame = NewRect("Card", root);
        Place(frame, Vector2.zero, cardSize + new Vector2(10f, 10f));

        Image frameEdge = NewImage("Frame", frame, inkColor);
        Stretch(frameEdge.rectTransform);

        Image paper = NewImage("Paper", frame, paperColor);
        RectTransform paperRect = paper.rectTransform;
        Stretch(paperRect);
        paperRect.offsetMin = new Vector2(5f, 5f);
        paperRect.offsetMax = new Vector2(-5f, -5f);

        RectTransform rule = Inset(NewRect("Rule", frame), 18f);
        AddBorder(rule, new Color(inkColor.r, inkColor.g, inkColor.b, 0.35f), 3f);

        // Title.
        TextMeshProUGUI title = NewCardLabel("Title", frame, titleText, 64f, inkColor, TextAlignmentOptions.Center, true);
        Place(title.rectTransform, new Vector2(0f, Layout.TitleRow), new Vector2(cardSize.x - 120f, 84f));

        // Caption, under the picture it describes rather than under the whole card.
        caption = NewCardLabel("Caption", frame, captionText, 34f, inkColor, TextAlignmentOptions.Center, true);
        Place(caption.rectTransform, new Vector2(Layout.PictureCentre, Layout.CaptionRow),
              new Vector2(Layout.PictureWidth, 52f));

        BuildStage();
        BuildControls();

        ApplyFocus();
    }

    /// <summary>
    /// The little scene the card plays in: a truck running down a road at a target, built on its own stage
    /// and filmed into the card (see <see cref="TutorialStage3D"/>), with the counter note beside it and the
    /// burst of the hit drawn over it.
    ///
    /// The picture is transparent wherever the models are not, so the road, the truck and the targets sit on the
    /// card's paper rather than inside a window cut into it.
    ///
    /// It takes the card's left column - see <see cref="Layout"/> for the grid the whole card is laid out on -
    /// and the counter note takes the column beside it.
    /// </summary>
    private void BuildStage()
    {
        stage = NewRect("Stage", frame);
        Place(stage, new Vector2(Layout.PictureCentre, Layout.MiddleRow),
              new Vector2(Layout.PictureWidth, Layout.StageHeight));

        stageGroup = stage.gameObject.AddComponent<CanvasGroup>();

        // One target, however many the level is asking for: the stage is a picture of the road rather than a
        // reconstruction of the level, and what it has to show is a truck running one of them down. The count
        // the level wants is the counter's job, in the column beside the picture.
        const int shown = 1;

        stage3D = new TutorialStage3D(shown, new Vector2(Layout.PictureWidth, Layout.StageHeight), StagePalette(),
                                     truckModel, targetModel);
        stage3D.Build();

        stageImage = NewRect("Stage picture", stage).gameObject.AddComponent<RawImage>();
        stageImage.texture = stage3D.Texture;
        stageImage.raycastTarget = false;
        Stretch(stageImage.rectTransform);

        BuildBurst();
        BuildCounter();
    }

    /// <summary>
    /// The card's colours, handed to the stage so the truck, the targets and the road it builds are drawn in the
    /// same hand as the card they are shown on.
    /// </summary>
    private TutorialStage3D.Palette StagePalette()
    {
        TutorialStage3D.Palette palette = new TutorialStage3D.Palette();

        palette.road = roadColor;
        palette.line = lineColor;
        palette.paper = paperColor;
        palette.ink = inkColor;
        palette.accent = accentColor;

        return palette;
    }

    /// <summary>
    /// The burst of the hit: a ring of spokes, thrown out and gone in a third of a second. It is drawn over the
    /// stage rather than in it, and is carried onto the hit every frame (see <see cref="ProjectedHit"/>), so it
    /// always lands on the target the stage actually struck.
    /// </summary>
    private void BuildBurst()
    {
        burst = NewRect("Burst", stage);
        Place(burst, Vector2.zero, new Vector2(120f, 120f));

        burstGroup = burst.gameObject.AddComponent<CanvasGroup>();
        burstGroup.alpha = 0f;

        for (int i = 0; i < 8; i++)
        {
            Image spoke = NewImage("Spoke", burst, i % 2 == 0 ? accentColor : inkColor);
            Place(spoke.rectTransform, Vector2.zero, new Vector2(7f, 58f));
            spoke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
        }

        Image centre = NewImage("Centre", burst, accentColor);
        Place(centre.rectTransform, Vector2.zero, new Vector2(34f, 34f));
    }

    /// <summary>
    /// Where the struck target is on the card, in the stage rect's own coordinates: its point in the stage is put
    /// through the stage's camera, so the flash follows the target wherever the camera has framed it rather than
    /// being pinned to a spot on the card. Without a stage there is nothing to follow, and the middle of the
    /// stage is where the burst is left.
    /// </summary>
    private Vector2 ProjectedHit()
    {
        if (stage3D == null || stage3D.Camera == null || stage == null) return Vector2.zero;

        Vector3 viewport = stage3D.Camera.WorldToViewportPoint(stage3D.HitPoint);

        return new Vector2((viewport.x - 0.5f) * stage.sizeDelta.x, (viewport.y - 0.5f) * stage.sizeDelta.y);
    }

    /// <summary>
    /// The counter, drawn the way the level's own is: the count large in the middle, the number being asked for
    /// beside it, on a paper note.
    /// </summary>
    private void BuildCounter()
    {
        RectTransform note = NewRect("Counter", stage);
        Place(note, new Vector2(Layout.NoteCentre - Layout.PictureCentre, 0f),
              new Vector2(Layout.NoteWidth, Layout.StageHeight));

        Image noteEdge = NewImage("Edge", note, inkColor);
        Stretch(noteEdge.rectTransform);

        Image notePaper = NewImage("Paper", note, paperColor);
        RectTransform notePaperRect = notePaper.rectTransform;
        Stretch(notePaperRect);
        notePaperRect.offsetMin = new Vector2(6f, 6f);
        notePaperRect.offsetMax = new Vector2(-6f, -6f);

        RectTransform noteRule = Inset(NewRect("Rule", note), 22f);
        AddBorder(noteRule, Fade(inkColor, 0.3f), 3f);

        Text label = NewHudLabel("Label", note, "TARGET", 30, inkColor, TextAnchor.UpperCenter, false);
        Place(label.rectTransform, new Vector2(0f, 118f), new Vector2(300f, 44f));

        Text wanted = NewHudLabel("Wanted", note, targetCount.ToString(), 44, accentColor, TextAnchor.UpperCenter, false);
        Place(wanted.rectTransform, new Vector2(0f, 58f), new Vector2(300f, 56f));

        counterNumberRect = NewRect("Count", note);
        Place(counterNumberRect, new Vector2(0f, -62f), new Vector2(300f, 140f));

        counterNumber = NewHudLabel("Number", counterNumberRect, startCount.ToString(), 120, inkColor, TextAnchor.MiddleCenter, true);
        Stretch(counterNumber.rectTransform);

        // The +1 that lifts off the note at the hit.
        plusOne = NewRect("Plus one", stage);
        Place(plusOne, new Vector2(Layout.NoteCentre - Layout.PictureCentre,
                                  Layout.StageHeight * 0.5f + 45f), new Vector2(220f, 70f));

        plusOneGroup = plusOne.gameObject.AddComponent<CanvasGroup>();
        plusOneGroup.alpha = 0f;

        TextMeshProUGUI plus = NewCardLabel("Text", plusOne, "+1", 56f, accentColor, TextAlignmentOptions.Center, true);
        Stretch(plus.rectTransform);
    }

    /// <summary>The two things the player can do here: start, and never see this card again.</summary>
    private void BuildControls()
    {
        // Start, with the accent ring that shows when it is the one under the highlight.
        // The two controls, on the columns' own edges: start at the left-hand margin, the switch ending at the
        // right-hand one, so they line up with the picture and the count above them.
        float startX = -Layout.InnerHalfWidth + Layout.Edge + 150f;
        float hideX = Layout.InnerHalfWidth - Layout.Edge - 280f;

        startRing = NewRect("Start ring", frame);
        Place(startRing, new Vector2(startX, Layout.ControlsRow), new Vector2(324f, 92f));
        Stretch(NewImage("Ring", startRing, accentColor).rectTransform);

        startPlate = NewPlate("Start", frame, new Vector2(startX, Layout.ControlsRow), new Vector2(300f, 76f), inkColor);

        // The button is here for its press tint and nothing else: the click itself is the card's own hit test
        // (see PointerPressed), which is also what a click works through when the event system is unhappy.
        Button start = startPlate.gameObject.AddComponent<Button>();
        start.targetGraphic = Fill(startPlate);
        start.colors = Pressed();

        TextMeshProUGUI startText = NewCardLabel("Label", startPlate, startLabel, 38f, paperColor, TextAlignmentOptions.Center, true);
        Stretch(startText.rectTransform);

        // The switch: the box and its words are one control, so either can be clicked.
        hideRow = NewPlate("Hide row", frame, new Vector2(hideX, Layout.ControlsRow), new Vector2(560f, 76f), paperColor);

        Button hide = hideRow.gameObject.AddComponent<Button>();
        hide.targetGraphic = Fill(hideRow);
        hide.colors = Pressed();

        hideRing = NewRect("Tick ring", hideRow);
        Place(hideRing, new Vector2(-238f, 0f), new Vector2(60f, 60f));
        Stretch(NewImage("Ring", hideRing, accentColor).rectTransform);

        RectTransform box = NewRect("Box", hideRow);
        Place(box, new Vector2(-238f, 0f), new Vector2(48f, 48f));

        Image boxPaper = NewImage("Paper", box, paperColor);
        Stretch(boxPaper.rectTransform);

        AddBorder(box, inkColor, 3f);

        hideTick = NewRect("Tick", box);
        Place(hideTick, Vector2.zero, new Vector2(34f, 34f));

        Image shortStroke = NewImage("Short", hideTick, inkColor);
        Place(shortStroke.rectTransform, new Vector2(-7f, -3f), new Vector2(7f, 18f));
        shortStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -42f);

        Image longStroke = NewImage("Long", hideTick, inkColor);
        Place(longStroke.rectTransform, new Vector2(4f, 3f), new Vector2(7f, 32f));
        longStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 38f);

        hideTick.gameObject.SetActive(false);

        TextMeshProUGUI hideText = NewCardLabel("Label", hideRow, hideLabel, 30f, inkColor, TextAlignmentOptions.Left, true);
        Place(hideText.rectTransform, new Vector2(52f, 0f), new Vector2(430f, 60f));
    }

    // ---------------------------------------------------------------- helpers

    private static ColorBlock Pressed()
    {
        ColorBlock colours = ColorBlock.defaultColorBlock;

        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colours.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colours.selectedColor = Color.white;
        colours.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        colours.fadeDuration = 0.08f;

        return colours;
    }

    /// <summary>
    /// The colour an element is tinted with as a control: the button system multiplies this into whatever the
    /// graphic already is, so white leaves it exactly as drawn.
    /// </summary>
    private static Color Fade(Color colour, float alpha)
    {
        return new Color(colour.r, colour.g, colour.b, colour.a * alpha);
    }

    /// <summary>
    /// Gives a label the white drop shadow the level's own HUD counters carry, which is what keeps them legible
    /// over whatever they are drawn on.
    /// </summary>
    private static void shadowFor(Text label)
    {
        Shadow shadow = label.gameObject.AddComponent<Shadow>();

        shadow.effectColor = new Color(1f, 1f, 1f, 0.55f);
        shadow.effectDistance = new Vector2(4f, -3f);
        shadow.useGraphicAlpha = true;
    }

    private static GameObject CreateEventSystem()
    {
        return new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>
    /// Gives the scene back the event system it did not have. The level is left exactly as it was found, so a
    /// scene that had none while the card was up has none once it is gone.
    /// </summary>
    private void ReleaseEventSystem()
    {
        if (ownedEventSystem == null) return;

        Destroy(ownedEventSystem);
        ownedEventSystem = null;
    }

    /// <summary>
    /// A font for the HUD-like parts when none is assigned - the built-in one, so the counter still draws
    /// something rather than coming out blank. The card's own text falls back to the project's TMP default.
    /// </summary>
    private static Font LegacyFont()
    {
        Font builtIn = null;

        try { builtIn = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (builtIn != null) return builtIn;

        try { builtIn = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }

        return builtIn;
    }

    private RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        return rect;
    }

    /// <summary>
    /// A flat plate: the shape everything on this card is made of. A single sprite-less Image draws a solid
    /// rectangle, so a border has to be four bars rather than one hollow rectangle - see <see cref="AddBorder"/>.
    /// </summary>
    private RectTransform NewPlate(string name, Transform parent, Vector2 position, Vector2 size, Color fill)
    {
        RectTransform root = NewRect(name, parent);
        Place(root, position, size);

        // The fill is the one graphic on the card that takes raycasts: it is what a button is clicked on.
        // Everything else is told not to, so a click can only ever land on a control.
        Image plate = NewImage("Fill", root, fill);
        plate.raycastTarget = true;
        Stretch(plate.rectTransform);

        return root;
    }

    /// <summary>
    /// Draws an outline around a rectangle, as four bars: a sprite-less Image can only be a solid block, so a
    /// rectangle with the middle left alone is four rectangles and not one.
    /// </summary>
    private void AddBorder(RectTransform target, Color colour, float thickness)
    {
        Bar(target, "Top", colour, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        Bar(target, "Bottom", colour, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        Bar(target, "Left", colour, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        Bar(target, "Right", colour, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));
    }

    private void Bar(Transform parent, string name, Color colour, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        Image bar = NewImage(name, parent, colour);

        RectTransform rect = bar.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;      // the axis the anchors stretch is the one the size leaves at zero
    }

    /// <summary>A rectangle inset from its parent on every side, for a rule or an outline inside one.</summary>
    private static RectTransform Inset(RectTransform rect, float amount)
    {
        Stretch(rect);

        rect.offsetMin = new Vector2(amount, amount);
        rect.offsetMax = new Vector2(-amount, -amount);

        return rect;
    }

    /// <summary>The plate's own background, which is what a Button tints.</summary>
    private static Image Fill(RectTransform plate)
    {
        Transform fill = plate.Find("Fill");

        return fill != null ? fill.GetComponent<Image>() : null;
    }

    private Image NewImage(string name, Transform parent, Color colour)
    {
        Image image = NewRect(name, parent).gameObject.AddComponent<Image>();

        image.color = colour;
        image.raycastTarget = false;

        return image;
    }

    /// <summary>The card's own text: the typewriter face the story scenes are written in.</summary>
    private TextMeshProUGUI NewCardLabel(string name, Transform parent, string text, float size, Color colour, TextAlignmentOptions alignment, bool bold)
    {
        TextMeshProUGUI label = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

        if (cardFont != null) label.font = cardFont;

        label.text = text;
        label.fontSize = size;
        label.color = colour;
        label.alignment = alignment;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        return label;
    }

    /// <summary>
    /// The HUD-like text: the level's own face, with the white drop shadow the level's counters carry, so the
    /// counter on this card is drawn exactly as the one the level puts on screen.
    /// </summary>
    private Text NewHudLabel(string name, Transform parent, string text, int size, Color colour, TextAnchor alignment, bool shadow)
    {
        Text label = NewRect(name, parent).gameObject.AddComponent<Text>();

        label.font = hudFont;
        label.text = text;
        label.fontSize = size;
        label.color = colour;
        label.alignment = alignment;
        label.fontStyle = FontStyle.Bold;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        if (shadow) shadowFor(label);

        return label;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
