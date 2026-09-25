using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Text))]
public class StoryTypeWriter : MonoBehaviour
{
    [Header("Typing")]
    [Min(1f)] public float charactersPerSecond = 30f;

    [Tooltip("How much faster the text types while Speed Up is switched on.")]
    [Min(1f)] public float speedUpMultiplier = 8f;

    [Header("UI Buttons (Optional)")]
    [Tooltip("Advances the story: finishes the page that is being typed, then types the next one.")]
    public Button continueButton;

    [Tooltip("Switches fast typing on and off. This is a separate button from the one that " +
             "skips the story, so neither has to share the other's job.")]
    public Button speedUpButton;

    [Tooltip("What the Speed Up button says while fast typing is off. The button carries the mode so the " +
             "player can see which one is on without pressing it. The lettering is set to size itself to the " +
             "plate, so a longer label wraps under the name instead of running off the cardboard.")]
    public string speedUpOffLabel = "Speed-up: OFF";

    [Tooltip("What the Speed Up button says while fast typing is on.")]
    public string speedUpOnLabel = "Speed-up: ON";

    [Tooltip("What the Speed Up button's lettering is coloured while fast typing is on. It is set back to " +
             "the colour the scene gave it the rest of the time.")]
    public Color speedUpOnLabelColor = new Color(0.72f, 0.28f, 0.05f, 1f);

    [Header("Typing Sound")]
    [Tooltip("A clack for each character as the page types itself out. The clips are made rather than " +
             "shipped - see TypewriterClip - and belong to the EFFECTS channel, so the sound settings govern " +
             "them like everything else the game plays at the player.")]
    public bool keystrokes = true;

    [Tooltip("How loud a clack is, before the player's own settings.")]
    [Range(0f, 1f)] public float keystrokeVolume = 0.42f;

    [Tooltip("The shortest gap between two clacks. Fast typing reveals several characters per frame, and a " +
             "clack for every one of them would be a buzz: this is what keeps it a typewriter at any speed.")]
    [Min(0f)] public float keystrokeInterval = 0.034f;

    [Tooltip("The same, while Speed Up is on. Shorter, so the typing is heard to be faster - but not as much " +
             "shorter as the text is, or it stops being a keyboard.")]
    [Min(0f)] public float keystrokeIntervalSpeedUp = 0.02f;

    [Tooltip("How far each clack's pitch is allowed to wander. Every key of a real typewriter sounds a " +
             "little different from the last, and the same clack over and over sounds like a machine gun.")]
    [Range(0f, 0.5f)] public float keystrokePitchSpread = 0.1f;

    [Tooltip("How much higher the clacks are while Speed Up is on, so the fast page sounds like the same " +
             "typewriter being used harder.")]
    [Range(0.5f, 2f)] public float keystrokeSpeedUpPitch = 1.09f;

    [Header("Auto Start")]
    [TextArea(3, 10)]
    public string startText;

    private TMP_Text tmp;
    private Coroutine typingCo;

    private bool isTyping;
    private bool speedUpActive;

    private int currentPage = 1;
    private int totalPages = 1;

    // ---- the Speed Up button's own lettering, so the button says which mode it is in
    private TMP_Text tmpLabel;
    private Text legacyLabel;
    private Color offLabelColor;

    // ---- the typing sound
    private AudioSource clacks;
    private float nextClack;
    private int clackedThrough;    // the last character a clack has been played for on this page

    void Awake()
    {
        tmp = GetComponent<TMP_Text>();

        tmp.overflowMode = TextOverflowModes.Page;
        tmp.enableWordWrapping = true;

        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);

        if (speedUpButton != null)
        {
            speedUpButton.onClick.AddListener(OnSpeedUpClicked);

            FindSpeedUpLabel();
        }

        MakeClackSource();

        // Shows which mode the button is in before the player has touched it, so the story never starts in a
        // state the button does not mention.
        ShowSpeedUpState();

        // Usable from the first frame, so the button can also hold the menu highlight while the
        // opening page types itself out.
        SetContinueInteractable(true);
    }

    // ---------------------------------------------------------------- the Speed Up button's look

    /// <summary>
    /// Finds the lettering on the Speed Up button, wherever the scene put it, and lets it size itself to the
    /// plate - the two labels are close in length but not identical, and the plate's text has to fit both.
    /// </summary>
    private void FindSpeedUpLabel()
    {
        legacyLabel = speedUpButton.GetComponentInChildren<Text>(true);

        if (legacyLabel != null)
        {
            legacyLabel.resizeTextForBestFit = true;
            legacyLabel.resizeTextMinSize = 12;
            legacyLabel.resizeTextMaxSize = legacyLabel.fontSize;

            offLabelColor = legacyLabel.color;
            return;
        }

        tmpLabel = speedUpButton.GetComponentInChildren<TMP_Text>(true);

        if (tmpLabel == null) return;

        tmpLabel.enableAutoSizing = true;
        tmpLabel.fontSizeMin = 12f;
        tmpLabel.fontSizeMax = tmpLabel.fontSize;

        offLabelColor = tmpLabel.color;
    }

    /// <summary>
    /// Puts the current mode on the Speed Up button: its words and its colour.
    ///
    /// This is the button's own lettering rather than a tint of its plate on purpose. ButtonFocusEffect owns the
    /// plate - it brightens it on hover and puts it back afterwards - so anything written there would be undone
    /// the next time the button lost the highlight. The lettering under the plate is not touched by anything else.
    /// </summary>
    private void ShowSpeedUpState()
    {
        string label = speedUpActive ? speedUpOnLabel : speedUpOffLabel;
        Color color = speedUpActive ? speedUpOnLabelColor : offLabelColor;

        if (legacyLabel != null)
        {
            legacyLabel.text = label;
            legacyLabel.color = color;
            return;
        }

        if (tmpLabel != null)
        {
            tmpLabel.text = label;
            tmpLabel.color = color;
        }
    }

    // ---------------------------------------------------------------- the typing sound

    /// <summary>
    /// The source the clacks are played from. It is marked handled, the way <see cref="UiSounds"/> does, because
    /// the player's EFFECTS setting is folded in where the sound is played rather than by anything sweeping over
    /// the sources afterwards - a source has one volume, and two writers would fight over it.
    /// </summary>
    private void MakeClackSource()
    {
        if (!keystrokes) return;

        clacks = GetComponent<AudioSource>();

        if (clacks == null) clacks = gameObject.AddComponent<AudioSource>();

        clacks.playOnAwake = false;
        clacks.loop = false;
        clacks.spatialBlend = 0f;       // the story is on the screen, not somewhere in the scene
        clacks.volume = 1f;

        SoundBus.MarkHandled(clacks);
    }

    /// <summary>
    /// One clack for the characters revealed since the last one, never closer together than the interval above.
    ///
    /// Which of the two sounds it is comes from the character it is being played for: a space or a line break is
    /// the bar at the bottom of the keyboard, anything else is a key.
    /// </summary>
    private void Clack(int shown)
    {
        if (clacks == null || shown <= clackedThrough) return;
        if (Time.time < nextClack) return;

        clackedThrough = shown;
        nextClack = Time.time + (speedUpActive ? keystrokeIntervalSpeedUp : keystrokeInterval);

        var info = tmp.textInfo;
        int index = shown - 1;
        char typed = index >= 0 && index < info.characterCount ? info.characterInfo[index].character : ' ';

        AudioClip clip = typed == ' ' || typed == '\n' || typed == '\t' || typed == '\r'
            ? TypewriterClip.Bar
            : TypewriterClip.Key;

        // Every key a little different, and the whole keyboard a little higher while the story is being rushed.
        float pitch = speedUpActive ? keystrokeSpeedUpPitch : 1f;
        clacks.pitch = Mathf.Clamp(pitch * (1f + Random.Range(-keystrokePitchSpread, keystrokePitchSpread)), 0.5f, 2f);

        float level = keystrokeVolume * Random.Range(0.85f, 1f);
        if (speedUpActive) level *= 0.8f;

        clacks.PlayOneShot(clip, level * SoundSettings.Volume(SoundChannel.Effects));
    }

    void Start()
    {
        if (!string.IsNullOrWhiteSpace(startText))
            SetText(startText);
    }

    public void SetText(string fullText)
    {
        StopTyping();

        tmp.text = fullText ?? "";
        tmp.maxVisibleCharacters = 0;

        tmp.ForceMeshUpdate();

        totalPages = Mathf.Max(1, tmp.textInfo.pageCount);
        currentPage = 1;

        StartTypingPage(currentPage);
    }

    private void StartTypingPage(int page)
    {
        StopTyping();

        tmp.pageToDisplay = page;
        tmp.ForceMeshUpdate();

        currentPage = Mathf.Clamp(page, 1, totalPages);

        var pageInfo = tmp.textInfo.pageInfo[currentPage - 1];
        int first = pageInfo.firstCharacterIndex;
        int last = pageInfo.lastCharacterIndex;

        if (last < first || last < 0)
        {
            tmp.maxVisibleCharacters = tmp.textInfo.characterCount;
            isTyping = false;
            SetContinueInteractable(currentPage < totalPages);
            return;
        }

        tmp.maxVisibleCharacters = first;
        isTyping = true;

        // A page starts with a clack of its own, at once rather than a beat later.
        clackedThrough = first - 1;
        nextClack = 0f;

        // Stays clickable while the page types: pressing it reveals the rest of the page, so the
        // player is never stuck waiting for the text and the highlight has somewhere to sit.
        SetContinueInteractable(true);
        typingCo = StartCoroutine(TypePageCoroutine(first, last));
    }

    private IEnumerator TypePageCoroutine(int first, int last)
    {
        // The reveal is driven by elapsed time rather than by one wait per character. WaitForSeconds can
        // never wait for less than a frame, so the old per-character loop quietly capped the text at about
        // one character per frame - at 30 characters per second the normal delay is already two frames, so
        // a 6x multiplier only bought about 2x in practice and the Speed Up button felt like it barely
        // did anything. Counting revealed characters off the clock removes that ceiling, so the multiplier
        // means what it says and any rate works - and it also keeps the typing honest on a slow machine,
        // where a low frame rate would otherwise stretch the page out.
        float rate = Mathf.Max(1f, charactersPerSecond);
        int remaining = last - first + 1;
        float revealed = 0f;

        while (revealed < remaining)
        {
            // Read every frame, so switching Speed Up on or off takes effect immediately.
            float multiplier = speedUpActive ? Mathf.Max(1f, speedUpMultiplier) : 1f;

            revealed = Mathf.Min(remaining, revealed + rate * multiplier * Time.deltaTime);

            int shown = first + Mathf.CeilToInt(revealed);
            tmp.maxVisibleCharacters = shown;

            Clack(shown);

            yield return null;
        }

        tmp.maxVisibleCharacters = last + 1;

        isTyping = false;
        typingCo = null;

        SetContinueInteractable(currentPage < totalPages);
    }

    private void StopTyping()
    {
        if (typingCo != null)
        {
            StopCoroutine(typingCo);
            typingCo = null;
        }
        isTyping = false;
    }

    public void OnContinueClicked()
    {
        if (tmp == null) return;

        tmp.ForceMeshUpdate();
        totalPages = Mathf.Max(1, tmp.textInfo.pageCount);

        if (isTyping)
        {
            var pageInfo = tmp.textInfo.pageInfo[currentPage - 1];
            int last = pageInfo.lastCharacterIndex;
            tmp.maxVisibleCharacters = Mathf.Max(tmp.maxVisibleCharacters, last + 1);

            StopTyping();
            SetContinueInteractable(currentPage < totalPages);
            return;
        }

        if (currentPage < totalPages)
        {
            StartTypingPage(currentPage + 1);
        }
        else
        {
            SetContinueInteractable(false);
        }
    }

    /// <summary>Switches fast typing on and off. Wired to the Speed Up button.</summary>
    public void OnSpeedUpClicked()
    {
        speedUpActive = !speedUpActive;

        // The button carries the mode, so it has to change the moment it is switched.
        ShowSpeedUpState();
    }

    private void SetContinueInteractable(bool value)
    {
        if (continueButton != null)
            continueButton.interactable = value;
    }
}
