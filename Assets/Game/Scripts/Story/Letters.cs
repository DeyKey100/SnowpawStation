using System.Collections.Generic;

/// <summary>
/// Письма №1-3 (текст дословно из NEWstory1-2-3day.md), условия чёткости и обмороженный вид.
/// </summary>
public static class Letters
{
    public const int Count = 3;

    private static readonly string[] Bodies =
    {
        "Спасибо, что согласился приехать, Рыжиков.",
        "Молодец, что чистишь снег и держишь огонь. Но помни: правда открывается тому, кто умеет смотреть.",
        "Сегодня ветер будет выть особенно сильно. Запаси дрова, не дай огню погаснуть.",
    };

    private static readonly string[] Descriptions =
    {
        "Открытка с заснеженными пиками, без марки.",
        "Конверт без марки, бледный штемпель «Перевальное п/о», дата стёрта.",
        "Серый конверт, сухой, хотя на улице метель.",
    };

    public static string GetBody(int letterNumber)
    {
        return IsValid(letterNumber) ? Bodies[letterNumber - 1] : null;
    }

    public static string GetDescription(int letterNumber)
    {
        return IsValid(letterNumber) ? Descriptions[letterNumber - 1] : null;
    }

    /// <summary>
    /// Чёткость текста на момент выдачи письма:
    /// №1 — печь растоплена и сугроб у двери убран;
    /// №2 — все 3 утренние задачи и среднее тепло за день не ниже 50%;
    /// №3 — к 18:00 в дровнике от 6 поленьев.
    /// </summary>
    public static bool EvaluateClarity(int letterNumber)
    {
        DayLoopManager loop = DayLoopManager.Instance;
        if (loop == null)
        {
            return false;
        }

        switch (letterNumber)
        {
            case 1:
                return loop.GetTaskProgress("stove_lit") >= 1 && loop.GetTaskProgress("snow_door") >= 1;
            case 2:
                return loop.IsMorningDone(2) &&
                       StationHeatSystem.Active != null &&
                       StationHeatSystem.Active.AverageHeatToday >= 50f;
            case 3:
                return StoryDirector.Instance != null && StoryDirector.Instance.WoodShedCount >= 6;
            default:
                return false;
        }
    }

    /// <summary>Текст с инеем, если письмо нечёткое (первая фраза поплыла / часть текста в инее).</summary>
    public static string RenderForNotebook(int letterNumber)
    {
        if (!IsValid(letterNumber))
        {
            return null;
        }

        string body = Bodies[letterNumber - 1];
        if (GameState.IsLetterClear(letterNumber))
        {
            return body;
        }

        // Нечёткое: первая фраза замаскирована, остальной текст читается.
        int cut = body.IndexOf('.');
        if (cut <= 0)
        {
            cut = body.Length / 2;
        }

        string masked = new string('▒', MathfMin(cut + 1, body.Length));
        return masked + body.Substring(MathfMin(cut + 1, body.Length)) + "\n<i>Часть текста в инее. Можно отогреть у печи (тепло от 60%).</i>";
    }

    /// <summary>Отогреть письмо у печи: переводит нечёткое письмо в чёткое при тепле ≥ 60%.</summary>
    public static bool TryWarmUp(int letterNumber)
    {
        if (!GameState.HasLetter(letterNumber) || GameState.IsLetterClear(letterNumber))
        {
            return false;
        }

        if (StationHeatSystem.Active == null || StationHeatSystem.Active.CurrentHeat < 60f)
        {
            return false;
        }

        GameState.WarmLetter(letterNumber);
        return true;
    }

    private static bool IsValid(int letterNumber) => letterNumber >= 1 && letterNumber <= Count;

    private static int MathfMin(int a, int b) => a < b ? a : b;
}

/// <summary>Товары магазина нулевой версии.</summary>
public static class Goods
{
    public class GoodDef
    {
        public string id;
        public string displayName;
        public bool needsKettle; // кофе/грелка: напиток горячий при тепле от 25%
    }

    public static readonly List<GoodDef> All = new List<GoodDef>
    {
        new GoodDef { id = "coffee", displayName = "Кофе", needsKettle = true },
        new GoodDef { id = "tea", displayName = "Чай", needsKettle = true },
        new GoodDef { id = "cookies", displayName = "Печенье" },
        new GoodDef { id = "cigarettes", displayName = "Сигареты" },
        new GoodDef { id = "canister", displayName = "Канистра", needsKettle = false },
        new GoodDef { id = "heater", displayName = "Грелка", needsKettle = true },
    };

    public static GoodDef Find(string id)
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].id == id)
            {
                return All[i];
            }
        }

        return null;
    }

    public static string DisplayName(string id)
    {
        GoodDef def = Find(id);
        return def != null ? def.displayName : id;
    }
}
