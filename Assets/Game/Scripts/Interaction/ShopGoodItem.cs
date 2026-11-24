using UnityEngine;

/// <summary>
/// Товар на полке магазина: модель-заглушка стоит на стеллаже весь день и видна
/// всегда, но взять её можно только когда этот товар просит текущий посетитель.
///
/// Так игрок сразу видит ассортимент («вот печенье, вот канистра»), а не пустую
/// полку, которая наполняется из воздуха по заказу. Подсказка при этом молчит,
/// пока товар не нужен: иначе стеллаж превращается в свалку плашек [E].
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopGoodItem : MonoBehaviour, IInteractable
{
    /// <summary>Идентификатор товара из <see cref="Goods"/>.</summary>
    public string GoodId { get; private set; }

    /// <summary>Габариты маркера близости: у мелкого товара достаточно метра с небольшим.</summary>
    private const float MarkerDistance = 1.8f;

    /// <summary>Создать товар на объекте сцены.</summary>
    public static ShopGoodItem Attach(GameObject target, string goodId)
    {
        if (target == null || string.IsNullOrEmpty(goodId))
        {
            return null;
        }

        ShopGoodItem item = target.GetComponent<ShopGoodItem>();
        if (item == null)
        {
            item = target.AddComponent<ShopGoodItem>();
        }

        item.GoodId = goodId;
        ProximityInteractable.EnsureOn(target, MarkerDistance);
        return item;
    }

    /// <summary>
    /// Нужен ли этот товар прямо сейчас. Требование «в лапах пусто» обязательно:
    /// иначе с полки можно было бы взять второй товар поверх первого.
    /// </summary>
    public static bool IsNeeded(string goodId, ServiceDirector service)
    {
        if (service == null || string.IsNullOrEmpty(goodId))
        {
            return false;
        }

        return service.CurrentOrder != null &&
               service.CurrentStage == OrderStage.AwaitingItem &&
               service.CurrentOrder.item == goodId &&
               !service.ItemDelivered &&
               string.IsNullOrEmpty(service.CarriedGood);
    }

    public string GetInteractPrompt()
    {
        ServiceDirector service = ServiceDirector.Instance;
        if (!IsNeeded(GoodId, service))
        {
            return "";
        }

        return $"[E] Взять {Goods.PickupName(GoodId)}";
    }

    public void Interact()
    {
        ServiceDirector service = ServiceDirector.Instance;
        if (!IsNeeded(GoodId, service))
        {
            return;
        }

        service.TakeGood(GoodId);
    }
}
