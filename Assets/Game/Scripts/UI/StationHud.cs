using UnityEngine;

public sealed class StationHud : MonoBehaviour
{
    [SerializeField] private DayLoopManager dayLoopManager;
    [SerializeField] private StationHeatSystem heatSystem;
    [SerializeField, Tooltip("Показывать HUD: текущий день, фаза смены и процент тепла.")]
    private bool hud = true;

    private string phaseLabel = "Утро";
    private int displayedDay = 1;
    private float displayedHeat = 75f;
    private float maximumHeat = 100f;
    private GUIStyle labelStyle;
    private GUIStyle heatBackStyle;
    private GUIStyle heatFillStyle;

    private void OnEnable()
    {
        SubscribeToManagers();
    }

    private void Start()
    {
        SubscribeToManagers();
        RefreshValues();
    }

    private void OnDisable()
    {
        UnsubscribeFromManagers();
    }

    private void SubscribeToManagers()
    {
        if (dayLoopManager == null)
        {
            dayLoopManager = DayLoopManager.Instance;
        }

        if (dayLoopManager != null)
        {
            dayLoopManager.PhaseChanged -= HandlePhaseChanged;
            dayLoopManager.PhaseChanged += HandlePhaseChanged;
            dayLoopManager.DayChanged -= HandleDayChanged;
            dayLoopManager.DayChanged += HandleDayChanged;
        }

        if (heatSystem == null)
        {
            heatSystem = FindFirstObjectByType<StationHeatSystem>();
        }

        if (heatSystem != null)
        {
            heatSystem.HeatChanged -= HandleHeatChanged;
            heatSystem.HeatChanged += HandleHeatChanged;
        }
    }

    private void UnsubscribeFromManagers()
    {
        if (dayLoopManager != null)
        {
            dayLoopManager.PhaseChanged -= HandlePhaseChanged;
            dayLoopManager.DayChanged -= HandleDayChanged;
        }

        if (heatSystem != null)
        {
            heatSystem.HeatChanged -= HandleHeatChanged;
        }
    }

    private void RefreshValues()
    {
        if (dayLoopManager != null)
        {
            HandlePhaseChanged(dayLoopManager.CurrentDay, dayLoopManager.CurrentState);
        }

        if (heatSystem != null)
        {
            HandleHeatChanged(heatSystem.CurrentHeat, heatSystem.MaxHeat);
        }
    }

    private void HandlePhaseChanged(int day, DayLoopManager.DayState state)
    {
        displayedDay = day;
        phaseLabel = GetPhaseLabel(state);
    }

    private void HandleHeatChanged(float heat, float max)
    {
        displayedHeat = heat;
        maximumHeat = max;
    }

    private void HandleDayChanged(int day)
    {
        displayedDay = day;
    }

    private void OnGUI()
    {
        if (!hud)
        {
            return;
        }

        EnsureStyles();

        const float margin = 20f;
        const float topOffset = 72f;
        const float lineHeight = 32f;
        GUI.Label(new Rect(margin, topOffset, 320f, lineHeight), $"День {displayedDay} · {phaseLabel}", labelStyle);

        const float barWidth = 220f;
        const float barHeight = 22f;
        Rect barRect = new Rect(margin, topOffset + lineHeight + 4f, barWidth, barHeight);
        GUI.Box(barRect, GUIContent.none, heatBackStyle);

        float normalizedHeat = maximumHeat > 0f ? Mathf.Clamp01(displayedHeat / maximumHeat) : 0f;
        Rect fillRect = new Rect(barRect.x + 2f, barRect.y + 2f, (barWidth - 4f) * normalizedHeat, barHeight - 4f);
        GUI.Box(fillRect, GUIContent.none, heatFillStyle);
        GUI.Label(new Rect(barRect.x + barWidth + 8f, barRect.y - 3f, 100f, barHeight + 6f), $"{displayedHeat:0}%", labelStyle);
    }

    private void EnsureStyles()
    {
        if (labelStyle != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        heatBackStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTexture(new Color(0.04f, 0.07f, 0.1f, 0.9f)) }
        };
        heatFillStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTexture(new Color(0.25f, 0.75f, 0.9f, 1f)) }
        };
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static string GetPhaseLabel(DayLoopManager.DayState state)
    {
        switch (state)
        {
            case DayLoopManager.DayState.Morning: return "Утро";
            case DayLoopManager.DayState.WorkShift: return "Смена";
            case DayLoopManager.DayState.Evening: return "Вечер";
            case DayLoopManager.DayState.NightTransition: return "Ночь";
            default: return state.ToString();
        }
    }
}
