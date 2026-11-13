using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Подъезд и отъезд сюжетного трака у первой заправки прохождения.</summary>
[DisallowMultipleComponent]
public sealed class TruckArrivalController : MonoBehaviour
{
    private const string TruckAssetPath = "Assets/RawAssets/Models/fbx/truck.fbx";
    private const float StartDistance = 14f;
    // Сколько трак проезжает вперёд от колонки перед тем, как исчезнуть (без разворота).
    private const float DepartDistance = 12f;
    // Зазор между поверхностью колонки и ближним бортом трака.
    private const float PumpSideGap = 0.55f;
    // Радиус колонки, если у неё нет коллайдера (цилиндр 0.6 → r = 0.3).
    private const float DefaultPumpRadius = 0.3f;
    // Запасной боковой уступ, если габариты модели получить не удалось (полуширина борта ≈ 1.5 м).
    private const float FallbackHalfWidth = 1.5f;
    // Дальше этого шланг не тянем: трак стоит у другой колонки.
    private const float MaxFuelHoseDistance = 6f;
    private const float TravelSpeed = 4f;
    private const float RotationSpeed = 8f;
    private const float ModelYawOffset = 270f;
    private const float CameraDistance = 3f;
    private const float CameraHeight = 6.5f;
    private const float CameraSideOffset = 1.5f;
    private const float CameraLookHeight = 1.2f;
    private const float ParkingSideOffset = 1.8f;
    private const float ParkingForwardOffset = 2.2f;
    private static readonly Vector3 TruckMeshLocalPosition = new Vector3(-0.47f, 0.5f, 2.6f);
    private static readonly Vector3 EmbeddedCameraLocalPosition = new Vector3(-7.3298f, 3.5f, 6.88517f);
    private const float EmbeddedCameraYaw = 125f;

    private Transform truck;
    private Vector3 approachPoint;
    private Vector3 exitPoint;
    private Vector3 leavePoint;
    private Vector3 bodyCenterOffset;
    private Vector3 bodySize;
    private Action arrived;
    private Action departed;
    private Motion motion;
    private Camera sceneCamera;
    private Vector3 previousCameraPosition;
    private Quaternion previousCameraRotation;
    private Behaviour cameraBrain;
    private Vector3 travelForward;
    private Coroutine delayedArrival;

    private enum Motion
    {
        None,
        Arriving,
        Parked,
        Leaving,
    }

    public bool IsActive => motion != Motion.None && truck != null;

    /// <summary>Стоящий у колонки трак (null — трака на месте нет).</summary>
    public bool IsParked => motion == Motion.Parked && truck != null;

    /// <summary>
    /// Точка на борту стоящего трака, обращённая к колонке pump: туда вставляется пистолет.
    /// false — трак не на месте или стоит у другой колонки (шланг тянуть некуда).
    /// </summary>
    public bool TryGetFuelPort(Transform pump, out Vector3 port)
    {
        port = Vector3.zero;
        if (!IsParked || pump == null || bodySize.sqrMagnitude <= 0f)
        {
            return false;
        }

        Bounds body = new Bounds(truck.position + bodyCenterOffset, bodySize);
        Vector3 toPump = pump.position - body.center;
        toPump.y = 0f;
        if (toPump.sqrMagnitude > MaxFuelHoseDistance * MaxFuelHoseDistance)
        {
            return false;
        }

        bool alongX = Mathf.Abs(toPump.x) >= Mathf.Abs(toPump.z);
        bool pumpOnPositiveSide = alongX ? toPump.x >= 0f : toPump.z >= 0f;

        float x;
        float z;
        if (alongX)
        {
            x = pumpOnPositiveSide ? body.max.x : body.min.x;
            z = Mathf.Clamp(pump.position.z, body.min.z + 0.4f, body.max.z - 0.4f);
        }
        else
        {
            x = Mathf.Clamp(pump.position.x, body.min.x + 0.4f, body.max.x - 0.4f);
            z = pumpOnPositiveSide ? body.max.z : body.min.z;
        }

        port = new Vector3(x, body.min.y + body.size.y * 0.55f, z);
        return true;
    }

    public bool BeginDelayed(Transform pump, float delaySeconds, Action onArrived)
    {
        if (IsActive || delayedArrival != null || pump == null)
        {
            return false;
        }

        delayedArrival = StartCoroutine(BeginAfterDelay(pump, delaySeconds, onArrived));
        return true;
    }

    public bool Begin(Transform pump, Action onArrived, Action onDeparted = null)
    {
        if (IsActive || pump == null)
        {
            return false;
        }

        GameObject model = LoadTruckModel();
        if (model == null)
        {
            Debug.LogError($"[Snowpaw Station] Не удалось загрузить модель трака: {TruckAssetPath}.");
            return false;
        }

        arrived = onArrived;
        departed = onDeparted;
        travelForward = pump.forward;
        travelForward.y = 0f;
        travelForward = travelForward.sqrMagnitude > 0.01f ? travelForward.normalized : Vector3.forward;

        Quaternion travelRotation = Quaternion.LookRotation(travelForward, Vector3.up)
            * Quaternion.Euler(0f, ModelYawOffset, 0f);
        truck = Instantiate(model, Vector3.zero, travelRotation).transform;
        truck.name = "FirstOrderFuelTruck";
        ConfigureTruckModel();
        DisableEmbeddedCameras();
        DisableEmbeddedLights();

        // Точку остановки считаем от реальных габаритов модели: меш внутри fbx смещён,
        // из-за жёсткой константы корпус вставал прямо в колонку.
        approachPoint = ComputeApproachPoint(pump, travelForward);
        exitPoint = approachPoint - travelForward * StartDistance;
        // Отъезд — дальше по ходу движения: трак уезжает вперёд, без разворота.
        leavePoint = approachPoint + travelForward * DepartDistance;
        truck.position = exitPoint;
        FocusCameraOnTruck();
        InteractionLock.Add("truck_arrival");
        motion = Motion.Arriving;
        Debug.Log($"[Snowpaw Station] Трак подъезжает к первой колонке. Точка остановки: {approachPoint}.");
        return true;
    }

    /// <summary>
    /// Куда встать: корпус трака справа от колонки, ближним бортом к ней, на земле под колонкой.
    /// Считаем по мировому AABB рендереров модели, поэтому любой сдвиг/масштаб узлов fbx учтён.
    /// </summary>
    private Vector3 ComputeApproachPoint(Transform pump, Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.01f)
        {
            right = Vector3.right;
        }

        right.Normalize();

        float pumpRadius = DefaultPumpRadius;
        float groundY = pump.position.y;
        if (pump.TryGetComponent(out Collider pumpCollider))
        {
            Vector3 extents = pumpCollider.bounds.extents;
            pumpRadius = Mathf.Max(extents.x, extents.z);
            groundY = pumpCollider.bounds.min.y;
        }

        if (!TryGetTruckBounds(out Bounds body))
        {
            bodyCenterOffset = Vector3.zero;
            bodySize = Vector3.zero;
            Vector3 fallback = pump.position + right * (pumpRadius + PumpSideGap + FallbackHalfWidth);
            fallback.y = groundY;
            return fallback;
        }

        // Полугабариты корпуса вдоль сторон света трака (AABB → проекция на оси).
        float halfAlongRight = Mathf.Abs(right.x) * body.extents.x + Mathf.Abs(right.z) * body.extents.z;

        // Центр корпуса относительно корня: нужен и для парковки, и для кадра камеры.
        bodyCenterOffset = body.center - truck.position;
        bodySize = body.size;

        // Ближний борт — на зазор от поверхности колонки, корпус — по центру колонки вдоль подъезда.
        float targetRight = Vector3.Dot(pump.position, right) + pumpRadius + PumpSideGap + halfAlongRight;
        float targetForward = Vector3.Dot(pump.position, forward);

        Vector3 shift = right * (targetRight - Vector3.Dot(body.center, right))
            + forward * (targetForward - Vector3.Dot(body.center, forward))
            + Vector3.up * (groundY - body.min.y);

        return truck.position + shift;
    }

    /// <summary>Мировые габариты корпуса трака (без партиклов и прочих не-мешей).</summary>
    private bool TryGetTruckBounds(out Bounds bounds)
    {
        bounds = default;
        if (truck == null)
        {
            return false;
        }

        Renderer[] renderers = truck.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!(renderer is MeshRenderer) || renderer.bounds.size.sqrMagnitude <= 0f)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                found = true;
            }
        }

        return found;
    }

    private void DisableEmbeddedCameras()
    {
        Camera[] embeddedCameras = truck.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < embeddedCameras.Length; i++)
        {
            embeddedCameras[i].enabled = false;
            embeddedCameras[i].tag = "Untagged";
        }

    }

    private void DisableEmbeddedLights()
    {
        Light[] embeddedLights = truck.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < embeddedLights.Length; i++)
        {
            embeddedLights[i].enabled = false;
        }
    }

    private void ConfigureTruckModel()
    {
        Transform mesh = truck.Find("truck1");
        if (mesh != null)
        {
            mesh.localPosition = TruckMeshLocalPosition;
        }

        ConfigureTruckCollider();

        Transform embeddedCamera = truck.Find("Camera");
        if (embeddedCamera != null)
        {
            embeddedCamera.localPosition = EmbeddedCameraLocalPosition;
            Vector3 angles = embeddedCamera.localEulerAngles;
            angles.y = EmbeddedCameraYaw;
            embeddedCamera.localEulerAngles = angles;
        }
    }

    private void ConfigureTruckCollider()
    {
        if (truck == null || truck.GetComponent<Collider>() != null)
        {
            return;
        }

        Renderer[] renderers = truck.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds localBounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!(renderers[i] is MeshRenderer) || renderers[i].bounds.size.sqrMagnitude <= 0f)
            {
                continue;
            }

            Bounds worldBounds = renderers[i].bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector3 localPoint = truck.InverseTransformPoint(corners[cornerIndex]);
                if (found)
                {
                    localBounds.Encapsulate(localPoint);
                }
                else
                {
                    localBounds = new Bounds(localPoint, Vector3.zero);
                    found = true;
                }
            }
        }

        if (!found)
        {
            return;
        }

        BoxCollider collider = truck.gameObject.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;
    }

    private System.Collections.IEnumerator BeginAfterDelay(Transform pump, float delaySeconds, Action onArrived)
    {
        yield return new WaitForSeconds(delaySeconds);
        delayedArrival = null;
        Begin(pump, onArrived);
    }

    public void Depart(Action onDeparted = null)
    {
        if (truck == null)
        {
            onDeparted?.Invoke();
            return;
        }

        departed = onDeparted ?? departed;
        motion = Motion.Leaving;
        Debug.Log("[Snowpaw Station] Трак уезжает от колонки.");
    }

    private void Update()
    {
        if (motion == Motion.None || truck == null)
        {
            return;
        }

        if (motion == Motion.Parked)
        {
            return;
        }

        Vector3 target = motion == Motion.Arriving ? approachPoint : leavePoint;
        truck.position = Vector3.MoveTowards(truck.position, target, TravelSpeed * Time.deltaTime);

        // Отъезд — строго по прямой: направление уже совпадает с курсом трака у колонки.
        if (motion == Motion.Arriving)
        {
            Vector3 direction = target - truck.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up)
                    * Quaternion.Euler(0f, ModelYawOffset, 0f);
                truck.rotation = Quaternion.Slerp(truck.rotation, rotation, RotationSpeed * Time.deltaTime);
            }
        }

        if ((truck.position - target).sqrMagnitude > 0.01f)
        {
            UpdateTruckCamera();
            return;
        }

        if (motion == Motion.Arriving)
        {
            truck.position = approachPoint;
            motion = Motion.Parked;
            RestoreCameraTarget();
            InteractionLock.Remove("truck_arrival");
            Action arrivalCallback = arrived;
            arrived = null;
            arrivalCallback?.Invoke();
            return;
        }

        Destroy(truck.gameObject);
        truck = null;
        RestoreCameraTarget();
        motion = Motion.None;
        Action departureCallback = departed;
        departed = null;
        departureCallback?.Invoke();
    }

    private void FocusCameraOnTruck()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            return;
        }

        previousCameraPosition = sceneCamera.transform.position;
        previousCameraRotation = sceneCamera.transform.rotation;
        cameraBrain = sceneCamera.GetComponent("CinemachineBrain") as Behaviour;
        if (cameraBrain != null)
        {
            cameraBrain.enabled = false;
        }

        UpdateTruckCamera();
    }

    private void UpdateTruckCamera()
    {
        if (sceneCamera == null || truck == null)
        {
            return;
        }

        // Кадр строим от корпуса, а не от корня модели: центр меша смещён от пивота на метры.
        Vector3 focus = truck.position + bodyCenterOffset;
        Vector3 groundFocus = new Vector3(focus.x, truck.position.y, focus.z);
        Vector3 cameraPosition = groundFocus - travelForward * CameraDistance
            + Vector3.Cross(Vector3.up, travelForward) * CameraSideOffset
            + Vector3.up * CameraHeight;
        Vector3 lookPoint = focus + Vector3.up * CameraLookHeight;
        sceneCamera.transform.SetPositionAndRotation(
            cameraPosition,
            Quaternion.LookRotation(lookPoint - cameraPosition, Vector3.up));
    }

    private void RestoreCameraTarget()
    {
        if (sceneCamera == null)
        {
            return;
        }

        if (cameraBrain != null)
        {
            cameraBrain.enabled = true;
        }

        sceneCamera.enabled = true;
        sceneCamera = null;
        cameraBrain = null;
    }

    private static GameObject LoadTruckModel()
    {
#if UNITY_EDITOR
        GameObject editorModel = AssetDatabase.LoadAssetAtPath<GameObject>(TruckAssetPath);
        if (editorModel != null)
        {
            return editorModel;
        }
#endif
        return Resources.Load<GameObject>("truck");
    }

    private void OnDestroy()
    {
        InteractionLock.Remove("truck_arrival");
    }
}
