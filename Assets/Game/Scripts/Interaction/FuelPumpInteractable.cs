using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Колонка. E — начать отпуск топлива по текущему заказу, затем зажать ЛКМ:
/// литры растут ~8 л/с, отпустить означает сдачу заказа с оценкой объёма.
/// Один активный поток: на время заправки вешается InteractionLock "refuel".
/// </summary>
[DisallowMultipleComponent]
public sealed class FuelPumpInteractable : MonoBehaviour, IInteractable
{
    private const float LitersPerSecond = 8f;
    private const float IdleCancelSeconds = 2f;

    private ServiceDirector service;
    private bool fueling;
    private float poured;
    private bool armed;
    private float armedTimer;

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
        if (s == null || s.CurrentOrder == null)
        {
            return "[E] Колонка №" + PumpIndex;
        }

        string good = string.IsNullOrEmpty(s.CurrentOrder.item) ? "" : $" + {Goods.DisplayName(s.CurrentOrder.item)}";
        return $"[E] Заправить: {s.CurrentOrder.liters:0} л{good}";
    }

    private ServiceDirector Service => service ??= ServiceDirector.Instance;

    public void Interact()
    {
        ServiceDirector s = Service;
        if (s == null || s.CurrentOrder == null || fueling)
        {
            return;
        }

        fueling = true;
        armed = true;
        poured = 0f;
        armedTimer = 0f;
        InteractionLock.Add("refuel");
        Debug.Log("[Snowpaw Station] Заправка начата: зажмите ЛКМ.");
    }

    private void Update()
    {
        if (!fueling)
        {
            return;
        }

        bool hold;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        hold = mouse != null && mouse.leftButton.isPressed;
#else
        hold = Input.GetMouseButton(0);
#endif

        if (hold)
        {
            armed = false; // игрок реально начал качать
            float target = Service.CurrentOrder != null ? Service.CurrentOrder.liters : 0f;
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
            InteractionLock.Remove("refuel");
        }
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
        InteractionLock.Remove("refuel");
        Service.OnFuelDelivered(poured);
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
