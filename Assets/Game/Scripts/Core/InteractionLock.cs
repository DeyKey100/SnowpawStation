using System.Collections.Generic;

/// <summary>
/// Центральный замок взаимодействия: диалог, мини-игра колки, заправка, ночные сценки.
/// PlayerInteraction и триггеры дверей проверяют IsLocked и не реагируют на E.
/// </summary>
public static class InteractionLock
{
    private static readonly HashSet<string> Holders = new HashSet<string>();

    public static bool IsLocked => Holders.Count > 0;

    /// <summary>Добавить держателя замка. Повторное добавление того же id безопасно.</summary>
    public static void Add(string holder)
    {
        if (string.IsNullOrEmpty(holder))
        {
            return;
        }

        Holders.Add(holder);
    }

    /// <summary>Снять держателя. Снятие незанятого id — без эффекта.</summary>
    public static void Remove(string holder)
    {
        Holders.Remove(holder);
    }

    /// <summary>Аварийное снятие всех держателей (смена дня/сцены).</summary>
    public static void Reset()
    {
        Holders.Clear();
    }
}
