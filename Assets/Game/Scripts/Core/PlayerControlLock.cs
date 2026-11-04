using System.Collections.Generic;

/// <summary>
/// Замок управления персонажем: пока он взведён, FirstPersonController не двигает
/// игрока и не крутит камеру. Это отдельный замок, а не InteractionLock: InteractionLock
/// лишь запрещает нажатия [E], и во время диалога ходить можно, а во время заправки —
/// нельзя, колонку надо отстоять на месте.
/// </summary>
public static class PlayerControlLock
{
    private static readonly HashSet<string> Holders = new HashSet<string>();

    /// <summary>Управление заморожено (хотя бы один держатель).</summary>
    public static bool IsLocked => Holders.Count > 0;

    /// <summary>Взвести замок. Повторное добавление того же id безопасно.</summary>
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

    /// <summary>Аварийное снятие всех держателей (смена дня, чтобы не остаться замороженным).</summary>
    public static void Reset()
    {
        Holders.Clear();
    }
}
