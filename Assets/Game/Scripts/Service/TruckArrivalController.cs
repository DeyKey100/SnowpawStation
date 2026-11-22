using System;
using UnityEngine;

/// <summary>
/// Подъезд и отъезд машины посетителя у колонки: модель выбирает вызывающий
/// (обычные посетители — случайная из Assets/NpcCars, сюжетные гости — заглушка).
/// Машина едет по прямой к колонке, крутит колёса по пройденному пути и стоит
/// на кинематическом Rigidbody, чтобы игрок не проходил сквозь корпус.
///
/// Покадрово анимирован только кузов: его раскачка пересчитывается ровно
/// 12 раз в секунду и между пересчётами держит позу. Сама машина и колёса
/// катятся плавно — рывками качается именно кузов, а не вся модель.
/// У каждой машины свой характер — скорость, разгон и размах раскачки.
/// </summary>
[DisallowMultipleComponent]
public sealed class TruckArrivalController : MonoBehaviour
{
    private const float StartDistance = 14f;
    // Сколько машина проезжает вперёд от колонки перед тем, как исчезнуть (без разворота).
    private const float DepartDistance = 12f;
    // Зазор между поверхностью колонки и ближним бортом машины.
    private const float PumpSideGap = 0.55f;
    // Радиус колонки, если у неё нет коллайдера (цилиндр 0.6 → r = 0.3).
    private const float DefaultPumpRadius = 0.3f;
    // Запасной боковой уступ, если габариты модели получить не удалось (полуширина борта ≈ 1.5 м).
    private const float FallbackHalfWidth = 1.5f;
    // Дальше этого шланг не тянем: машина стоит у другой колонки.
    private const float MaxFuelHoseDistance = 6f;
    private const float RotationSpeed = 8f;
    private const string CarObjectName = "VisitorCar";
    private const string BodyPivotName = "BodyPivot";

    // --- Характер движения. У каждой машины он свой: крейсерская скорость,
    // разгон и рывки выбираются случайно в этих пределах, поэтому две подряд
    // подъехавшие машины ведут себя по-разному.
    private const float MinCruiseSpeed = 3.0f;
    private const float MaxCruiseSpeed = 4.8f;
    private const float MinAcceleration = 1.6f;
    private const float MaxAcceleration = 3.2f;
    private const float DepartSpeedBoost = 1.15f;

    // --- Покадровая раскачка кузова. Сама машина едет по дороге каждый кадр,
    // а вот наклон корпуса пересчитывается ровно 12 раз в секунду: между
    // пересчётами кузов держит позу, как кадр анимации. Колёса и позиция при
    // этом остаются плавными — рывками качается только кузов.
    private const float SwayStepRate = 12f;
    private const float SwayStepDuration = 1f / SwayStepRate;
    private const int MaxSwayStepsPerFrame = 4;
    private const float ArriveTolerance = 0.05f;
    private const float SettleDuration = 0.9f;

    private Transform truck;
    private string modelPath;
    private Vector3 approachPoint;
    private Vector3 exitPoint;
    private Vector3 leavePoint;
    private Vector3 bodyCenterOffset;
    private Vector3 bodySize;
    private Action arrived;
    private Action departed;
    private Motion motion;
    private Vector3 travelForward;
    private CarWheelSpin wheelSpin;
    private Vector3 lastPosition;

    // Характер движения текущей машины.
    private float cruiseSpeed;
    private float acceleration;
    private float swayTimer;
    private float speed;
    private float settleTimer;

    // Раскачка корпуса. Отдельный узел внутри корня машины, чтобы наклон
    // не ломал и коллайдер, и постановку у колонки.
    private Transform bodyPivot;
    private Vector3 bodyBaseLocalPosition;
    private float pitch;
    private float roll;
    private float pitchVelocity;
    private float rollVelocity;
    private float lastSpeed;
    private float swayPhase;
    private float swayFrequency;

    private enum Motion
    {
        None,
        Arriving,
        Parked,
        Leaving,
    }

    public bool IsActive => motion != Motion.None && truck != null;

    /// <summary>Стоящая у колонки машина (null — машины на месте нет).</summary>
    public bool IsParked => motion == Motion.Parked && truck != null;

    /// <summary>Модель текущей машины (для восстановления после выхода из магазина).</summary>
    public string CurrentModelPath => modelPath;

    /// <summary>
    /// Точка на борту стоящей машины, обращённая к колонке pump: туда вставляется пистолет.
    /// false — машина не на месте или стоит у другой колонки (шланг тянуть некуда).
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

    /// <summary>Подъехать к колонке на случайной модели посетителя.</summary>
    public bool Begin(Transform pump, Action onArrived, Action onDeparted = null)
    {
        return Begin(NpcCarModels.RandomVisitorPath(), pump, onArrived, onDeparted);
    }

    /// <summary>Подъехать к колонке на конкретной модели (сюжетный гость — заглушка).</summary>
    public bool Begin(string assetPath, Transform pump, Action onArrived, Action onDeparted = null)
    {
        if (IsActive || pump == null)
        {
            return false;
        }

        string path = string.IsNullOrEmpty(assetPath) ? NpcCarModels.PlaceholderPath : assetPath;
        GameObject model = NpcCarModels.Load(path);
        if (model == null)
        {
            Debug.LogError($"[Snowpaw Station] Не удалось загрузить модель машины: {path}.");
            return false;
        }

        arrived = onArrived;
        departed = onDeparted;
        modelPath = path;
        travelForward = pump.forward;
        travelForward.y = 0f;
        travelForward = travelForward.sqrMagnitude > 0.01f ? travelForward.normalized : Vector3.forward;

        RollCharacter();

        truck = Instantiate(model, Vector3.zero, TravelRotation(travelForward)).transform;
        truck.name = CarObjectName;
        ConfigureModel(path, model);
        DisableEmbeddedCameras();
        DisableEmbeddedLights();

        // Точку остановки считаем от реальных габаритов модели: меш внутри fbx смещён,
        // из-за жёсткой константы корпус вставал прямо в колонку.
        approachPoint = ComputeApproachPoint(pump, travelForward);
        exitPoint = approachPoint - travelForward * StartDistance;
        // Отъезд — дальше по ходу движения: машина уезжает вперёд, без разворота.
        leavePoint = approachPoint + travelForward * DepartDistance;
        truck.position = exitPoint;
        lastPosition = exitPoint;

        InteractionLock.Add("truck_arrival");
        motion = Motion.Arriving;
        Debug.Log($"[Snowpaw Station] Посетитель подъезжает к колонке. Модель: {System.IO.Path.GetFileNameWithoutExtension(path)}.");
        return true;
    }

    /// <summary>Полностью сбросить и убрать текущую машину (при смене сцены или сбросе смены).</summary>
    public void CancelArrival()
    {
        arrived = null;
        departed = null;

        if (truck != null)
        {
            Destroy(truck.gameObject);
            truck = null;
        }

        wheelSpin = null;
        InteractionLock.Remove("truck_arrival");
        motion = Motion.None;
    }

    /// <summary>
    /// Мгновенно ставит машину у колонки (если игрок заходил в магазин и вернулся).
    /// Модель остаётся той, что подъехала: посетитель не меняет машину на ходу.
    /// </summary>
    public bool SpawnParked(Transform pump)
    {
        if (pump == null)
        {
            return false;
        }

        string path = string.IsNullOrEmpty(modelPath) ? NpcCarModels.RandomVisitorPath() : modelPath;
        CancelArrival();

        GameObject model = NpcCarModels.Load(path);
        if (model == null)
        {
            Debug.LogError($"[Snowpaw Station] Не удалось загрузить модель машины: {path}.");
            return false;
        }

        modelPath = path;
        travelForward = pump.forward;
        travelForward.y = 0f;
        travelForward = travelForward.sqrMagnitude > 0.01f ? travelForward.normalized : Vector3.forward;

        RollCharacter();

        truck = Instantiate(model, Vector3.zero, TravelRotation(travelForward)).transform;
        truck.name = CarObjectName;
        ConfigureModel(path, model);
        DisableEmbeddedCameras();
        DisableEmbeddedLights();

        approachPoint = ComputeApproachPoint(pump, travelForward);
        exitPoint = approachPoint - travelForward * StartDistance;
        leavePoint = approachPoint + travelForward * DepartDistance;

        truck.position = approachPoint;
        lastPosition = approachPoint;
        motion = Motion.Parked;
        settleTimer = 0f;
        return true;
    }

    /// <summary>
    /// Поворот машины «носом по направлению движения». Доворот берём из каталога:
    /// модели NpcCars смотрят носом в +Z, у заглушки меш развёрнут на 270°.
    /// </summary>
    private Quaternion TravelRotation(Vector3 forward)
    {
        return Quaternion.LookRotation(forward, Vector3.up)
            * Quaternion.Euler(0f, NpcCarModels.YawOffset(modelPath), 0f);
    }

    /// <summary>
    /// Куда встать: корпус машины справа от колонки, ближним бортом к ней, на земле под колонкой.
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

        // Полугабариты корпуса вдоль сторон света машины (AABB → проекция на оси).
        float halfAlongRight = Mathf.Abs(right.x) * body.extents.x + Mathf.Abs(right.z) * body.extents.z;

        // Центр корпуса относительно корня: нужен и для парковки, и для шланга.
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

    /// <summary>
    /// Мировые габариты корпуса (без партиклов и прочих не-мешей).
    /// Считаем по вершинам мешей: у некоторых моделей пустые узлы с огромными
    /// границами (jeep — 6.28 м вместо 2.41 м), и рендерер-границы врут.
    /// Если меш недоступен на чтение, откатываемся на границы рендерера.
    /// </summary>
    private bool TryGetTruckBounds(out Bounds bounds)
    {
        bounds = default;
        if (truck == null)
        {
            return false;
        }

        MeshFilter[] filters = truck.GetComponentsInChildren<MeshFilter>(true);
        bool found = false;
        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
            if (mesh.isReadable)
            {
                Vector3[] vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 point = toWorld.MultiplyPoint3x4(vertices[v]);
                    if (found)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                }

                continue;
            }

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.bounds.size.sqrMagnitude <= 0f)
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

    private void ConfigureModel(string assetPath, GameObject model)
    {
        // У заглушки меш внутри fbx сдвинут — новый каталог это правит сам.
        NpcCarModels.ApplyLayout(assetPath, truck);

        // Физика: кинематический Rigidbody + один корпусный бокс. Машина едет по своему
        // маршруту, но игрок и предметы не проходят сквозь неё.
        ConfigureRigidbody();
        ConfigureTruckCollider();

        // Колёса ищем до узла раскачки: нарезанные колёса фургона создаются здесь
        // и должны остаться прямыми детьми корня, а не попасть под наклон кузова.
        wheelSpin = CarWheelSpin.Attach(truck, assetPath);

        // Раскачка — на отдельном узле, чтобы не наклонять ни коллайдер, ни колёса.
        CreateBodyPivot();
    }

    private void ConfigureRigidbody()
    {
        Rigidbody body = truck.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = truck.gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
        // Без интерполяции: она сглаживает кинематическое тело между физическими
        // кадрами и превращает движение машины в ровное скольжение.
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void ConfigureTruckCollider()
    {
        if (truck == null || truck.GetComponent<Collider>() != null)
        {
            return;
        }

        // Корпусный бокс по тем же вершинам, что и парковка: один коллайдер на машину,
        // иначе у jeep он накрывал бы всю стоянку.
        if (!TryGetTruckBounds(out Bounds worldBounds))
        {
            return;
        }

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

        Bounds localBounds = new Bounds(truck.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++)
        {
            localBounds.Encapsulate(truck.InverseTransformPoint(corners[i]));
        }

        BoxCollider collider = truck.gameObject.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;
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
        // Трогается не с нуля: водитель уже держал ногу на газе, пока расплачивался.
        speed = cruiseSpeed * 0.25f;
        lastSpeed = speed;
        Debug.Log("[Snowpaw Station] Посетитель уезжает от колонки.");
    }

    /// <summary>
    /// Выбрать характер движения для только что подъехавшей машины. Вызывается
    /// в Begin/SpawnParked, поэтому каждая машина едет по-своему: одна ползёт
    /// 3 м/с, другая гонит 4.8, у одной разгон вялый, у другой резкий.
    /// </summary>
    private void RollCharacter()
    {
        cruiseSpeed = UnityEngine.Random.Range(MinCruiseSpeed, MaxCruiseSpeed);
        acceleration = UnityEngine.Random.Range(MinAcceleration, MaxAcceleration);

        swayPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        swayFrequency = UnityEngine.Random.Range(0.7f, 1.3f);

        speed = 0f;
        lastSpeed = 0f;
        swayTimer = 0f;
        settleTimer = 0f;
        pitch = 0f;
        roll = 0f;
        pitchVelocity = 0f;
        rollVelocity = 0f;
    }

    private void Update()
    {
        if (motion == Motion.None)
        {
            return;
        }

        if (truck == null)
        {
            if (motion == Motion.Leaving)
            {
                motion = Motion.None;
                Action dep = departed;
                departed = null;
                dep?.Invoke();
            }
            else if (motion == Motion.Arriving)
            {
                motion = Motion.None;
                InteractionLock.Remove("truck_arrival");
                arrived = null;
            }

            return;
        }

        float dt = Time.deltaTime;

        // Едет машина каждый кадр — иначе на 60 fps она бы дёргалась целиком.
        AdvanceMotion(dt);

        // А вот кузов качается покадрово: поза раскачки пересчитывается ровно
        // 12 раз в секунду. Между пересчётами кузов держит наклон как на кадре
        // анимации, и это заметно именно на покачивании.
        swayTimer += dt;
        int steps = 0;
        while (swayTimer >= SwayStepDuration && steps < MaxSwayStepsPerFrame)
        {
            swayTimer -= SwayStepDuration;
            StepSway(SwayStepDuration);
            steps++;
        }

        if (motion == Motion.Arriving)
        {
            Vector3 toTarget = approachPoint - truck.position;
            if (toTarget.sqrMagnitude <= ArriveTolerance * ArriveTolerance)
            {
                truck.position = approachPoint;
                lastPosition = approachPoint;
                motion = Motion.Parked;
                speed = 0f;
                lastSpeed = 0f;
                settleTimer = SettleDuration;
                InteractionLock.Remove("truck_arrival");
                Action arrivalCallback = arrived;
                arrived = null;
                arrivalCallback?.Invoke();
            }

            return;
        }

        if ((truck.position - leavePoint).sqrMagnitude <= ArriveTolerance * ArriveTolerance)
        {
            Destroy(truck.gameObject);
            truck = null;
            wheelSpin = null;
            bodyPivot = null;
            motion = Motion.None;
            Action departureCallback = departed;
            departed = null;
            departureCallback?.Invoke();
        }
    }

    /// <summary>
    /// Один кадр движения: разгон к своей крейсерской скорости, проезд по прямой
    /// и вращение колёс по пройденному пути. Раскачки тут нет — она покадровая.
    /// </summary>
    private void AdvanceMotion(float dt)
    {
        if (motion == Motion.Parked)
        {
            settleTimer = Mathf.Max(0f, settleTimer - dt);
            return;
        }

        Vector3 target = motion == Motion.Arriving ? approachPoint : leavePoint;
        float desired = motion == Motion.Leaving ? cruiseSpeed * DepartSpeedBoost : cruiseSpeed;

        // Разгон не мгновенный: машина набирает ход и так же плавно гасит его у колонки.
        float distanceLeft = Vector3.Distance(truck.position, target);
        float braking = Mathf.Sqrt(2f * acceleration * Mathf.Max(distanceLeft, 0.0001f));
        speed = Mathf.MoveTowards(speed, Mathf.Min(desired, braking), acceleration * dt);

        Vector3 before = truck.position;
        truck.position = Vector3.MoveTowards(before, target, speed * dt);

        // Колёса крутятся ровно на пройденный путь — пробуксовки и «качения на месте» нет.
        if (wheelSpin != null)
        {
            wheelSpin.Advance(Vector3.Dot(truck.position - before, travelForward));
        }

        lastPosition = truck.position;

        // Подъезд — с доворотом носа по курсу; отъезд идёт строго по прямой.
        if (motion == Motion.Arriving)
        {
            Vector3 direction = target - truck.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion rotation = TravelRotation(direction.normalized);
                truck.rotation = Quaternion.Slerp(truck.rotation, rotation, RotationSpeed * dt);
            }
        }
    }

    /// <summary>
    /// Один кадр раскачки кузова: кивок при разгоне и торможении, крен на
    /// неровностях и лёгкое подпрыгивание на ходу. Вызывается ровно 12 раз
    /// в секунду, поэтому кузов меняет наклон ступенями.
    /// </summary>
    private void StepSway(float dt)
    {
        if (bodyPivot == null)
        {
            return;
        }

        bool driving = motion == Motion.Arriving || motion == Motion.Leaving;

        float accel = (speed - lastSpeed) / Mathf.Max(dt, 0.0001f);
        lastSpeed = speed;

        // Кивок: на разгоне нос вверх, на торможении клюёт вниз.
        float targetPitch = Mathf.Clamp(-accel * 0.28f, -1.8f, 1.8f);
        pitchVelocity += (targetPitch - pitch) * 64f * dt;
        pitchVelocity *= Mathf.Exp(-8f * dt);
        pitch += pitchVelocity * dt;

        // Крен и мелкая тряска — от «дороги»: две разные частоты дают вид рессор.
        float travelled = Time.time * swayFrequency + swayPhase;
        float targetRoll = Mathf.Sin(travelled * 2.3f) * 0.35f + Mathf.Sin(travelled * 5.7f) * 0.12f;
        if (!driving)
        {
            // Только что встала — качается ещё сильно, потом рессоры успокаиваются.
            float settle = Mathf.Clamp01(settleTimer / SettleDuration);
            targetRoll *= Mathf.Lerp(0.25f, 1f, settle);
        }

        rollVelocity += (targetRoll - roll) * 36f * dt;
        rollVelocity *= Mathf.Exp(-7f * dt);
        roll += rollVelocity * dt;

        bodyPivot.localRotation = Quaternion.Euler(pitch, 0f, roll);

        // Подпрыгивание: амплитуда растёт со скоростью и падает до нуля на стоянке.
        float bob = Mathf.Sin(travelled * 7.1f) * 0.006f * Mathf.Clamp01(speed / Mathf.Max(cruiseSpeed, 0.01f));
        bodyPivot.localPosition = bodyBaseLocalPosition + Vector3.up * bob;
    }

    /// <summary>
    /// Узел раскачки между корнем машины и её мешами. Корень остаётся
    /// выровненным по дороге (на нём висит коллайдер), а наклоняется только корпус.
    /// </summary>
    private void CreateBodyPivot()
    {
        GameObject pivot = new GameObject(BodyPivotName);
        pivot.transform.SetParent(truck, false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;

        bodyPivot = pivot.transform;
        bodyBaseLocalPosition = bodyPivot.localPosition;

        MoveChildrenUnderPivot();
    }

    /// <summary>
    /// Перенести под узел раскачки только кузов, сохраняя мировые позы. Колёса
    /// остаются прямыми детьми корня: они катятся по дороге и не должны
    /// наклоняться вместе с кузовом.
    /// </summary>
    private void MoveChildrenUnderPivot()
    {
        if (truck == null || bodyPivot == null)
        {
            return;
        }

        Transform[] children = new Transform[truck.childCount];
        int count = 0;
        for (int i = 0; i < truck.childCount; i++)
        {
            Transform child = truck.GetChild(i);
            if (child != bodyPivot && !IsWheel(child))
            {
                children[count] = child;
                count++;
            }
        }

        for (int i = 0; i < count; i++)
        {
            children[i].SetParent(bodyPivot, true);
        }
    }

    /// <summary>Колесо ли это: отдельный узел модели или нарезанное колесо фургона.</summary>
    private bool IsWheel(Transform candidate)
    {
        if (wheelSpin == null || wheelSpin.Wheels == null)
        {
            return false;
        }

        Transform[] wheels = wheelSpin.Wheels;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] != null && wheels[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDestroy()
    {
        InteractionLock.Remove("truck_arrival");
    }
}
