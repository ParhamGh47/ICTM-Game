using Cinemachine;
using UnityEngine;

/// <summary>
/// How the race is being viewed, for the effects that should look different from above than from behind.
///
/// The project's Above camera mode looks about 40 degrees off straight down and the chase cameras about 75,
/// and that difference is the whole reason this exists: a view from above has no sky to leave sharp, no
/// vanishing point to radiate from and no road ahead - it is all ground rushing past. The speed blur widens
/// and the speed trails spread when the camera tips, and both decide that from here.
///
/// Reading the camera's own angle rather than asking <see cref="CameraController"/> which mode it is in keeps
/// the effects independent of the camera rig, means a blend between cameras passes through the change instead
/// of jumping it, and keeps working if the mode list is ever rearranged.
/// </summary>
public static class CameraView
{
    /// <summary>
    /// The camera that draws the race. The level's Main Camera is tagged, and if that is missing the camera a
    /// Cinemachine brain is driving is the next best answer - the virtual cameras are not the ones rendering.
    /// </summary>
    public static Camera Gameplay()
    {
        Camera main = Camera.main;
        if (main != null) return main;

        CinemachineBrain brain = Object.FindObjectOfType<CinemachineBrain>();
        if (brain != null) return brain.OutputCamera;

        return null;
    }

    /// <summary>
    /// 1 when the camera is looking straight down on the race and 0 when it is an ordinary view down the road,
    /// with the ramp between the two across lookingDownFrom..lookingDownTo degrees away from straight down.
    /// </summary>
    public static float LookingDown(Camera camera, float lookingDownFrom, float lookingDownTo)
    {
        if (camera == null) return 0f;

        float fromDown = Vector3.Angle(camera.transform.forward, Vector3.down);

        return 1f - Mathf.InverseLerp(lookingDownFrom, lookingDownTo, fromDown);
    }

    /// <summary>
    /// How much of the way towards a target a value should move this frame, to arrive in roughly
    /// <paramref name="seconds"/>. Unscaled, so an effect keeps following the camera while the game is paused.
    /// </summary>
    public static float FollowFraction(float seconds)
    {
        float delta = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);

        return 1f - Mathf.Exp(-delta / Mathf.Max(0.01f, seconds));
    }

    /// <summary>
    /// <paramref name="current"/> eased towards <paramref name="target"/> over roughly
    /// <paramref name="seconds"/>. Used where a value should settle rather than snap - a camera switch is a
    /// cut, and nothing driven by the view should cut with it.
    /// </summary>
    public static float Follow(float current, float target, float seconds)
    {
        return Mathf.Lerp(current, target, FollowFraction(seconds));
    }
}
