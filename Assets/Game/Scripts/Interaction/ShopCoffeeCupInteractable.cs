using UnityEngine;

/// <summary>
/// Стакан кофе для Дяди Котлеты: примитив-цилиндр на стойке Mesh_124 в модели
/// магазина. Виден и доступен только тогда, когда клиент действительно просит кофе
/// (стадия «ждём товар» и нужный товар — кофе). До этого он висел бы на виду весь
/// день и путался с задачами, поэтому рендерер и коллайдер просто выключаются.
///
/// Взять стакан — обычный [E], дальше общий цикл магазина: в лапах → стойка с
/// кассой → подтвердить. Подсказку и габариты держит ProximityInteractable.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopCoffeeCupInteractable : MonoBehaviour, IInteractable
{
    /// <summary>Товар, который выдаёт этот стакан.</summary>
    public const string GoodId = "coffee";

    /// <summary>Имя объекта в сцене.</summary>
    public const string ObjectName = "ShopCoffeeCup";

    /// <summary>Высота стакана, метры.</summary>
    public const float CupHeight = 0.14f;

    /// <summary>Диаметр стакана, метры.</summary>
    public const float CupDiameter = 0.11f;

    /// <summary>Цвет стакана: тёплый бежевый, как у одноразового стаканчика.</summary>
    public static readonly Color CupColor = new Color(0.88f, 0.84f, 0.76f, 1f);

    private ProximityInteractable marker;
    private Renderer cupRenderer;
    private Collider cupCollider;

    /// <summary>
    /// Нужен ли стакан прямо сейчас: есть заказ, он ждёт товар, товар — кофе,
    /// и в лапах пока пусто. После выдачи на кассе заказ ещё не закрыт (идёт реплика
    /// клиента), поэтому стакан не возвращается — иначе он появлялся бы снова
    /// на пару секунд, пока играет диалог.
    /// </summary>
    public static bool IsWanted
    {
        get
        {
            ServiceDirector service = ServiceDirector.Instance;
            return service != null &&
                   service.CurrentOrder != null &&
                   service.CurrentStage == OrderStage.AwaitingItem &&
                   service.CurrentOrder.item == GoodId &&
                   !service.ItemDelivered &&
                   string.IsNullOrEmpty(service.CarriedGood);
        }
    }

    public string GetInteractPrompt()
    {
        return IsWanted ? $"[E] Взять {Goods.DisplayName(GoodId)}" : "";
    }

    public void Interact()
    {
        if (!IsWanted || ServiceDirector.Instance == null)
        {
            return;
        }

        ServiceDirector.Instance.TakeGood(GoodId);

        // Гасим стакан сразу по нажатию, а не следующим кадром: иначе он успевает
        // мелькнуть в руках ещё на один кадр после того, как товар уже взят.
        RefreshVisibility();
    }

    /// <summary>Показать/спрятать стакан. Объект остаётся в сцене, гаснут только компоненты.</summary>
    public void RefreshVisibility()
    {
        bool visible = IsWanted;
        if (cupRenderer != null)
        {
            cupRenderer.enabled = visible;
        }

        if (cupCollider != null)
        {
            cupCollider.enabled = visible;
        }
    }

    private void Awake()
    {
        CacheParts();
    }

    private void OnEnable()
    {
        CacheParts();
        if (marker == null)
        {
            marker = ProximityInteractable.EnsureOn(gameObject, 2.2f);
        }

        RefreshVisibility();
    }

    private void Update()
    {
        // Состояние заказа меняется редко, а проверка сводится к нескольким сравнениям.
        RefreshVisibility();
    }

    private void CacheParts()
    {
        cupRenderer = GetComponent<Renderer>();
        cupCollider = GetComponent<Collider>();
        marker = GetComponent<ProximityInteractable>();
    }

    /// <summary>
    /// Поставить стакан на верхнюю плоскость стойки Mesh_124. Позиция берётся из
    /// габаритов модели, а не из констант: пивот у мешей FBX стоит в начале координат,
    /// и захардкоженная точка уехала бы при любом сдвиге интерьера.
    /// </summary>
    public void SnapToCounter()
    {
        if (!InteriorShopAnchors.TryGetCoffeeCupPoint(out Vector3 point))
        {
            Debug.LogWarning(
                "[Snowpaw Station] Стойка Mesh_124 не найдена: стакан кофе остался на месте.");
            return;
        }

        // TryGetCoffeeCupPoint отдаёт верхнюю плоскость; цилиндр стоит на своём пивоте
        // (он в центре), поэтому поднимаем его на половину высоты.
        transform.position = point + Vector3.up * (CupHeight * 0.5f);
        transform.rotation = Quaternion.identity;
    }

    private void Start()
    {
        SnapToCounter();
        RefreshVisibility();
    }
}
