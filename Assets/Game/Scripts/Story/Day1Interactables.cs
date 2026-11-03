using UnityEngine;

/// <summary>Открытка, оставшаяся на пороге после исчезновения Котова (взгляд отведён).</summary>
[DisallowMultipleComponent]
public sealed class DoorstepLetterInteractable : MonoBehaviour, IInteractable
{
    private int letterDay = 1;

    public void Setup(int day) => letterDay = day;

    public string GetInteractPrompt() => "[E] Взять открытку";

    public void Interact()
    {
        StoryDirector.Instance?.Kotov?.OnDoorstepLetterTaken(letterDay);
        Destroy(gameObject);
    }
}

/// <summary>Записка предыдущего смотрителя под кружкой (задача note_read, день 1).</summary>
[DisallowMultipleComponent]
public sealed class KeeperNoteInteractable : MonoBehaviour, IInteractable
{
    /// <summary>Записку прочитали: повторно не спавнится после смены сцены.</summary>
    public const string ReadEventId = "keeper_note_read";

    private bool read;

    public string GetInteractPrompt() => read ? "[E] Перечитать записку" : "[E] Прочитать записку";

    public void Interact()
    {
        DialogueScript s = DialogueBuilder.Create("keeper_note", "Записка предыдущего смотрителя:");
        s.Note("«Корми печь, руби дрова во дворе, держи дорожки чистыми. Перевал не прощает холода».");
        s.Note("*«Перевал не прощает холода». Ободряюще.*");
        s.Choose(DialogueBuilder.Option("Убрать записку", null, null, null));

        // Запоминаем объект: к концу диалога сцену могли сменить и компонент уже уничтожен.
        GameObject noteObject = gameObject;

        if (StoryDirector.Instance != null && StoryDirector.Instance.Dialogue != null)
        {
            StoryDirector.Instance.Dialogue.Show(s, () => RemoveAfterReading(noteObject));
        }

        if (!read)
        {
            read = true;
            GameState.MarkDayEventOnce(ReadEventId);
            DayLoopManager.Instance?.NotifyMorningTaskDone("note_read");
        }
    }

    /// <summary>Записка убрана в карман: с земли не валяется.</summary>
    private static void RemoveAfterReading(GameObject noteObject)
    {
        GameState.AddNote("Записка смотрителя: «Перевал не прощает холода».");

        if (noteObject != null)
        {
            Destroy(noteObject);
        }
    }
}

/// <summary>Дровник: до 6 поленьев. E — переложить всё из лап в дровник.</summary>
[DisallowMultipleComponent]
public sealed class WoodShedInteractable : MonoBehaviour, IInteractable
{
    public string GetInteractPrompt()
    {
        int carried = FirewoodStorage.Active != null ? FirewoodStorage.Active.CurrentFirewood : 0;
        return carried > 0 ? $"Сложить поленья в дровник ({StoryDirector.Instance?.WoodShedCount ?? 0}/{StoryDirector.MaxWoodShed})" : "Дровник";
    }

    public void Interact()
    {
        FirewoodStorage storage = FirewoodStorage.Active;
        StoryDirector director = StoryDirector.Instance;
        if (storage == null || director == null)
        {
            return;
        }

        int moved = 0;
        while (storage.CurrentFirewood > 0 && director.WoodShedCount < StoryDirector.MaxWoodShed)
        {
            if (storage.TryConsumeFirewood())
            {
                moved++;
            }
            else
            {
                break;
            }
        }

        if (moved > 0)
        {
            director.AddToWoodShed(moved);
        }
    }
}
