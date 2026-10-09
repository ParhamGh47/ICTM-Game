using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class KillDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Text targetKillText;
    public Text currentKillText;

    [Header("Kill Settings")]
    [Tooltip("The number of targets the level asks for on Easy. Each difficulty has its own value, so a " +
             "level can be made easier or harder on its own rather than as a fixed fraction of Medium; the " +
             "one the player has chosen is what the HUD shows (see GameDifficulty).")]
    public int easyTargetKills = 7;

    [Tooltip("The number of targets the level asks for on Medium - the game as authored.")]
    public int mediumTargetKills = 10;

    [Tooltip("The number of targets the level asks for on Hard.")]
    public int hardTargetKills = 14;

    [Header("Kill Slow Motion")]
    [Tooltip("Whether a kill gives the game a brief slow-motion beat, which is what makes the hit land " +
             "with some weight.")]
    public bool killSlowMotion = true;

    [Tooltip("How slow the beat is, against whatever the clock is already running at. 1 would be no slow " +
             "motion; lower is a heavier hit. It multiplies the current speed rather than replacing it, so " +
             "a kill during the jump cinematic stays slower than the shot itself.")]
    [Range(0.05f, 1f)]
    public float killSlowMotionScale = 0.35f;

    [Tooltip("How long the slow motion is held before it starts easing back, in real seconds.")]
    public float killSlowMotionHold = 0.07f;

    [Tooltip("How long the ease back to full speed takes, in real seconds.")]
    public float killSlowMotionRecover = 0.22f;

    private int currentKills = 0;

    // The requirement at the difficulty the level started with. Read once, in Start, so a difficulty chosen
    // while the level is running cannot change it - it lands on the next level, or on this one when it is
    // restarted.
    private int requiredKills = 0;

    // The kill beat. The clock values are held as fields rather than locals so OnDisable can give the
    // clock back if the level ends while a beat is still running, and so a kill that lands on top of
    // another only extends the one beat instead of dipping an already-dipped clock.
    private Coroutine killSlowMotionRoutine;
    private float killSlowMotionUntil;
    private float killSlowMotionFullScale = 1f;
    private float killSlowMotionDipScale = 1f;
    private float killSlowMotionLastWritten = 1f;

    
    void Start()
    {
        requiredKills =
            GameDifficulty.KillTarget(easyTargetKills, mediumTargetKills, hardTargetKills);

        UpdateDisplay();
    }

    public void IncrementKills()
    {
        currentKills++;
        UpdateDisplay();

        PlayKillSlowMotion();
    }

    private void UpdateDisplay()
    {
        if (targetKillText != null)
            targetKillText.text = requiredKills.ToString();

        if (currentKillText != null)
            currentKillText.text = currentKills.ToString();
    }

    /// <summary>
    /// Whether crossing the line satisfies the level's target, and shows the game over screen if it does not.
    ///
    /// The rule is all-or-nothing: a level that asks for targets is satisfied by hitting none of them (the
    /// player simply drove past them) or by hitting the whole number - or more - but a run that clips a few
    /// and falls short is a failure. Returns true when the game is over so the caller can stop there rather
    /// than entering the end scene on a failed run.
    /// </summary>
    public bool CheckGameOver()
    {
        if (currentKills == 0 || currentKills >= requiredKills)
            return false;

        if (gameOver.Instance != null)
            gameOver.Instance.ShowGameOver();

        return true;
    }

    /// <summary>
    /// The beat a kill plays: the clock dips for a moment and then eases back. Paced on real seconds, so the
    /// beat lasts the same however slow the clock it starts from.
    ///
    /// It is skipped outright while the game is paused, so a kill that lands on a paused frame cannot set
    /// the clock running under the pause menu.
    /// </summary>
    private void PlayKillSlowMotion()
    {
        if (!killSlowMotion)
            return;

        if (PauseTracker.Instance != null && PauseTracker.Instance.isPaused)
            return;

        // Already frozen by something else (the pause or the game over screen): the clock is not ours to
        // take.
        if (Time.timeScale <= 0f)
            return;

        // A kill during the beat extends it instead of starting a fresh dip, so the clock is never dipped
        // from a value a previous dip already lowered.
        killSlowMotionUntil = Time.unscaledTime + Mathf.Max(0f, killSlowMotionHold);

        if (killSlowMotionRoutine != null)
            return;

        killSlowMotionRoutine = StartCoroutine(KillSlowMotionRoutine());
    }

    private IEnumerator KillSlowMotionRoutine()
    {
        // The speed the beat started from. Every recovery eases back to this value rather than to a fixed
        // 1, so a kill during the jump cinematic's own slow motion leaves that slow motion running.
        killSlowMotionFullScale = Time.timeScale;
        killSlowMotionDipScale = Mathf.Max(0.01f, killSlowMotionFullScale * killSlowMotionScale);
        killSlowMotionLastWritten = killSlowMotionDipScale;

        Time.timeScale = killSlowMotionDipScale;

        while (true)
        {
            // The last value written. On every step the clock is checked against it, and if it is no
            // longer ours - the pause menu or the game over screen having taken it, or the jump cinematic
            // having set its own - the beat simply stops, leaving the clock to whoever owns it now.
            if (!Mathf.Approximately(Time.timeScale, killSlowMotionLastWritten))
            {
                killSlowMotionRoutine = null;
                yield break;
            }

            // Held while the beat is due. Written every frame so the value checked against above stays
            // this beat's own.
            if (Time.unscaledTime < killSlowMotionUntil)
            {
                killSlowMotionLastWritten = killSlowMotionDipScale;
                Time.timeScale = killSlowMotionDipScale;

                yield return null;
                continue;
            }

            float elapsed = 0f;

            while (killSlowMotionRecover > 0f && elapsed < killSlowMotionRecover)
            {
                if (!Mathf.Approximately(Time.timeScale, killSlowMotionLastWritten))
                {
                    killSlowMotionRoutine = null;
                    yield break;
                }

                // A kill landed mid-recovery: hold again from the dipped speed rather than easing on.
                if (Time.unscaledTime < killSlowMotionUntil)
                    break;

                elapsed += Time.unscaledDeltaTime;

                killSlowMotionLastWritten =
                    Mathf.Lerp(killSlowMotionDipScale, killSlowMotionFullScale,
                               Mathf.Clamp01(elapsed / killSlowMotionRecover));

                Time.timeScale = killSlowMotionLastWritten;

                yield return null;
            }

            if (Time.unscaledTime < killSlowMotionUntil)
                continue;

            if (Mathf.Approximately(Time.timeScale, killSlowMotionLastWritten))
                Time.timeScale = killSlowMotionFullScale;

            killSlowMotionRoutine = null;
            yield break;
        }
    }

    /// <summary>
    /// Gives the clock back if the level ends while a beat is still running - Time.timeScale outlives a
    /// scene load, so a beat abandoned here would slow the next level down. The clock is only touched
    /// while it is still the value this beat last set, so a pause that took it keeps it.
    /// </summary>
    private void OnDisable()
    {
        if (killSlowMotionRoutine == null)
            return;

        StopCoroutine(killSlowMotionRoutine);
        killSlowMotionRoutine = null;

        if (Mathf.Approximately(Time.timeScale, killSlowMotionLastWritten))
            Time.timeScale = killSlowMotionFullScale;
    }
}
