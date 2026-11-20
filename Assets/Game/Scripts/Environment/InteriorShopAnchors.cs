using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Точки новой модели магазина (Assets/GasStation/shop_interior/shop_interior_joined.fbx).
/// Скрипты интерьера больше не пользуются старыми координатами: они спрашивают у модели,
/// где на самом деле стойка, полка и печь. Имена мешей — части FBX, поэтому поиск идёт
/// по имени дочернего объекта, а точка берётся из габаритов рендерера (пивот у мешей
/// стоит в начале координат модели, а не в центре геометрии).
/// </summary>
public static class InteriorShopAnchors
{
    /// <summary>Корень интерьерной модели в сцене.</summary>
    public const string InteriorRootName = "shop_interior_joined";

    /// <summary>Прилавок с кассой (передняя стойка покупателя).</summary>
    public const string CashDeskMesh = "Mesh_135";

    /// <summary>Кассовый аппарат на прилавке.</summary>
    public const string CashRegisterMesh = "Mesh_137";

    /// <summary>Стеллаж с товаром у задней стены.</summary>
    public const string GoodsShelfMesh = "Mesh_52";

    /// <summary>Второй (боковой) стеллаж.</summary>
    public const string SideShelfMesh = "Mesh_4";

    /// <summary>Длинная стойка в центре зала.</summary>
    public const string LongCounterMesh = "Mesh_108";

    /// <summary>Очаг/печь: языки пламени в модели.</summary>
    public const string StoveFlameMesh = "Stove_Flame_Center";

    /// <summary>Дымоход над печью — по нему проверяем, что печь на месте.</summary>
    public const string StoveFlueMesh = "Mesh_91";

    /// <summary>Найти корень интерьерной модели в загруженных сценах.</summary>
    public static Transform FindRoot()
    {
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
                if (roots[i].name == InteriorRootName)
                {
                    return roots[i].transform;
                }
            }
        }

        return null;
    }

    /// <summary>Найти меш модели по имени среди детей корня (поиск по всему дереву).</summary>
    public static Renderer FindMesh(string meshName)
    {
        Transform root = FindRoot();
        if (root == null || string.IsNullOrEmpty(meshName))
        {
            return null;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].name == meshName)
            {
                return renderers[i];
            }
        }

        return null;
    }

    /// <summary>Габариты меша в мировых координатах.</summary>
    public static bool TryGetBounds(string meshName, out Bounds bounds)
    {
        Renderer renderer = FindMesh(meshName);
        if (renderer == null)
        {
            bounds = default;
            return false;
        }

        bounds = renderer.bounds;
        return true;
    }

    /// <summary>
    /// Верхняя плоскость стойки с кассой. Записка смотрителя лежит именно здесь,
    /// рядом с аппаратом, но так, чтобы он её не перекрывал.
    /// </summary>
    public static bool TryGetCashDeskSurface(float extraHeight, out Vector3 point)
    {
        point = Vector3.zero;
        if (!TryGetBounds(CashDeskMesh, out Bounds desk))
        {
            return false;
        }

        // Касса стоит на прилавке слева; записку кладём правее неё, ближе к покупателю.
        float x = desk.center.x + desk.extents.x * 0.35f;
        float z = desk.center.z;
        if (TryGetBounds(CashRegisterMesh, out Bounds register))
        {
            // Гарантируем, что точка не окажется под корпусом кассы.
            float rightOfRegister = register.max.x + Mathf.Max(0.18f, desk.size.x * 0.08f);
            if (rightOfRegister < desk.max.x - 0.15f)
            {
                x = Mathf.Max(x, rightOfRegister);
            }
        }

        point = new Vector3(x, desk.max.y + extraHeight, z);
        return true;
    }

    /// <summary>Точка, с которой берут товар: перед стеллажом с товаром.</summary>
    public static bool TryGetGoodsShelfPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (!TryGetBounds(GoodsShelfMesh, out Bounds shelf))
        {
            return false;
        }

        // Стеллаж стоит у задней стены, поэтому точка смещается к центру зала (+Z).
        point = new Vector3(shelf.center.x, shelf.center.y, shelf.max.z + 0.35f);
        return true;
    }

    /// <summary>Точка выдачи товара: прилавок с кассой со стороны покупателя.</summary>
    public static bool TryGetCashDeskCounterPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (!TryGetBounds(CashDeskMesh, out Bounds desk))
        {
            return false;
        }

        // Передняя стойка стоит поперёк зала: покупатель подходит с юга (максимум Z).
        point = new Vector3(desk.center.x, desk.center.y, desk.max.z + 0.35f);
        return true;
    }

    /// <summary>Печь в интерьере: центр пламени у задней стены.</summary>
    public static bool TryGetStovePoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (TryGetBounds(StoveFlameMesh, out Bounds flame))
        {
            point = flame.center;
            return true;
        }

        if (TryGetBounds(StoveFlueMesh, out Bounds flue))
        {
            // Дымоход шире очага: берём низ трубы и чуть вперёд к центру зала.
            point = new Vector3(flue.center.x, flue.min.y + 0.35f, flue.max.z + 0.2f);
            return true;
        }

        return false;
    }
}
