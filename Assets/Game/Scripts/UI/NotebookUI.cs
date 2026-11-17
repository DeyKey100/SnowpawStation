using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Записная книжка Рыжикова (Tab): «Смена», «Письма», «Наблюдения».
/// Самоподнимается при загрузке сцены — правок существующих сцен не требует.
/// </summary>
public sealed class NotebookUI : MonoBehaviour
{
    private static NotebookUI instance;

    private bool open;
    private int tab;
    private Vector2 scroll;
    private static readonly string[] TabNames = { "Смена", "Письма", "Наблюдения" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("NotebookUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NotebookUI>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (PauseMenu.IsPaused || DialogueManager.IsActive || InteractionLock.IsLocked)
        {
            if (open && InteractionLock.IsLocked && !DialogueManager.IsActive)
            {
                open = false;
            }

            return;
        }

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
        pressed = Input.GetKeyDown(KeyCode.Tab);
#endif
        if (pressed)
        {
            open = !open;
        }
        else if (open && IsEscapePressed())
        {
            open = false;
        }
    }

    private static bool IsEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void OnGUI()
    {
        DrawHint();
        if (!open) return;

        float w = Mathf.Min(560f, Screen.width - 80f);
        float h = Mathf.Min(420f, Screen.height - 120f);
        Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        GUI.Box(panel, GUIContent.none);
        int day = DayLoopManager.Instance != null ? DayLoopManager.Instance.CurrentDay : 1;
        string heading = $"Записная книжка — День {day}";
        float titleHeight = UiTheme.TextHeight(Title(), heading, w - 32f);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, w - 32f, titleHeight), heading, Title());

        GUIStyle tabStyle = UiTheme.Label(15f, UiTheme.Text, alignment: TextAnchor.MiddleCenter,
            scaleWithScreen: false);
        float tabWidth = (w - 32f) / TabNames.Length;
        float tabHeight = UiTheme.TextHeight(tabStyle, "Смена Письма Наблюдения", tabWidth);
        float tabY = panel.y + 16f + titleHeight;
        for (int i = 0; i < TabNames.Length; i++)
        {
            Rect tabRect = new Rect(panel.x + 16f + i * tabWidth, tabY, tabWidth - 4f, tabHeight);
            GUI.Box(tabRect, GUIContent.none);
            GUI.Label(tabRect, (i == tab ? "● " : "") + TabNames[i], tabStyle);
            if (GUI.Button(tabRect, GUIContent.none, GUIStyle.none))
            {
                tab = i;
                scroll = Vector2.zero;
            }
        }

        float contentY = tabY + tabHeight + 10f;
        Rect content = new Rect(panel.x + 16f, contentY, w - 48f, Mathf.Max(1f, panel.yMax - contentY - 16f));
        float contentHeight = ContentHeight();
        float maxScroll = Mathf.Max(0f, contentHeight - content.height);

        // Колёсико обрабатываем до BeginScrollView: иначе событие «съедается» скроллом.
        // При захваченном курсоре mousePosition — центр экрана, он внутри панели.
        if (Event.current != null && Event.current.type == EventType.ScrollWheel)
        {
            scroll.y = Mathf.Clamp(scroll.y + Event.current.delta.y * 30f, 0f, maxScroll);
            Event.current.Use();
        }

        scroll = GUI.BeginScrollView(content, scroll, new Rect(0f, 0f, content.width - 20f, contentHeight));
        switch (tab)
        {
            case 0: DrawShiftTab(); break;
            case 1: DrawLettersTab(); break;
            default: DrawObservationsTab(); break;
        }
        GUI.EndScrollView();

        const string footer = "Tab — закрыть · 1/2/3 — вкладки";
        GUI.Label(new Rect(panel.x, panel.yMax + 6f, w, UiTheme.TextHeight(Small(), footer, w)),
            footer, Small());

        HandleTabKeys();
    }

    private void HandleTabKeys()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) tab = 0;
        else if (kb.digit2Key.wasPressedThisFrame) tab = 1;
        else if (kb.digit3Key.wasPressedThisFrame) tab = 2;
#endif
    }

    // Один проход раскладки для измерения и рисования: прокрутка включает все строки и переносы.
    private float ContentWidth => Mathf.Max(1f, Mathf.Min(560f, Screen.width - 80f) - 68f);

    private float ContentHeight() => DrawContent(false);
    private void DrawShiftTab() => DrawContent(true);
    private void DrawLettersTab() => DrawContent(true);
    private void DrawObservationsTab() => DrawContent(true);

    private float DrawContent(bool draw)
    {
        float y = 4f;
        switch (tab)
        {
            case 0:
                ContentLabel("Журнал смены", Title(), ref y, draw);
                var log = GameState.GetShiftLog();
                if (log.Count == 0) ContentLabel("— пусто —", Small(), ref y, draw);
                for (int i = 0; i < log.Count; i++) ContentLabel(log[i], Small(), ref y, draw);
                y += 8f;
                ContentLabel("Заметки", Title(), ref y, draw);
                var notes = GameState.GetNotes();
                if (notes.Count == 0) ContentLabel("— пусто —", Small(), ref y, draw);
                for (int i = 0; i < notes.Count; i++) ContentLabel(notes[i], Small(), ref y, draw);
                break;
            case 1:
                for (int n = 1; n <= GameState.LetterCount; n++)
                {
                    ContentLabel($"Письмо №{n}", Title(), ref y, draw);
                    ContentLabel(GameState.HasLetter(n) ? Letters.RenderForNotebook(n) : "— не получено —",
                        Small(), ref y, draw);
                    y += 8f;
                }
                break;
            default:
                var observations = GameState.GetObservations();
                if (observations.Count == 0) ContentLabel("— пусто —", Small(), ref y, draw);
                for (int i = 0; i < observations.Count; i++)
                    ContentLabel("• " + observations[i], Small(), ref y, draw);
                break;
        }
        return y + 4f;
    }

    private void ContentLabel(string text, GUIStyle style, ref float y, bool draw)
    {
        float width = Mathf.Max(1f, ContentWidth - 8f);
        float height = UiTheme.TextHeight(style, text, width);
        if (draw) GUI.Label(new Rect(8f, y, width, height), text, style);
        y += height + 4f;
    }

    private void DrawHint()
    {
        if (open) return;
        const string hint = "[Tab] Записная книжка";
        float width = Mathf.Min(Screen.width - 24f, Mathf.Max(220f, Small().CalcSize(new GUIContent(hint)).x));
        float height = UiTheme.TextHeight(Small(), hint, width);
        GUI.Label(new Rect(Screen.width - width - 12f, Screen.height - height - 12f, width, height),
            hint, Small());
    }

    private GUIStyle titleStyle;
    private GUIStyle smallStyle;

    private GUIStyle Title()
    {
        if (titleStyle == null)
        {
            titleStyle = UiTheme.Label(18f, new Color(0.95f, 0.9f, 0.8f),
                FontStyle.Bold, wordWrap: true, scaleWithScreen: false);
        }
        return titleStyle;
    }

    private GUIStyle Small()
    {
        if (smallStyle == null)
        {
            smallStyle = UiTheme.Label(15f, new Color(0.85f, 0.85f, 0.88f),
                wordWrap: true, scaleWithScreen: false);
        }
        return smallStyle;
    }
}
