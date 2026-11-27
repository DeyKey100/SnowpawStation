using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-only спавнер StationMain. Живёт рядом с Day1WorldBootstrap и сохраняет
/// очищенные слоты при посещении магазина. Точки берёт из расставленных в сцене
/// SnowSpawnPoint; если их нет, создаёт набор по умолчанию (DefaultPoints), так что
/// сохранённая сцена может оставаться без изменений.
/// </summary>
[DisallowMultipleComponent]
public sealed class SnowPileSpawner : MonoBehaviour
{
    internal const float GroundY = 0.5f;

    // Запас по X/Z включает место для обхода игроком; нижняя грань выше SnowFloor.
    internal static readonly Vector3 CheckHalfExtents = new Vector3(1.5f, 0.24f, 1.25f);

    private const string StationSceneName = "StationMain";
    private const float RetryInterval = 1f;
    private const int MaxPileCount = 6;

    private struct DefaultPoint
    {
        public Vector3 position;
        public string taskId;
    }

    // Набор по умолчанию: используется, пока в StationMain не расставлены SnowSpawnPoint,
    // и как шаблон для меню Snowpaw/Snow Piles/Create Default Spawn Points.
    // Координаты выбраны по мировым bounds StationMain, не по смещённым pivot FBX.
    // Центральные островки колонок и полоса движения машины справа от них свободны.
    private static readonly DefaultPoint[] DefaultPoints =
    {
        new DefaultPoint { position = new Vector3(-13.7f, 0.75f, -4.7f), taskId = "snow_door" },   // подход к магазину с юга
        new DefaultPoint { position = new Vector3(-7.1f, 0.75f, -6.35f), taskId = "snow_pump1" },  // южный подход к колонке №1, левее полосы машины
        new DefaultPoint { position = new Vector3(-7.1f, 0.75f, -8.95f), taskId = "snow_pump1" },  // продолжение подхода к колонке №1
        new DefaultPoint { position = new Vector3(-17.5f, 0.75f, -5.5f), taskId = "snow_yard" },   // западный двор, в стороне от колоды
        new DefaultPoint { position = new Vector3(-15f, 0.75f, -8.5f), taskId = "snow_yard" },
        new DefaultPoint { position = new Vector3(-11.5f, 0.75f, -10.8f), taskId = "snow_yard" },
        new DefaultPoint { position = new Vector3(-18f, 0.75f, -9f), taskId = null },              // свободные точки для визуальных сугробов
        new DefaultPoint { position = new Vector3(-14f, 0.75f, -12f), taskId = null },
        new DefaultPoint { position = new Vector3(-11f, 0.75f, -14f), taskId = null },
        new DefaultPoint { position = new Vector3(-18f, 0.75f, -12.5f), taskId = null },
    };

    private enum PointFilter { MatchingTask, WithoutTask, Any }

    private readonly Collider[] overlapResults = new Collider[64];
    private readonly List<GameObject> sceneRoots = new List<GameObject>();
    private readonly List<SnowPileInteractable> pool = new List<SnowPileInteractable>(MaxPileCount);
    private readonly List<Renderer> scenery = new List<Renderer>();
    private readonly List<string> requiredTasks = new List<string>(MaxPileCount);
    private readonly List<SnowSpawnPoint> spawnPoints = new List<SnowSpawnPoint>();
    private readonly List<SnowSpawnPoint> pointBuffer = new List<SnowSpawnPoint>();
    private readonly bool[] clearedSlots = new bool[MaxPileCount];
    private readonly bool[] placedSlots = new bool[MaxPileCount];
    private readonly int[] occupiedPoints = new int[MaxPileCount];
    private readonly Action[] clearCallbacks = new Action[MaxPileCount];

    private DayLoopManager manager;
    private Scene stationScene;
    private Transform poolRoot;
    private int[] dayOrder = Array.Empty<int>();
    private int currentDay = -1;
    private int pileCount;
    private bool refreshPending;
    private float nextRetryTime;

    private void Awake()
    {
        for (int i = 0; i < MaxPileCount; i++)
        {
            int slot = i;
            clearCallbacks[i] = () => OnPileCleared(slot);
            occupiedPoints[i] = -1;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        refreshPending = true;
        // При повторном включении загрузка StationMain могла пройти без sceneLoaded.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Single);
        }
    }

    private void Start()
    {
        BindManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null)
            {
                pool[i].gameObject.SetActive(false);
            }
        }
        // DayStarted слушаем до уничтожения, чтобы не потерять даже StartDay того
        // же номера при временно выключенном координаторе.
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.DayStarted -= OnDayStarted;
        }
    }

    private bool BindManager()
    {
        DayLoopManager active = DayLoopManager.Instance;
        if (manager == active && active != null)
        {
            return true;
        }
        if (active == null)
        {
            return false;
        }
        if (manager != null)
        {
            manager.DayStarted -= OnDayStarted;
        }
        manager = active;
        manager.DayStarted += OnDayStarted;
        OnDayStarted(manager.CurrentDay);
        return true;
    }

    private void OnDayStarted(int day)
    {
        // Событие может прийти между FixedUpdate и продолжением clearing-корутины.
        // Сразу прерываем старые анимации, чтобы они не засчитались уже новому дню.
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null)
            {
                pool[i].gameObject.SetActive(false);
            }
        }
        currentDay = day;
        Array.Clear(clearedSlots, 0, clearedSlots.Length);
        refreshPending = true;
        nextRetryTime = 0f;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != StationSceneName ||
            (stationScene.IsValid() && stationScene.handle == scene.handle))
        {
            return;
        }

        stationScene = scene;
        pool.Clear();
        sceneRoots.Clear();
        scene.GetRootGameObjects(sceneRoots);
        // В сцене два отдельных корневых сугроба: собираем оба, включая неактивные.
        for (int i = 0; i < sceneRoots.Count; i++)
        {
            CollectPiles(sceneRoots[i].transform);
        }

        if (pool.Count == 0)
        {
            Debug.LogError("[Snowpaw Station] В StationMain нет шаблона SnowPileInteractable.", this);
            return;
        }

        GameObject holder = new GameObject("DailySnowPiles");
        SceneManager.MoveGameObjectToScene(holder, scene);
        poolRoot = holder.transform;
        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].gameObject.SetActive(false);
            pool[i].transform.SetParent(poolRoot, true);
        }
        SnowPileInteractable template = pool[0];
        while (pool.Count < MaxPileCount)
        {
            SnowPileInteractable copy = Instantiate(template, poolRoot);
            copy.name = "SnowPile_Pooled_" + pool.Count;
            pool.Add(copy);
        }

        CollectSpawnPoints(scene);
        if (spawnPoints.Count == 0)
        {
            CreateDefaultSpawnPoints(scene);
        }
        refreshPending = true;
        nextRetryTime = 0f;
    }

    /// <summary>Создаёт набор точек по умолчанию под parent (используется и меню редактора).</summary>
    public static void PopulateDefaultPoints(Transform parent, List<SnowSpawnPoint> created)
    {
        for (int i = 0; i < DefaultPoints.Length; i++)
        {
            GameObject pointObject = new GameObject("SnowPoint_" + i.ToString("00"));
            pointObject.transform.SetParent(parent, false);
            pointObject.transform.position = DefaultPoints[i].position;
            SnowSpawnPoint point = pointObject.AddComponent<SnowSpawnPoint>();
            point.Configure(DefaultPoints[i].taskId);
            created?.Add(point);
        }
    }

    private void CollectSpawnPoints(Scene scene)
    {
        spawnPoints.Clear();
        sceneRoots.Clear();
        scene.GetRootGameObjects(sceneRoots);
        for (int i = 0; i < sceneRoots.Count; i++)
        {
            sceneRoots[i].GetComponentsInChildren(true, pointBuffer);
            spawnPoints.AddRange(pointBuffer);
        }
        pointBuffer.Clear();
    }

    private void CreateDefaultSpawnPoints(Scene scene)
    {
        GameObject holder = new GameObject("SnowSpawnPoints");
        SceneManager.MoveGameObjectToScene(holder, scene);
        PopulateDefaultPoints(holder.transform, spawnPoints);
    }

    internal int ClearActivePilesImmediately()
    {
        int cleared = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].TryClearImmediately())
            {
                cleared++;
            }
        }

        return cleared;
    }

    private void CollectPiles(Transform node)
    {
        if (node.TryGetComponent(out SnowPileInteractable pile))
        {
            pool.Add(pile);
        }
        for (int i = 0; i < node.childCount; i++)
        {
            CollectPiles(node.GetChild(i));
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (stationScene.handle != scene.handle)
        {
            return;
        }
        stationScene = default;
        poolRoot = null;
        pool.Clear();
        spawnPoints.Clear();
        dayOrder = Array.Empty<int>();
        scenery.Clear();
        sceneRoots.Clear();
        Array.Clear(placedSlots, 0, placedSlots.Length);
        // clearedSlots живут до DayStarted, а не до выхода из StationMain.
    }

    private void FixedUpdate()
    {
        // При async-загрузке игрок перемещается ПОСЛЕ sceneLoaded. Проверяем уже
        // конечную позицию игрока, а не позицию капсулы из сохранённой сцены.
        if (SceneLoadingScreen.IsLoading || !BindManager() || !stationScene.IsValid() ||
            !stationScene.isLoaded || poolRoot == null || spawnPoints.Count == 0)
        {
            return;
        }
        if (refreshPending)
        {
            PrepareDay();
        }
        if (Time.fixedTime < nextRetryTime)
        {
            return;
        }
        nextRetryTime = Time.fixedTime + RetryInterval;
        PlacePendingSlots();
    }

    private void PrepareDay()
    {
        refreshPending = false;
        pileCount = Mathf.Min(MaxPileCount, DayPlans.GetSnowPileCount(currentDay));
        requiredTasks.Clear();
        IReadOnlyList<DayPlan.MorningTask> tasks = manager.GetCurrentMorningTasks();
        for (int i = 0; i < tasks.Count; i++)
        {
            DayPlan.MorningTask task = tasks[i];
            if (!task.required || string.IsNullOrEmpty(task.id) ||
                !task.id.StartsWith("snow", StringComparison.Ordinal))
            {
                continue;
            }
            for (int j = 0; j < task.target && requiredTasks.Count < MaxPileCount; j++)
            {
                requiredTasks.Add(task.id);
            }
        }
        pileCount = Mathf.Max(pileCount, requiredTasks.Count);
        Array.Clear(placedSlots, 0, placedSlots.Length);
        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < occupiedPoints.Length; i++)
        {
            occupiedPoints[i] = -1;
        }
        dayOrder = BuildDayOrder(currentDay);
        nextRetryTime = 0f;
    }

    /// <summary>
    /// Порядок обхода точек на день: перемешан с seed от номера дня, поэтому в разные дни
    /// заняты разные точки, а повторная загрузка того же дня даёт ту же раскладку.
    /// </summary>
    private int[] BuildDayOrder(int day)
    {
        int[] order = new int[spawnPoints.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        System.Random random = new System.Random(day * 7919 + order.Length);
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int swap = order[i];
            order[i] = order[j];
            order[j] = swap;
        }
        return order;
    }

    private void PlacePendingSlots()
    {
        bool hasPending = false;
        for (int i = 0; i < pileCount; i++)
        {
            hasPending |= !clearedSlots[i] && !placedSlots[i];
        }
        if (!hasPending)
        {
            return;
        }

        CollectScenery();
        // sceneLoaded и предыдущие SetActive/SetPosition уже отражены в физических запросах.
        Physics.SyncTransforms();
        for (int slot = 0; slot < pileCount; slot++)
        {
            if (clearedSlots[slot] || placedSlots[slot])
            {
                continue;
            }
            string taskId = slot < requiredTasks.Count ? requiredTasks[slot] : null;
            int pointIndex = FindFreePoint(taskId);
            if (pointIndex < 0)
            {
                continue;
            }
            Transform point = spawnPoints[pointIndex].transform;
            SnowPileInteractable pile = pool[slot];
            // Reset восстанавливает позицию шаблона, поэтому конфигурация ПЕРЕД размещением.
            pile.ConfigureSpawn(manager, taskId, clearCallbacks[slot]);
            pile.transform.SetPositionAndRotation(point.position, point.rotation);
            occupiedPoints[slot] = pointIndex;
            placedSlots[slot] = true;
            pile.gameObject.SetActive(true);
        }
        // Занятая игроком/новым пропом точка не используется: незаспавненные слоты
        // повторяют попытку раз в секунду, не сбрасывая уже активные/расчищаемые сугробы.
    }

    /// <summary>
    /// Точка для слота: сугроб задачи сначала ищет точку своей задачи, потом свободную,
    /// потом любую; визуальный сугроб — сначала свободную, потом любую.
    /// </summary>
    private int FindFreePoint(string taskId)
    {
        int pointIndex = -1;
        if (!string.IsNullOrEmpty(taskId))
        {
            pointIndex = FindFreePoint(PointFilter.MatchingTask, taskId);
        }
        if (pointIndex < 0)
        {
            pointIndex = FindFreePoint(PointFilter.WithoutTask, taskId);
        }
        if (pointIndex < 0)
        {
            pointIndex = FindFreePoint(PointFilter.Any, taskId);
        }
        return pointIndex;
    }

    private int FindFreePoint(PointFilter filter, string taskId)
    {
        for (int i = 0; i < dayOrder.Length; i++)
        {
            int pointIndex = dayOrder[i];
            SnowSpawnPoint point = spawnPoints[pointIndex];
            if (!IsUsable(point) || IsPointOccupied(pointIndex))
            {
                continue;
            }
            if (filter == PointFilter.MatchingTask && !point.ServesTask(taskId))
            {
                continue;
            }
            if (filter == PointFilter.WithoutTask && point.HasTask)
            {
                continue;
            }
            if (IsClear(point.transform.position))
            {
                return pointIndex;
            }
        }
        return -1;
    }

    private static bool IsUsable(SnowSpawnPoint point)
    {
        return point != null && point.IsActive && point.gameObject.activeInHierarchy;
    }

    private bool IsPointOccupied(int pointIndex)
    {
        for (int i = 0; i < pileCount; i++)
        {
            if (occupiedPoints[i] == pointIndex)
            {
                return true;
            }
        }
        return false;
    }

    private void CollectScenery()
    {
        scenery.Clear();
        sceneRoots.Clear();
        stationScene.GetRootGameObjects(sceneRoots);
        for (int i = 0; i < sceneRoots.Count; i++)
        {
            CollectRenderers(sceneRoots[i].transform);
        }
    }

    private void CollectRenderers(Transform node)
    {
        if (node == poolRoot || !node.gameObject.activeInHierarchy)
        {
            return;
        }
        if (node.TryGetComponent(out Renderer renderer) && renderer.enabled)
        {
            scenery.Add(renderer);
        }
        for (int i = 0; i < node.childCount; i++)
        {
            CollectRenderers(node.GetChild(i));
        }
    }

    private bool IsClear(Vector3 position)
    {
        Vector3 center = new Vector3(position.x, GroundY + 0.26f, position.z);
        Bounds check = new Bounds(center, CheckHalfExtents * 2f);
        // Bounds пропов тоже проверяются: WoodenWithAxe/новые модели могут не иметь Collider.
        for (int i = 0; i < scenery.Count; i++)
        {
            Bounds bounds = scenery[i].bounds;
            if (bounds.max.y > GroundY + 0.01f && check.Intersects(bounds))
            {
                return false;
            }
        }
        int count = Physics.OverlapBoxNonAlloc(center, CheckHalfExtents, overlapResults,
            Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        if (count == overlapResults.Length)
        {
            return false; // переполненный буфер не считается свободным местом
        }
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapResults[i];
            if (collider != null && !collider.transform.IsChildOf(poolRoot))
            {
                return false;
            }
        }
        // Новые экземпляры ещё не синхронизированы с Physics: проверяем принятые места сами.
        for (int i = 0; i < pileCount; i++)
        {
            if (occupiedPoints[i] >= 0 && occupiedPoints[i] < spawnPoints.Count)
            {
                Vector3 other = spawnPoints[occupiedPoints[i]].transform.position;
                if (Mathf.Abs(position.x - other.x) < CheckHalfExtents.x * 2f &&
                    Mathf.Abs(position.z - other.z) < CheckHalfExtents.z * 2f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void OnPileCleared(int slot)
    {
        clearedSlots[slot] = true;
        placedSlots[slot] = false;
        // Не SetActive(false) внутри завершающейся корутины. Renderer/Collider скрыты
        // самим интерактивом, объект переиспользуется на следующий DayStarted.
    }
}
