using StarterAssets;
using UnityEngine;

/// <summary>
/// Эффекты холода: при тепле станции ниже 25% Рыжиков замедляется (×0.6 скорости)
/// и по краям экрана проступает иней. Холод не убивает — только неудобство.
/// Скорость правится через FirstPersonController.MoveSpeed (бега в игре нет).
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerColdEffects : MonoBehaviour
{
    private const float SlowFactor = 0.6f;
    private const float OriginalMoveSpeed = 4f;

    private FirstPersonController player;
    private bool applied;
    private Texture2D frost;

    private void Update()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<FirstPersonController>();
        }

        StationHeatSystem heat = StationHeatSystem.Active;
        bool cold = heat != null && heat.IsCold;

        if (player != null && cold != applied)
        {
            applied = cold;
            player.MoveSpeed = cold ? OriginalMoveSpeed * SlowFactor : OriginalMoveSpeed;
            Debug.Log(cold
                ? "[Snowpaw Station] Холод: скорость ×0.6, иней по краям экрана."
                : "[Snowpaw Station] Тепло вернулось: скорость в норме.");
        }
    }

    private void OnGUI()
    {
        StationHeatSystem heat = StationHeatSystem.Active;
        if (heat == null || !heat.IsCold)
        {
            return;
        }

        if (frost == null)
        {
            frost = new Texture2D(1, 1);
            frost.SetPixel(0, 0, new Color(0.75f, 0.9f, 1f, 0.35f));
            frost.Apply();
        }

        float edge = Mathf.Min(Screen.width, Screen.height) * 0.12f;
        DrawEdge(new Rect(0f, 0f, Screen.width, edge));
        DrawEdge(new Rect(0f, Screen.height - edge, Screen.width, edge));
        DrawEdge(new Rect(0f, 0f, edge, Screen.height));
        DrawEdge(new Rect(Screen.width - edge, 0f, edge, Screen.height));
    }

    private void DrawEdge(Rect rect)
    {
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.55f);
        GUI.DrawTexture(rect, frost);
        GUI.color = prev;
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.MoveSpeed = OriginalMoveSpeed;
        }

        if (frost != null)
        {
            Destroy(frost);
        }
    }
}
