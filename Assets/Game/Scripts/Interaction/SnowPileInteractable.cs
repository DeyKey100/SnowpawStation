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

    private void Awake()
    {
        EnsureMoundMesh();
        visualTransform = snowRenderer != null ? snowRenderer.transform : transform;
        initialLocalPosition = visualTransform.localPosition;
        initialLocalScale = visualTransform.localScale;

        if (dayLoopManager == null)
        {
            dayLoopManager = DayLoopManager.Instance;
        }
        if (dayLoopManager != null)
        {
            observedDay = dayLoopManager.currentDay;
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
        if (dayLoopManager == null)
        {
            dayLoopManager = DayLoopManager.Instance;
        }

        if (dayLoopManager != null && dayLoopManager.currentDay != observedDay)
        {
            observedDay = dayLoopManager.currentDay;
            ResetForNewDay();
        }
    }

    public string GetInteractPrompt()
    {
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

        return "[E] Расчистить сугроб";
    }

    public void Interact()
    {
        RefreshForNewDay();

        if (cleared || clearingRoutine != null)
        {
            return;
        }

        if (dayLoopManager == null)
        {
            Debug.LogError("SnowPileInteractable не назначил DayLoopManager.", this);
            return;
        }

        if (!CanClearDuringCurrentPhase())
        {
            Debug.Log("Сейчас сугроб нельзя расчищать: дождитесь утренней фазы.", this);
            return;
        }

        clearingRoutine = StartCoroutine(ClearSnowRoutine());
    }

    private bool CanClearDuringCurrentPhase()
    {
        if (dayLoopManager == null)
        {
            return false;
        }

        return dayLoopManager.currentState == DayLoopManager.DayState.Morning ||
               dayLoopManager.MorningTaskCompleted;
    }

    private IEnumerator ClearSnowRoutine()
    {
        Vector3 startPosition = initialLocalPosition;
        Vector3 startScale = initialLocalScale;
        Vector3 endPosition = startPosition + Vector3.down * sinkDistance;
        Vector3 endScale = new Vector3(startScale.x, 0.01f, startScale.z);

        if (snowCollider != null)
        {
            snowCollider.enabled = false;
        }

        float elapsed = 0f;
        while (elapsed < clearingDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / clearingDuration);
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
        cleared = true;
        clearedDay = dayLoopManager != null ? dayLoopManager.currentDay : -1;
        clearingRoutine = null;

        if (snowRenderer != null)
        {
            snowRenderer.enabled = false;
        }

        if (dayLoopManager != null && !dayLoopManager.MorningTaskCompleted &&
            dayLoopManager.currentState == DayLoopManager.DayState.Morning)
        {
            Debug.Log("[Snowpaw Station] Обязательный сугроб расчищен. Утро завершено.", this);
            dayLoopManager.CompleteMorningTask();
        }
        else
        {
            Debug.Log("[Snowpaw Station] Дополнительный сугроб расчищен.", this);
        }
    }

    private void RefreshForNewDay()
    {
        if (dayLoopManager == null || clearedDay < 0 ||
            dayLoopManager.currentDay == clearedDay)
        {
            return;
        }

        observedDay = dayLoopManager.currentDay;
        ResetForNewDay();
    }

    private void ResetForNewDay()
    {
        if (clearingRoutine != null)
        {
            StopCoroutine(clearingRoutine);
            clearingRoutine = null;
        }

        cleared = false;
        clearedDay = -1;
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

    private void OnDisable()
    {
        if (clearingRoutine != null)
        {
            StopCoroutine(clearingRoutine);
            clearingRoutine = null;
        }

        if (visualTransform != null)
        {
            visualTransform.localPosition = initialLocalPosition;
            visualTransform.localScale = initialLocalScale;
        }
    }
}
