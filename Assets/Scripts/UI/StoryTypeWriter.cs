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
    [Min(1f)] public float speedUpMultiplier = 6f;

    [Header("UI Buttons (Optional)")]
    [Tooltip("Advances the story: finishes the page that is being typed, then types the next one.")]
    public Button continueButton;

    [Tooltip("Switches fast typing on and off. This is a separate button from the one that " +
             "skips the story, so neither has to share the other's job.")]
    public Button speedUpButton;

    [Header("Auto Start")]
    [TextArea(3, 10)]
    public string startText;

    private TMP_Text tmp;
    private Coroutine typingCo;

    private bool isTyping;
    private bool speedUpActive;

    private int currentPage = 1;
    private int totalPages = 1;

    void Awake()
    {
        tmp = GetComponent<TMP_Text>();

        tmp.overflowMode = TextOverflowModes.Page;
        tmp.enableWordWrapping = true;

        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (speedUpButton != null) speedUpButton.onClick.AddListener(OnSpeedUpClicked);

        // Usable from the first frame, so the button can also hold the menu highlight while the
        // opening page types itself out.
        SetContinueInteractable(true);
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

        // Stays clickable while the page types: pressing it reveals the rest of the page, so the
        // player is never stuck waiting for the text and the highlight has somewhere to sit.
        SetContinueInteractable(true);
        typingCo = StartCoroutine(TypePageCoroutine(first, last));
    }

    private IEnumerator TypePageCoroutine(int first, int last)
    {
        float baseDelay = 1f / Mathf.Max(1f, charactersPerSecond);

        for (int i = first; i <= last; i++)
        {
            tmp.maxVisibleCharacters = i + 1;

            float mult = speedUpActive ? Mathf.Max(1f, speedUpMultiplier) : 1f;
            yield return new WaitForSeconds(baseDelay / mult);
        }

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
    }

    private void SetContinueInteractable(bool value)
    {
        if (continueButton != null)
            continueButton.interactable = value;
    }
}
