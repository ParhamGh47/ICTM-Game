using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Speed blur for the gameplay camera: a radial smear that grows with how fast the truck is going, strongest
/// out at the sides of the picture and leaving the middle crisp, which is what gives a racing game its sense
/// of speed.
///
/// It is a small image effect - a handful of taps along the line from a focus point outwards (see
/// Resources/Shaders/SpeedMotionBlur.shader) - so it needs no post-processing stack and composes happily
/// with the project's existing bloom: the built-in pipeline runs OnRenderImage after the PostProcessLayer has
/// had its turn.
///
/// How fast, and therefore how much blur, is read straight off the player's <see cref="CarController"/>, so
/// nothing here needs wiring up: <see cref="Bootstrap"/> puts the effect on the level's camera as the scene
/// loads, and only in scenes that actually have a player truck - the menus, the tips and the customize screen
/// pay nothing for it. The HUD is a Screen Space - Overlay canvas, so it is drawn after the camera and stays
/// sharp.
///
/// The look is deliberately gentle: a lap at half throttle is almost clean, and top speed smears the corners
/// by about one percent of the screen. Every number is exposed, so it can be tuned live in play mode.
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/Speed Motion Blur")]
public class SpeedMotionBlur : MonoBehaviour
{
    [Header("Speed")]
    [Tooltip("The speed the blur starts at, as a fraction of the truck's own top speed. Below it the picture " +
             "is clean, so crawling through a junction is not a smear.")]
    [Range(0f, 1f)]
    public float startFraction = 0.18f;

    [Tooltip("How far the sides of the picture streak at top speed, as a fraction of the screen height. " +
             "0.04 is about 55 pixels on a 1080p picture - unmistakable at speed, and gone by the time the " +
             "truck is slow. This is the dial for how much blur there is overall.")]
    public float maxBlur = 0.04f;

    [Tooltip("How long the blur takes to follow the speed, in seconds. A little lag stops it flickering " +
             "when the throttle is feathered or the wheels bounce.")]
    public float smoothTime = 0.18f;

    [Tooltip("A multiplier for anything that should feel faster than the speed alone suggests - a boost, an " +
             "impact, a scripted moment. Left at 1 it does nothing.")]
    public float pull = 1f;

    [Header("Shape")]
    [Tooltip("The point the blur radiates from, in screen space. A little above the middle is where the road " +
             "goes over the horizon, so that stays the sharpest part of the picture.")]
    public Vector2 focus = new Vector2(0.5f, 0.52f);

    [Tooltip("How much of the middle stays sharp, as a fraction of the half-height. Raise it if the truck " +
             "itself is picking up smear.")]
    [Range(0f, 0.8f)]
    public float clearRadius = 0.2f;

    [Tooltip("How sharply the blur ramps in past that. Higher keeps more of the picture clean and then " +
             "streaks harder at the very edge.")]
    [Range(0.2f, 4f)]
    public float falloff = 1.2f;

    [Tooltip("How much more the sides of the picture streak than the top and bottom. This is the part that " +
             "makes it read as speed - 1 is a plain radial blur.")]
    [Range(1f, 2.5f)]
    public float sideBoost = 1.5f;

    [Tooltip("Leans the streaks away from the focus, so they trail past rather than sit over the pixel. 0 " +
             "blurs centred on each pixel and looks still.")]
    [Range(0f, 0.4f)]
    public float outward = 0.22f;

    [Tooltip("Steering drags the blur's centre with it, so a corner swells on the inside and the car feels " +
             "like it is being pushed through the turn. 0 turns it off.")]
    [Range(0f, 0.06f)]
    public float steerShift = 0.018f;

    [Tooltip("A touch of darkening at the edges, which keeps the eye on the road. It follows the blur, so a " +
             "slow lap is untouched.")]
    [Range(0f, 0.3f)]
    public float edgeDarken = 0.1f;

    [Header("Quality")]
    [Tooltip("How many taps make up the streak. The taps read a half-resolution copy of the frame, which is " +
             "what keeps a long streak smooth instead of ghosted, so this can stay low.")]
    [Range(2, 24)]
    public int samples = 8;

    [Tooltip("Build the streak from a half-resolution copy of the frame. This is the smoothing: without it a " +
             "long streak shows the taps as separate copies of the picture.")]
    public bool soften = true;

    [Tooltip("Diagnostic: wash the streaked part of the picture red, so you can see exactly where the blur " +
             "is and how strong it is, standing still or at speed. Turn it off for racing.")]
    public bool debugTint;

    private const string ShaderName = "Hidden/Freebuff/SpeedMotionBlur";
    private const string ShaderResourcePath = "Shaders/SpeedMotionBlur";

    private CarController car;
    private Material material;

    // 0 - 1, how much of the blur is asked for right now: eased towards the speed so it never snaps.
    private float intensity;

    // The steering, eased separately: the eye swings a little slower than the throttle.
    private float steer;

    // Looking for the truck costs a scene search, so a camera with no truck in sight only looks now and
    // then rather than every frame.
    private float nextCarSearch;

    // ---------------------------------------------------------------- bootstrap

    /// <summary>
    /// Puts the blur on the gameplay camera of every level that has a player truck. The camera in a level is a
    /// fresh instance of the CameraManager prefab on each load, which is why this has to happen that often.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        InstallInActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInActiveScene();
    }

    private static void InstallInActiveScene()
    {
        // Only a scene with the player's truck in it has any speed to blur. Menus, tips and the customize
        // screen have no CarController, so they do not even pay for the blit.
        if (Object.FindObjectsOfType<CarController>().Length == 0) return;

        Camera gameplay = FindGameplayCamera();

        if (gameplay == null)
        {
            Debug.LogWarning("[SpeedBlur] There is a player truck in this scene but no Main Camera to put " +
                             "the speed blur on, so there is none. Tag the camera that draws the race.");
            return;
        }

        // A camera that already carries the effect - put there by hand, or on the prefab - is left alone, so
        // hand-tuned values are never overwritten by a default.
        if (gameplay.GetComponent<SpeedMotionBlur>() != null) return;

        gameplay.gameObject.AddComponent<SpeedMotionBlur>();

        // Said out loud because 'the blur is not doing anything' is otherwise impossible to tell apart from
        // 'the blur was never installed'.
        Debug.Log("[SpeedBlur] Speed blur installed on '" + gameplay.name + "'.");
    }

    /// <summary>
    /// The camera that draws the race. The level's Main Camera is tagged, and if that is missing the camera a
    /// Cinemachine brain is driving is the next best answer - the virtual cameras are not the ones rendering.
    /// </summary>
    private static Camera FindGameplayCamera()
    {
        Camera main = Camera.main;
        if (main != null) return main;

        Cinemachine.CinemachineBrain brain = Object.FindObjectOfType<Cinemachine.CinemachineBrain>();
        if (brain != null) return brain.OutputCamera;

        return null;
    }

    // ---------------------------------------------------------------- lifecycle

    private void Awake()
    {
        Shader shader = Resources.Load<Shader>(ShaderResourcePath);

        if (shader == null) shader = Shader.Find(ShaderName);

        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("[SpeedBlur] The speed blur shader could not be loaded from Resources or found " +
                             "by name, so the effect is off.");
            enabled = false;
            return;
        }

        material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);

        // The sceneLoaded subscription belongs to the one-off bootstrap rather than to this instance, so it
        // is deliberately left in place: a camera torn down on the way to the next level must not take the
        // hook that installs the blur on the camera waiting there.
    }

    // ---------------------------------------------------------------- per frame

    private void Update()
    {
        float delta = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

        // The truck can be spawned after the scene loads (a level that builds its player, a respawn), so the
        // effect keeps looking rather than giving up at startup - but only a few times a second.
        if (car == null && Time.unscaledTime >= nextCarSearch)
        {
            nextCarSearch = Time.unscaledTime + 0.5f;
            car = FindObjectOfType<CarController>();
        }

        float wanted = 0f;
        float wantedSteer = 0f;

        if (car != null)
        {
            float top = Mathf.Max(1f, car.topSpeed);
            float speed = Mathf.Clamp01(car.currentSpeedKPH / top);

            // Nothing below the starting fraction, everything at top speed, and eased at both ends of that
            // ramp so the blur arrives gradually rather than switching on.
            wanted = Mathf.Clamp01(Mathf.InverseLerp(startFraction, 1f, speed));
            wanted = wanted * wanted * (3f - 2f * wanted);

            wantedSteer = Mathf.Clamp(car.steerInput, -1f, 1f);
        }

        float follow = 1f - Mathf.Exp(-delta / Mathf.Max(0.01f, smoothTime));

        intensity = Mathf.Lerp(intensity, wanted, follow);
        steer = Mathf.Lerp(steer, wantedSteer, follow * 0.6f);

        if (intensity < 0.0005f) intensity = 0f;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null || maxBlur <= 0f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // How far the blur is pushed: the speed, and then whatever 'pull' says on top of it. Both the length
        // of the streak and how much of the pixel it takes over follow this, so a boost lengthens the smear
        // and lets it take over fully rather than only stretching it.
        float force = Mathf.Clamp01(intensity * Mathf.Max(0f, pull));

        // The tint is there to answer "is this thing on at all", so it has to work standing still.
        if (debugTint) force = Mathf.Max(force, 0.4f);

        if (force <= 0f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        float amount = maxBlur * force;

        // Steering moves where the blur radiates from, so the side being turned into streaks a little harder
        // and the car feels like it is being pushed through the corner.
        Vector2 centre = new Vector2(Mathf.Clamp01(focus.x - steer * steerShift), focus.y);

        // The taps read a half-resolution copy of the frame. Sampling the full-resolution frame at a long
        // range shows each tap as its own copy of the picture - a trail of ghosts - where the softened copy
        // gives one continuous smear, which is the whole difference between this looking cheap and looking
        // like speed. It is also four times cheaper to sample.
        RenderTextureReadWrite space =
            source.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;

        RenderTexture softened = null;
        RenderTexture taps = source;

        if (soften)
        {
            softened = RenderTexture.GetTemporary(Mathf.Max(1, source.width / 2),
                                                  Mathf.Max(1, source.height / 2), 0, source.format, space);
            softened.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, softened);

            taps = softened;
        }

        material.SetVector("_MainTex_TexelSize",
                           new Vector4(1f / Mathf.Max(1, taps.width), 1f / Mathf.Max(1, taps.height),
                                       taps.width, taps.height));
        material.SetTexture("_SourceTex", source);
        material.SetFloat("_Blur", amount);
        material.SetVector("_Focus", new Vector4(centre.x, centre.y, 0f, 0f));
        material.SetFloat("_Clear", clearRadius);
        material.SetFloat("_Falloff", falloff);
        material.SetFloat("_SideBoost", Mathf.Max(1f, sideBoost));
        material.SetFloat("_Outward", Mathf.Clamp(outward, 0f, 0.49f));
        material.SetFloat("_Darken", edgeDarken * force);
        material.SetFloat("_Samples", Mathf.Clamp(samples, 2, 24));
        material.SetFloat("_Mask", force);
        material.SetFloat("_Tint", debugTint ? 1f : 0f);

        Graphics.Blit(taps, destination, material);

        material.SetTexture("_SourceTex", null);

        if (softened != null) RenderTexture.ReleaseTemporary(softened);
    }

    /// <summary>
    /// What the effect is doing right now, 0 - 1. Exposed so anything that wants to react to the picture
    /// smearing - a sound, a HUD element, a camera shake - can follow the blur instead of guessing at the
    /// speed a second time.
    /// </summary>
    public float Intensity
    {
        get { return intensity; }
    }
}
