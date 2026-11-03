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
        GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, w - 32f, 28f),
            $"Записная книжка — День {day}", Title());

        float tabY = panel.y + 44f;
        for (int i = 0; i < TabNames.Length; i++)
        {
            if (GUI.Button(new Rect(panel.x + 16f + i * 150f, tabY, 140f, 30f),
                    (i == tab ? "● " : "") + TabNames[i]))
            {
                tab = i;
                scroll = Vector2.zero;
            }
        }

        Rect content = new Rect(panel.x + 16f, tabY + 40f, w - 48f, h - 110f);
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

        GUI.Label(new Rect(panel.x, panel.yMax + 6f, w, 22f),
            "Tab — закрыть · 1/2/3 — вкладки", Small());

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

    private float ContentHeight()
    {
        switch (tab)
        {
            case 0: return 24f * (GameState.GetShiftLog().Count + GameState.GetNotes().Count + 2);
            case 1: return 140f * GameState.LetterCount;
            default: return 24f * (GameState.GetObservations().Count + 1);
        }
    }

    private void DrawShiftTab()
    {
        float y = 0f;
        y = Section("Журнал смены", y);
        var log = GameState.GetShiftLog();
        if (log.Count == 0)
        {
            GUI.Label(new Rect(8f, y, 480f, 22f), "— пусто —", Small());
            y += 24f;
        }
        for (int i = 0; i < log.Count; i++)
        {
            GUI.Label(new Rect(8f, y, 480f, 22f), log[i], Small());
            y += 24f;
        }

        y = Section("Заметки", y + 8f);
        var notes = GameState.GetNotes();
        if (notes.Count == 0)
        {
            GUI.Label(new Rect(8f, y, 480f, 22f), "— пусто —", Small());
            return;
        }
        for (int i = 0; i < notes.Count; i++)
        {
            GUI.Label(new Rect(8f, y, 480f, 22f), notes[i], Small());
            y += 24f;
        }
    }

    private void DrawLettersTab()
    {
        float y = 0f;
        for (int n = 1; n <= GameState.LetterCount; n++)
        {
            GUI.Label(new Rect(8f, y, 480f, 24f), $"Письмо №{n}", Title());
            y += 26f;
            string body = GameState.HasLetter(n)
                ? Letters.RenderForNotebook(n)
                : "— не получено —";
            Rect r = new Rect(8f, y, 480f, 100f);
            GUI.Label(r, body, Small());
            y += 110f;
        }
    }

    private void DrawObservationsTab()
    {
        float y = 0f;
        var obs = GameState.GetObservations();
        if (obs.Count == 0)
        {
            GUI.Label(new Rect(8f, y, 480f, 22f), "— пусто —", Small());
            return;
        }
        for (int i = 0; i < obs.Count; i++)
        {
            GUI.Label(new Rect(8f, y, 480f, 22f), "• " + obs[i], Small());
            y += 24f;
        }
    }

    private float Section(string title, float y)
    {
        GUI.Label(new Rect(0f, y, 480f, 24f), title, Title());
        return y + 26f;
    }

    private void DrawHint()
    {
        if (open) return;
        GUI.Label(new Rect(Screen.width - 230f, Screen.height - 40f, 220f, 24f),
            "[Tab] Записная книжка", Small());
    }

    private GUIStyle titleStyle;
    private GUIStyle smallStyle;

    private GUIStyle Title()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.9f, 0.8f) }
            };
        }
        return titleStyle;
    }

    private GUIStyle Small()
    {
        if (smallStyle == null)
        {
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.85f, 0.88f) }
            };
        }
        return smallStyle;
    }
}
