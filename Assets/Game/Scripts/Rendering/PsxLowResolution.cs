using UnityEngine;

/// <summary>
/// Совместимый пустой фасад старого PS1-фильтра.
/// Постобработка теперь выполняется BayerDitherFeature в PC_Renderer.
/// Класс оставлен только для старых вызовов UI и проекционных хелперов.
/// </summary>
[DisallowMultipleComponent]
public sealed class PsxLowResolution : MonoBehaviour
{
    public enum AspectMode
    {
        FollowScreen = 0,
        Pillarbox = 1,
    }

    public static PsxLowResolution Instance => null;
    public static bool IsActive => false;
    public static Rect ScreenRect => new Rect(0f, 0f, Screen.width, Screen.height);

    public static Vector3 WorldToScreenPoint(Camera camera, Vector3 worldPoint)
    {
        if (camera == null)
        {
            return Vector3.zero;
        }

        return camera.WorldToScreenPoint(worldPoint);
    }

    public int TargetHeight => 240;
    public AspectMode CurrentAspectMode => AspectMode.FollowScreen;

    public void SetEnabled(bool on) { }
    public void SetTargetHeight(int height) { }
    public void SetAspectMode(AspectMode mode) { }
}
