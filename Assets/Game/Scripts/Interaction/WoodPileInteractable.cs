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

        // Записка — первая задача утра: пока её не прочли, колода просто не предлагает взаимодействие.
        if (!IsUnlocked())
        {
            return string.Empty;
        }

        return s.CanAddFirewood ? "[E] Колоть полено" : "Лапы полны (до 3 поленьев)";
    }

    private static bool IsUnlocked()
    {
        DayLoopManager loop = DayLoopManager.Instance;
        return loop == null || loop.IsMorningTaskUnlocked("chop_logs");
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

        if (!IsUnlocked())
        {
            Debug.Log("[Snowpaw Station] Колоть дрова пока рано: сначала записка в доме.");
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

        minigame.Begin(() => AddChoppedLog(s));
    }

    internal bool TryChopImmediately()
    {
        FirewoodStorage target = Storage;
        return target != null && target.CanAddFirewood && IsUnlocked() && AddChoppedLog(target);
    }

    private static bool AddChoppedLog(FirewoodStorage target)
    {
        if (target == null || !target.TryAddFirewood())
        {
            return false;
        }

        DayLoopManager loop = DayLoopManager.Instance;
        if (loop != null && loop.CurrentState == DayLoopManager.DayState.Morning)
        {
            var tasks = DayPlans.GetRawMorningTasks(loop.CurrentDay);
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].id == "chop_logs")
                {
                    loop.NotifyMorningTaskDone("chop_logs");
                    break;
                }
            }
        }

        return true;
    }
}
