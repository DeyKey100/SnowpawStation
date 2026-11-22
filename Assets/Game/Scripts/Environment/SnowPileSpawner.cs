using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime-only спавнер StationMain. Живёт рядом с Day1WorldBootstrap и сохраняет
/// очищенные слоты при посещении магазина. Сохранённая сцена и её ассеты не меняются.
/// </summary>
[DisallowMultipleComponent]
public sealed class SnowPileSpawner : MonoBehaviour
{
    private const string StationSceneName = "StationMain";
    private const float GroundY = 0.5f;
    private const float RetryInterval = 1f;
    private const int MaxPileCount = 6;

    // Координаты выбраны по мировым bounds StationMain, не по смещённым pivot FBX.
    // Центральные островки колонок и полоса движения машины справа от них свободны.
    private static readonly Vector3[] SpawnPositions =
    {
        new Vector3(-13.7f, 0.75f, -4.7f),  // подход к магазину с юга
        new Vector3(-7.1f, 0.75f, -6.35f),  // южный подход к колонке №1, левее полосы машины
        new Vector3(-7.1f, 0.75f, -8.95f),  // продолжение подхода к колонке №1
        new Vector3(-17.5f, 0.75f, -5.5f),  // западный двор, в стороне от колоды
        new Vector3(-15f, 0.75f, -8.5f),
        new Vector3(-11.5f, 0.75f, -10.8f),
        new Vector3(-18f, 0.75f, -9f),       // запасные точки при занятом основном месте
        new Vector3(-14f, 0.75f, -12f),
        new Vector3(-11f, 0.75f, -14f),
        new Vector3(-18f, 0.75f, -12.5f),
    };

    private static readonly int[][] PointOrder =
    {
        new[] { 0, 3, 4, 5, 6, 7, 8, 9, 1, 2 },
        new[] { 1, 2, 0, 3, 4, 5, 6, 7, 8, 9 },
        new[] { 0, 3, 4, 1, 2, 5, 6, 7, 8, 9 },
    };

    // Запас по X/Z включает место для обхода игроком; нижняя грань выше SnowFloor.
    private static readonly Vector3 CheckHalfExtents = new Vector3(1.5f, 0.24f, 1.25f);
    private readonly Collider[] overlapResults = new Collider[64];
    private readonly List<GameObject> sceneRoots = new List<GameObject>();
    private readonly List<SnowPileInteractable> pool = new List<SnowPileInteractable>(MaxPileCount);
    private readonly List<Renderer> scenery = new List<Renderer>();
    private readonly List<string> requiredTasks = new List<string>(MaxPileCount);
    private readonly bool[] clearedSlots = new bool[MaxPileCount];
    private readonly bool[] placedSlots = new bool[MaxPileCount];
    private readonly int[] occupiedPoints = new int[MaxPileCount];
    private readonly Action[] clearCallbacks = new Action[MaxPileCount];

    private DayLoopManager manager;
    private Scene stationScene;
    private Transform poolRoot;
    private Transform pointRoot;
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

        GameObject points = new GameObject("SnowSpawnPoints");
        SceneManager.MoveGameObjectToScene(points, scene);
        pointRoot = points.transform;
        for (int i = 0; i < SpawnPositions.Length; i++)
        {
            Transform point = new GameObject("SnowPoint_" + i.ToString("00")).transform;
            point.SetParent(pointRoot, false);
            point.position = SpawnPositions[i];
        }
        refreshPending = true;
        nextRetryTime = 0f;
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
        pointRoot = null;
        pool.Clear();
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
            !stationScene.isLoaded || poolRoot == null)
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
        nextRetryTime = 0f;
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
        int[] order = PointOrder[Mathf.Clamp(currentDay - 1, 0, PointOrder.Length - 1)];
        for (int slot = 0; slot < pileCount; slot++)
        {
            if (clearedSlots[slot] || placedSlots[slot])
            {
                continue;
            }
            for (int attempt = 0; attempt < order.Length; attempt++)
            {
                int pointIndex = order[(slot + attempt) % order.Length];
                if (IsPointOccupied(pointIndex))
                {
                    continue;
                }
                Vector3 position = pointRoot.GetChild(pointIndex).position;
                if (!IsClear(position))
                {
                    continue;
                }
                SnowPileInteractable pile = pool[slot];
                string taskId = slot < requiredTasks.Count ? requiredTasks[slot] : null;
                // Reset восстанавливает позицию шаблона, поэтому конфигурация ПЕРЕД размещением.
                pile.ConfigureSpawn(manager, taskId, clearCallbacks[slot]);
                pile.transform.SetPositionAndRotation(position, Quaternion.identity);
                occupiedPoints[slot] = pointIndex;
                placedSlots[slot] = true;
                pile.gameObject.SetActive(true);
                break;
            }
        }
        // Занятая игроком/новым пропом точка не используется: незаспавненные слоты
        // повторяют попытку раз в секунду, не сбрасывая уже активные/расчищаемые сугробы.
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
            if (occupiedPoints[i] >= 0)
            {
                Vector3 other = pointRoot.GetChild(occupiedPoints[i]).position;
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
