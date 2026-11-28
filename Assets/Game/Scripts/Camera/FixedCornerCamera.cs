using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Fixed room viewpoint with a small, smoothed look corridor. Attach only to
/// cameras whose Cinemachine/follow drivers have been disabled for this scene.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class FixedCornerCamera : MonoBehaviour
{
    private const float MinimumPitch = 5f;
    private const float MaximumPitch = 85f;

    [Header("Input")]
    [SerializeField] private StarterAssetsInputs inputSource;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
    [SerializeField, Min(0f)] private float gamepadSpeed = 25f;

    [Header("Look corridor (degrees)")]
    [SerializeField, Range(0f, 30f)] private float yawLimit = 6f;
    [SerializeField, Range(0f, 20f)] private float pitchLimit = 4f;
    [SerializeField, Min(0f)] private float smoothing = 12f;

    private Vector3 fixedPosition;
    private float basePitch;
    private float baseYaw;
    private Vector2 lookOffset;
    private Vector2 smoothedOffset;
    private bool hasFocus;
    private bool skipNextInput;
#if ENABLE_INPUT_SYSTEM
    private StarterAssetsInputs cachedInputSource;
    private PlayerInput playerInput;
#endif

    /// <summary>Requested offset in degrees: x is yaw, y is pitch.</summary>
    public Vector2 LookOffset => lookOffset;

    private bool CanLook => hasFocus && Time.timeScale > 0f &&
                            !PlayerControlLock.IsLocked &&
                            !DialogueManager.IsActive &&
                            !SceneLoadingScreen.IsLoading;

    private void OnEnable()
    {
        fixedPosition = transform.position;
        basePitch = Mathf.Clamp(NormalizeAngle(transform.eulerAngles.x), MinimumPitch, MaximumPitch);
        baseYaw = NormalizeAngle(transform.eulerAngles.y);
        hasFocus = Application.isFocused;
        skipNextInput = true;
        ResetLook();
    }

    private void Update()
    {
        if (!CanLook)
        {
            // Do not finish an old mouse movement during dialogue, pause or loading.
            lookOffset = smoothedOffset;
            skipNextInput = true;
            return;
        }

        if (skipNextInput)
        {
            skipNextInput = false;
            return;
        }

        if (inputSource == null || !inputSource.isActiveAndEnabled)
        {
            return;
        }

        bool isMouse = true;
#if ENABLE_INPUT_SYSTEM
        if (cachedInputSource != inputSource || playerInput == null)
        {
            cachedInputSource = inputSource;
            playerInput = inputSource.GetComponent<PlayerInput>();
        }
        if (playerInput != null)
        {
            isMouse = playerInput.currentControlScheme == "KeyboardMouse";
        }
#endif
        ApplyLookInput(inputSource.look, isMouse, Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (CanLook)
        {
            float blend = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            smoothedOffset = Vector2.Lerp(smoothedOffset, lookOffset, blend);
        }

        transform.SetPositionAndRotation(fixedPosition, Quaternion.Euler(
            Mathf.Clamp(basePitch + smoothedOffset.y, MinimumPitch, MaximumPitch),
            baseYaw + smoothedOffset.x, 0f));
    }

    /// <summary>
    /// Accumulates look input without reading devices. Mouse input is a per-frame
    /// delta; stick input is a rate and therefore uses deltaTime. Update handles locks.
    /// </summary>
    public void ApplyLookInput(Vector2 input, bool isMouse, float deltaTime)
    {
        float scale = isMouse ? Mathf.Max(0f, mouseSensitivity) :
            Mathf.Max(0f, gamepadSpeed) * Mathf.Max(0f, deltaTime);
        lookOffset = ClampLookOffset(lookOffset + input * scale, yawLimit, pitchLimit);
        lookOffset.y = Mathf.Clamp(basePitch + lookOffset.y, MinimumPitch, MaximumPitch) - basePitch;
    }

    /// <summary>Pure clamp: x is yaw, y is pitch; negative limits disable that axis.</summary>
    public static Vector2 ClampLookOffset(Vector2 offset, float yawLimit, float pitchLimit)
    {
        yawLimit = Mathf.Max(0f, yawLimit);
        pitchLimit = Mathf.Max(0f, pitchLimit);
        return new Vector2(
            Mathf.Clamp(offset.x, -yawLimit, yawLimit),
            Mathf.Clamp(offset.y, -pitchLimit, pitchLimit));
    }

    /// <summary>Returns immediately to the captured corner viewpoint, without moving the anchor.</summary>
    public void ResetLook()
    {
        lookOffset = Vector2.zero;
        smoothedOffset = Vector2.zero;
        transform.SetPositionAndRotation(fixedPosition, Quaternion.Euler(basePitch, baseYaw, 0f));
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        skipNextInput = true;
        lookOffset = smoothedOffset;
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }
}
