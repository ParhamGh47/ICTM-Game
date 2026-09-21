using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Locks the levels the player has not reached yet, on the level list.
///
/// It installs itself when the level list scene loads, so there is nothing to wire up in the scene, and works
/// purely from <see cref="LevelProgress"/> - the same store the buttons and the finish line use. A locked
/// level gets its button switched off, which does three things at once: it cannot be clicked, the project's
/// menu navigation skips it (it only manages buttons that are interactable, so a gamepad or the keyboard
/// cannot land on it either), and the button's own disabled colour fades the tile. On top of that the level's
/// number is hidden and a padlock is hung in its place, so the list reads as locked rather than as broken.
///
/// The padlock is drawn in code, so the lock needs no art of its own. Assign <see cref="padlockSprite"/> to
/// use your own icon instead.
///
/// Buttons are found by name: <see cref="buttonPrefix"/> plus the level number, so "level1".."level4" for the
/// four levels the project has. A level with no such button is reported rather than passed over, because a
/// level that quietly cannot be locked is worse than one that is obviously missing.
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectLocks : MonoBehaviour
{
    /// <summary>The scene the level list lives in. The installer looks for this name.</summary>
    public const string DefaultSceneName = "Levels";

    [Header("The level list")]
    [Tooltip("Scene to install the locks in. Only one scene is the level list, so this is normally left as it " +
             "is; it is here for when the list is ever renamed or a second one added.")]
    public string sceneName = DefaultSceneName;

    [Tooltip("Buttons are matched by name: this prefix plus the level number, so \"level1\" for level 1.")]
    public string buttonPrefix = "level";

    [Header("Locked look")]
    [Tooltip("Hang a padlock on a locked level, in the space its number leaves.")]
    public bool showPadlock = true;

    [Tooltip("Hide the number of a locked level, so the padlock takes its place rather than sitting over it.")]
    public bool hideNumber = true;

    [Tooltip("Leave empty to use the padlock drawn in code.")]
    public Sprite padlockSprite;

    [Tooltip("Tint for the padlock. The default matches the level numbers, which is what the faded tile needs.")]
    public Color padlockColour = new Color(0.196f, 0.196f, 0.196f, 0.85f);

    [Tooltip("How much of the button the padlock takes up, as a share of its width and height.")]
    [Range(0.1f, 1f)]
    public float padlockSize = 0.45f;

    private class LockState
    {
        public GameObject padlock;
        public GameObject number;
        public bool numberWasActive;
    }

    private readonly Dictionary<Button, LockState> states = new Dictionary<Button, LockState>();

    private static Sprite drawnPadlock;

    // ---------------------------------------------------------------- install

    /// <summary>
    /// Puts a set of locks in the level list whenever that scene loads. Nothing is built in any other scene,
    /// and a list that already has one (put there by hand, with its own settings) is left alone.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        Consider(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Consider(scene);
    }

    private static void Consider(Scene scene)
    {
        if (!scene.IsValid()) return;

        if (!string.Equals(scene.name, DefaultSceneName, System.StringComparison.OrdinalIgnoreCase)) return;

        if (Object.FindObjectOfType<LevelSelectLocks>() != null) return;

        new GameObject("Level Locks").AddComponent<LevelSelectLocks>();
    }

    // ---------------------------------------------------------------- lifecycle

    private void OnEnable()
    {
        // The list can be open when progress changes - finishing a level and coming straight back, or the
        // editor's progress tools being used while the scene plays.
        LevelProgress.Changed += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        LevelProgress.Changed -= Refresh;
    }

    // ---------------------------------------------------------------- the locks

    /// <summary>Brings every level's lock up to date with the saved progress.</summary>
    public void Refresh()
    {
        for (int level = 1; level <= LevelProgress.LevelCount; level++)
        {
            Button button = FindButton(level);

            if (button == null)
            {
                Debug.LogWarning("[LevelProgress] The level list has no button called '" + buttonPrefix + level +
                                 "', so level " + level + " cannot be shown as locked or unlocked.");
                continue;
            }

            Apply(button, level, !LevelProgress.IsUnlocked(level));
        }
    }

    private void Apply(Button button, int level, bool isLocked)
    {
        button.interactable = !isLocked;

        LockState state;

        if (!states.TryGetValue(button, out state))
        {
            state = new LockState();
            state.number = FindNumber(level);
            state.numberWasActive = state.number == null || state.number.activeSelf;

            states[button] = state;
        }

        if (!isLocked)
        {
            // Opened since last time: put the number back and take the padlock off.
            if (state.number != null) state.number.SetActive(state.numberWasActive);

            if (state.padlock != null)
            {
                Destroy(state.padlock);
                state.padlock = null;
            }

            return;
        }

        if (hideNumber && state.number != null) state.number.SetActive(false);

        if (showPadlock && state.padlock == null) state.padlock = BuildPadlock(button);
    }

    /// <summary>The button that opens a level, by the name the list gives it.</summary>
    private Button FindButton(int level)
    {
        string wanted = buttonPrefix + level;

        foreach (Button button in FindObjectsOfType<Button>(true))
        {
            if (button == null) continue;

            if (string.Equals(button.gameObject.name, wanted, System.StringComparison.OrdinalIgnoreCase))
                return button;
        }

        return null;
    }

    /// <summary>
    /// The number shown on a level's tile. The list keeps those as separate labels named after the level, so
    /// that is what this looks for; a level whose number cannot be found still gets its padlock and its button
    /// switched off, it just keeps the number underneath.
    /// </summary>
    private GameObject FindNumber(int level)
    {
        string wanted = level.ToString();

        foreach (Text label in FindObjectsOfType<Text>(true))
        {
            if (label == null) continue;

            if (string.Equals(label.gameObject.name, wanted, System.StringComparison.OrdinalIgnoreCase))
                return label.gameObject;
        }

        return null;
    }

    // ---------------------------------------------------------------- the padlock

    /// <summary>A padlock hung in the middle of a level's tile.</summary>
    private GameObject BuildPadlock(Button button)
    {
        GameObject padlock = new GameObject("Padlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        padlock.transform.SetParent(button.transform, false);

        // Anchored to a share of the button rather than given a size in pixels, so the padlock follows the
        // tile - and the canvas - however large the screen is, and needs no measuring at all.
        float share = Mathf.Clamp(padlockSize, 0.1f, 1f) * 0.5f;

        RectTransform rect = (RectTransform)padlock.transform;
        rect.anchorMin = new Vector2(0.5f - share, 0.5f - share);
        rect.anchorMax = new Vector2(0.5f + share, 0.5f + share);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = padlock.GetComponent<Image>();
        image.sprite = padlockSprite != null ? padlockSprite : DrawnPadlock();
        image.color = padlockColour;
        image.preserveAspect = true;

        // The button is switched off while locked, so nothing here should be catching the raycast either.
        image.raycastTarget = false;

        return padlock;
    }

    /// <summary>
    /// A padlock drawn into a texture once and kept, so the lock needs no art in the project: a rounded body
    /// with the shackle arcing out of its top. White with an antialiased edge, so <see cref="padlockColour"/>
    /// can tint it.
    /// </summary>
    private static Sprite DrawnPadlock()
    {
        if (drawnPadlock != null) return drawnPadlock;

        const int width = 64;
        const int height = 80;

        // The body, and the shackle ring whose lower half is buried in it.
        Vector2 bodyCentre = new Vector2(32f, 20f);
        Vector2 bodyHalf = new Vector2(21f, 15f);
        float corner = 6f;
        Vector2 shackleCentre = new Vector2(32f, 36f);
        float shackleRadius = 14f;
        float shackleThickness = 6f;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Padlock";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 at = new Vector2(x + 0.5f, y + 0.5f);

                float ring = Mathf.Abs((at - shackleCentre).magnitude - shackleRadius) - shackleThickness * 0.5f;

                // A rounded rectangle, as the distance to its edge: negative inside, positive outside.
                Vector2 offset = new Vector2(Mathf.Abs(at.x - bodyCentre.x) - (bodyHalf.x - corner),
                                             Mathf.Abs(at.y - bodyCentre.y) - (bodyHalf.y - corner));

                float body = Mathf.Min(Mathf.Max(offset.x, offset.y), 0f) +
                             new Vector2(Mathf.Max(offset.x, 0f), Mathf.Max(offset.y, 0f)).magnitude - corner;

                float alpha = Mathf.Clamp01(0.5f - Mathf.Min(body, ring));

                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        drawnPadlock = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        drawnPadlock.name = "Padlock";

        return drawnPadlock;
    }
}
