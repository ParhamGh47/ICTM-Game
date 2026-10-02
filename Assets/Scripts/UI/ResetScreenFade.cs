using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The screen effect for an automatic verge reset: the view darkens from the edges inwards onto a warm tinted
/// cover, holds while the truck is picked up and put back on the road, and opens out again from the middle.
///
/// It is deliberately not <see cref="ScreenFade"/>. That one is the plain black used between the menu and level
/// scenes, where a cut should be invisible. A reset is something that happened to the player - they left the
/// road and the game brought them back - and it earns a beat of its own rather than the same blink the menus
/// use. The shape is what makes it read as its own thing: a vignette that closes in before the flat cover
/// lands, so it looks like the view is being taken away from the edges rather than dimmed from the middle.
///
/// Like <see cref="ScreenFade"/> the UI is built in code, the object survives scene loads, and it is created
/// once for the session. It draws above every other overlay, because nothing should be able to appear over the
/// frame the truck is moved in.
/// </summary>
[DisallowMultipleComponent]
public class ResetScreenFade : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("The flat colour the screen is covered with, and the colour it opens out of. Warm rather than " +
             "pure black, so the reset does not read as the menu blink.")]
    public Color color = new Color(0.07f, 0.025f, 0.035f, 1f);

    [Tooltip("The colour of the vignette that closes in first. A little lighter and warmer than the flat cover, " +
             "so the edges going dark is seen before the screen does.")]
    public Color vignetteColor = new Color(0.17f, 0.06f, 0.075f, 1f);

    [Tooltip("How much of the middle the vignette leaves clear, as a fraction of the half-diagonal. 0 = the " +
             "whole screen darkens evenly, 0.3 = only the middle of the view is still clear.")]
    [Range(0f, 1f)]
    public float vignetteClearRadius = 0.24f;

    /// <summary>The single persistent instance, or null before <see cref="Ensure"/> has been called.</summary>
    public static ResetScreenFade Instance { get; private set; }

    private Canvas canvas;
    private Image vignette;
    private Image cover;

    // ---------------------------------------------------------------- bootstrap

    /// <summary>Returns the persistent overlay, creating it on first use.</summary>
    public static ResetScreenFade Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("Reset Screen Fade");
        return go.AddComponent<ResetScreenFade>();
    }

    /// <summary>Draws one invisible frame at startup, so the first reset does not pay for first-time UI work.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        ResetScreenFade fade = Ensure();
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

    // ---------------------------------------------------------------- the two halves

    /// <summary>
    /// Closes the view down: the vignette first, then the flat cover over it. Yields until the screen is
    /// completely covered, which is when the caller may move the truck unseen.
    /// </summary>
    public IEnumerator Cover(float vignetteTime, float solidTime)
    {
        canvas.enabled = true;
        cover.raycastTarget = true;

        yield return AlphaRoutine(vignette, vignette.color.a, 1f, vignetteTime);
        yield return AlphaRoutine(cover, cover.color.a, 1f, solidTime);
    }

    /// <summary>
    /// Opens the view again: the flat cover lifts first, then the vignette opens out from the middle. Yields
    /// until the screen is clear and stops paying for the overlay until the next reset.
    /// </summary>
    public IEnumerator Reveal(float solidTime, float vignetteTime)
    {
        yield return AlphaRoutine(cover, cover.color.a, 0f, solidTime);
        yield return AlphaRoutine(vignette, vignette.color.a, 0f, vignetteTime);

        cover.raycastTarget = false;
        canvas.enabled = false;
    }

    private static IEnumerator AlphaRoutine(Graphic graphic, float from, float to, float duration)
    {
        Color c = graphic.color;

        if (duration <= 0f)
        {
            c.a = to;
            graphic.color = c;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            graphic.color = c;
            yield return null;
        }

        c.a = to;
        graphic.color = c;
    }

    // ---------------------------------------------------------------- UI construction

    private void Build()
    {
        GameObject canvasGo = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32500;               // above ScreenFade, which sits at 32000

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        vignette = MakeImage(canvasGo.transform, "Vignette", vignetteColor, BuildVignetteSprite());
        cover = MakeImage(canvasGo.transform, "Cover", color, null);

        canvas.enabled = false;
    }

    private static Image MakeImage(Transform parent, string name, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.color = new Color(color.r, color.g, color.b, 0f);

        return image;
    }

    /// <summary>
    /// Builds the vignette: a square texture, transparent in the middle and solid at the edges, so fading it
    /// in darkens the view from the outside in. It is white so the image's own colour can tint it.
    /// </summary>
    private Sprite BuildVignetteSprite()
    {
        const int size = 128;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float clear = Mathf.Clamp01(vignetteClearRadius);
        float corner = Mathf.Sqrt(2f);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;

                float r = Mathf.Sqrt(nx * nx + ny * ny) / corner;    // 0 at the middle, 1 at a corner
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clear, 1f, r));

                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
