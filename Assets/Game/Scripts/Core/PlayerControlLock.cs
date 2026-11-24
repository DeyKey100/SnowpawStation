using System;
using System.Collections.Generic;

/// <summary>
/// Замки управления персонажем. Держателей два вида:
///
/// 1. Полный замок (Add/Remove) — не двигает и не крутит камеру. Это заправка,
///    сон, рубка дров: игрок стоит у колонки, пока идёт отпуск топлива.
/// 2. Замок движения (AddMoveOnly/RemoveMoveOnly) — не даёт идти, но камера
///    остаётся живой. Это диалог: с Котовым можно оглянуться, но не уйти
///    посреди разговора.
///
/// Это отдельные замки, а не InteractionLock: InteractionLock лишь запрещает
/// нажатия [E] и на ходьбу не влияет.
/// </summary>
public static class PlayerControlLock
{
    private static readonly HashSet<string> Holders = new HashSet<string>();
    private static readonly HashSet<string> MoveHolders = new HashSet<string>();

    public static event Action<bool> LockStateChanged;

    /// <summary>Управление заморожено полностью (хотя бы один держатель).</summary>
    public static bool IsLocked => Holders.Count > 0;

    /// <summary>
    /// Идти нельзя: либо полный замок, либо кто-то держит только движение.
    /// Камеру это не трогает — её запрещает лишь <see cref="IsLocked"/>.
    /// </summary>
    public static bool IsMoveLocked => Holders.Count > 0 || MoveHolders.Count > 0;

    internal static bool IsHeldBy(string holder) => Holders.Contains(holder) || MoveHolders.Contains(holder);

    /// <summary>Взвести замок. Повторное добавление того же id безопасно.</summary>
    public static void Add(string holder)
    {
        if (string.IsNullOrEmpty(holder))
        {
            return;
        }

        if (Holders.Add(holder))
        {
            LockStateChanged?.Invoke(true);
        }
    }

    /// <summary>Снять держателя. Снятие незанятого id — без эффекта.</summary>
    public static void Remove(string holder)
    {
        if (Holders.Remove(holder) && Holders.Count == 0)
        {
            LockStateChanged?.Invoke(false);
        }
    }

    /// <summary>Запретить только ходьбу; камера остаётся управляемой.</summary>
    public static void AddMoveOnly(string holder)
    {
        if (string.IsNullOrEmpty(holder))
        {
            return;
        }

        if (MoveHolders.Add(holder))
        {
            LockStateChanged?.Invoke(true);
        }
    }

    /// <summary>Снять запрет ходьбы.</summary>
    public static void RemoveMoveOnly(string holder)
    {
        if (MoveHolders.Remove(holder) && MoveHolders.Count == 0 && Holders.Count == 0)
        {
            LockStateChanged?.Invoke(false);
        }
    }

    /// <summary>Аварийное снятие всех держателей (смена дня, чтобы не остаться замороженным).</summary>
    public static void Reset()
    {
        if (Holders.Count == 0 && MoveHolders.Count == 0)
        {
            return;
        }

        Holders.Clear();
        MoveHolders.Clear();
        LockStateChanged?.Invoke(false);
    }
}
