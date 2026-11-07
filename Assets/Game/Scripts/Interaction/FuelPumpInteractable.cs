using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Колонка. E — начать отпуск топлива по текущему заказу, затем зажать ЛКМ:
/// литры растут ~8 л/с, отпустить означает сдачу заказа с оценкой объёма.
/// Подсказка появляется только у той колонки, к которой подъехал посетитель:
/// пустые колонки молчат. PlayerControlLock "refuel" включается только пока
/// игрок зажимает ЛКМ, поэтому до начала подачи топлива можно свободно подойти.
/// </summary>
[DisallowMultipleComponent]
public sealed class FuelPumpInteractable : MonoBehaviour, IInteractable
{
    private const float LitersPerSecond = 8f;
    private const float IdleCancelSeconds = 2f;

    // Шланг: провисающая линия от колонки к борту трака + утолщённый конец ("пистолет").
    private const int HoseSegments = 10;
    private const float HoseSag = 0.45f;
    private const float HoseWidth = 0.05f;
    private const float NozzleWidth = 0.1f;
    private const float NozzleLength = 0.22f;

    private ServiceDirector service;
    private bool fueling;
    private float poured;
    private bool armed;
    private float armedTimer;
    private TruckArrivalController truckArrival;
    private LineRenderer hose;
    private LineRenderer nozzle;
    private Material hoseMaterial;

    // Номер колонки хранится в сцене: иначе после загрузки сцены он сбрасывался бы в 0.
    [SerializeField] private int pumpIndex = 1;

    public int PumpIndex => pumpIndex;
    public bool IsFueling => fueling;

    public void Setup(int index) => pumpIndex = index;

    private void Awake()
    {
        service = ServiceDirector.Instance;
        if (service == null)
        {
            // ServiceDirector поднимается позже в том же кадре (StoryDirector.Awake) — лениво достанем позже.
            service = null;
        }
    }

    public string GetInteractPrompt()
    {
        ServiceDirector s = Service;
        if (s == null || !s.CanRefuel)
        {
            // Никто не приехал — колонка молчит и плашки не показывает.
            return "";
        }

        if (s.OrderPumpNumber != PumpIndex)
        {
            // Посетитель стоит у другой колонки: эта занята ничем, подсказки нет.
            return "";
        }

        string good = string.IsNullOrEmpty(s.CurrentOrder.item) ? "" : $" + {Goods.DisplayName(s.CurrentOrder.item)}";
        return $"[E] Заправить: {s.CurrentOrder.liters:0} л{good}";
    }

    private ServiceDirector Service => service ??= ServiceDirector.Instance;

    public void Interact()
    {
        ServiceDirector s = Service;
        if (s == null || !s.CanRefuel || fueling || s.OrderPumpNumber != PumpIndex)
        {
            return;
        }

        fueling = true;
        armed = true;
        poured = 0f;
        armedTimer = 0f;
        truckArrival = FindAnyObjectByType<TruckArrivalController>();
        Debug.Log("[Snowpaw Station] Заправка начата: зажмите ЛКМ.");
    }

    private void Update()
    {
        if (!fueling)
        {
            return;
        }

        UpdateHose();

        bool hold;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        hold = mouse != null && mouse.leftButton.isPressed;
#else
        hold = Input.GetMouseButton(0);
#endif

        if (hold)
        {
            if (armed)
            {
                InteractionLock.Add("refuel");
                // Игрок не двигается только во время фактической подачи топлива.
                PlayerControlLock.Add("refuel");
            }

            armed = false; // игрок реально начал качать
            ServiceDirector s = Service;
            float target = s != null && s.CurrentOrder != null ? s.CurrentOrder.liters : 0f;
            poured = Mathf.Min(poured + LitersPerSecond * Time.deltaTime, Mathf.Max(target * 1.2f, 5f));
            return;
        }

        if (!armed)
        {
            // Отпуск ЛКМ = сдача заказа.
            FinishRefuel();
            return;
        }

        // E нажали, но ЛКМ так и не зажали — не держим игрока в блокировке навсегда.
        armedTimer += Time.deltaTime;
        if (armedTimer >= IdleCancelSeconds)
        {
            Debug.Log("[Snowpaw Station] Заправка отменена: ЛКМ не зажата.");
            fueling = false;
            armed = false;
            ReleaseLocks();
        }
    }

    /// <summary>
    /// Пока идёт заправка — шланг от колонки к борту стоящего трака.
    /// Нет трака у колонки (или он у другой) — шланга нет.
    /// </summary>
    private void UpdateHose()
    {
        if (truckArrival == null || !truckArrival.TryGetFuelPort(transform, out Vector3 port))
        {
            HideHose();
            return;
        }

        if (hose == null)
        {
            CreateHose();
        }

        Vector3 pumpPoint = HoseAnchor();
        hose.enabled = true;
        nozzle.enabled = true;

        int count = HoseSegments;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 point = Vector3.Lerp(pumpPoint, port, t);
            point.y -= HoseSag * 4f * t * (1f - t);
            hose.SetPosition(i, point);
        }

        Vector3 direction = (port - hose.GetPosition(count - 2)).normalized;
        nozzle.SetPosition(0, port + direction * NozzleLength);
        nozzle.SetPosition(1, port - direction * NozzleLength * 0.6f);
    }

    private void CreateHose()
    {
        Material material = HoseMaterial();
        hose = CreateLine("FuelHose", material, HoseWidth, HoseSegments);
        nozzle = CreateLine("FuelNozzle", material, NozzleWidth, 2);
    }

    private LineRenderer CreateLine(string lineName, Material material, float width, int positions)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = positions;
        line.widthMultiplier = width;
        line.numCapVertices = 3;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        line.enabled = false;
        return line;
    }

    private void HideHose()
    {
        if (hose != null)
        {
            hose.enabled = false;
        }

        if (nozzle != null)
        {
            nozzle.enabled = false;
        }
    }

    /// <summary>Место пистолета на самой колонке — верх цилиндра.</summary>
    private Vector3 HoseAnchor()
    {
        if (TryGetComponent(out Collider pumpCollider))
        {
            Bounds bounds = pumpCollider.bounds;
            return new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.8f, bounds.center.z);
        }

        return transform.position + Vector3.up * 0.8f;
    }

    private Material HoseMaterial()
    {
        if (hoseMaterial != null)
        {
            return hoseMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        hoseMaterial = new Material(shader)
        {
            name = "FuelHoseMaterial",
            color = new Color(0.13f, 0.13f, 0.14f),
        };
        return hoseMaterial;
    }

    /// <summary>Завершить заправку текущим объёмом (ЛКМ отпущена или E повторно).</summary>
    private void FinishRefuel()
    {
        if (!fueling)
        {
            return;
        }

        fueling = false;
        armed = false;
        ReleaseLocks();
        Service.OnFuelDelivered(poured);
    }

    /// <summary>Снять оба замка: взаимодействие и заморозку персонажа.</summary>
    private void ReleaseLocks()
    {
        InteractionLock.Remove("refuel");
        PlayerControlLock.Remove("refuel");
    }

    private void OnDisable()
    {
        HideHose();

        // Сцену выгрузили или объект выключили посреди заправки — иначе игрок
        // остался бы замороженным навсегда.
        if (fueling)
        {
            fueling = false;
            armed = false;
        }

        ReleaseLocks();
    }

    private void OnGUI()
    {
        if (!fueling)
        {
            return;
        }

        GUIStyle style = new GUIStyle
        {
            fontSize = 26,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        float target = Service.CurrentOrder != null ? Service.CurrentOrder.liters : 0f;
        GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.42f, 240f, 40f),
            $"{poured:0.0} л / {target:0} л — отпустите ЛКМ", style);
    }
}
