using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Диалоговый движок нулевой версии: имя говорящего, очередь реплик, варианты ответа,
/// условия показа (DialogueConditions), действия (DialogueActions), корректное завершение.
/// На время диалога блокирует обычное взаимодействие (InteractionLock) и ходьбу
/// (PlayerControlLock.AddMoveOnly), но не камеру: на Котова можно смотреть/отворачиваться.
/// Внешний вид — через общую тему UiTheme.
///
/// Клавиша E у диалога в приоритете: Update выполняется раньше мира (Execution Order
/// −200) и забирает нажатие через InputPriority, поэтому стоя у двери игрок листает
/// реплики, а не уезжает в другую сцену.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class DialogueManager : MonoBehaviour
{
    private const float TextSpeed = 60f; // символов в секунду
    private const string AnswerPlate = "ВАШ ОТВЕТ";

    /// <summary>Идентификатор замка движения на время диалога.</summary>
    private const string DialogueLockId = "dialogue_move";

    public static DialogueManager Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.script != null;

    public event Action<DialogueScript> DialogueStarted;
    public event Action<DialogueScript> DialogueFinished;

    private DialogueScript script;
    private int entryIndex;
    private float revealed;
    private Action onFinished;
    private string pendingFinishId;
    private int selectedChoice = -1; // индекс среди видимых вариантов; -1 = ничего не подсвечено
    private int inputBlockedFrame = -1;
    private float openedAt;
    private float entryShownAt;
    private readonly List<int> visibleChoices = new List<int>();
    private readonly Queue<(DialogueScript script, Action finished)> queue =
        new Queue<(DialogueScript, Action)>();

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

        pendingFinishId = null;
        Begin(newScript, finished);
    }

    private void Begin(DialogueScript newScript, Action finished)
    {
        script = newScript;
        entryIndex = 0;
        revealed = 0f;
        selectedChoice = -1;
        onFinished = finished;
        openedAt = Time.unscaledTime;
        entryShownAt = openedAt;
        // Нажатие E, открывшее диалог, не должно в тот же кадр «допечатать» первую реплику.
        inputBlockedFrame = Time.frameCount;
        InteractionLock.Add("dialogue");
        // Ходить во время разговора нельзя: игрок не должен уйти от собеседника
        // посреди реплики. Камеру намеренно не замораживаем — оглянуться можно.
        PlayerControlLock.AddMoveOnly(DialogueLockId);
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
        }

        if (Time.frameCount == inputBlockedFrame || PauseMenu.IsPaused || AdminPanel.IsOpen)
        {
            return;
        }

        bool confirm;
        bool confirmE;
        int move = 0;
        int digit = -1;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        confirmE = keyboard.eKey.wasPressedThisFrame;
        confirm = confirmE || keyboard.spaceKey.wasPressedThisFrame ||
                  keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
        if (keyboard.upArrowKey.wasPressedThisFrame) move = -1;
        if (keyboard.downArrowKey.wasPressedThisFrame) move = 1;
        if (keyboard.digit1Key.wasPressedThisFrame) digit = 0;
        if (keyboard.digit2Key.wasPressedThisFrame) digit = 1;
        if (keyboard.digit3Key.wasPressedThisFrame) digit = 2;
        if (keyboard.digit4Key.wasPressedThisFrame) digit = 3;
#else
        confirmE = Input.GetKeyDown(KeyCode.E);
        confirm = confirmE || Input.GetKeyDown(KeyCode.Space) ||
                  Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        if (Input.GetKeyDown(KeyCode.UpArrow)) move = -1;
        if (Input.GetKeyDown(KeyCode.DownArrow)) move = 1;
        if (Input.GetKeyDown(KeyCode.Alpha1)) digit = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) digit = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) digit = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4)) digit = 3;
#endif

        // E забираем себе отдельно от Space/Enter: на E висят ещё дверь и предметы,
        // и именно её нельзя отдавать миру в кадр, когда диалог её же и обработал.
        if (confirmE)
        {
            InputPriority.TryConsume();
        }

        if (!entry.IsChoice)
        {
            if (!confirm)
            {
                return;
            }

            if (fullyRevealed)
            {
                NextEntry();
            }
            else
            {
                // Первое нажатие допечатывает реплику целиком, второе — листает дальше.
                revealed = fullText.Length;
            }

            return;
        }

        // Выбор: цифра отвечает сразу, стрелки двигают подсветку, E/Space/Enter подтверждают её.
        // Без подсветки подтверждение ничего не делает — выбор нельзя «проскочить», листая реплики.
        RefreshVisibleChoices(entry);
        if (visibleChoices.Count == 0)
        {
            if (confirm)
            {
                NextEntry();
            }

            return;
        }

        if (move != 0)
        {
            selectedChoice = selectedChoice < 0
                ? (move > 0 ? 0 : visibleChoices.Count - 1)
                : (selectedChoice + move + visibleChoices.Count) % visibleChoices.Count;
        }

        if (digit >= 0 && digit < visibleChoices.Count)
        {
            PickChoice(entry, visibleChoices[digit]);
        }
        else if (confirm && selectedChoice >= 0 && selectedChoice < visibleChoices.Count)
        {
            PickChoice(entry, visibleChoices[selectedChoice]);
        }
    }

    /// <summary>Индексы вариантов, прошедших условие показа, — в порядке отображения.</summary>
    private void RefreshVisibleChoices(DialogueEntry entry)
    {
        visibleChoices.Clear();
        for (int i = 0; i < entry.choices.Count; i++)
        {
            if (DialogueConditions.Evaluate(entry.choices[i].condition))
            {
                visibleChoices.Add(i);
            }
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
        selectedChoice = -1;
        entryShownAt = Time.unscaledTime;

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

    internal void DiscardForDaySkip()
    {
        script = null;
        onFinished = null;
        pendingFinishId = null;
        queue.Clear();
        InteractionLock.Remove("dialogue");
        PlayerControlLock.RemoveMoveOnly(DialogueLockId);
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
            Begin(next.script, next.finished);
        }
    }

    private System.Collections.IEnumerator RemoveLockNextFrame()
    {
        yield return null; // Ждем 1 кадр, пока "сгорит" нажатие E

        // К этому кадру из очереди мог уже начаться следующий диалог (Finish ->
        // Begin в том же кадре). Замки принадлежат активному диалогу, поэтому
        // снимаем их только если на экране по-прежнему ничего нет.
        if (script != null)
        {
            yield break;
        }

        InteractionLock.Remove("dialogue");
        PlayerControlLock.RemoveMoveOnly(DialogueLockId);
    }

    // ───────────────────────── Отрисовка ─────────────────────────

    private void OnGUI()
    {
        if (script == null)
        {
            return;
        }

        float s = UiTheme.Scale;
        DialogueEntry entry = script.entries[entryIndex];

        // Плавное появление окна: лёгкий подъём снизу и проявление.
        float appear = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.2f);
        appear = 1f - (1f - appear) * (1f - appear);
        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, appear);

        float width = Mathf.Min(Screen.width * 0.72f, 980f * s);
        float pad = 28f * s;
        float x = (Screen.width - width) * 0.5f;
        float bottom = Screen.height - 44f * s + (1f - appear) * 18f * s;

        Rect panel = entry.IsChoice
            ? DrawChoices(entry, x, bottom, width, pad, s)
            : DrawLine(entry, x, bottom, width, pad, s);

        if (!string.IsNullOrEmpty(script.header))
        {
            DrawHeader(script.header, panel, s);
        }

        GUI.color = previousColor;
    }

    private Rect DrawLine(DialogueEntry entry, float x, float bottom, float width, float pad, float s)
    {
        bool hasSpeaker = !string.IsNullOrEmpty(entry.speaker);
        string fullText = entry.text ?? "";
        bool fullyRevealed = revealed >= fullText.Length;
        float innerW = width - pad * 2f;

        // Ремарки и мысли (без говорящего) — холодным курсивом, реплики — обычным текстом.
        GUIStyle textStyle = hasSpeaker
            ? UiTheme.Label(23f, UiTheme.Text, wordWrap: true)
            : UiTheme.Label(22f, UiTheme.Ice, FontStyle.Italic, wordWrap: true);
        textStyle.richText = true;

        float topPad = hasSpeaker ? 36f * s : 24f * s;
        float bottomPad = 40f * s;
        float textHeight = textStyle.CalcHeight(new GUIContent(fullText), innerW);
        float height = Mathf.Max(150f * s, topPad + textHeight + bottomPad);

        Rect panel = new Rect(x, bottom - height, width, height);
        UiTheme.Frame(panel, UiTheme.Night, UiTheme.Cream);

        // Недопечатанный хвост рисуем прозрачным: переносы строк не «прыгают» по мере печати.
        string shown = fullText;
        if (!fullyRevealed)
        {
            int count = Mathf.Clamp(Mathf.FloorToInt(revealed), 0, fullText.Length);
            shown = fullText.Substring(0, count) + "<color=#00000000>" + fullText.Substring(count) + "</color>";
        }

        GUI.Label(new Rect(panel.x + pad, panel.y + topPad, innerW, textHeight), shown, textStyle);

        if (hasSpeaker)
        {
            DrawNamePlate(entry.speaker, panel, s);
        }

        if (fullyRevealed)
        {
            DrawContinueHint(panel, pad, s);
        }

        return panel;
    }

    private Rect DrawChoices(DialogueEntry entry, float x, float bottom, float width, float pad, float s)
    {
        RefreshVisibleChoices(entry);
        float innerW = width - pad * 2f;

        // Над вариантами оставляем реплику, на которую отвечаем, — иначе вопрос пропадает с экрана.
        DialogueEntry question = entryIndex > 0 ? script.entries[entryIndex - 1] : null;
        string questionText = null;
        if (question != null && !question.IsChoice && !string.IsNullOrEmpty(question.text))
        {
            questionText = string.IsNullOrEmpty(question.speaker)
                ? question.text
                : $"{question.speaker}: {question.text}";
        }

        GUIStyle questionStyle = UiTheme.Label(19f, UiTheme.Muted, wordWrap: true);
        GUIStyle choiceStyle = UiTheme.Label(22f, UiTheme.Text, alignment: TextAnchor.MiddleLeft, wordWrap: true);
        GUIStyle badgeStyle = UiTheme.Label(21f, UiTheme.Outline, FontStyle.Bold, TextAnchor.MiddleCenter);
        GUIStyle hintStyle = UiTheme.Label(16f, UiTheme.Muted, alignment: TextAnchor.MiddleRight);

        float badge = 30f * s;
        float rowPad = 12f * s;
        float gap = 8f * s;
        float labelW = innerW - badge - rowPad * 3f;
        float topPad = 34f * s;
        float hintH = 34f * s;

        float questionH = questionText != null
            ? questionStyle.CalcHeight(new GUIContent(questionText), innerW) + 12f * s
            : 0f;

        float rowsH = 0f;
        for (int i = 0; i < visibleChoices.Count; i++)
        {
            rowsH += RowHeight(entry.choices[visibleChoices[i]].label, choiceStyle, labelW, s) + gap;
        }

        float height = topPad + questionH + rowsH + hintH;
        Rect panel = new Rect(x, bottom - height, width, height);
        UiTheme.Frame(panel, UiTheme.Night, UiTheme.Cream);
        DrawNamePlate(AnswerPlate, panel, s);

        float y = panel.y + topPad;
        if (questionText != null)
        {
            GUI.Label(new Rect(panel.x + pad, y, innerW, questionH), questionText, questionStyle);
            y += questionH;
        }

        Event e = Event.current;
        bool mouseFree = Cursor.lockState != CursorLockMode.Locked;

        for (int i = 0; i < visibleChoices.Count; i++)
        {
            DialogueChoice choice = entry.choices[visibleChoices[i]];
            float rowH = RowHeight(choice.label, choiceStyle, labelW, s);
            Rect row = new Rect(panel.x + pad, y, innerW, rowH);

            // Свободный курсор подсвечивает строку под собой; при захваченном работают клавиши.
            if (mouseFree && e.type == EventType.MouseMove && row.Contains(e.mousePosition))
            {
                selectedChoice = i;
            }

            bool selected = i == selectedChoice;
            UiTheme.Frame(row,
                selected ? UiTheme.EmberDark : UiTheme.NightSoft,
                selected ? UiTheme.Ember : UiTheme.Frost);

            Rect badgeRect = new Rect(row.x + rowPad, row.y + (rowH - badge) * 0.5f, badge, badge);
            UiTheme.Fill(badgeRect, selected ? UiTheme.Ember : UiTheme.Cream);
            GUI.Label(badgeRect, (i + 1).ToString(), badgeStyle);

            choiceStyle.normal.textColor = selected ? UiTheme.Cream : UiTheme.Text;
            GUI.Label(new Rect(badgeRect.xMax + rowPad, row.y, labelW, rowH), choice.label, choiceStyle);

            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                e.Use();
                PickChoice(entry, visibleChoices[i]);
                return panel;
            }

            y += rowH + gap;
        }

        string hint = visibleChoices.Count > 1
            ? $"1–{visibleChoices.Count} — ответить  ·  стрелки + E — выбрать"
            : "1 — ответить";
        GUI.Label(new Rect(panel.x + pad, panel.yMax - hintH - 2f * s, innerW, hintH), hint, hintStyle);

        return panel;
    }

    private static float RowHeight(string label, GUIStyle style, float labelWidth, float s)
    {
        return Mathf.Max(46f * s, style.CalcHeight(new GUIContent(label), labelWidth) + 18f * s);
    }

    /// <summary>Плашка с именем «сидит» на верхней кромке панели. Котов — холодный, остальные — тёплые.</summary>
    private static void DrawNamePlate(string speaker, Rect panel, float s)
    {
        GUIStyle style = UiTheme.Label(21f, UiTheme.Outline, FontStyle.Bold, TextAnchor.MiddleCenter);
        float plateW = style.CalcSize(new GUIContent(speaker)).x + 36f * s;
        float plateH = 36f * s;
        Rect plate = new Rect(panel.x + 22f * s, panel.y - plateH * 0.5f, plateW, plateH);

        Color fill = speaker == "КОТОВ" ? UiTheme.Ice : speaker == AnswerPlate ? UiTheme.Cream : UiTheme.Ember;
        UiTheme.Frame(plate, fill, fill);
        GUI.Label(plate, speaker, style);
    }

    /// <summary>Ремарка сцены над окном диалога — на собственной подложке, чтобы читалась на снегу.</summary>
    private static void DrawHeader(string header, Rect panel, float s)
    {
        GUIStyle style = UiTheme.Label(18f, UiTheme.Muted, FontStyle.Italic, TextAnchor.MiddleCenter, true);
        float padX = 18f * s;
        float maxTextW = panel.width - 80f * s - padX * 2f;
        float textW = Mathf.Min(style.CalcSize(new GUIContent(header)).x + 2f, maxTextW);
        float textH = style.CalcHeight(new GUIContent(header), textW);
        float stripW = textW + padX * 2f;
        float stripH = textH + 16f * s;

        Rect strip = new Rect(panel.x + (panel.width - stripW) * 0.5f, panel.y - stripH - 30f * s, stripW, stripH);
        UiTheme.Frame(strip, new Color(UiTheme.Night.r, UiTheme.Night.g, UiTheme.Night.b, 0.8f), UiTheme.Frost);
        GUI.Label(new Rect(strip.x + padX, strip.y, textW, stripH), header, style);
    }

    /// <summary>Покачивающаяся стрелка «дальше» и подпись клавиши.</summary>
    private void DrawContinueHint(Rect panel, float pad, float s)
    {
        GUIStyle hintStyle = UiTheme.Label(16f, UiTheme.Muted, alignment: TextAnchor.MiddleRight);
        int px = UiTheme.PixelSize;
        float bob = Mathf.Round(Mathf.PingPong((Time.unscaledTime - entryShownAt) * 2.4f, 1f) * 2f) * px;

        float arrowW = 7 * px;
        float arrowX = panel.xMax - pad - arrowW;
        float arrowY = panel.yMax - 28f * s + bob;
        for (int i = 0; i < 4; i++)
        {
            UiTheme.Fill(new Rect(arrowX + i * px, arrowY + i * px, arrowW - i * 2 * px, px), UiTheme.Ember);
        }

        GUI.Label(new Rect(panel.x + pad, panel.yMax - 38f * s, panel.width - pad * 2f - arrowW - 10f * s, 28f * s),
            "E / Space", hintStyle);
    }
}
