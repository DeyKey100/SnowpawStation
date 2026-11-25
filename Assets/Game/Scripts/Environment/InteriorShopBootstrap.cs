using UnityEngine;

/// <summary>
/// Достройка новой модели магазина (Assets/GasStation/shop_interior/shop_interior_joined.fbx).
///
/// Модель импортирована как диорама: в ней есть только восточная и задняя стены,
/// пол и обстановка. Западной стены и передней (южной) стены с входом нет, поэтому
/// игрок уходил в пустоту. Скрипт достраивает недостающие стены из габаритов самой
/// модели, прорезает проём на месте Door_Exit, навешивает коллайдеры и включает
/// PS1-материал, чтобы магазин не выбивался из стиля.
///
/// Стены по умолчанию НЕВИДИМЫ (showShellWalls = false): это чистый барьер, чтобы
/// игрок не вышел за пределы зала. Потолок не строится вовсе — диорама должна
/// просматриваться сверху. Модель игрока в интерьере скрыта: камера смотрит из-за
/// спины, и голова кота занимает полкадра.
///
/// Порядок важен: сначала коллайдеры и печь на готовой геометрии, затем PS1-конвертация
/// (чтобы она прошла по всей модели разом), и только потом оболочка — она сразу получает
/// готовый PS1-материал и не пересоздаётся повторно.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class InteriorShopBootstrap : MonoBehaviour
{
    [Header("Оболочка")]
    [SerializeField] private bool buildMissingWalls = true;
    [SerializeField, Tooltip("Показывать панели оболочки. По умолчанию выключено: стены нужны только как невидимый барьер.")]
    private bool showShellWalls = false;
    [SerializeField, Range(0.02f, 1f)] private float wallAlpha = 0.22f;
    [SerializeField, Min(0.5f)] private float doorwayWidth = 1.8f;
    [SerializeField, Min(1f)] private float doorwayHeight = 2.35f;
    [SerializeField] private Color wallColor = new Color(0.113f, 0.164f, 0.176f);

    [Header("Персонаж")]
    [SerializeField, Tooltip("Гасить служебный меш капсулы-коллайдера: игрок не должен видеть геометрию коллизии вокруг себя.")]
    private bool hidePlayerCapsule = true;

    [SerializeField, Tooltip("Прятать модель игрока, когда камера подъехала вплотную. По умолчанию выключено — модель остаётся видимой.")]
    private bool hidePlayerModelWhenCameraClose = false;

    [Header("Коллайдеры")]
    [SerializeField] private bool ensureColliders = true;

    [Header("Печь")]
    [SerializeField] private bool ensureStove = true;

    [Header("Стиль")]
    [SerializeField] private bool applyPsxLook = true;

    [Header("Освещение")]
    [SerializeField] private bool ensureLighting = true;
    [SerializeField, Min(1)] private int interiorLightCount = 3;
    [SerializeField] private Color interiorLightColor = new Color(1f, 0.88f, 0.72f);
    [SerializeField, Min(0f)] private float interiorLightIntensity = 5.5f;
    [SerializeField, Min(0.5f)] private float interiorLightRange = 11f;

    [Header("Камера")]
    [SerializeField] private bool ensureCamera = true;
    [SerializeField, Min(0.5f)] private float interiorCameraDistance = 3f;
    [SerializeField, Min(0f)] private float interiorShoulderOffset = 0.7f;
    [SerializeField, Tooltip("Высота точки обзора над плечом. Интерьер низкий, поэтому камера смотрит сверху: при малом значении вид упирается в пояса и прилавок.")]
    private float interiorShoulderHeight = 1.7f;

    private const string WallMeshEast = "Mesh_1";
    private const string WallMeshBack = "Mesh_2";
    private const string FloorMesh = "Mesh_0";

    private void Awake()
    {
        // Модель может быть ещё не подгружена в момент Awake (у неё свой порядок
        // инициализации), поэтому достройка идёт из Start, когда сцена собрана целиком.
    }

    private void Start()
    {
        Build();
    }

    /// <summary>Вся достройка модели: коллайдеры, печь, PS1-материал и оболочка.</summary>
    private void Build()
    {
        if (InteriorShopAnchors.FindRoot() == null)
        {
            Debug.LogWarning("[Snowpaw Station] Магазин: модель не найдена, достройка пропущена.");
            return;
        }

        if (ensureColliders)
        {
            EnsureColliders();
        }

        if (ensureStove)
        {
            EnsureStove();
        }

        if (applyPsxLook)
        {
            ApplyPsxLook();
        }

        if (ensureLighting)
        {
            EnsureLighting();
        }

        if (ensureCamera)
        {
            EnsureCamera();
        }

        if (buildMissingWalls)
        {
            BuildShell();
        }
    }

    /// <summary>
    /// Камера внутри дома. Снаружи игрок ходит с третьим лицом на 5.2 м — для улицы это
    /// нормально, но в магазине камера оказывается за передней стеной и снимает зал
    /// через проём. Поджимаем дистанцию и плечо на время нахождения в интерьере.
    /// </summary>
    private void EnsureCamera()
    {
        Transform follow = null;
        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "PlayerFollowCamera")
                {
                    follow = root.transform;
                    break;
                }
            }

            if (follow != null)
            {
                break;
            }
        }

        if (follow == null)
        {
            return;
        }

        var vcam = follow.GetComponent<Unity.Cinemachine.CinemachineVirtualCamera>();
        if (vcam == null)
        {
            return;
        }

        var third = vcam.GetCinemachineComponent<Unity.Cinemachine.Cinemachine3rdPersonFollow>();
        if (third == null)
        {
            return;
        }

        third.CameraDistance = interiorCameraDistance;
        third.ShoulderOffset = new Vector3(interiorShoulderOffset, 0f, 0f);
        // Модель игрока в интерьере скрыта, поэтому камеру держим почти на уровне
        // глаз: высокий рычаг был нужен только чтобы заглянуть через голову кота.
        third.VerticalArmLength = interiorShoulderHeight;

        // Камера в интерьере не должна упираться в стены: Cinemachine3rdPersonFollow
        // на препятствии подтягивает её к персонажу рывком, и обзор прыгает у самого
        // края зала. Обнуляем маску столкновений — камера проходит сквозь любую
        // геометрию и держит заданную дистанцию везде, включая прилавок и стеллажи
        // у задней стены. Стен в зале всё равно не видно, поэтому «проход сквозь
        // стену» игрок не замечает, а рывка обзора при подходе к краю нет.
        third.CameraCollisionFilter = 0;

        // Капсула-коллайдер — служебная геометрия, её меш гасим всегда.
        // Модель прячем только если явно попросили: по умолчанию она должна быть видна.
        GameObject playerRoot = GameObject.Find("PlayerCharacter");
        if (playerRoot != null)
        {
            ThirdPersonCameraGuard guard = ThirdPersonCameraGuard.EnsureOn(playerRoot, hidePlayerCapsule);
            guard.HideModelWhenCameraClose = hidePlayerModelWhenCameraClose;
        }
    }

    /// <summary>
    /// Освещение внутри. Улица освещена направленным светом и небом, а за закрытой
    /// оболочкой направленный свет не достаёт: без ламп магазин превращается в чёрный
    /// ящик. Ставим несколько тёплых точечных источников под потолком.
    /// </summary>
    private void EnsureLighting()
    {
        if (!TryResolveRoom(out Bounds room, out float wallTop))
        {
            return;
        }

        const string RootName = "InteriorLighting";
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            return;
        }

        GameObject root = new GameObject(RootName);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        int count = Mathf.Max(1, interiorLightCount);
        // Лампы держим на уровне человеческого роста, а не под самым потолком:
        // у потолка они пересвечивают оболочку до плоского серого.
        float y = Mathf.Lerp(room.max.y, wallTop, 0.55f);
        for (int i = 0; i < count; i++)
        {
            // Раскладываем лампы вдоль зала, от передней стены к задней.
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float z = Mathf.Lerp(room.max.z - 1.4f, room.min.z + 1.4f, t);
            float x = room.center.x;

            GameObject lamp = new GameObject($"InteriorLamp_{i + 1}");
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.position = new Vector3(x, y, z);

            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = interiorLightColor;
            light.intensity = interiorLightIntensity;
            light.range = interiorLightRange;
            light.shadows = LightShadows.Soft;
            light.renderMode = LightRenderMode.ForcePixel;
        }
    }

    /// <summary>
    /// PS1-конвертация модели. Компонент вешается на сам корень модели (иначе
    /// GetComponentsInChildren не увидит её меши), а материалы создаются в рантайме,
    /// поэтому исходный PS1_Master_Material в проекте не меняется.
    /// </summary>
    private void ApplyPsxLook()
    {
        Transform root = InteriorShopAnchors.FindRoot();
        if (root == null)
        {
            return;
        }

        PSXVertexSnap snap = root.GetComponent<PSXVertexSnap>();
        if (snap == null)
        {
            snap = root.gameObject.AddComponent<PSXVertexSnap>();
        }

        snap.ApplyToRenderers();
    }

    /// <summary>
    /// Коллайдеры на меши, у которых их нет: в модели часть стеллажей и стен
    /// (в том числе восточная стена) была без столкновений, и сквозь них проходили.
    /// </summary>
    private void EnsureColliders()
    {
        Transform root = InteriorShopAnchors.FindRoot();
        if (root == null)
        {
            return;
        }

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        int added = 0;
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            added++;
        }

        if (added > 0)
        {
            Debug.Log($"[Snowpaw Station] Магазин: добавлено коллайдеров — {added}.");
        }
    }

    /// <summary>
    /// Печь в интерьере: раньше интерактивная печь была только во дворе, хотя в новой
    /// модели очаг стоит у задней стены. Вешаем StoveInteractable на языки пламени.
    /// </summary>
    private void EnsureStove()
    {
        Renderer flame = InteriorShopAnchors.FindMesh(InteriorShopAnchors.StoveFlameMesh);
        if (flame == null)
        {
            Debug.LogWarning("[Snowpaw Station] Магазин: очаг в модели не найден, печь не подключена.");
            return;
        }

        if (flame.GetComponent<StoveInteractable>() != null)
        {
            return;
        }

        if (flame.gameObject.GetComponent<Collider>() == null)
        {
            BoxCollider box = flame.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.9f, 0.9f, 0.9f);
        }

        flame.gameObject.AddComponent<StoveInteractable>();
        ProximityInteractable.EnsureOn(flame.gameObject, 2.2f, 1);
        Debug.Log("[Snowpaw Station] Магазин: печь подключена к очагу у задней стены.");
    }

    /// <summary>
    /// Недостающие стены. Размеры берутся из габаритов пола и целых стен,
    /// поэтому не зависят от того, куда модель поставили в сцене.
    /// Оболочка живёт отдельным корнем: сама модель остаётся нетронутой импортированной
    /// префаб-инстанцией, и её легко сравнивать с исходным FBX.
    ///
    /// Потолок намеренно НЕ строится: модель — открытая диорама, и зал должен
    /// просматриваться сверху так же, как его задумал художник. Стены прозрачные —
    /// они держат игрока внутри и дают тень/контур помещения, но не отрезают зал
    /// от улицы, которую видно через витрину.
    /// </summary>
    private void BuildShell()
    {
        if (!TryResolveRoom(out Bounds room, out float wallTop))
        {
            Debug.LogWarning("[Snowpaw Station] Магазин: пол/стены модели не найдены, оболочка не построена.");
            return;
        }

        Transform root = ResolveShellRoot();
        // Материал нужен только если панели действительно рисуются.
        Material shellMaterial = showShellWalls ? CreateShellMaterial() : null;
        float floorY = room.max.y;

        BuildWestWall(root, shellMaterial, room, floorY, wallTop);
        BuildSouthWall(root, shellMaterial, room, floorY, wallTop);
        BuildDoorBarrier(root, room, floorY, wallTop);
    }

    /// <summary>
    /// Загородка дверного проёма. Пол модели кончается на передней стене, и через
    /// открытый проём игрок уходил в пустоту и падал. Ставим в проём сплошную панель:
    /// она держит игрока, но лежит на Ignore Raycast — камера её не видит, иначе
    /// третье лицо упиралось в невидимую стену и подтягивалось к персонажу.
    ///
    /// Выход из магазина остаётся прежним: SceneDoorTrigger срабатывает по близости,
    /// проходить сквозь створку не нужно.
    /// </summary>
    private void BuildDoorBarrier(Transform root, Bounds room, float floorY, float wallTop)
    {
        int barrierLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (barrierLayer < 0)
        {
            return;
        }

        float z = room.max.z;
        float doorX = ResolveDoorX(room);
        float half = doorwayWidth * 0.5f;
        float doorTop = Mathf.Min(floorY + doorwayHeight, wallTop - 0.05f);

        float doorLeft = Mathf.Clamp(doorX - half, room.min.x + 0.05f, room.max.x - 0.05f);
        float doorRight = Mathf.Clamp(doorX + half, room.min.x + 0.05f, room.max.x - 0.05f);

        GameObject barrier = new GameObject("InteriorDoorBarrier");
        barrier.transform.SetParent(root, false);
        barrier.layer = barrierLayer;

        // Панель чуть толще плоскости стены: нулевой толщины коллайдер CharacterController
        // проскакивает на быстром шаге.
        BoxCollider box = barrier.AddComponent<BoxCollider>();
        box.center = new Vector3((doorLeft + doorRight) * 0.5f, (floorY + doorTop) * 0.5f, z);
        box.size = new Vector3(Mathf.Max(doorRight - doorLeft, 0.1f), Mathf.Max(doorTop - floorY, 0.1f), 0.4f);
        box.isTrigger = false;
    }

    /// <summary>
    /// Контейнер оболочки: переиспользуется между загрузками. Корень стоит в нуле,
    /// потому что вершины панелей задаются в мировых координатах комнаты — иначе
    /// смещение корня сдвинуло бы готовую оболочку от модели.
    /// </summary>
    private Transform ResolveShellRoot()
    {
        const string ShellRootName = "InteriorShell";
        GameObject existing = GameObject.Find(ShellRootName);
        if (existing != null)
        {
            return existing.transform;
        }

        GameObject shell = new GameObject(ShellRootName);
        shell.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        shell.transform.localScale = Vector3.one;
        shell.layer = ResolveShellLayer();
        return shell.transform;
    }

    /// <summary>
    /// Слой панелей — Ignore Raycast. Стены невидимы, поэтому камера третьего лица
    /// не должна в них упираться: иначе она подтягивается к персонажу рывком у самого
    /// края зала. На этом слое панели остаются твёрдыми для игрока (столкновения
    /// слоёв по умолчанию включены), но камера, чья маска — только Default, их
    /// не замечает и проходит насквозь.
    ///
    /// Видимая геометрия модели (пол, задняя и восточная стены) лежит на Default и
    /// для камеры остаётся препятствием — это то поведение, которого ждёт игрок.
    /// </summary>
    private static int ResolveShellLayer()
    {
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        return layer >= 0 ? layer : 2;
    }

    private bool TryResolveRoom(out Bounds room, out float wallTop)
    {
        room = default;
        wallTop = 0f;

        bool hasFloor = InteriorShopAnchors.TryGetBounds(FloorMesh, out Bounds floor);
        bool hasEast = InteriorShopAnchors.TryGetBounds(WallMeshEast, out Bounds east);
        bool hasBack = InteriorShopAnchors.TryGetBounds(WallMeshBack, out Bounds back);

        if (!hasFloor)
        {
            return false;
        }

        room = floor;
        if (hasEast)
        {
            wallTop = Mathf.Max(wallTop, east.max.y);
        }

        if (hasBack)
        {
            wallTop = Mathf.Max(wallTop, back.max.y);
        }

        if (wallTop <= room.max.y)
        {
            // Стены не найдены — берём разумную высоту потолка над полом.
            wallTop = room.max.y + 4.2f;
        }

        return true;
    }

    private void BuildWestWall(Transform root, Material material, Bounds room, float floorY, float wallTop)
    {
        float x = room.min.x;
        Vector3 a = new Vector3(x, floorY, room.min.z);
        Vector3 b = new Vector3(x, floorY, room.max.z);
        Vector3 c = new Vector3(x, wallTop, room.max.z);
        Vector3 d = new Vector3(x, wallTop, room.min.z);
        CreatePanel(root, "InteriorWall_West", a, b, c, d, Vector3.right, material, showShellWalls);
    }
    private void BuildSouthWall(Transform root, Material material, Bounds room, float floorY, float wallTop)
    {
        float z = room.max.z;
        float doorX = ResolveDoorX(room);
        float half = doorwayWidth * 0.5f;
        float doorTop = Mathf.Min(floorY + doorwayHeight, wallTop - 0.05f);

        float leftEdge = room.min.x;
        float rightEdge = room.max.x;
        float doorLeft = Mathf.Clamp(doorX - half, leftEdge + 0.05f, rightEdge - 0.05f);
        float doorRight = Mathf.Clamp(doorX + half, leftEdge + 0.05f, rightEdge - 0.05f);

        // Передняя стена смотрит внутрь зала, то есть в сторону −Z.
        Vector3 inward = Vector3.back;

        // Слева от проёма.
        if (doorLeft > leftEdge + 0.01f)
        {
            CreatePanel(root, "InteriorWall_South_L",
                new Vector3(leftEdge, floorY, z), new Vector3(doorLeft, floorY, z),
                new Vector3(doorLeft, wallTop, z), new Vector3(leftEdge, wallTop, z), inward, material, showShellWalls);
        }

        // Справа от проёма.
        if (rightEdge > doorRight + 0.01f)
        {
            CreatePanel(root, "InteriorWall_South_R",
                new Vector3(doorRight, floorY, z), new Vector3(rightEdge, floorY, z),
                new Vector3(rightEdge, wallTop, z), new Vector3(doorRight, wallTop, z), inward, material, showShellWalls);
        }

        // Над проёмом.
        if (wallTop > doorTop + 0.01f)
        {
            CreatePanel(root, "InteriorWall_South_Top",
                new Vector3(doorLeft, doorTop, z), new Vector3(doorRight, doorTop, z),
                new Vector3(doorRight, wallTop, z), new Vector3(doorLeft, wallTop, z), inward, material, showShellWalls);
        }
    }

    /// <summary>Проём ставим туда, где стоит Door_Exit, иначе по центру передней стены.</summary>
    private static float ResolveDoorX(Bounds room)
    {
        GameObject door = GameObject.Find("Door_Exit");
        float x = door != null ? door.transform.position.x : room.center.x;
        return Mathf.Clamp(x, room.min.x + 0.6f, room.max.x - 0.6f);
    }

    /// <summary>
    /// Перевод материала в прозрачный режим. Шейдер PS1 свой (не URP/Lit), поэтому
    /// выставляем поверхность вручную: альфа-блендинг, отключённая запись глубины и
    /// очередь «прозрачное». Для URP-материалов дополнительно правим _Surface/ключевые
    /// слова, иначе редактор и шейдер согласятся не сразу.
    /// </summary>
    private static void ApplyTranslucent(Material material, float alpha, int renderQueue)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        // Дверь видна с обеих сторон, поэтому обратноотсечение выключено.
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = renderQueue;
    }

    /// <summary>
    /// Прямоугольная панель. Коллайдер ставится всегда — он и держит игрока в зале.
    /// Меш с материалом добавляется только при <paramref name="visible"/>: невидимые
    /// стены не должны ни рисоваться, ни отбрасывать тень, ни попадать в отражения.
    /// Нормали разворачиваются внутрь помещения (<paramref name="inward"/>), чтобы
    /// внутренняя сторона панели освещалась лампами, если её всё-таки покажут.
    /// </summary>
    private static void CreatePanel(
        Transform root, string name, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inward,
        Material material, bool visible)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(root, false);
        panel.layer = root.gameObject.layer;

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(new[] { a, b, c, d });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
        mesh.RecalculateNormals();

        // Треугольники задают нормаль по правилу правой руки; если она смотрит наружу,
        // разворачиваем порядок вершин, чтобы свет попал на видимую изнутри сторону.
        if (mesh.normals.Length > 0 && Vector3.Dot(mesh.normals[0], inward) < 0f)
        {
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();

        MeshFilter filter = panel.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        if (visible)
        {
            MeshRenderer renderer = panel.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
        }

        MeshCollider collider = panel.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    /// <summary>
    /// Материал оболочки: тот же PS1-шейдер, что и у модели, с цветом её стен,
    /// но в прозрачном режиме — стены только намечают границу зала и не отрезают
    /// интерьер от улицы, которую видно через витрину.
    /// </summary>
    private Material CreateShellMaterial()
    {
        Shader shader = Shader.Find("Snowpaw/PSX Lit Vertex Snap");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        Material material = new Material(shader) { name = "InteriorShell" };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", wallColor);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", wallColor);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.05f);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_SnapRows"))
        {
            material.SetFloat("_SnapRows", 400f);
        }

        // Стены прозрачные, но коллайдер у панелей остаётся: игрока держит
        // именно столкновение, а не непрозрачность.
        ApplyTranslucent(material, wallAlpha, 3000);

        return material;
    }
}
