using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FuelResult { Underfilled, Accepted, Perfect, Overfilled }

/// <summary>
/// Рабочая смена: очередь заказов дня (2 обычные машины + сюжетные гости по расписанию),
/// заправка у колонок (зажать ЛКМ, пороги 75%, 91% и 110%), цикл магазина
/// (товар с полки → на стойку → подтвердить), реакция клиента, квота, переход к вечеру.
/// День 1 полностью: легковушка 35 л, микроавтобус 50 л, Дядя Котлета 80 л + двойной кофе.
/// </summary>
[DisallowMultipleComponent]
public sealed class ServiceDirector : MonoBehaviour
{
    private const float MinimumAcceptedFuelRatio = 0.75f;
    private const float GoodFuelRatio = 0.91f;
    private const float OverfilledFuelRatio = 1.10f;

    public class Order
    {
        public string id;
        public string displayName;
        public float liters;
        public string item;
        public bool isGuest;
        public int pumpIndex;
    }

    /// <summary>Текущий заказ смены; null — смена не идёт.</summary>
    public Order CurrentOrder { get; private set; }

    /// <summary>Часы: каждая обслуженная машина сдвигает время, таймера нет.</summary>
    public string Clock => CurrentState == "morning" ? "08:00" : CurrentState == "evening" ? "18:00" : clockText;

    private string CurrentState = "morning";
    private readonly List<Order> queue = new List<Order>();
    private string clockText = "09:00";
    private int servedCount;
    private DialogueManager dialogue;
    private bool itemStageActive;
    private bool fuelDeliveredForCurrentOrder;
    private Coroutine shiftStartRoutine;

    /// <summary>Товар, взятый с полки (в «лапах» до подтверждения на стойке).</summary>
    public string CarriedGood { get; private set; }

    private readonly List<GameObject> runtimePumps = new List<GameObject>();
    private readonly List<GameObject> runtimeShop = new List<GameObject>();

    public static ServiceDirector Instance { get; private set; }

    public event System.Action<Order> OrderChanged;

    private void Start()
    {
        Instance = this;
        dialogue = StoryDirector.Instance != null ? StoryDirector.Instance.Dialogue : null;
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged += OnPhaseChanged;
            DayLoopManager.Instance.DayStarted += OnDayStarted;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsurePumps(SceneManager.GetActiveScene());
        EnsureShop(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        Instance = null;
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged -= OnPhaseChanged;
            DayLoopManager.Instance.DayStarted -= OnDayStarted;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDayStarted(int day) => CurrentState = "morning";

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePumps(scene);
        EnsureShop(scene);
    }

    private void OnPhaseChanged(int day, DayLoopManager.DayState state)
    {
        if (state == DayLoopManager.DayState.WorkShift)
        {
            CurrentState = "shift";
            clockText = "09:00";
            servedCount = 0;
            BuildQueue(day);
            // Не выдёргиваем игрока из текущего диалога (например, «печь растоплена»):
            // первый заказ подъезжает только когда экран свободен.
            if (shiftStartRoutine != null)
            {
                StopCoroutine(shiftStartRoutine);
            }

            shiftStartRoutine = StartCoroutine(BeginShiftDelayed());
        }
        else if (state == DayLoopManager.DayState.Evening)
        {
            CurrentState = "evening";
            CurrentOrder = null;
            CarriedGood = null;
            OrderChanged?.Invoke(null);
        }
        else if (state == DayLoopManager.DayState.Morning)
        {
            CurrentState = "morning";
            CurrentOrder = null;
            OrderChanged?.Invoke(null);
        }
    }

    /// <summary>Пауза перед первым заказом: игрок должен увидеть свою реплику, а не прыжок в смену.</summary>
    private System.Collections.IEnumerator BeginShiftDelayed()
    {
        // Ждём, пока закроется текущий диалог (заправка печи и т.п.), максимум ~3 с.
        float waited = 0f;
        while (DialogueManager.IsActive && waited < 3f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.8f);

        if (CurrentState == "shift" && CurrentOrder == null)
        {
            NextOrder();
        }
    }

    private void BuildQueue(int day)
    {
        queue.Clear();
        List<RegularCarDef> cars = DayPlans.GetRegularCars(day);
        List<GuestDef> guests = DayPlans.GetGuests(day);

        int carIndex = 0;
        // Данила (день 3, после первой машины) — вставляется после первой обычной машины.
        GuestDef afterFirst = guests.Find(g => g.schedule == "after_first_car");
        if (cars.Count > 0)
        {
            queue.Add(CarOrder(cars[carIndex++], 0));
        }

        if (afterFirst != null)
        {
            queue.Add(GuestOrder(afterFirst));
        }

        while (carIndex < cars.Count)
        {
            queue.Add(CarOrder(cars[carIndex], carIndex));
            carIndex++;
        }

        for (int i = 0; i < guests.Count; i++)
        {
            if (guests[i].schedule == "after_quota")
            {
                queue.Add(GuestOrder(guests[i]));
            }
        }
    }

    private Order CarOrder(RegularCarDef car, int index)
    {
        return new Order { id = "car_" + index, displayName = car.displayName, liters = car.liters, isGuest = false, pumpIndex = index % 2 };
    }

    private Order GuestOrder(GuestDef guest)
    {
        return new Order { id = guest.id, displayName = guest.displayName, liters = guest.liters, item = guest.item, isGuest = true, pumpIndex = guest.pumpIndex };
    }

    private void NextOrder()
    {
        CarriedGood = null;
        itemStageActive = false;
        fuelDeliveredForCurrentOrder = false;
        CurrentOrder = queue.Count > 0 ? queue[0] : null;

        if (CurrentOrder != null)
        {
            Debug.Log($"[Snowpaw Station] Заказ: {CurrentOrder.displayName} — {CurrentOrder.liters:0} л{(CurrentOrder.item != null ? $", {Goods.DisplayName(CurrentOrder.item)}" : "")}.");
            OrderChanged?.Invoke(CurrentOrder);
            ShowGreeting(CurrentOrder);
        }
    }

    private void ShowGreeting(Order order)
    {
        if (dialogue == null)
        {
            return;
        }

        switch (order.id)
        {
            case "kotleta": dialogue.Show(StoryDialogues.KotletaGreeting()); break;
            case "courier": dialogue.Show(StoryDialogues.CourierGreeting()); break;
            case "danila": dialogue.Show(StoryDialogues.DanilaGreeting()); break;
            case "red_pickup": dialogue.Show(StoryDialogues.PickupArrival()); break;
            default: dialogue.Show(StoryDialogues.RegularCarGreeting(order.displayName, order.liters)); break;
        }
    }

    // ================= ЗАПРАВКА =================

    /// <summary>Колонка отпустила liters л. Проверяем долю заказа и показываем реакцию.</summary>
    public void OnFuelDelivered(float liters)
    {
        Order order = CurrentOrder;
        if (order == null || itemStageActive || fuelDeliveredForCurrentOrder)
        {
            return;
        }

        FuelResult result = Classify(liters, order.liters);
        Debug.Log($"[Snowpaw Station] Налито {liters:0.0} л из {order.liters:0} — {result}.");

        if (dialogue != null)
        {
            DialogueScript reaction = order.id switch
            {
                "kotleta" => StoryDialogues.KotletaFuelReaction(result),
                "courier" => StoryDialogues.CourierFuelReaction(result),
                "danila" => StoryDialogues.DanilaFuelReaction(result),
                _ => StoryDialogues.RegularCarResult(result),
            };
            dialogue.Show(reaction);
        }

        if (result == FuelResult.Underfilled)
        {
            Debug.Log($"[Snowpaw Station] Заправка не завершена: налито только {liters:0.0} л из {order.liters:0}. Нужно не менее {MinimumAcceptedFuelRatio:P0}. Заказ остаётся текущим.");
            return;
        }

        fuelDeliveredForCurrentOrder = true;
        AdvanceClock();

        if (order.isGuest && !string.IsNullOrEmpty(order.item))
        {
            itemStageActive = true;
            Debug.Log($"[Snowpaw Station] Ждём товар: {Goods.DisplayName(order.item)} (полка → стойка → подтвердить).");

            // Подсказка после заправки: товар берут на полке и отдают на стойке — всё по [E].
            DialogueScript hint = DialogueBuilder.Create("item_hint", "[Магазин]");
            hint.Note($"Нужен {Goods.DisplayName(order.item)}: [E] на полке в магазине, затем [E] на стойке с кассой.");
            if (dialogue != null)
            {
                dialogue.Show(hint);
            }
        }
        else
        {
            CompleteOrder();
        }
    }

    private FuelResult Classify(float actual, float requested)
    {
        float ratio = actual / requested;
        if (ratio < MinimumAcceptedFuelRatio) return FuelResult.Underfilled;
        if (ratio < GoodFuelRatio) return FuelResult.Accepted;
        return ratio < OverfilledFuelRatio ? FuelResult.Perfect : FuelResult.Overfilled;
    }

    private void AdvanceClock()
    {
        servedCount++;
        clockText = (9 + servedCount * 2) + ":00";
    }

    // ================= МАГАЗИН =================

    /// <summary>Взять товар с полки. Один товар за раз.</summary>
    public bool TakeGood(string goodId)
    {
        if (CurrentOrder == null || !itemStageActive || !string.IsNullOrEmpty(CarriedGood))
        {
            return false;
        }

        CarriedGood = goodId;
        Debug.Log($"[Snowpaw Station] Товар в лапах: {Goods.DisplayName(goodId)}. Несите на стойку.");
        return true;
    }

    /// <summary>Подтвердить товар на стойке (рычаг кассы): товар выдан клиенту.</summary>
    public void ConfirmOnCounter()
    {
        if (CurrentOrder == null || !itemStageActive)
        {
            return;
        }

        if (CarriedGood != CurrentOrder.item)
        {
            Debug.Log($"[Snowpaw Station] Не тот товар: нужен {Goods.DisplayName(CurrentOrder.item)}.");
            return;
        }

        bool kettleHot = StationHeatSystem.Active != null && StationHeatSystem.Active.CurrentHeat >= 25f;
        CarriedGood = null;
        itemStageActive = false;

        if (dialogue != null)
        {
            switch (CurrentOrder.id)
            {
                case "kotleta":
                    dialogue.Show(StoryDialogues.KotletaItemReaction(kettleHot), delegate { OnItemReactionDone(); });
                    return;
                default:
                    CompleteOrder();
                    return;
            }
        }

        CompleteOrder();
    }

    private void OnItemReactionDone()
    {
        if (StoryDirector.Instance != null)
        {
            StoryDirector.Instance.Dialogue.Show(StoryDialogues.KotletaFarewell());
        }

        CompleteOrder();
    }

    private void CompleteOrder()
    {
        if (CurrentOrder == null)
        {
            return;
        }

        Order done = CurrentOrder;
        if (queue.Count > 0 && queue[0].id == done.id)
        {
            queue.RemoveAt(0);
        }

        if (done.isGuest)
        {
            DayLoopManager.Instance.NotifyGuestServed(done.id);
        }
        else
        {
            DayLoopManager.Instance.NotifyRegularCarServed();
        }

        if (DayLoopManager.Instance.CurrentState == DayLoopManager.DayState.WorkShift)
        {
            NextOrder();
        }
    }

    // Заглушки дня 3 (красный пикап) — следующий этап.
    public void MarkPickupWarmed() { }
    public void MarkPickupDone() { }

    // ================= КОЛОНКИ / МАГАЗИН В РАНТАЙМЕ =================

    // Четыре колонки заправки: два острова по две колонки, координаты из меша gas_station.fbx.
    // Сцена уже содержит объекты FuelPump_1..4; таблица — запасной вариант, если их нет.
    private static readonly Vector3[] FallbackPumpPositions =
    {
        new Vector3(-6.12f, PumpGroundY, -2.87f),
        new Vector3(-6.12f, PumpGroundY, 2.97f),
        new Vector3(0.91f, PumpGroundY, -2.87f),
        new Vector3(0.91f, PumpGroundY, 2.97f),
    };

    /// <summary>Верх снежного пола: на нём стоят колонки и остальные объекты двора.</summary>
    private const float PumpGroundY = 0.5f;

    /// <summary>Радиус, в котором у колонки появляется табличка «[E] ...».</summary>
    private const float PumpPromptDistance = 2.2f;

    /// <summary>Колонки — только на улице (не в интерьере магазина).</summary>
    private void EnsurePumps(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name.ToLowerInvariant().Contains("interior"))
        {
            return;
        }

        // Находим существующие колонки в активной сцене...
        List<GameObject> pumps = FindScenePumps();
        int created = 0;
        for (int i = pumps.Count; i < FallbackPumpPositions.Length; i++)
        {
            GameObject pump = new GameObject("FuelPump_" + (i + 1));
            pump.transform.position = FallbackPumpPositions[i];

            // Колонка — только точка взаимодействия: меш колонки уже есть в gas_station.fbx,
            // поэтому своего коллайдера у неё нет (иначе луч прицела ловил бы пустышку).
            pump.layer = 0;
            FuelPumpInteractable fuel = pump.AddComponent<FuelPumpInteractable>();
            fuel.Setup(i + 1);
            ProximityInteractable proximity = pump.AddComponent<ProximityInteractable>();
            proximity.Setup(PumpPromptDistance);
            runtimePumps.Add(pump);
            created++;
        }

        for (int i = 0; i < pumps.Count; i++)
        {
            FuelPumpInteractable fuel = pumps[i].GetComponent<FuelPumpInteractable>();
            if (fuel == null)
            {
                fuel = pumps[i].AddComponent<FuelPumpInteractable>();
                fuel.Setup(i + 1);
            }

            // Колонки в сцене могли быть расставлены вручную: доводим до нужного вида.
            ProximityInteractable proximity = pumps[i].GetComponent<ProximityInteractable>();
            if (proximity == null)
            {
                proximity = pumps[i].AddComponent<ProximityInteractable>();
            }

            proximity.Setup(PumpPromptDistance);
        }

        if (created > 0)
        {
            Debug.Log($"[Snowpaw Station] Создано колонок в рантайме: {created} (сцена не правится).");
        }
    }

    private static List<GameObject> FindScenePumps()
    {
        List<GameObject> result = new List<GameObject>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                // Колонки сцены — это FuelPump_1..4. Меш колонок внутри gas_station.fbx
                // (stat_fuel_dispenser_mirror) по имени не матчится, поэтому лишних объектов не будет.
                CollectByName(roots[i].transform, result, t =>
                {
                    string n = t.name.ToLowerInvariant();
                    return n.StartsWith("fuelpump_") || n.Contains("колонк") || n.Contains("runtimefuelpump");
                });
            }
        }

        return result;
    }

    private static GameObject FindStationAnchor()
    {
        List<GameObject> found = new List<GameObject>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CollectByName(roots[i].transform, found, t => t.name.ToLowerInvariant().Contains("shop") || t.name.ToLowerInvariant().Contains("kassa"));
            }
        }

        return found.Count > 0 ? found[0] : null;
    }

    private static void CollectByName(Transform root, List<GameObject> sink, System.Predicate<Transform> match)
    {
        if (match(root))
        {
            sink.Add(root.gameObject);
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            CollectByName(root.GetChild(i), sink, match);
        }
    }

    /// <summary>Магазин создаётся только в интерьерной сцене (полка + стойка с рычагом кассы).</summary>
    private void EnsureShop(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !scene.name.ToLowerInvariant().Contains("interior"))
        {
            return;
        }

        // После выхода из магазина объекты уничтожены вместе со сценой, но остаются в списке:
        // чистим мёртвые ссылки, иначе при повторном заходе полка и стойка не создаются.
        runtimeShop.RemoveAll(go => go == null);

        if (runtimeShop.Count > 0)
        {
            return;
        }

        int layer = LayerMask.NameToLayer("Interactable");
        if (layer < 0)
        {
            layer = 0;
        }

        GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "ShopShelf";
        shelf.transform.position = new Vector3(3f, 1f, -3.5f);
        shelf.transform.localScale = new Vector3(1.6f, 0.1f, 0.6f);
        shelf.layer = layer;
        shelf.AddComponent<ShopShelfInteractable>();
        ProximityInteractable.EnsureOn(shelf, 2.2f);

        GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        counter.name = "ShopCounter";
        counter.transform.position = new Vector3(0f, 1f, 0f);
        counter.transform.localScale = new Vector3(2.2f, 0.1f, 0.8f);
        counter.layer = layer;
        counter.AddComponent<ShopCounterInteractable>();
        ProximityInteractable.EnsureOn(counter, 2.2f);

        runtimeShop.Add(shelf);
        runtimeShop.Add(counter);
        Debug.Log("[Snowpaw Station] Магазин в интерьере создан в рантайме: полка + стойка.");
    }
}

/// <summary>Полка магазина: E — взять нужный товар (кофе, печенье, сигареты, грелка...).</summary>
[DisallowMultipleComponent]
public sealed class ShopShelfInteractable : MonoBehaviour, IInteractable
{
    public string GetInteractPrompt()
    {
        ServiceDirector service = ServiceDirector.Instance;
        if (service == null || service.CurrentOrder == null || string.IsNullOrEmpty(service.CurrentOrder.item))
        {
            return "[E] Полка с товаром";
        }

        return !string.IsNullOrEmpty(service.CarriedGood)
            ? $"В лапах: {Goods.DisplayName(service.CarriedGood)} — несите на стойку"
            : $"[E] Взять {Goods.DisplayName(service.CurrentOrder.item)} с полки";
    }

    public void Interact()
    {
        ServiceDirector service = ServiceDirector.Instance;
        if (service == null || service.CurrentOrder == null)
        {
            return;
        }

        string need = service.CurrentOrder.item;
        if (string.IsNullOrEmpty(need) || !string.IsNullOrEmpty(service.CarriedGood))
        {
            return;
        }

        service.TakeGood(need);
    }
}

/// <summary>Стойка с рычагом кассы: E — подтвердить товар (товар → клиенту).</summary>
[DisallowMultipleComponent]
public sealed class ShopCounterInteractable : MonoBehaviour, IInteractable
{
    public string GetInteractPrompt()
    {
        ServiceDirector service = ServiceDirector.Instance;
        return service != null && !string.IsNullOrEmpty(service.CarriedGood)
            ? $"[E] Подтвердить: {Goods.DisplayName(service.CarriedGood)} (касса)"
            : "[E] Стойка с кассой";
    }

    public void Interact()
    {
        ServiceDirector.Instance?.ConfirmOnCounter();
    }
}
