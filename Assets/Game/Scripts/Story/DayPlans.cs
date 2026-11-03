using System;
using System.Collections.Generic;

/// <summary>Утренняя задача дня нулевой версии.</summary>
[Serializable]
public class MorningTaskDef
{
    public string id;
    public string label;
    public int target = 1;
    public bool required = true;
}

/// <summary>Сюжетный гость дня: заказ и расписание появления.</summary>
[Serializable]
public class GuestDef
{
    public string id;
    public string displayName;
    public float liters;
    public string item;          // id товара или null
    /// <summary>after_quota — после квоты обычных машин; after_first_car — после первой машины дня.</summary>
    public string schedule = "after_quota";
    public int pumpIndex = 0;    // 0/1 — к какой колонке встаёт
}

/// <summary>Обычная машина дня (квота нулевой версии: 2 в день).</summary>
[Serializable]
public class RegularCarDef
{
    public string displayName;
    public float liters;
}

/// <summary>
/// Канонические данные дней 1-3 из NEWstory1-2-3day.md. Код, не ScriptableObject:
/// значения фиксированы сценарием и не правятся в инспекторе, а как [Serializable]
/// классы остаются сериализуемыми и переносятся в SO без переписывания.
/// </summary>
public static class DayPlans
{
    public const int DayCount = 3;

    private static readonly List<MorningTaskDef> Day1Tasks = new List<MorningTaskDef>
    {
        new MorningTaskDef { id = "note_read", label = "Осмотреть дом и найти записку" },
        new MorningTaskDef { id = "chop_logs", label = "Нарубить поленья у колоды", target = 1 },
        new MorningTaskDef { id = "stove_lit", label = "Растопить печь" },
        new MorningTaskDef { id = "snow_door", label = "Расчистить сугроб у двери" },
    };

    private static readonly List<MorningTaskDef> Day2Tasks = new List<MorningTaskDef>
    {
        new MorningTaskDef { id = "chop_logs", label = "Нарубить 3 полена", target = 3 },
        new MorningTaskDef { id = "stove_lit", label = "Разжечь остывшую печь" },
        new MorningTaskDef { id = "snow_pump1", label = "Расчистить сугроб у колонки №1" },
    };

    private static readonly List<MorningTaskDef> Day3Tasks = new List<MorningTaskDef>
    {
        new MorningTaskDef { id = "woodshed_6", label = "Наполнить дровник до 6 поленьев", target = 6 },
        new MorningTaskDef { id = "keep_warm", label = "Держать тепло в метель (необязательно)", required = false },
    };

    private static readonly Dictionary<int, DayPlanDef> Plans = new Dictionary<int, DayPlanDef>
    {
        {
            1, new DayPlanDef
            {
                day = 1,
                quota = 2,
                morningTasks = Day1Tasks,
                regularCars = new List<RegularCarDef>
                {
                    new RegularCarDef { displayName = "Легковушка", liters = 35f },
                    new RegularCarDef { displayName = "Микроавтобус", liters = 50f },
                },
                guests = new List<GuestDef>
                {
                    new GuestDef { id = "kotleta", displayName = "Дядя Котлета", liters = 80f, item = "coffee", schedule = "after_quota", pumpIndex = 1 },
                },
            }
        },
        {
            2, new DayPlanDef
            {
                day = 2,
                quota = 2,
                morningTasks = Day2Tasks,
                regularCars = new List<RegularCarDef>
                {
                    new RegularCarDef { displayName = "Каблучок", liters = 45f },
                    new RegularCarDef { displayName = "Внедорожник", liters = 25f },
                },
                guests = new List<GuestDef>
                {
                    new GuestDef { id = "courier", displayName = "Курьер ВБ", liters = 30f, item = "cookies", schedule = "after_quota", pumpIndex = 0 },
                },
            }
        },
        {
            3, new DayPlanDef
            {
                day = 3,
                quota = 2,
                morningTasks = Day3Tasks,
                regularCars = new List<RegularCarDef>
                {
                    new RegularCarDef { displayName = "Фургон", liters = 30f },
                    new RegularCarDef { displayName = "Старый седан", liters = 40f },
                },
                guests = new List<GuestDef>
                {
                    // Данила — после первой машины дня; красный пикап — после квоты, в сумерках.
                    new GuestDef { id = "danila", displayName = "Данила", liters = 20f, item = "cigarettes", schedule = "after_first_car", pumpIndex = 0 },
                    new GuestDef { id = "red_pickup", displayName = "Красный пикап", liters = 10f, item = "heater", schedule = "after_quota", pumpIndex = 1 },
                },
            }
        },
    };

    public static bool IsValidDay(int day) => day >= 1 && day <= DayCount;

    public static int GetRegularCarQuota(int day) => Plans[Clamp(day)].quota;

    public static List<MorningTaskDef> GetRawMorningTasks(int day) => Plans[Clamp(day)].morningTasks;

    public static List<DayPlan.MorningTask> GetMorningTasks(int day)
    {
        List<MorningTaskDef> raw = GetRawMorningTasks(day);
        List<DayPlan.MorningTask> result = new List<DayPlan.MorningTask>(raw.Count);
        for (int i = 0; i < raw.Count; i++)
        {
            result.Add(new DayPlan.MorningTask
            {
                id = raw[i].id,
                label = raw[i].label,
                target = raw[i].target,
                required = raw[i].required,
            });
        }

        return result;
    }

    public static List<string> GetGuestSchedule(int day) => Plans[Clamp(day)].guestIds;

    public static List<GuestDef> GetGuests(int day) => Plans[Clamp(day)].guests;

    public static GuestDef FindGuest(string guestId)
    {
        foreach (KeyValuePair<int, DayPlanDef> pair in Plans)
        {
            List<GuestDef> guests = pair.Value.guests;
            for (int i = 0; i < guests.Count; i++)
            {
                if (guests[i].id == guestId)
                {
                    return guests[i];
                }
            }
        }

        return null;
    }

    public static List<RegularCarDef> GetRegularCars(int day) => Plans[Clamp(day)].regularCars;

    private static int Clamp(int day) => MathfClamp(day);

    private static int MathfClamp(int day)
    {
        if (day < 1)
        {
            return 1;
        }

        return day > DayCount ? DayCount : day;
    }

    [Serializable]
    private class DayPlanDef
    {
        public int day;
        public int quota = 2;
        public List<MorningTaskDef> morningTasks = new List<MorningTaskDef>();
        public List<RegularCarDef> regularCars = new List<RegularCarDef>();
        public List<GuestDef> guests = new List<GuestDef>();

        public List<string> guestIds
        {
            get
            {
                List<string> ids = new List<string>(guests.Count);
                for (int i = 0; i < guests.Count; i++)
                {
                    ids.Add(guests[i].id);
                }

                return ids;
            }
        }
    }
}

/// <summary>Точка стыковки с DayLoopManager (именованные задачи).</summary>
public static class DayPlan
{
    [Serializable]
    public class MorningTask
    {
        public string id;
        public string label;
        public int target = 1;
        public bool required = true;
    }
}
