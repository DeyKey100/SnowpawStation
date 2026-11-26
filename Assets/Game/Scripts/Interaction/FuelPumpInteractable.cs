using UnityEngine;

/// <summary>
/// Колонка. E — открыть мини-игру заправки по текущему заказу: открыть лючок,
/// взять пистолет, вставить в горловину, удерживать ЛКМ, вернуть пистолет и
/// закрыть лючок. Подсказка появляется только у той колонки, к которой подъехал
/// посетитель: пустые колонки молчат. Замки "refuel" ставятся сразу на E и
/// держатся до конца мини-игры, поэтому уйти от колонки посреди заправки нельзя.
/// </summary>
[DisallowMultipleComponent]
public sealed class FuelPumpInteractable : MonoBehaviour, IInteractable
{
    private ServiceDirector service;
    private bool fueling;
    private TruckArrivalController truckArrival;

    // Номер колонки хранится в сцене: иначе после загрузки сцены он сбрасывался бы в 0.
    [SerializeField] private int pumpIndex = 1;

    public int PumpIndex => pumpIndex;
    public bool IsFueling => fueling;

    public void Setup(int index) => pumpIndex = index;

    private void Awake() => service = ServiceDirector.Instance;

    public string GetInteractPrompt()
    {
        ServiceDirector s = Service;
        if (s == null || !s.CanRefuel)
        {
            // Никто не приехал — колонка молчит и плашки не показывает.
            return "";
        }

        if (s.OrderPumpNumber != PumpIndex)
        {
            // Посетитель стоит у другой колонки: эта занята ничем, подсказки нет.
            return "";
        }

        string good = string.IsNullOrEmpty(s.CurrentOrder.item) ? "" : $" + {Goods.DisplayName(s.CurrentOrder.item)}";
        return $"[E] Заправить: {s.CurrentOrder.liters:0} л{good}";
    }

    private ServiceDirector Service => service ??= ServiceDirector.Instance;

    public void Interact()
    {
        ServiceDirector s = Service;
        if (s == null || !s.CanRefuel || fueling || s.OrderPumpNumber != PumpIndex)
        {
            return;
        }

        truckArrival = FindAnyObjectByType<TruckArrivalController>();
        string modelPath = truckArrival != null ? truckArrival.CurrentModelPath : null;
        int carIndex = FuelingMinigame.CarIndexForModel(modelPath);

        // Игрок встал к колонке и взялся за пистолет — с этого момента он стоит
        // на месте: отойти можно, только завершив заправку.
        InteractionLock.Add("refuel");
        PlayerControlLock.Add("refuel");

        bool started = FuelingMinigame.Begin(carIndex, s.CurrentOrder.liters, OnMinigameFinished);
        if (!started)
        {
            ReleaseLocks();
            return;
        }

        fueling = true;
        Debug.Log("[Snowpaw Station] Заправка начата: откройте лючок и возьмите пистолет.");
    }

    /// <summary>Мини-игра закрылась: сдаём налитое или молча отпускаем игрока.</summary>
    private void OnMinigameFinished(FuelingResult result)
    {
        fueling = false;
        ReleaseLocks();

        if (result.Committed)
        {
            Service.OnFuelDelivered(result.Liters);
        }
        else
        {
            Debug.Log("[Snowpaw Station] Заправка отменена.");
        }
    }

    /// <summary>Снять оба замка: взаимодействие и заморозку персонажа.</summary>
    private void ReleaseLocks()
    {
        InteractionLock.Remove("refuel");
        PlayerControlLock.Remove("refuel");
    }

    private void OnDisable()
    {
        // Сцену выгрузили или объект выключили посреди заправки — иначе игрок
        // остался бы замороженным навсегда.
        if (fueling)
        {
            fueling = false;
            FuelingMinigame.CancelActive();
        }

        ReleaseLocks();
    }
}
