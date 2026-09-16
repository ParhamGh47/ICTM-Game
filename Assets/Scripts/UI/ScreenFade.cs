using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A full-screen fade, used for the transitions that are too short to warrant the loading screen.
///
/// Moving between the light menu scenes (see <see cref="SceneLoader.InstantScenes"/>) is instant, so
/// the loading screen was deliberately dropped there - but that left those hops cutting from one
/// screen straight to the next with no transition at all. This overlay stands in for it: the screen
/// dips to black, the new scene loads behind it and the black lifts again. Short enough that it never
/// reads as a wait, long enough that the swap has a beat.
///
/// Like <see cref="LoadingScreen"/> the UI is built in code, the object survives scene loads and it
/// is created once for the whole session.
/// </summary>
[DisallowMultipleComponent]
public class ScreenFade : MonoBehaviour
{
    [Header("Look")]
    public Color color = Color.black;

    [Header("Timing (unscaled seconds)")]
    [Tooltip("Length of each half of an instant scene transition: black in, then black out again.")]
    public float fadeDuration = 0.18f;

    /// <summary>The single persistent instance, or null before <see cref="Ensure"/> has been called.</summary>
    public static ScreenFade Instance { get; private set; }

    /// <summary>True while the screen is (or is heading towards being) fully covered.</summary>
    public bool IsOpaque { get { return group != null && group.alpha >= 0.999f; } }

    private Canvas canvas;
    private CanvasGroup group;

    // ---------------------------------------------------------------- bootstrap

    /// <summary>Returns the persistent overlay, creating it on first use.</summary>
    public static ScreenFade Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("Screen Fade");
        return go.AddComponent<ScreenFade>();
    }

    /// <summary>
    /// Draws one invisible frame at startup, so the first transition does not have to pay for
    /// first-time canvas and UI shader work on top of the scene load.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        ScreenFade fade = Ensure();
        fade.StartCoroutine(fade.WarmUpRoutine());
    }

    private IEnumerator WarmUpRoutine()
    {
        canvas.enabled = true;
        yield return null;

        canvas.enabled = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- fading

    /// <summary>Covers the screen. Yields until the fade has finished.</summary>
    public IEnumerator FadeToOpaque()
    {
        return FadeToOpaque(fadeDuration);
    }

    /// <summary>Covers the screen over the given time. Yields until it has finished.</summary>
    public IEnumerator FadeToOpaque(float duration)
    {
        // No fade at all: reveal instantly rather than leaving the screen half covered.
        if (duration <= 0f || group == null)
        {
            SetAlpha(1f);
            yield break;
        }

        canvas.enabled = true;
        group.blocksRaycasts = true;

        yield return FadeRoutine(group.alpha, 1f, duration);
    }

    /// <summary>Uncovers the screen. Yields until the fade has finished.</summary>
    public IEnumerator FadeToClear()
    {
        return FadeToClear(fadeDuration);
    }

    /// <summary>Uncovers the screen over the given time. Yields until it has finished.</summary>
    public IEnumerator FadeToClear(float duration)
    {
        if (duration <= 0f || group == null)
        {
            SetAlpha(0f);
            yield break;
        }

        yield return FadeRoutine(group.alpha, 0f, duration);

        // Nothing left to draw: stop paying for the overlay until the next transition.
        group.blocksRaycasts = false;
        group.alpha = 0f;
        canvas.enabled = false;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    private void SetAlpha(float alpha)
    {
        if (canvas == null) Build();

        canvas.enabled = alpha > 0.001f;
        group.alpha = Mathf.Clamp01(alpha);
        group.blocksRaycasts = group.alpha > 0.001f;
    }

    // ---------------------------------------------------------------- UI construction

    private void Build()
    {
        GameObject canvasGo = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;               // above the loading screen, which sits at 30000

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        canvas.enabled = false;

        GameObject imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)imageGo.transform;
        rect.SetParent(canvasGo.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        Image image = imageGo.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;                // swallows clicks meant for the scene underneath
    }
}
