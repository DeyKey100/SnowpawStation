using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StationHud : MonoBehaviour
{
    private static StationHud persistent;

    [SerializeField] private DayLoopManager dayLoopManager;
    [SerializeField] private StationHeatSystem heatSystem;
    [SerializeField, Tooltip("Показывать HUD: текущий день, фаза смены и процент тепла.")]
    private bool hud = true;

    /// <summary>
    /// HUD переезжает между сценами: иначе в интерьере магазина задачи и тепло пропадают.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Adopt();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Adopt();
    }

    private static void Adopt()
    {
        if (persistent != null)
        {
            return;
        }

        StationHud found = FindFirstObjectByType<StationHud>();
        if (found == null)
        {
            GameObject go = new GameObject("StationHUD");
            found = go.AddComponent<StationHud>();
        }

        persistent = found;
        if (found.transform.parent == null)
        {
            DontDestroyOnLoad(found.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (persistent == this)
        {
            persistent = null;
        }
    }

    private string phaseLabel = "Утро";
    private int displayedDay = 1;
    private float displayedHeat = 75f;
    private float maximumHeat = 100f;
    private GUIStyle labelStyle;
    private GUIStyle heatBackStyle;
    private GUIStyle heatFillStyle;

    private void Awake()
    {
        // Дубль из загруженной сцены не нужен: HUD уже переехал сюда.
        if (persistent != null && persistent != this)
        {
            Destroy(gameObject);
            return;
        }

        persistent = this;
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedInstance;
        SceneManager.sceneLoaded += HandleSceneLoadedInstance;
        SubscribeToManagers();
        RefreshValues();
    }

    private void Start()
    {
        SubscribeToManagers();
        RefreshValues();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedInstance;
        UnsubscribeFromManagers();
    }

    /// <summary>После перехода в другую сцену пересобираем ссылки: менеджеры могли пересоздаться.</summary>
    private void HandleSceneLoadedInstance(Scene scene, LoadSceneMode mode)
    {
        SubscribeToManagers();
        RefreshValues();
    }

    private void SubscribeToManagers()
    {
        // Ссылка из инспектора после перехода в другую сцену становится «мёртвой»: падаем на синглтоны.
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
            heatSystem = StationHeatSystem.Active != null
                ? StationHeatSystem.Active
                : FindFirstObjectByType<StationHeatSystem>();
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
        GUI.Label(new Rect(margin, topOffset, 420f, lineHeight), $"День {displayedDay} · {phaseLabel}", labelStyle);

        const float barWidth = 220f;
        const float barHeight = 22f;
        Rect barRect = new Rect(margin, topOffset + lineHeight + 4f, barWidth, barHeight);
        GUI.Box(barRect, GUIContent.none, heatBackStyle);

        float normalizedHeat = maximumHeat > 0f ? Mathf.Clamp01(displayedHeat / maximumHeat) : 0f;
        Rect fillRect = new Rect(barRect.x + 2f, barRect.y + 2f, (barWidth - 4f) * normalizedHeat, barHeight - 4f);
        GUI.Box(fillRect, GUIContent.none, heatFillStyle);
        GUI.Label(new Rect(barRect.x + barWidth + 8f, barRect.y - 3f, 100f, barHeight + 6f), $"{displayedHeat:0}%", labelStyle);

        DrawStoryPanel(margin, topOffset + lineHeight + barHeight + 16f);
    }

    /// <summary>Утренние задачи (фаза «Утро»), текущий заказ и часы (фаза «Смена»).</summary>
    private void DrawStoryPanel(float x, float y)
    {
        if (dayLoopManager == null)
        {
            return;
        }

        GUIStyle small = new GUIStyle(labelStyle) { fontSize = 18 };

        if (dayLoopManager.CurrentState == DayLoopManager.DayState.Morning)
        {
            GUI.Label(new Rect(x, y, 420f, 26f), "Утро — до смены:", small);
            System.Collections.Generic.List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(dayLoopManager.CurrentDay);
            int row = 0;
            for (int i = 0; i < tasks.Count; i++)
            {
                bool done = dayLoopManager.GetTaskProgress(tasks[i].id) >= tasks[i].target;

                // Закрытые задачи в списке не показываем: игрок видит только то, что уже доступно.
                if (!done && !dayLoopManager.IsMorningTaskUnlocked(tasks[i].id))
                {
                    continue;
                }

                Color prev = GUI.color;
                GUI.color = done ? new Color(0.55f, 0.85f, 0.55f) : Color.white;
                GUI.Label(new Rect(x + 14f, y + 28f + row * 24f, 460f, 24f),
                    (done ? "✓ " : "• ") + tasks[i].label, small);
                GUI.color = prev;
                row++;
            }
        }
        else if (dayLoopManager.CurrentState == DayLoopManager.DayState.WorkShift)
        {
            ServiceDirector service = ServiceDirector.Instance;
            string clock = service != null ? service.Clock : "09:00";
            GUI.Label(new Rect(x, y, 420f, 26f), $"Смена · {clock} · машин: {dayLoopManager.CarsServed}/{dayLoopManager.RegularCarQuota}", small);

            if (service != null && service.CurrentOrder != null)
            {
                var order = service.CurrentOrder;
                string item = string.IsNullOrEmpty(order.item) ? "" : $" + {Goods.DisplayName(order.item)}";
                GUI.Label(new Rect(x + 14f, y + 28f, 460f, 26f), $"Заказ: {order.displayName} — {order.liters:0} л{item}", small);
            }
        }
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
