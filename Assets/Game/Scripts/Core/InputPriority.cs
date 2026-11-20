using UnityEngine;

/// <summary>
/// Очередь на одну клавишу за кадр. Клавиша E одна, а желающих много: диалог,
/// дверь, предмет под ногами, колонка. Раньше каждый читал Input.GetKeyDown(E)
/// самостоятельно и полагался только на InteractionLock, поэтому в кадре, когда
/// замок уже снят (или сброшен сменой дня), одно нажатие успевало и закрыть
/// диалог, и открыть дверь.
///
/// Теперь нажатие забирает первый, кто до него дошёл, а порядок задан явными
/// Execution Order: диалог — самый ранний и потому всегда выигрывает у мира.
/// </summary>
public static class InputPriority
{
    private static int consumedFrame = -1;

    /// <summary>Клавишу уже разобрали в этом кадре — остальным нажатие не достаётся.</summary>
    public static bool IsConsumed => consumedFrame == Time.frameCount;

    /// <summary>Забрать нажатие себе. false — в этом кадре его уже забрал кто-то выше по приоритету.</summary>
    public static bool TryConsume()
    {
        if (consumedFrame == Time.frameCount)
        {
            return false;
        }

        consumedFrame = Time.frameCount;
        return true;
    }

    /// <summary>Забыть про кадр (на случай остановки Play Mode без перезагрузки домена).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        consumedFrame = -1;
    }
}
