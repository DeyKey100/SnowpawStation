using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сюжетное состояние нулевой версии (дни 1-3). Статический класс: переживает смену сцен
/// (StationMain / GasStationInterior), не зависит от объектов сцены и не создаёт дубликатов.
/// Сбрасывается только при явном ResetRun (новый прогон) — не при загрузке сцены.
/// </summary>
public static class GameState
{
    public const int DayCount = 3;
    public const int LetterCount = 3;

    // --- обязательные сюжетные флаги ---
    public static readonly bool[] LetterClarity = new bool[LetterCount];
    public static readonly bool[] LettersReceived = new bool[LetterCount];
    public static readonly bool[] GazeHeld = new bool[LetterCount];
    public static bool NoticedBarePaw { get; private set; }
    public static bool RedPickupHelped { get; private set; }
    public static bool HasGlove { get; private set; }

    // --- служебное состояние дня ---
    private static readonly HashSet<string> DoneDayEvents = new HashSet<string>();
    private static readonly List<string> Observations = new List<string>();
    private static readonly List<string> Notes = new List<string>();
    private static readonly List<string> ShiftLog = new List<string>();

    public static event Action FlagsChanged;
    public static event Action<int> LetterReceived;
    public static event Action<string> ObservationAdded;
    public static event Action<string> NoteAdded;

    /// <summary>Получено ли письмо №index (1-3).</summary>
    public static bool HasLetter(int letterNumber)
    {
        return IsValid(letterNumber) && LettersReceived[letterNumber - 1];
    }

    /// <summary>Чёткий ли текст письма №index (1-3).</summary>
    public static bool IsLetterClear(int letterNumber)
    {
        return IsValid(letterNumber) && LetterClarity[letterNumber - 1];
    }

    /// <summary>
    /// Выдать письмо. Повторный вызов не дублирует письмо и не меняет уже записанную чёткость.
    /// </summary>
    /// <returns>true, если письмо выдано этим вызовом.</returns>
    public static bool GiveLetter(int letterNumber, bool clear)
    {
        if (!IsValid(letterNumber))
        {
            Debug.LogError($"GameState.GiveLetter: недопустимый номер письма {letterNumber}.");
            return false;
        }

        int index = letterNumber - 1;
        if (LettersReceived[index])
        {
            Debug.LogWarning($"GameState: письмо №{letterNumber} уже получено, повторный вызов проигнорирован.");
            return false;
        }

        LettersReceived[index] = true;
        LetterClarity[index] = clear;
        LetterReceived?.Invoke(letterNumber);
        FlagsChanged?.Invoke();
        Debug.Log($"[Snowpaw Station] Письмо №{letterNumber} получено (чёткое: {clear}).");
        return true;
    }

    public static void SetGazeHeld(int letterNumber, bool value)
    {
        if (!IsValid(letterNumber) || GazeHeld[letterNumber - 1] == value)
        {
            return;
        }

        GazeHeld[letterNumber - 1] = value;
        FlagsChanged?.Invoke();
        Debug.Log($"[Snowpaw Station] gazeHeld[{letterNumber}] = {value}.");
    }

    public static void SetNoticedBarePaw(bool value)
    {
        if (NoticedBarePaw == value)
        {
            return;
        }

        NoticedBarePaw = value;
        FlagsChanged?.Invoke();
        if (value)
        {
            AddObservation("Котлета уехал в одной перчатке.");
        }
    }

    public static void SetRedPickupHelped(bool value)
    {
        if (RedPickupHelped == value)
        {
            return;
        }

        RedPickupHelped = value;
        FlagsChanged?.Invoke();
        if (value)
        {
            AddObservation("Красному пикапу помогли: канистра и грелка.");
        }
    }

    public static void SetHasGlove(bool value)
    {
        if (HasGlove == value)
        {
            return;
        }

        HasGlove = value;
        FlagsChanged?.Invoke();
        Debug.Log($"[Snowpaw Station] hasGlove = {value}.");
    }

    /// <summary>Отогреть письмо у печи: нечёткое письмо становится чётким.</summary>
    public static void WarmLetter(int letterNumber)
    {
        if (!IsValid(letterNumber) || !LettersReceived[letterNumber - 1] || LetterClarity[letterNumber - 1])
        {
            return;
        }

        LetterClarity[letterNumber - 1] = true;
        FlagsChanged?.Invoke();
        Debug.Log($"[Snowpaw Station] Письмо №{letterNumber} отогрето у печи: текст чёткий.");
    }

    /// <summary>Одноразовая отметка события дня. true = событие ещё не происходило.</summary>
    public static bool MarkDayEventOnce(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            Debug.LogError("GameState.MarkDayEventOnce: пустой id события.");
            return false;
        }

        if (DoneDayEvents.Contains(eventId))
        {
            return false;
        }

        DoneDayEvents.Add(eventId);
        return true;
    }

    public static bool IsDayEventDone(string eventId)
    {
        return !string.IsNullOrEmpty(eventId) && DoneDayEvents.Contains(eventId);
    }

    public static void AddObservation(string text)
    {
        if (string.IsNullOrEmpty(text) || Observations.Contains(text))
        {
            return;
        }

        Observations.Add(text);
        ObservationAdded?.Invoke(text);
        FlagsChanged?.Invoke();
    }

    public static void AddNote(string text)
    {
        if (string.IsNullOrEmpty(text) || Notes.Contains(text))
        {
            return;
        }

        Notes.Add(text);
        NoteAdded?.Invoke(text);
        FlagsChanged?.Invoke();
    }

    public static void AddShiftLog(string text)
    {
        if (string.IsNullOrEmpty(text) || ShiftLog.Contains(text))
        {
            return;
        }

        ShiftLog.Add(text);
        FlagsChanged?.Invoke();
    }

    public static IReadOnlyList<string> GetObservations() => Observations;
    public static IReadOnlyList<string> GetNotes() => Notes;
    public static IReadOnlyList<string> GetShiftLog() => ShiftLog;

    /// <summary>Полный сброс прогона: чистый запуск с дня 1.</summary>
    public static void ResetRun()
    {
        for (int i = 0; i < LetterCount; i++)
        {
            LetterClarity[i] = false;
            LettersReceived[i] = false;
            GazeHeld[i] = false;
        }

        NoticedBarePaw = false;
        RedPickupHelped = false;
        HasGlove = false;
        DoneDayEvents.Clear();
        Observations.Clear();
        Notes.Clear();
        ShiftLog.Clear();
        FlagsChanged?.Invoke();
        Debug.Log("[Snowpaw Station] GameState сброшен: новый прогон.");
    }

    private static bool IsValid(int letterNumber)
    {
        return letterNumber >= 1 && letterNumber <= LetterCount;
    }
}
