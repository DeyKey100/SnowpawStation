using System.Collections;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class SnowPileInteractable : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private DayLoopManager dayLoopManager;
    [SerializeField] private Renderer snowRenderer;
    [SerializeField] private Collider snowCollider;
    [SerializeField] private MeshFilter snowMeshFilter;

    [Header("Clearing")]
    [SerializeField, Tooltip("Id утренней задачи: snow_door (дверь, день 1) или snow_pump1 (колонка №1, день 2).")]
    private string taskId = "snow_door";
    [SerializeField, Min(0.1f)] private float clearingDuration = 1.25f;
    [SerializeField, Min(0f)] private float sinkDistance = 0.08f;
    [SerializeField] private AnimationCurve clearingEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Range(8, 24)] private int moundSegments = 12;
    [SerializeField, Range(2, 6)] private int moundRings = 4;

    private Transform visualTransform;
    private Vector3 initialLocalPosition;
    private Vector3 initialLocalScale;
    private Coroutine clearingRoutine;
    private bool cleared;
    private int clearedDay = -1;
    private int observedDay = -1;
    private bool initialized;
    private bool managedSpawn;
    private DayLoopManager spawnManager;
    private string assignedTaskId;
    private System.Action onCleared;
    private Vector3 animationStartLocalPosition;
    private Vector3 animationStartLocalScale;

    private DayLoopManager CurrentManager => managedSpawn ? spawnManager : dayLoopManager;

    private void Awake()
    {
        EnsureMoundMesh();
        if (!Application.IsPlaying(gameObject))
        {
            return;
        }

        EnsureInitialized();
        if (managedSpawn)
        {
            return;
        }

        if (dayLoopManager == null)
        {
            dayLoopManager = DayLoopManager.Instance;
        }
        if (dayLoopManager != null)
        {
            observedDay = dayLoopManager.CurrentDay;
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        EnsureMoundMesh();
        if (snowRenderer == null)
        {
            TryGetComponent(out snowRenderer);
        }
        if (snowCollider == null)
        {
            TryGetComponent(out snowCollider);
        }

        visualTransform = snowRenderer != null ? snowRenderer.transform : transform;
        initialLocalPosition = visualTransform.localPosition;
        initialLocalScale = visualTransform.localScale;
        initialized = true;
    }

    /// <summary>Configure an inactive pooled pile BEFORE moving it to its spawn point.</summary>
    public void ConfigureSpawn(DayLoopManager manager, string assignedTaskId, System.Action onCleared)
    {
        if (!Application.IsPlaying(gameObject))
        {
            return;
        }

        managedSpawn = true;
        spawnManager = manager;
        this.assignedTaskId = assignedTaskId;
        this.onCleared = onCleared;
        PrepareForSpawn();
    }

    /// <summary>Reset to the cached template pose BEFORE positioning/activating a pooled pile.</summary>
    public void PrepareForSpawn()
    {
        if (!Application.IsPlaying(gameObject))
        {
            return;
        }

        EnsureInitialized();
        if (clearingRoutine != null)
        {
            StopCoroutine(clearingRoutine);
            clearingRoutine = null;
        }

        cleared = false;
        clearedDay = -1;
        observedDay = CurrentManager != null ? CurrentManager.CurrentDay : -1;
        visualTransform.localPosition = initialLocalPosition;
        visualTransform.localScale = initialLocalScale;

        if (snowRenderer != null)
        {
            snowRenderer.enabled = true;
        }
        if (snowCollider != null)
        {
            snowCollider.enabled = true;
        }
    }

    private void OnValidate()
    {
        EnsureMoundMesh();
    }

    private const string MoundMeshName = "SnowPile_Mound_v3";

    private void EnsureMoundMesh()
    {
        if (snowMeshFilter == null)
        {
            snowMeshFilter = GetComponent<MeshFilter>();
        }

        if (snowMeshFilter == null)
        {
            return;
        }

        Mesh moundMesh = snowMeshFilter.sharedMesh;
        if (moundMesh != null && moundMesh.name == MoundMeshName)
        {
            return;
        }

        moundMesh = CreateMoundMesh();
        moundMesh.name = MoundMeshName;
        snowMeshFilter.sharedMesh = moundMesh;
    }

    [ContextMenu("Rebuild Snow Mound Mesh")]
    private void RebuildMoundMesh()
    {
        if (snowMeshFilter == null)
        {
            snowMeshFilter = GetComponent<MeshFilter>();
        }

        if (snowMeshFilter == null)
        {
            return;
        }

        snowMeshFilter.sharedMesh = null;
        EnsureMoundMesh();
    }

    private Mesh CreateMoundMesh()
    {
        int segments = Mathf.Clamp(moundSegments, 8, 24);
        int rings = Mathf.Clamp(moundRings, 2, 6);
        int vertexCount = rings * segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uv = new Vector2[vertexCount];
        int[] triangles = new int[rings * segments * 6];

        int vertexIndex = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            float normalizedHeight = (float)ring / rings;
            float domeHeight = Mathf.Sin(normalizedHeight * Mathf.PI * 0.5f);
            float height = Mathf.Lerp(-0.5f, 0.5f, domeHeight);
            float radius = 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - domeHeight * domeHeight));

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                float irregularity = 1f + 0.035f * Mathf.Sin(segment * 3.17f + ring * 1.41f);
                vertices[vertexIndex] = new Vector3(
                    Mathf.Cos(angle) * radius * irregularity,
                    height,
                    Mathf.Sin(angle) * radius * irregularity);
                uv[vertexIndex] = new Vector2(segment / (float)segments, domeHeight);
                vertexIndex++;
            }
        }

        int topIndex = rings * segments;
        vertices[topIndex] = new Vector3(0f, 0.5f, 0f);
        uv[topIndex] = new Vector2(0.5f, 1f);

        int bottomCenterIndex = topIndex + 1;
        vertices[bottomCenterIndex] = new Vector3(0f, -0.5f, 0f);
        uv[bottomCenterIndex] = new Vector2(0.5f, 0f);

        int triangleIndex = 0;

        // Боковые грани: обход даёт нормали наружу.
        for (int ring = 0; ring < rings - 1; ring++)
        {
            int currentRingStart = ring * segments;
            int nextRingStart = (ring + 1) * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int nextSegment = (segment + 1) % segments;
                triangles[triangleIndex++] = currentRingStart + segment;
                triangles[triangleIndex++] = nextRingStart + segment;
                triangles[triangleIndex++] = nextRingStart + nextSegment;
                triangles[triangleIndex++] = currentRingStart + segment;
                triangles[triangleIndex++] = nextRingStart + nextSegment;
                triangles[triangleIndex++] = currentRingStart + nextSegment;
            }
        }

        // Верхняя крышка: полюс -> следующий сегмент -> текущий, нормали смотрят вверх (+Y).
        int topRingStart = (rings - 1) * segments;
        for (int segment = 0; segment < segments; segment++)
        {
            int nextSegment = (segment + 1) % segments;
            triangles[triangleIndex++] = topIndex;
            triangles[triangleIndex++] = topRingStart + nextSegment;
            triangles[triangleIndex++] = topRingStart + segment;
        }

        // Нижняя крышка: нормали смотрят вниз (-Y), чтобы меш был замкнутым.
        for (int segment = 0; segment < segments; segment++)
        {
            int nextSegment = (segment + 1) % segments;
            triangles[triangleIndex++] = bottomCenterIndex;
            triangles[triangleIndex++] = segment;
            triangles[triangleIndex++] = nextSegment;
        }

        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            uv = uv
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Update()
    {
        if (!Application.IsPlaying(gameObject) || managedSpawn)
        {
            return;
        }

        if (dayLoopManager == null)
        {
            dayLoopManager = DayLoopManager.Instance;
        }

        if (dayLoopManager != null && dayLoopManager.CurrentDay != observedDay)
        {
            observedDay = dayLoopManager.CurrentDay;
            ResetForNewDay();
        }
    }

    public string GetInteractPrompt()
    {
        if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled)
        {
            return string.Empty;
        }

        RefreshForNewDay();

        if (cleared)
        {
            return string.Empty;
        }

        if (clearingRoutine != null)
        {
            return "Расчищаем сугроб...";
        }

        if (!CanClearDuringCurrentPhase())
        {
            return string.Empty;
        }

        // Записка — первая задача утра: пока её не прочли, сугроб не предлагает взаимодействие.
        if (!IsUnlocked())
        {
            return string.Empty;
        }

        return managedSpawn && string.IsNullOrEmpty(assignedTaskId)
            ? "[E] Расчистить сугроб (дополнительный)"
            : "[E] Расчистить сугроб";
    }

    /// <summary>Записка — первая задача утра: пока её не прочли, сугробы не считаются.</summary>
    private bool IsUnlocked()
    {
        DayLoopManager manager = CurrentManager;
        if (manager == null)
        {
            return true;
        }

        string id = ResolveTaskId();
        // Сугроба нет в плане дня (лишний сугроб) — записка ему не указ.
        return string.IsNullOrEmpty(id) || manager.IsMorningTaskUnlocked(id);
    }

    public void Interact()
    {
        if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled)
        {
            return;
        }

        RefreshForNewDay();

        if (cleared || clearingRoutine != null)
        {
            return;
        }

        if (CurrentManager == null)
        {
            Debug.LogError("SnowPileInteractable не назначил DayLoopManager.", this);
            return;
        }

        if (!CanClearDuringCurrentPhase())
        {
            Debug.Log("Сейчас сугроб нельзя расчищать: дождитесь утренней фазы.", this);
            return;
        }

        if (!IsUnlocked())
        {
            Debug.Log("[Snowpaw Station] Сугроб пока не считается: сначала записка в доме.", this);
            return;
        }

        clearingRoutine = StartCoroutine(ClearSnowRoutine());
    }

    private bool CanClearDuringCurrentPhase()
    {
        DayLoopManager manager = CurrentManager;
        if (manager == null)
        {
            return false;
        }

        return manager.CurrentState == DayLoopManager.DayState.Morning ||
               manager.MorningTaskCompleted;
    }

    private IEnumerator ClearSnowRoutine()
    {
        EnsureInitialized();
        // The root may have moved since PrepareForSpawn; animate from its spawn pose.
        Vector3 startPosition = visualTransform.localPosition;
        Vector3 startScale = visualTransform.localScale;
        animationStartLocalPosition = startPosition;
        animationStartLocalScale = startScale;
        Vector3 endPosition = startPosition + Vector3.down * sinkDistance;
        Vector3 endScale = new Vector3(startScale.x, 0.01f, startScale.z);

        if (snowCollider != null)
        {
            snowCollider.enabled = false;
        }

        float duration = Mathf.Max(0.1f, clearingDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = clearingEase != null
                ? clearingEase.Evaluate(normalizedTime)
                : Mathf.SmoothStep(0f, 1f, normalizedTime);

            visualTransform.localPosition = Vector3.LerpUnclamped(
                startPosition,
                endPosition,
                easedTime);
            visualTransform.localScale = Vector3.LerpUnclamped(
                startScale,
                endScale,
                easedTime);
            yield return null;
        }

        visualTransform.localPosition = endPosition;
        visualTransform.localScale = endScale;
        FinishClearing();
    }

    internal bool TryClearImmediately()
    {
        if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled)
        {
            return false;
        }

        RefreshForNewDay();
        if (cleared || !CanClearDuringCurrentPhase() || !IsUnlocked())
        {
            return false;
        }

        if (clearingRoutine != null)
        {
            StopCoroutine(clearingRoutine);
        }

        FinishClearing();
        return true;
    }

    private void FinishClearing()
    {
        cleared = true;
        DayLoopManager manager = CurrentManager;
        int completedDay = manager != null ? manager.CurrentDay : -1;
        string completedTaskId = ResolveTaskId();
        clearedDay = completedDay;
        clearingRoutine = null;

        if (snowCollider != null)
        {
            snowCollider.enabled = false;
        }
        if (snowRenderer != null)
        {
            snowRenderer.enabled = false;
        }

        // Snapshot before the callback: returning to the pool may reconfigure this pile.
        System.Action callback = onCleared;
        onCleared = null;
        try
        {
            callback?.Invoke();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (manager != null && manager.CurrentDay == completedDay &&
            manager.CurrentState == DayLoopManager.DayState.Morning &&
            !string.IsNullOrEmpty(completedTaskId))
        {
            Debug.Log("[Snowpaw Station] Сугроб расчищен.", this);
            manager.NotifyMorningTaskDone(completedTaskId);
        }
        else
        {
            Debug.Log("[Snowpaw Station] Дополнительный сугроб расчищен.", this);
        }
    }

    /// <summary>
    /// Задача сугроба зависит от дня (день 1 — дверь, день 2 — колонка №1).
    /// Если заданный id в плане дня отсутствует, берём первую «снежную» задачу дня.
    /// </summary>
    private string ResolveTaskId()
    {
        if (managedSpawn)
        {
            // null/empty explicitly means optional; never fall back to another snow task.
            return assignedTaskId;
        }

        if (dayLoopManager == null)
        {
            return null;
        }

        var tasks = dayLoopManager.GetCurrentMorningTasks();
        if (!string.IsNullOrEmpty(taskId))
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].id == taskId)
                {
                    return taskId;
                }
            }
        }

        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].id != null && tasks[i].id.StartsWith("snow"))
            {
                return tasks[i].id;
            }
        }

        return null;
    }

    private void RefreshForNewDay()
    {
        if (managedSpawn || dayLoopManager == null || clearedDay < 0 ||
            dayLoopManager.CurrentDay == clearedDay)
        {
            return;
        }

        observedDay = dayLoopManager.CurrentDay;
        ResetForNewDay();
    }

    private void ResetForNewDay()
    {
        PrepareForSpawn();
    }

    private void OnDisable()
    {
        if (clearingRoutine == null)
        {
            return;
        }

        StopCoroutine(clearingRoutine);
        clearingRoutine = null;
        if (visualTransform != null)
        {
            visualTransform.localPosition = animationStartLocalPosition;
            visualTransform.localScale = animationStartLocalScale;
        }
        if (snowRenderer != null)
        {
            snowRenderer.enabled = true;
        }
        if (snowCollider != null)
        {
            snowCollider.enabled = true;
        }
        // Interrupted clearing is not completion: no callback or task progress.
    }
}
