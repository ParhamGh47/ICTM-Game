using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Text))]
public class StoryTypeWriter : MonoBehaviour
{
    [Header("Typing")]
    [Min(1f)] public float charactersPerSecond = 30f;
    [Min(1f)] public float skipMultiplier = 6f;

    [Header("UI Buttons (Optional)")]
    public Button continueButton;
    public Button skipButton;

    [Header("Auto Start")]
    [TextArea(3, 10)]
    public string startText;

    private TMP_Text tmp;
    private Coroutine typingCo;

    private bool isTyping;
    private bool skipActive;

    private int currentPage = 1;
    private int totalPages = 1;

    void Awake()
    {
        tmp = GetComponent<TMP_Text>();

        tmp.overflowMode = TextOverflowModes.Page;
        tmp.enableWordWrapping = true;

        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);

        SetContinueInteractable(false);
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

        SetContinueInteractable(false);
        typingCo = StartCoroutine(TypePageCoroutine(first, last));
    }

    private IEnumerator TypePageCoroutine(int first, int last)
    {
        float baseDelay = 1f / Mathf.Max(1f, charactersPerSecond);

        for (int i = first; i <= last; i++)
        {
            tmp.maxVisibleCharacters = i + 1;

            float mult = skipActive ? Mathf.Max(1f, skipMultiplier) : 1f;
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

    public void OnSkipClicked()
    {
        skipActive = !skipActive;
    }

    private void SetContinueInteractable(bool value)
    {
        if (continueButton != null)
            continueButton.interactable = value;
    }
}
