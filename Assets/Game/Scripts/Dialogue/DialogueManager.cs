using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Диалоговый движок нулевой версии: имя говорящего, очередь реплик, варианты ответа,
/// условия показа (DialogueConditions), действия (DialogueActions), корректное завершение.
/// На время диалога блокирует обычное взаимодействие (InteractionLock) — но не камеру:
/// на Котова можно смотреть/отворачиваться во время визита.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueManager : MonoBehaviour
{
    private const float TextSpeed = 60f; // символов в секунду

    public static DialogueManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.script != null;

    public event Action<DialogueScript> DialogueStarted;
    public event Action<DialogueScript> DialogueFinished;

    private DialogueScript script;
    private int entryIndex;
    private float revealed;
    private Action onFinished;
    private GUIStyle panelStyle;
    private GUIStyle speakerStyle;
    private GUIStyle textStyle;
    private GUIStyle choiceStyle;
    private Texture2D panelTexture;
    private string pendingFinishId;
    private readonly System.Collections.Generic.Queue<(DialogueScript script, Action finished)> queue =
        new System.Collections.Generic.Queue<(DialogueScript, Action)>();

    public DialogueScript Current => script;

    /// <summary>Вставить реплику сразу после текущего элемента диалога.</summary>
    public void InsertNextEntry(string speaker, string text)
    {
        if (script == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        script.entries.Insert(entryIndex + 1, new DialogueEntry
        {
            speaker = speaker,
            text = text,
        });
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (panelTexture != null)
        {
            Destroy(panelTexture);
        }
    }

    /// <summary>Показать сценарий. Если диалог уже идёт — новый встанет в очередь, а не потеряется.</summary>
    public void Show(DialogueScript newScript, Action finished = null)
    {
        if (newScript == null || newScript.entries.Count == 0)
        {
            Debug.LogError("DialogueManager: пустой сценарий диалога.");
            finished?.Invoke();
            return;
        }

        if (IsActive)
        {
            // Раньше реплика просто терялась (например, приветствие следующей машины).
            queue.Enqueue((newScript, finished));
            Debug.Log($"[Snowpaw Station] Диалог '{script.id}' идёт — '{newScript.id}' встал в очередь (всего {queue.Count}).");
            return;
        }

        script = newScript;
        entryIndex = 0;
        revealed = 0f;
        onFinished = finished;
        pendingFinishId = null;
        InteractionLock.Add("dialogue");
        DialogueStarted?.Invoke(script);
    }

    private void Update()
    {
        if (script == null)
        {
            return;
        }

        DialogueEntry entry = script.entries[entryIndex];
        string fullText = entry.IsChoice ? "" : entry.text;
        bool fullyRevealed = string.IsNullOrEmpty(fullText) || revealed >= fullText.Length;

        if (!fullyRevealed)
        {
            revealed += TextSpeed * Time.unscaledDeltaTime;
            fullyRevealed = revealed >= fullText.Length;
        }

        bool advance = false;
        int choiceIndex = -1;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            advance = (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                       keyboard.enterKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) && fullyRevealed;
            if (entry.IsChoice && fullyRevealed)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) choiceIndex = 0;
                if (keyboard.digit2Key.wasPressedThisFrame) choiceIndex = 1;
                if (keyboard.digit3Key.wasPressedThisFrame) choiceIndex = 2;
                if (keyboard.digit4Key.wasPressedThisFrame) choiceIndex = 3;
            }
        }
#else
        if (fullyRevealed)
        {
            advance = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.LeftArrow);
            if (entry.IsChoice)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) choiceIndex = 0;
                if (Input.GetKeyDown(KeyCode.Alpha2)) choiceIndex = 1;
                if (Input.GetKeyDown(KeyCode.Alpha3)) choiceIndex = 2;
                if (Input.GetKeyDown(KeyCode.Alpha4)) choiceIndex = 3;
            }
        }
#endif

        if (choiceIndex >= 0)
        {
            PickChoice(entry, choiceIndex);
        }
        else if (advance)
        {
            NextEntry();
        }
    }

    private void PickChoice(DialogueEntry entry, int index)
    {
        if (index >= entry.choices.Count)
        {
            return;
        }

        DialogueChoice choice = entry.choices[index];
        DialogueScript activeScript = script;
        DialogueActions.Run(choice.action);

        // Действие варианта могло оборвать диалог (например, gaze_break → Abort) или
        // подставить из очереди следующий — тогда этот уже не наш.
        if (script == null || script != activeScript)
        {
            return;
        }

        if (!string.IsNullOrEmpty(choice.response))
        {
            script.entries.Insert(entryIndex + 1, new DialogueEntry { speaker = "", text = choice.response });
        }

        NextEntry();
    }

    private void NextEntry()
    {
        if (script == null)
        {
            return;
        }

        entryIndex++;
        revealed = 0f;

        if (entryIndex >= script.entries.Count)
        {
            Finish();
        }
    }

    /// <summary>Тихое завершение (например, Котов исчез из-за отведённого взгляда).</summary>
    public void Abort()
    {
        if (script != null)
        {
            pendingFinishId = script.id;
            Finish();
        }
    }

    private void Finish()
    {
        if (script == null)
        {
            return;
        }

        DialogueScript finished = script;
        script = null;
        entryIndex = 0;
        StartCoroutine(RemoveLockNextFrame());

        DialogueFinished?.Invoke(finished);
        onFinished?.Invoke();
        onFinished = null;

        if (pendingFinishId != null)
        {
            Debug.Log($"[Snowpaw Station] Диалог '{pendingFinishId}' прерван.");
            pendingFinishId = null;
        }

        // Очередь: показываем отложенный сценарий (приветствие следующей машины и т.п.).
        if (queue.Count > 0)
        {
            var next = queue.Dequeue();

            script = next.script;
            entryIndex = 0;
            revealed = 0f;
            onFinished = next.finished;
            InteractionLock.Add("dialogue");
            DialogueStarted?.Invoke(script);
        }
    }

    private System.Collections.IEnumerator RemoveLockNextFrame()
    {
        yield return null; // Ждем 1 кадр, пока "сгорит" нажатие E
        InteractionLock.Remove("dialogue");
    }

    private void OnGUI()
    {
        if (script == null)
        {
            return;
        }

        // Масштаб интерфейса от высоты экрана: на 1440p/4K старый фиксированный размер был нечитаем.
        float scale = Mathf.Clamp(Screen.height / 900f, 1f, 2.2f);
        EnsureStyles(scale);

        DialogueEntry entry = script.entries[entryIndex];
        string fullText = entry.IsChoice ? "" : entry.text;
        bool fullyRevealed = string.IsNullOrEmpty(fullText) || revealed >= fullText.Length;
        string shownText = fullText == null ? "" : (fullyRevealed ? fullText : fullText.Substring(0, Mathf.FloorToInt(Mathf.Min(revealed, fullText.Length))));

        int visibleChoices = 0;
        if (entry.IsChoice)
        {
            for (int i = 0; i < entry.choices.Count; i++)
            {
                if (DialogueConditions.Evaluate(entry.choices[i].condition))
                {
                    visibleChoices++;
                }
            }
        }

        float width = Mathf.Min(Screen.width * 0.7f, 900f * scale);
        float height = entry.IsChoice
            ? (76f + Mathf.Max(1, visibleChoices) * 58f) * scale
            : 180f * scale;
        Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 40f * scale, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);

        float innerX = panel.x + 24f * scale;
        float innerW = panel.width - 48f * scale;

        if (!string.IsNullOrEmpty(script.header))
        {
            GUI.Label(new Rect(innerX, panel.y - 34f * scale, innerW, 30f * scale), script.header, speakerStyle);
        }

        if (!entry.IsChoice)
        {
            float y = panel.y + 14f * scale;
            if (!string.IsNullOrEmpty(entry.speaker))
            {
                GUI.Label(new Rect(innerX, y, innerW, 32f * scale), entry.speaker, speakerStyle);
                y += 32f * scale;
            }

            GUI.Label(new Rect(innerX, y, innerW, panel.yMax - y - 44f * scale), shownText, textStyle);

            if (fullyRevealed)
            {
                GUI.Label(new Rect(panel.xMax - 260f * scale, panel.yMax - 36f * scale, 240f * scale, 28f * scale),
                    "E / Space — далее", choiceStyle);
            }
        }
        else
        {
            GUI.Label(new Rect(innerX, panel.y + 12f * scale, innerW, 30f * scale), "Выберите ответ (1-4):", speakerStyle);
            int shown = 0;
            for (int i = 0; i < entry.choices.Count; i++)
            {
                DialogueChoice choice = entry.choices[i];
                if (!DialogueConditions.Evaluate(choice.condition))
                {
                    continue;
                }

                Rect button = new Rect(innerX, panel.y + (52f + shown * 58f) * scale, innerW, 50f * scale);
                string label = $"{i + 1}. {choice.label}";
                if (GUI.Button(button, label, choiceStyle))
                {
                    PickChoice(entry, i);
                    break;
                }

                shown++;
            }
        }
    }

    private float styleScale = -1f;

    private void EnsureStyles(float scale)
    {
        if (panelStyle != null && Mathf.Abs(styleScale - scale) < 0.01f)
        {
            return;
        }

        styleScale = scale;

        if (panelTexture == null)
        {
            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.03f, 0.05f, 0.08f, 0.94f));
            panelTexture.Apply();
        }

        int body = Mathf.RoundToInt(20f * scale);
        int head = Mathf.RoundToInt(22f * scale);

        panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
        speakerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = head,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.95f, 0.9f, 0.8f) }
        };
        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = body,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
        choiceStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = body,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true,
            padding = new RectOffset(Mathf.RoundToInt(12f * scale), Mathf.RoundToInt(12f * scale), 4, 4)
        };
    }
}
