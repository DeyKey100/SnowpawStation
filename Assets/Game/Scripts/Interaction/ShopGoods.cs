using UnityEngine;

/// <summary>
/// Товары на полках магазина. Модели-заглушки стоят на стеллажах весь день и видны
/// всегда: игрок сразу понимает, что тут продают. Взять и пробить товар можно
/// только когда его просит текущий посетитель — за это отвечает ShopGoodItem.
///
/// Полки — готовые стеллажи модели, переименованные в сцене в shelf1 и shelf2.
/// Ничего достраивать не нужно: у каждого есть столешница, на неё товар и ставим.
/// Заглушки — примитивы разного цвета и размера, чтобы на полке не стояли шесть
/// одинаковых кубов.
/// </summary>
public static class ShopGoods
{
    /// <summary>Один товар на полке: как выглядит, где стоит и как называется в сцене.</summary>
    public sealed class Display
    {
        public string GoodId;
        public string ObjectName;

        /// <summary>Имя стеллажа в сцене: "shelf1" или "shelf2".</summary>
        public string ShelfName;

        /// <summary>Положение поперёк полки: 0 — левый край, 1 — правый.</summary>
        public float Spread;

        public PrimitiveType Shape;
        public Vector3 Size;
        public Color Color;

        /// <summary>Имя объекта из старой версии сцены, который можно переиспользовать.</summary>
        public string ReuseName;
    }

    /// <summary>
    /// Отступ товара от краёв столешницы. Спереди больше: у стеллажа по центру
    /// приподнята полка (до 1.696), и товар не должен в неё упираться.
    /// </summary>
    private const float SideInset = 0.45f;
    private const float FrontInset = 0.22f;

    /// <summary>Высота столешницы, если луч почему-то не попал в сам стеллаж.</summary>
    private const float FallbackTopY = 1.251f;

    public static readonly Display[] All =
    {
        new Display
        {
            GoodId = "coffee", ObjectName = "ShopGood_Coffee", ShelfName = "shelf1", Spread = 0f,
            Shape = PrimitiveType.Cylinder, Size = new Vector3(0.11f, 0.07f, 0.11f),
            Color = new Color(0.88f, 0.84f, 0.76f), ReuseName = "ShopCoffeeCup",
        },
        new Display
        {
            GoodId = "tea", ObjectName = "ShopGood_Tea", ShelfName = "shelf1", Spread = 0.5f,
            Shape = PrimitiveType.Cube, Size = new Vector3(0.17f, 0.24f, 0.12f),
            Color = new Color(0.25f, 0.42f, 0.28f),
        },
        new Display
        {
            GoodId = "cookies", ObjectName = "ShopGood_Cookies", ShelfName = "shelf1", Spread = 1f,
            Shape = PrimitiveType.Cube, Size = new Vector3(0.26f, 0.16f, 0.18f),
            Color = new Color(0.78f, 0.62f, 0.35f),
        },
        new Display
        {
            GoodId = "cigarettes", ObjectName = "ShopGood_Cigarettes", ShelfName = "shelf2", Spread = 0f,
            Shape = PrimitiveType.Cube, Size = new Vector3(0.16f, 0.09f, 0.11f),
            Color = new Color(0.92f, 0.93f, 0.95f),
        },
        new Display
        {
            GoodId = "canister", ObjectName = "ShopGood_Canister", ShelfName = "shelf2", Spread = 0.5f,
            Shape = PrimitiveType.Cube, Size = new Vector3(0.19f, 0.34f, 0.14f),
            Color = new Color(0.62f, 0.14f, 0.10f),
        },
        new Display
        {
            GoodId = "heater", ObjectName = "ShopGood_Heater", ShelfName = "shelf2", Spread = 1f,
            Shape = PrimitiveType.Cylinder, Size = new Vector3(0.22f, 0.15f, 0.22f),
            Color = new Color(0.85f, 0.45f, 0.12f),
        },
    };

    /// <summary>Создать товар-заглушку на его стеллаже (если его ещё нет в сцене).</summary>
    public static ShopGoodItem EnsureOne(Display display)
    {
        if (display == null || string.IsNullOrEmpty(display.ShelfName))
        {
            return null;
        }

        Renderer shelf = InteriorShopAnchors.FindMesh(display.ShelfName);
        if (shelf == null)
        {
            return null;
        }

        GameObject item = FindExisting(display);
        if (item == null)
        {
            item = GameObject.CreatePrimitive(display.Shape);
        }

        // Имя выравниваем всегда: старый стакан кофе из прежней версии сцены
        // переиспользуется как обычный товар и должен называться так же.
        item.name = display.ObjectName;

        // Товар — декорация: коллайдер мешал бы пройти мимо стеллажа и ловил лучи.
        DropCollider(item);

        Bounds bounds = shelf.bounds;
        float x = Mathf.Lerp(bounds.min.x + SideInset, bounds.max.x - SideInset, display.Spread);
        float z = bounds.max.z - FrontInset;
        float top = MeasureTop(shelf, bounds, x, z);

        item.transform.SetParent(shelf.transform, true);
        // top — плоскость столешницы, а позиция объекта — его центр.
        // У цилиндра Unity высота 2, поэтому половина высоты равна Size.y,
        // у куба — Size.y * 0.5. Без поправки товар тонул бы в столешнице.
        item.transform.SetPositionAndRotation(
            new Vector3(x, top + HalfHeight(display), z), Quaternion.identity);
        item.transform.localScale = display.Size;

        Renderer renderer = item.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Material material = new Material(renderer.sharedMaterial);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", display.Color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", display.Color);
            }

            material.SetFloat("_Smoothness", 0.15f);
            renderer.sharedMaterial = material;
        }

        return ShopGoodItem.Attach(item, display.GoodId);
    }

    /// <summary>
    /// Высота столешницы под точкой товара. Луч сверху попадает либо в сам стеллаж,
    /// либо в то, что стоит перед ним (ящик, другой товар) — во втором случае берём
    /// известную высоту столешницы модели.
    /// </summary>
    private static float MeasureTop(Renderer shelf, Bounds bounds, float x, float z)
    {
        Vector3 origin = new Vector3(x, bounds.max.y + 0.5f, z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, bounds.size.y + 1f,
            ~0, QueryTriggerInteraction.Ignore);
        float top = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != null && hits[i].collider.transform.name == shelf.name &&
                hits[i].point.y > top)
            {
                top = hits[i].point.y;
            }
        }

        return top > float.NegativeInfinity ? top : FallbackTopY;
    }

    /// <summary>
    /// Убрать коллайдер у декорации. DestroyImmediate в редакторе (из меню и при
    /// сборке сцены) и Destroy в игре — иначе Unity ругается, что Destroy вызван
    /// вне Play Mode, и коллайдер остаётся, ловя лучи и ломая проход.
    /// </summary>
    private static void DropCollider(GameObject target)
    {
        Collider blocker = target.GetComponent<Collider>();
        if (blocker == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(blocker);
        }
        else
        {
            Object.DestroyImmediate(blocker);
        }
    }

    /// <summary>
    /// Половина фактической высоты примитива: у цилиндра Unity высота равна 2,
    /// поэтому масштаб по Y даёт полную высоту 2 * Size.y, а не Size.y.
    /// </summary>
    private static float HalfHeight(Display display)
    {
        return display.Shape == PrimitiveType.Cylinder ? display.Size.y : display.Size.y * 0.5f;
    }

    /// <summary>
    /// Уже стоящий в сцене товар: сначала по имени объекта, потом по переиспользуемому
    /// имени (стакан кофе из старой версии сцены), в конце — по идентификатору товара.
    /// </summary>
    private static GameObject FindExisting(Display display)
    {
        ShopGoodItem[] items =
            Object.FindObjectsByType<ShopGoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].name == display.ObjectName)
            {
                return items[i].gameObject;
            }
        }

        if (!string.IsNullOrEmpty(display.ReuseName))
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == display.ReuseName)
                {
                    return all[i].gameObject;
                }
            }
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].GoodId == display.GoodId)
            {
                return items[i].gameObject;
            }
        }

        return null;
    }
}
