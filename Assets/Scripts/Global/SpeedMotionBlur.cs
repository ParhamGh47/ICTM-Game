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
/// Three things shape it:
///
///  * a near band and a far band. The near band is the tight ramp around the truck - the one that gives the
///    close sense of speed. The far band (<see cref="wideReach"/>) reaches most of the way into the picture,
///    so at speed the whole frame streams rather than a ring around the player.
///  * the camera. Looking steeply down (the "Above" camera mode) there is no sky to leave sharp and nothing
///    to radiate from, so the smear widens to cover the picture, stretches, and cuts the truck itself out by
///    its own projected outline (<see cref="truckMargin"/>). The far band and the cut ease in as the camera
///    tips down, so switching camera never pops.
///  * colour is held (<see cref="holdColour"/>). A blur is an average, and the average of a large area is a
///    colour of its own - which is why a heavy smear over a green level in the rain washes the picture
///    green. Each pixel keeps its own hue and takes only the smeared brightness.
///
/// Every number is exposed, so it can be tuned live in play mode.
/// </summary>
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/Speed Motion Blur")]
public class SpeedMotionBlur : MonoBehaviour
{
    [Header("Speed")]
    [Tooltip("The speed the blur starts at, as a fraction of the truck's own top speed. Below it the picture " +
             "is clean, so crawling through a junction is not a smear.")]
    [Range(0f, 1f)]
    public float startFraction = 0.16f;

    [Tooltip("How far the streak gets at top speed, as a fraction of the screen height. 0.04 is about 50 " +
             "pixels out where the streaks are longest on a 1080p picture - a light smear, which is the idea: " +
             "a little blur over the whole race reads as speed, a heavy one around the player reads as a " +
             "smudge. This and maxMix are the two dials for how much there is overall.")]
    public float maxBlur = 0.04f;

    [Tooltip("The most of any one pixel the smear is ever allowed to take over, however far out it is. " +
             "Under 1 the crisp frame always shows through, so the far reaches of the picture are lightened " +
             "rather than replaced - which is what lets the effect cover most of the screen without " +
             "swallowing it. Raising this blurs harder, and is the first thing to try if it feels too faint.")]
    [Range(0.05f, 1f)]
    public float maxMix = 0.65f;

    [Tooltip("How long the blur takes to follow the speed, in seconds. A little lag stops it flickering " +
             "when the throttle is feathered or the wheels bounce.")]
    public float smoothTime = 0.22f;

    [Tooltip("A multiplier for anything that should feel faster than the speed alone suggests - a boost, an " +
             "impact, a scripted moment. Left at 1 it does nothing.")]
    public float pull = 1f;

    [Header("Reach")]
    [Tooltip("How far the far band reaches into the picture, on top of the tight band around the truck. 0 " +
             "leaves only the ring around the player; 1 means the smear is at its cap from fairly close in. " +
             "This is how even the effect is across the frame - high, and the picture is veiled all over " +
             "rather than smeared at the edges and clean in the middle.")]
    [Range(0f, 1f)]
    public float wideReach = 0.85f;

    [Tooltip("How much longer the streak gets out where the far band is at full strength. The world crosses " +
             "the screen fastest out there, so that is what should trail the most - but a big difference " +
             "between the shortest and the longest streak is what makes the middle of the picture look " +
             "untouched next to the edges. Kept near 1 the smear is much the same length everywhere.")]
    [Range(1f, 3f)]
    public float wideStretch = 1.2f;

    [Tooltip("How much of the smear keeps this pixel's own colour instead of the colour it averaged. A blur " +
             "returns the average colour of a whole area, so a green level under pale rain turns the picture " +
             "green; holding the colour keeps the motion and drops the wash. 0 is the raw average, 1 keeps " +
             "hue and saturation exactly and takes only the smeared brightness.")]
    [Range(0f, 1f)]
    public float holdColour = 0.45f;

    [Tooltip("How much brighter taps are favoured over darker ones, so lights, wet tarmac and rain leave the " +
             "trail rather than whatever happens to be there. The streak is still normalised, so this " +
             "reshapes it instead of brightening it.")]
    [Range(0f, 1f)]
    public float highlightBias = 0.3f;

    [Header("Shape")]
    [Tooltip("The point the blur radiates from, in screen space. A little above the middle is where the road " +
             "goes over the horizon, so that stays the sharpest part of the picture. In the top-down view the " +
             "focus moves to the truck itself.")]
    public Vector2 focus = new Vector2(0.5f, 0.52f);

    [Tooltip("How much of the middle stays sharp, as a fraction of the half-height. Raise it if the truck " +
             "itself is picking up smear.")]
    [Range(0f, 0.8f)]
    public float clearRadius = 0.2f;

    [Tooltip("How sharply the near band ramps in past that. Higher keeps more of the picture clean and then " +
             "streaks harder at the very edge. The ramp is eased at both ends, so lowering this widens the " +
             "band around the truck without putting a visible ring at the edge of the clear disc, and it is " +
             "what carries the effect in towards the middle of the picture.")]
    [Range(0.2f, 4f)]
    public float falloff = 0.85f;

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
    public float steerShift = 0.02f;

    [Tooltip("A touch of darkening at the edges, which keeps the eye on the road. It follows the blur, so a " +
             "slow lap is untouched.")]
    [Range(0f, 0.3f)]
    public float edgeDarken = 0.15f;

    [Header("Top-Down (Above) Camera")]
    [Tooltip("How far the camera has to tip from straight down before this counts as a normal chase view. " +
             "Between the two angles the top-down shaping fades in, so switching camera never pops.")]
    [Range(0f, 90f)]
    public float topDownFrom = 48f;

    [Tooltip("Looking down by less than this and it is fully a top-down view; more than it and it is fully a " +
             "chase view. The level's Above camera looks about 40 degrees down, and the chase cameras about " +
             "75.")]
    [Range(0f, 90f)]
    public float topDownTo = 68f;

    [Tooltip("How long the top-down shaping takes to settle after a camera switch. The camera itself cuts, so " +
             "this is what keeps the blur from cutting too.")]
    public float modeSmoothTime = 0.3f;

    [Tooltip("How much longer the streaks are in the top-down view, where the whole frame is moving. Kept " +
             "low: from above the picture should be veiled all over rather than streaked hard.")]
    [Range(1f, 3f)]
    public float topDownBlur = 1.15f;

    [Tooltip("How far the smear reaches in the top-down view, where there is no sky to leave sharp and the " +
             "frame wants covering evenly.")]
    [Range(0f, 1f)]
    public float topDownWide = 0.95f;

    [Tooltip("The clear disc around the truck in the top-down view, as a fraction of the usual one. It can be " +
             "small there because the truck is cut out of the smear by its own outline instead, which leaves " +
             "the effect covering the picture right up to the truck.")]
    [Range(0f, 1f)]
    public float topDownClear = 0.25f;

    [Tooltip("How much room to leave around the truck's outline in the top-down view, as a fraction of the " +
             "truck's own size on screen. At 0 the cut hugs the model and can nibble its edges.")]
    [Range(0f, 1f)]
    public float truckMargin = 0.12f;

    [Tooltip("How softly the truck's outline fades into the smear. Relative to the truck's size on screen, " +
             "so it stays right as the camera moves in and out.")]
    [Range(0f, 1f)]
    public float truckFeather = 0.18f;

    [Header("Quality")]
    [Tooltip("How many taps make up the streak. The taps read a half-resolution copy of the frame, which is " +
             "what keeps a long streak smooth instead of ghosted, so this can stay low.")]
    [Range(2, 24)]
    public int samples = 12;

    [Tooltip("Build the streak from a half-resolution copy of the frame. This is the smoothing: without it a " +
             "long streak shows the taps as separate copies of the picture.")]
    public bool soften = true;

    [Tooltip("Diagnostic: wash the streaked part of the picture red, so you can see exactly where the blur " +
             "is and how strong it is, standing still or at speed. Turn it off for racing.")]
    public bool debugTint;

    private const string ShaderName = "Hidden/Freebuff/SpeedMotionBlur";
    private const string ShaderResourcePath = "Shaders/SpeedMotionBlur";

    private Camera cam;
    private CarController car;

    // Only the truck's own meshes: its exhaust is a ParticleSystem and its tire marks are a TrailRenderer,
    // both of which trail a long way behind it and would drag the outline out into the road.
    private Renderer[] truckRenderers;

    private Material material;

    // 0 - 1, how much of the blur is asked for right now: eased towards the speed so it never snaps.
    private float intensity;

    // 0 - 1, how much of a top-down view the camera is giving at the moment. This is the switch between the
    // chase shaping and the top-down shaping, and it is eased because the camera cuts between the two.
    private float topDown;

    // The focus the shader is using, eased: the speed and the steering both move it about.
    private Vector2 focusNow;

    // The truck's place on screen, refreshed each frame while we are in a top-down view.
    private Vector2 truckCentre;
    private Vector4 truckRect;
    private bool truckOnScreen;

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
        return CameraView.Gameplay();
    }

    // ---------------------------------------------------------------- lifecycle

    private void Awake()
    {
        cam = GetComponent<Camera>();
        focusNow = focus;

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
        // The truck can be spawned after the scene loads (a level that builds its player, a respawn), so the
        // effect keeps looking rather than giving up at startup - but only a few times a second.
        if (car == null && Time.unscaledTime >= nextCarSearch)
        {
            nextCarSearch = Time.unscaledTime + 0.5f;
            car = FindObjectOfType<CarController>();

            if (car != null) truckRenderers = car.GetComponentsInChildren<Renderer>(true);
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

        float follow = CameraView.FollowFraction(smoothTime);

        intensity = Mathf.Lerp(intensity, wanted, follow);

        if (intensity < 0.0005f) intensity = 0f;

        UpdateCameraShape();

        // The focus follows the steering - and, in the top-down view, the truck - so it is eased as a whole
        // rather than only the steering part of it.
        Vector2 wantedFocus = focus;

        if (truckOnScreen && topDown > 0.001f)
        {
            Vector2 onTruck = new Vector2(Mathf.Clamp(truckCentre.x, 0.12f, 0.88f),
                                          Mathf.Clamp(truckCentre.y, 0.08f, 0.92f));

            wantedFocus = Vector2.Lerp(wantedFocus, onTruck, topDown);
        }

        wantedFocus.x = Mathf.Clamp01(wantedFocus.x - wantedSteer * steerShift);

        // Slower than the throttle: the eye swings a little behind the wheels.
        focusNow = Vector2.Lerp(focusNow, wantedFocus, follow * 0.6f);
    }

    /// <summary>
    /// How much of a top-down view the camera is giving, and where the truck sits on screen because of it.
    ///
    /// This reads the camera's own angle (see <see cref="CameraView.LookingDown"/>) rather than asking the
    /// CameraController which mode it is in: what the effect cares about is whether the frame is a horizon
    /// view or a view from above, the Above camera is about 40 degrees off straight down against the chase
    /// cameras' 75, and a camera blend between the two then passes through the shaping rather than jumping it.
    /// </summary>
    private void UpdateCameraShape()
    {
        float wanted = CameraView.LookingDown(cam, topDownFrom, topDownTo);

        topDown = CameraView.Follow(topDown, wanted, modeSmoothTime);

        truckOnScreen = false;

        // The outline is only needed where it is used, and it costs a bounds union and eight projections.
        if (topDown < 0.01f || car == null) return;

        Vector2 min, max;

        if (!ProjectTruck(out min, out max)) return;

        float aspect = cam != null && cam.pixelHeight > 0
            ? (float)cam.pixelWidth / cam.pixelHeight
            : 1f;

        Vector2 centre = (min + max) * 0.5f;
        Vector2 size = (max - min) * (1f + Mathf.Max(0f, truckMargin)) * 0.5f;

        truckCentre = centre;
        truckOnScreen = true;

        // Half extents, with the x measured in screen shape like the shader's radius is, and capped so a
        // camera that has ended up inside the truck cannot cut the whole picture out of the smear.
        truckRect = new Vector4(centre.x * aspect,
                                centre.y,
                                Mathf.Min(size.x * aspect, 0.4f),
                                Mathf.Min(size.y, 0.4f));
    }

    /// <summary>
    /// The truck's own outline, as viewport space min and max. False if it cannot be worked out - no meshes, or
    /// the camera is level with it and part of the box is behind the lens, where there is no outline to speak
    /// of.
    /// </summary>
    private bool ProjectTruck(out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;

        if (cam == null || truckRenderers == null || truckRenderers.Length == 0) return false;

        Bounds bounds = new Bounds();
        bool any = false;

        for (int i = 0; i < truckRenderers.Length; i++)
        {
            Renderer r = truckRenderers[i];

            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

            // Particles and trails belong to the truck but trail far behind it; the outline is the body.
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            if (any) bounds.Encapsulate(r.bounds);
            else
            {
                bounds = r.bounds;
                any = true;
            }
        }

        if (!any) return false;

        Vector3 centre = bounds.center;
        Vector3 extents = bounds.extents;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 at = centre + new Vector3((corner & 1) == 0 ? -extents.x : extents.x,
                                              (corner & 2) == 0 ? -extents.y : extents.y,
                                              (corner & 4) == 0 ? -extents.z : extents.z);

            Vector3 viewport = cam.WorldToViewportPoint(at);

            if (viewport.z <= 0.01f) return false;

            if (viewport.x < minX) minX = viewport.x;
            if (viewport.x > maxX) maxX = viewport.x;
            if (viewport.y < minY) minY = viewport.y;
            if (viewport.y > maxY) maxY = viewport.y;
        }

        min = new Vector2(minX, minY);
        max = new Vector2(maxX, maxY);

        return true;
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

        // Looking down on the race, everything in the picture is travelling: the streaks are longer, the far
        // band covers the frame rather than hugging the truck, the clear disc can shrink (the truck is cut
        // out of the smear by its own outline instead) and the sides no longer lead the top and bottom,
        // because from above there is no sky and no road ahead - it is all ground rushing past.
        float amount = maxBlur * force * Mathf.Lerp(1f, topDownBlur, topDown);
        float clear = clearRadius * Mathf.Lerp(1f, topDownClear, topDown);
        float wide = Mathf.Lerp(wideReach, Mathf.Max(wideReach, topDownWide), topDown);
        float sides = Mathf.Lerp(Mathf.Max(1f, sideBoost), 1f, topDown);

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

        float feather = truckOnScreen
            ? Mathf.Clamp(Mathf.Max(truckRect.z, truckRect.w) * truckFeather, 0.01f, 0.25f)
            : 0.01f;

        material.SetVector("_MainTex_TexelSize",
                           new Vector4(1f / Mathf.Max(1, taps.width), 1f / Mathf.Max(1, taps.height),
                                       taps.width, taps.height));
        material.SetTexture("_SourceTex", source);
        material.SetFloat("_Blur", amount);
        material.SetFloat("_Wide", Mathf.Clamp01(wide));
        material.SetFloat("_Stretch", Mathf.Max(1f, wideStretch));
        material.SetVector("_Focus", new Vector4(focusNow.x, focusNow.y, 0f, 0f));
        material.SetFloat("_Clear", Mathf.Clamp(clear, 0f, 0.79f));
        material.SetFloat("_Falloff", falloff);
        material.SetFloat("_SideBoost", sides);
        material.SetFloat("_Outward", Mathf.Clamp(outward, 0f, 0.49f));
        material.SetFloat("_Darken", edgeDarken * force);
        material.SetFloat("_Samples", Mathf.Clamp(samples, 2, 24));
        material.SetFloat("_Mask", force);
        material.SetFloat("_MaxMix", Mathf.Clamp(maxMix, 0.05f, 1f));
        material.SetFloat("_Tint", debugTint ? 1f : 0f);
        material.SetFloat("_HoldColour", Mathf.Clamp01(holdColour));
        material.SetFloat("_Highlight", Mathf.Clamp01(highlightBias));
        material.SetVector("_TruckRect", truckRect);
        material.SetFloat("_TruckHole", truckOnScreen ? topDown : 0f);
        material.SetFloat("_TruckEdge", feather);

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
