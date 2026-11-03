using UnityEngine;

/// <summary>
/// Колода (поленница): E запускает мини-игру колки (ЛКМ в зелёной зоне).
/// Полено попадает в лапы (до 3) и идёт в зачёт утренней задачи chop_logs.
/// </summary>
public class WoodPileInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private FirewoodStorage storage;
    private WoodChoppingMinigame minigame;

    public string GetInteractPrompt()
    {
        FirewoodStorage s = Storage;
        if (s == null)
        {
            return string.Empty;
        }

        return s.CanAddFirewood ? "[E] Колоть полено" : "Лапы полны (до 3 поленьев)";
    }

    private FirewoodStorage Storage
    {
        get
        {
            if (storage == null)
            {
                // Перезагруженная сцена: сериализованная ссылка указывает на уничтоженный дубль GameSystems.
                storage = FirewoodStorage.Active;
            }

            return storage;
        }
    }

    public void Interact()
    {
        FirewoodStorage s = Storage;
        if (s == null)
        {
            Debug.LogError("WoodPileInteractable: FirewoodStorage не найден.", this);
            return;
        }

        if (!s.CanAddFirewood)
        {
            return;
        }

        if (minigame == null)
        {
            minigame = gameObject.AddComponent<WoodChoppingMinigame>();
        }

        if (minigame.IsActive)
        {
            return;
        }

        minigame.Begin(() =>
        {
            if (s.TryAddFirewood())
            {
                DayLoopManager.Instance?.NotifyMorningTaskDone("chop_logs");
                Debug.Log("[Snowpaw Station] Полено нарублено (в лапах " + s.CurrentFirewood + "/3).");
            }
        });
    }
}
