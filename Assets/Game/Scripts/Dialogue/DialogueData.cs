using System;
using System.Collections.Generic;

/// <summary>Одна реплика: говорящий (пусто = ремарка/мысль от автора) и текст.</summary>
[Serializable]
public class DialogueLine
{
    public string speaker;
    public string text;

    public DialogueLine(string speaker, string text)
    {
        this.speaker = speaker;
        this.text = text;
    }
}

/// <summary>Вариант ответа игрока. Условие/действие — строковые id из реестров ниже.</summary>
[Serializable]
public class DialogueChoice
{
    public string label;
    public string response;
    public string condition;
    public string action;
}

/// <summary>Элемент сценария диалога: реплика, ремарка или выбор.</summary>
[Serializable]
public class DialogueEntry
{
    public string speaker;
    public string text;
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    public bool IsChoice => choices != null && choices.Count > 0;
}

/// <summary>
/// Сценарий диалога. Строится кодом (StoryDialogues) и передаётся DialogueManager.
/// condition-строки решает DialogueConditions, action-строки — DialogueActions.
/// </summary>
public class DialogueScript
{
    public string id;
    public string header;
    public List<DialogueEntry> entries = new List<DialogueEntry>();
}

public static class DialogueBuilder
{
    public static DialogueScript Create(string id, string header = null)
    {
        return new DialogueScript { id = id, header = header };
    }

    public static DialogueScript Say(this DialogueScript script, string speaker, string text)
    {
        script.entries.Add(new DialogueEntry { speaker = speaker, text = text });
        return script;
    }

    public static DialogueScript Note(this DialogueScript script, string text)
    {
        script.entries.Add(new DialogueEntry { speaker = "", text = text });
        return script;
    }

    public static DialogueScript Choose(this DialogueScript script, params DialogueChoice[] choices)
    {
        script.entries.Add(new DialogueEntry { choices = new List<DialogueChoice>(choices) });
        return script;
    }

    public static DialogueChoice Option(string label, string response = null, string condition = null, string action = null)
    {
        return new DialogueChoice { label = label, response = response, condition = condition, action = action };
    }
}

/// <summary>
/// Условия показа вариантов ответа. Реестр строковых id → bool.
/// Неизвестное условие = false + LogError (не прятать опечатку).
/// </summary>
public static class DialogueConditions
{
    private static readonly Dictionary<string, Func<bool>> Resolvers = new Dictionary<string, Func<bool>>
    {
        // Котов, день 1: «Хорошо у тебя печь дышит» / «Холодно у тебя»
        { "day1_stove_lit", () => DayLoopManager.Instance != null && DayLoopManager.Instance.IsStoveLitToday() },
        // Котов, день 2: «Ты чистишь. Я вижу» / «Снег ждать не будет»
        { "day2_morning_done", () => DayLoopManager.Instance != null && DayLoopManager.Instance.IsMorningDone(2) },
        // Котов, день 3: дровник
        { "day3_woodshed_full", () => StoryDirector.Instance != null && StoryDirector.Instance.WoodShedCount >= 6 },
        { "day3_woodshed_low", () => StoryDirector.Instance == null || StoryDirector.Instance.WoodShedCount < 6 },
        // Красный пикап: пустить к печи
        { "heat_at_least_50", () => StationHeatSystem.Active != null && StationHeatSystem.Active.CurrentHeat >= 50f },
        { "heat_below_50", () => StationHeatSystem.Active == null || StationHeatSystem.Active.CurrentHeat < 50f },
        // Кофе для Котлеты: горячий при тепле от 25%
        { "kettle_hot", () => StationHeatSystem.Active != null && StationHeatSystem.Active.CurrentHeat >= 25f },
        // Видел ли голую лапу Котлеты (мысль у силуэта)
        { "noticed_bare_paw", () => GameState.NoticedBarePaw },
        // Перчатка уже взята (не показывать повторно)
        { "glove_not_taken", () => !GameState.HasGlove },
    };

    public static bool Evaluate(string condition)
    {
        if (string.IsNullOrEmpty(condition))
        {
            return true;
        }

        if (Resolvers.TryGetValue(condition, out Func<bool> resolver))
        {
            return resolver();
        }

        UnityEngine.Debug.LogError($"DialogueConditions: неизвестное условие '{condition}'.");
        return false;
    }
}

/// <summary>
/// Действия по выбору игрока (id → обработчик). Unknown id = LogError.
/// </summary>
public static class DialogueActions
{
    private static readonly Dictionary<string, Action> Handlers = new Dictionary<string, Action>();

    public static void Register(string id, Action handler)
    {
        if (string.IsNullOrEmpty(id) || handler == null)
        {
            return;
        }

        Handlers[id] = handler;
    }

    public static void Clear()
    {
        Handlers.Clear();
    }

    public static void Run(string action)
    {
        if (string.IsNullOrEmpty(action))
        {
            return;
        }

        if (Handlers.TryGetValue(action, out Action handler))
        {
            handler();
            return;
        }

        UnityEngine.Debug.LogError($"DialogueActions: неизвестное действие '{action}'.");
    }
}
