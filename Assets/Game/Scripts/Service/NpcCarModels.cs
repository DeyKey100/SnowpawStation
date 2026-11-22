using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Каталог моделей машин у колонок. Обычные посетители приезжают на случайной
/// модели из Assets/NpcCars, сюжетные гости пока на заглушке (старый трак):
/// у них будут свои машины.
///
/// Модели из NpcCars смотрят носом в +Z, поэтому поворот считается прямо от
/// направления подъезда. У заглушки меш внутри fbx развёрнут иначе — для неё
/// остался старый доворот 270° и сдвиг узла truck1.
/// </summary>
public static class NpcCarModels
{
    /// <summary>Заглушка для сюжетных гостей и запасной вариант.</summary>
    public const string PlaceholderPath = "Assets/RawAssets/Models/fbx/truck.fbx";

    /// <summary>Машины посетителей: любая из них может встать к колонке.</summary>
    public static readonly string[] VisitorPaths =
    {
        "Assets/NpcCars/hatchback/hatchback.fbx",
        "Assets/NpcCars/jeep/jeep.fbx",
        "Assets/NpcCars/pickup/pickup.fbx",
        "Assets/NpcCars/truck1/truck.fbx",
        "Assets/NpcCars/van-pochta/van-pochta.fbx",
    };

    // Заглушка повёрнута относительно направления движения на 270° (старый fbx).
    private const float PlaceholderYawOffset = 270f;

    // Узел меша заглушки внутри fbx: без сдвига корпус стоял в колонке.
    private static readonly Vector3 PlaceholderMeshLocalPosition = new Vector3(-0.47f, 0.5f, 2.6f);

    private static string lastVisitorPath;

    public static bool IsPlaceholder(string assetPath) => assetPath == PlaceholderPath;

    /// <summary>Доворот модели относительно направления подъезда, градусы.</summary>
    public static float YawOffset(string assetPath) => IsPlaceholder(assetPath) ? PlaceholderYawOffset : 0f;

    /// <summary>
    /// Случайная модель посетителя. Подряд одна и та же машина не приезжает:
    /// иначе рандом выглядит как один и тот же «фургон» два раза кряду.
    /// </summary>
    public static string RandomVisitorPath()
    {
        if (VisitorPaths.Length == 0)
        {
            return PlaceholderPath;
        }

        if (VisitorPaths.Length == 1)
        {
            lastVisitorPath = VisitorPaths[0];
            return lastVisitorPath;
        }

        string picked = lastVisitorPath;
        for (int guard = 0; guard < 8; guard++)
        {
            picked = VisitorPaths[UnityEngine.Random.Range(0, VisitorPaths.Length)];
            if (picked != lastVisitorPath)
            {
                break;
            }
        }

        lastVisitorPath = picked;
        return picked;
    }

    /// <summary>Забыть предыдущую машину (новая смена — снова любой вариант).</summary>
    public static void ResetHistory() => lastVisitorPath = null;

    /// <summary>
    /// Каталог моделей в Resources. Он держит прямые ссылки на fbx, поэтому
    /// модели попадают в билд, хотя сами лежат вне Resources.
    /// </summary>
    private const string CatalogResourcePath = "NpcCarCatalog";

    private static NpcCarCatalog catalog;
    private static bool catalogSearched;

    private static NpcCarCatalog Catalog
    {
        get
        {
            if (!catalogSearched)
            {
                catalogSearched = true;
                catalog = Resources.Load<NpcCarCatalog>(CatalogResourcePath);
            }

            return catalog;
        }
    }

    public static GameObject Load(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

#if UNITY_EDITOR
        GameObject editorModel = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (editorModel != null)
        {
            return editorModel;
        }
#endif

        // В собранной игре ассетов по путям нет: модели приезжают через каталог.
        NpcCarCatalog models = Catalog;
        if (models != null)
        {
            GameObject fromCatalog = models.Find(assetPath);
            if (fromCatalog != null)
            {
                return fromCatalog;
            }
        }

        // Последняя линия обороны — заглушка, если каталог забыли пересобрать.
        return Resources.Load<GameObject>("truck");
    }

    /// <summary>
    /// Довести дочерние узлы конкретной модели: у заглушки поправить сдвиг меша,
    /// чтобы корпус не стоял в колонке. Новым моделям ничего не нужно.
    /// </summary>
    public static void ApplyLayout(string assetPath, Transform instance)
    {
        if (instance == null || !IsPlaceholder(assetPath))
        {
            return;
        }

        Transform mesh = instance.Find("truck1");
        if (mesh != null)
        {
            mesh.localPosition = PlaceholderMeshLocalPosition;
        }
    }
}
