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
    private string storyTitle;
    private readonly System.Collections.Generic.List<string> storyRows = new System.Collections.Generic.List<string>();
    private readonly System.Collections.Generic.List<int> storyMarks = new System.Collections.Generic.List<int>();

    private const int MarkNone = 0;
    private const int MarkTodo = 1;
    private const int MarkDone = 2;

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

        BuildStoryRows();

        float s = UiTheme.Scale;
        float margin = 20f * s;
        float pad = 14f * s;
        float rowHeight = 24f * s;
        float titleHeight = 26f * s;
        float barHeight = 16f * s;
        float markSize = 12f * s;

        GUIStyle title = UiTheme.Label(20f, UiTheme.Cream, FontStyle.Bold, TextAnchor.MiddleLeft);
        GUIStyle caption = UiTheme.Label(16f, UiTheme.Ice, FontStyle.Bold, TextAnchor.MiddleLeft);
        GUIStyle row = UiTheme.Label(16f, UiTheme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
        GUIStyle rowDone = UiTheme.Label(16f, UiTheme.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
        GUIStyle percent = UiTheme.Label(16f, UiTheme.Text, FontStyle.Bold, TextAnchor.MiddleRight);

        // Ширина панели подстраивается под самую длинную строку.
        float inner = 280f * s;
        if (storyTitle != null)
        {
            inner = Mathf.Max(inner, caption.CalcSize(new GUIContent(storyTitle)).x);
        }

        for (int i = 0; i < storyRows.Count; i++)
        {
            float indent = storyMarks[i] == MarkNone ? 0f : markSize + 8f * s;
            inner = Mathf.Max(inner, row.CalcSize(new GUIContent(storyRows[i])).x + indent);
        }

        inner = Mathf.Min(inner, Screen.width - margin * 2f - pad * 2f);

        float height = pad * 2f + titleHeight + 6f * s + barHeight;
        if (storyTitle != null)
        {
            height += 12f * s + rowHeight * (1 + storyRows.Count);
        }

        Rect panel = new Rect(margin, 72f * s, inner + pad * 2f, height);
        UiTheme.Frame(panel, UiTheme.Night, UiTheme.Frost);

        float x = panel.x + pad;
        float y = panel.y + pad;
        GUI.Label(new Rect(x, y, inner, titleHeight), $"День {displayedDay} · {phaseLabel}", title);
        y += titleHeight + 6f * s;

        // Тепло: холодная полоса — лёд, тёплая — огонь печи.
        float normalizedHeat = maximumHeat > 0f ? Mathf.Clamp01(displayedHeat / maximumHeat) : 0f;
        float percentWidth = 52f * s;
        Color heatColor = Color.Lerp(UiTheme.Ice, UiTheme.Ember, Mathf.InverseLerp(0.2f, 0.7f, normalizedHeat));
        UiTheme.Bar(new Rect(x, y, inner - percentWidth, barHeight), normalizedHeat, heatColor);

        // Когда станция выстывает, процент мигает — без звука и лишних окон.
        if (normalizedHeat < 0.25f)
        {
            float blink = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            percent.normal.textColor = Color.Lerp(UiTheme.Muted, UiTheme.Ice, blink);
        }

        GUI.Label(new Rect(x + inner - percentWidth, y - 4f * s, percentWidth, barHeight + 8f * s), $"{displayedHeat:0}%", percent);
        y += barHeight;

        if (storyTitle == null)
        {
            return;
        }

        y += 6f * s;
        UiTheme.Fill(new Rect(x, y, inner, Mathf.Max(1f, UiTheme.PixelSize * 0.5f)), new Color(UiTheme.Frost.r, UiTheme.Frost.g, UiTheme.Frost.b, 0.6f));
        y += 6f * s;

        GUI.Label(new Rect(x, y, inner, rowHeight), storyTitle, caption);
        y += rowHeight;

        for (int i = 0; i < storyRows.Count; i++)
        {
            float textX = x;
            int mark = storyMarks[i];
            if (mark != MarkNone)
            {
                DrawMark(new Rect(x, y + (rowHeight - markSize) * 0.5f, markSize, markSize), mark == MarkDone);
                textX += markSize + 8f * s;
            }

            GUI.Label(new Rect(textX, y, inner - (textX - x), rowHeight), storyRows[i], mark == MarkDone ? rowDone : row);
            y += rowHeight;
        }
    }

    /// <summary>Пиксельный чекбокс вместо символов «✓/•»: их может не быть в шрифте.</summary>
    private static void DrawMark(Rect rect, bool done)
    {
        float pixel = UiTheme.PixelSize;
        UiTheme.Fill(rect, UiTheme.Outline);
        Rect inner = new Rect(rect.x + pixel, rect.y + pixel, rect.width - pixel * 2f, rect.height - pixel * 2f);
        UiTheme.Fill(inner, done ? UiTheme.Ember : UiTheme.Frost);
        if (!done)
        {
            UiTheme.Fill(new Rect(inner.x + pixel, inner.y + pixel, inner.width - pixel * 2f, inner.height - pixel * 2f), UiTheme.Night);
        }
    }

    /// <summary>Утренние задачи (фаза «Утро»), текущий заказ и часы (фаза «Смена»).</summary>
    private void BuildStoryRows()
    {
        storyTitle = null;
        storyRows.Clear();
        storyMarks.Clear();

        if (dayLoopManager == null)
        {
            return;
        }

        if (dayLoopManager.CurrentState == DayLoopManager.DayState.Morning)
        {
            storyTitle = "До смены";
            System.Collections.Generic.List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(dayLoopManager.CurrentDay);
            for (int i = 0; i < tasks.Count; i++)
            {
                bool done = dayLoopManager.GetTaskProgress(tasks[i].id) >= tasks[i].target;

                // Закрытые задачи в списке не показываем: игрок видит только то, что уже доступно.
                if (!done && !dayLoopManager.IsMorningTaskUnlocked(tasks[i].id))
                {
                    continue;
                }

                storyRows.Add(tasks[i].label);
                storyMarks.Add(done ? MarkDone : MarkTodo);
            }
        }
        else if (dayLoopManager.CurrentState == DayLoopManager.DayState.WorkShift)
        {
            ServiceDirector service = ServiceDirector.Instance;
            string clock = service != null ? service.Clock : "09:00";
            storyTitle = $"{clock} · машин: {dayLoopManager.CarsServed}/{dayLoopManager.RegularCarQuota}";

            if (service != null && service.CurrentOrder != null)
            {
                var order = service.CurrentOrder;
                string item = string.IsNullOrEmpty(order.item) ? "" : $" + {Goods.DisplayName(order.item)}";
                storyRows.Add($"Заказ: {order.displayName} — {order.liters:0} л{item}");
                storyMarks.Add(MarkNone);
            }
        }
        else if (dayLoopManager.CurrentState == DayLoopManager.DayState.Evening)
        {
            storyTitle = "18:00 · Вечер";
            KotovVisit kotov = StoryDirector.Instance != null ? StoryDirector.Instance.Kotov : null;
            if (kotov != null && kotov.IsLetterWaiting)
            {
                storyRows.Add("Подобрать открытку у порога");
                storyMarks.Add(MarkTodo);
            }
            else
            {
                storyRows.Add("Визит Котова");
                storyMarks.Add(MarkTodo);
            }
        }
        else if (dayLoopManager.CurrentState == DayLoopManager.DayState.NightTransition)
        {
            storyTitle = "Ночь";
            var nightTasks = DayPlans.GetNightTasks(dayLoopManager.CurrentDay);
            bool allDone = true;
            for (int i = 0; i < nightTasks.Count; i++)
            {
                bool done = dayLoopManager.GetTaskProgress(nightTasks[i].id) >= nightTasks[i].target;
                if (!done)
                {
                    allDone = false;
                    storyRows.Add(nightTasks[i].label);
                    storyMarks.Add(MarkTodo);
                }
            }

            if (allDone)
            {
                storyRows.Add("Лечь спать (кровать в доме)");
                storyMarks.Add(MarkTodo);
            }
        }
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
