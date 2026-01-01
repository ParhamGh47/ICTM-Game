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

        // مهم: برای صفحه‌بندی
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

    /// <summary>متن جدید بده (از بیرون هم می‌تونی صدا بزنی)</summary>
    public void SetText(string fullText)
    {
        StopTyping();

        tmp.text = fullText ?? "";
        tmp.maxVisibleCharacters = 0;

        // تولید اطلاعات صفحه‌ها
        tmp.ForceMeshUpdate();

        totalPages = Mathf.Max(1, tmp.textInfo.pageCount);
        currentPage = 1;

        StartTypingPage(currentPage);
    }

    private void StartTypingPage(int page)
    {
        StopTyping();

        // اطمینان از آپدیت شدن pageInfo
        tmp.pageToDisplay = page;
        tmp.ForceMeshUpdate();

        currentPage = Mathf.Clamp(page, 1, totalPages);

        // محدودۀ کاراکترهای همین صفحه
        var pageInfo = tmp.textInfo.pageInfo[currentPage - 1];
        int first = pageInfo.firstCharacterIndex;
        int last = pageInfo.lastCharacterIndex;

        // اگر صفحه خالی بود:
        if (last < first || last < 0)
        {
            tmp.maxVisibleCharacters = tmp.textInfo.characterCount;
            isTyping = false;
            SetContinueInteractable(currentPage < totalPages);
            return;
        }

        // از اول این صفحه شروع کن
        tmp.maxVisibleCharacters = first;
        isTyping = true;

        SetContinueInteractable(false);
        typingCo = StartCoroutine(TypePageCoroutine(first, last));
    }

    private IEnumerator TypePageCoroutine(int first, int last)
    {
        // سرعت امن: صفر نباشه
        float baseDelay = 1f / Mathf.Max(1f, charactersPerSecond);

        // تا آخرین کاراکتر این صفحه تایپ کن
        for (int i = first; i <= last; i++)
        {
            tmp.maxVisibleCharacters = i + 1;

            float mult = skipActive ? Mathf.Max(1f, skipMultiplier) : 1f;
            yield return new WaitForSeconds(baseDelay / mult);
        }

        isTyping = false;
        typingCo = null;

        // وقتی صفحه تموم شد، اگر صفحه بعدی هست Continue فعال شه
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

    /// <summary>Continue: اگر در حال تایپ هست، همین صفحه رو سریع تا آخر نشون بده. اگر تموم شده، برو صفحه بعد.</summary>
    public void OnContinueClicked()
    {
        if (tmp == null) return;

        tmp.ForceMeshUpdate();
        totalPages = Mathf.Max(1, tmp.textInfo.pageCount);

        if (isTyping)
        {
            // سریع صفحه جاری رو کامل کن
            var pageInfo = tmp.textInfo.pageInfo[currentPage - 1];
            int last = pageInfo.lastCharacterIndex;
            tmp.maxVisibleCharacters = Mathf.Max(tmp.maxVisibleCharacters, last + 1);

            StopTyping();
            SetContinueInteractable(currentPage < totalPages);
            return;
        }

        if (currentPage < totalPages)
        {
            // حس "پاک شدن صفحه قبل" با PageToDisplay انجام میشه (صفحه قبلی نمایش داده نمیشه)
            StartTypingPage(currentPage + 1);
        }
        else
        {
            SetContinueInteractable(false);
        }
    }

    /// <summary>Skip: سرعت تایپ رو زیاد/کم می‌کنه (Toggle)</summary>
    public void OnSkipClicked()
    {
        skipActive = !skipActive;

        // اگر دوست داری ظاهر دکمه تغییر کنه، اینجا می‌تونی رنگ/متن رو عوض کنی
        // مثلا: skipButton.GetComponentInChildren<TMP_Text>().text = skipActive ? "SKIP: ON" : "SKIP";
    }

    private void SetContinueInteractable(bool value)
    {
        if (continueButton != null)
            continueButton.interactable = value;
    }
}
