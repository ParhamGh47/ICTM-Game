using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The single entry point for every scene transition in the game.
///
/// It shows the <see cref="LoadingScreen"/> overlay, loads the scene asynchronously and only then
/// swaps - so no transition ever happens abruptly or without feedback. Use this instead of
/// <c>SceneManager.LoadScene</c> everywhere.
///
/// Transitions between the light menu scenes (<see cref="InstantScenes"/>) skip the overlay entirely:
/// they are tiny, so the loading screen would only be a delay. Anything involving a gameplay scene
/// still gets it. Those menu hops are not left without feedback though - they get the short
/// <see cref="ScreenFade"/> instead, so every transition in the game is smooth.
/// </summary>
public static class SceneLoader
{
    /// <summary>True while a transition (including the overlay's fade out) is in progress.</summary>
    public static bool IsLoading { get; private set; }

    /// <summary>
    /// Scenes that are pure menus. Moving directly between two of these - menu, level list, controls,
    /// customization, and back again - happens without the loading screen, because they are tiny and
    /// instant. (The customize scene is one of them even though it builds a 3D preview: it holds no level
    /// content, and the truck is a single object.)
    /// A transition from a level into one of these still shows it, since unloading a level does not
    /// happen instantly. Add or remove scene names here to change the rule.
    /// </summary>
    public static readonly HashSet<string> InstantScenes = new HashSet<string>(System.StringComparer.Ordinal)
    {
        "Menu",
        "Levels",
        "Option",
        "Credits",
        "Tips",
        "Customize",
    };

    // Safety nets: never leave the player staring at the overlay forever.
    private const float LoadTimeout = 30f;
    private const float ActivationTimeout = 30f;

    /// <summary>Loads a scene by name (must be in Build Settings).</summary>
    public static void Load(string sceneName)
    {
        Begin(sceneName, -1, null);
    }

    /// <summary>
    /// Loads a scene by name, forcing the loading screen on (<paramref name="showLoadingScreen"/> true)
    /// or off (false), regardless of the <see cref="InstantScenes"/> rule.
    /// </summary>
    public static void Load(string sceneName, bool showLoadingScreen)
    {
        Begin(sceneName, -1, showLoadingScreen);
    }

    /// <summary>Loads a scene by its index in Build Settings.</summary>
    public static void Load(int buildIndex, bool showLoadingScreen = true)
    {
        Begin(null, buildIndex, showLoadingScreen);
    }

    /// <summary>Reloads the currently active scene.</summary>
    public static void Reload()
    {
        Begin(null, SceneManager.GetActiveScene().buildIndex, true);
    }

    /// <summary>Loads the scene that comes after the active one in Build Settings.</summary>
    public static void LoadNext()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("[SceneLoader] There is no scene after the active one in Build Settings.");
            return;
        }

        Begin(null, next, true);
    }

    private static void Begin(string sceneName, int buildIndex, bool? showLoadingScreen)
    {
        if (IsLoading)
        {
            Debug.LogWarning(string.Format(
                "[SceneLoader] Ignored a request for '{0}' because a transition is already in progress.",
                string.IsNullOrEmpty(sceneName) ? buildIndex.ToString() : sceneName));
            return;
        }

        if (!ValidateTarget(sceneName, buildIndex)) return;

        IsLoading = true;

        bool showScreen = ShouldShowLoadingScreen(sceneName, showLoadingScreen);

        // Both overlays survive scene loads, so the loading screen doubles as a safe host for the
        // transition coroutine.
        LoadingScreen screen = LoadingScreen.Ensure();

        // Even a menu hop - which shows no loading screen at all - gets a transition, so nothing in
        // the game ever cuts straight from one screen to the next.
        ScreenFade fade = ScreenFade.Ensure();

        if (showScreen)
        {
            screen.SetProgress(0f, true);
            screen.Show();
        }

        screen.StartCoroutine(TransitionRoutine(screen, fade, sceneName, buildIndex, showScreen));
    }

    private static bool ShouldShowLoadingScreen(string targetSceneName, bool? overrideValue)
    {
        if (overrideValue.HasValue) return overrideValue.Value;

        // Index-based loads (restart, next scene) always involve the level we are in.
        if (string.IsNullOrEmpty(targetSceneName)) return true;

        // Menu -> menu hops are instant, so they skip the overlay. Leaving a level for the menu is not
        // instant (the level has to be torn down), so that still gets one.
        string currentSceneName = SceneManager.GetActiveScene().name;
        return !(InstantScenes.Contains(targetSceneName) && InstantScenes.Contains(currentSceneName));
    }

    private static bool ValidateTarget(string sceneName, int buildIndex)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName)) return true;

            Debug.LogError(string.Format(
                "[SceneLoader] Scene '{0}' is not in Build Settings (File > Build Settings...), so it cannot be loaded. Transition skipped.",
                sceneName));
            return false;
        }

        if (buildIndex >= 0 && buildIndex < SceneManager.sceneCountInBuildSettings) return true;

        Debug.LogError(string.Format(
            "[SceneLoader] Build index {0} is out of range ({1} scenes in Build Settings). Transition skipped.",
            buildIndex, SceneManager.sceneCountInBuildSettings));
        return false;
    }

    private static IEnumerator TransitionRoutine(LoadingScreen screen, ScreenFade fade, string sceneName, int buildIndex, bool showScreen)
    {
        if (showScreen)
        {
            // Get the overlay onto the screen *before* any loading work starts. The frame that kicks off a
            // load does a large slice of the scene deserialization on the main thread (Core-1..4 are 5-8 MB),
            // so without these two frames the overlay would be enabled but not drawn yet - the player would
            // watch the old scene freeze and only then see the loading screen appear, already at 90%.
            yield return null;
            yield return null;
        }
        else
        {
            // A menu hop: no loading screen to show, so the fade *is* the transition. It has to be seen
            // before the new scene appears, which is why the load starts only once it is opaque.
            yield return fade.FadeToOpaque();
        }

        AsyncOperation operation = StartLoad(sceneName, buildIndex);

        if (operation == null)
        {
            // Last resort: let Unity load the old way rather than leaving the player stuck.
            IsLoading = false;
            if (showScreen) screen.Release();

            if (string.IsNullOrEmpty(sceneName)) SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
            else SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

            if (!showScreen) yield return fade.FadeToClear();
            yield break;
        }

        // Without an overlay there is nothing to fill, so let Unity activate the scene the moment it is
        // ready instead of holding it back at 90%. Menu hops stay as fast as a plain scene load.
        if (!showScreen)
        {
            float plainStartedAt = Time.realtimeSinceStartup;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - plainStartedAt > ActivationTimeout)
                {
                    Debug.LogWarning("[SceneLoader] Scene activation is taking longer than expected.");
                    break;
                }
                yield return null;
            }

            // The loading screen would have faded out to reveal the new scene; here the black does it,
            // so the scene is never seen for the first time mid-swap.
            yield return fade.FadeToClear();

            IsLoading = false;
            yield break;
        }

        // Hold the swap back until the bar has something to show.
        operation.allowSceneActivation = false;

        float startedAt = Time.realtimeSinceStartup;

        // Unity reports up to 0.9 while activation is blocked; map that onto the first 90% of the bar.
        while (operation.progress < 0.9f)
        {
            screen.SetProgress(Mathf.Min(operation.progress, 0.9f));
            if (Time.realtimeSinceStartup - startedAt > LoadTimeout)
            {
                Debug.LogWarning("[SceneLoader] The load is taking longer than expected; activating anyway.");
                break;
            }
            yield return null;
        }

        screen.SetProgress(0.9f);

        // Keep the overlay up long enough to read, so it never flashes for a single frame.
        while (Time.realtimeSinceStartup - startedAt < screen.minimumDisplayTime)
        {
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / Mathf.Max(0.0001f, screen.minimumDisplayTime));
            screen.SetProgress(Mathf.Lerp(0.9f, 1f, t));
            yield return null;
        }

        screen.SetProgress(1f);
        operation.allowSceneActivation = true;

        float activationStartedAt = Time.realtimeSinceStartup;
        while (!operation.isDone)
        {
            if (Time.realtimeSinceStartup - activationStartedAt > ActivationTimeout)
            {
                Debug.LogWarning("[SceneLoader] Scene activation is taking longer than expected.");
                break;
            }
            yield return null;
        }

        // Give the new scene a frame to run its Awake/Start before revealing it.
        yield return null;

        screen.Release();
        IsLoading = false;
    }

    private static AsyncOperation StartLoad(string sceneName, int buildIndex)
    {
        try
        {
            return string.IsNullOrEmpty(sceneName)
                ? SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single)
                : SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("[SceneLoader] Could not start the load: " + exception.Message);
            return null;
        }
    }
}
