using UnityEngine;

/// <summary>
/// Маркер «подойди и нажми E»: объект попадает в список PlayerInteraction и,
/// когда игрок оказывается ближе activateDistance, показывает над собой плашку.
/// Луч-прицел не используется — срабатывает только расстояние до объекта.
/// Расстояние считается до габаритов (коллайдер/меш), а не до пивота: у дровяной
/// кучи и печи пивот стоит далеко от самой геометрии.
/// </summary>
[DisallowMultipleComponent]
public class ProximityInteractable : MonoBehaviour
{
    [Header("Text")]
    [Tooltip("Пусто — подсказка берётся у IInteractable на этом объекте.")]
    [SerializeField] private string promptText = "";

    [Header("Placing")]
    [Tooltip("Насколько плашка выше верхней точки объекта, метры.")]
    [SerializeField] private float plateHeightOffset = 0.35f;
    [Tooltip("Высота плашки над пивотом, если у объекта нет ни коллайдера, ни меша.")]
    [SerializeField] private float fallbackHeight = 1.9f;

    [Header("Proximity")]
    [SerializeField] private float activateDistance = 2.2f;
    [SerializeField] private float maxVerticalDifference = 3f;
    [Tooltip("Больше — важнее. При равной близости выигрывает маркер с высоким приоритетом:\n" +
             "так записка на столе перебивает стойку и полку, рядом с которыми лежит.")]
    [SerializeField] private int priority;

    private IInteractable interactable;
    private Collider[] cachedColliders;
    private Renderer[] cachedRenderers;
    private Bounds cachedBounds;
    private bool hasBounds;
    private Vector3 plateAnchor;
    private float nextBoundsRefresh;

    /// <summary>Игрок в радиусе (выставляет PlayerInteraction при выборе ближайшего).</summary>
    public bool PlayerInRange { get; set; }

    public Vector3 PlateWorldPoint => plateAnchor;

    public bool HasWorldBounds => hasBounds && cachedBounds.size.sqrMagnitude > 1e-6f;

    /// <summary>
    /// Точка привязки плашки: центр габаритов. Камера висит высоко и смотрит вниз,
    /// поэтому точку «над крышей» она уводит за верхний край экрана — берём центр.
    /// </summary>
    public Vector3 CenterWorldPoint => hasBounds ? cachedBounds.center : transform.position;

    public float ActivateDistance => activateDistance;

    /// <summary>Вес маркера при выборе ближайшего: больше — важнее (записка важнее стойки).</summary>
    public int Priority => priority;

    public void SetPriority(int value)
    {
        priority = value;
    }

    public void Setup(float distance)
    {
        activateDistance = distance;
    }

    public void SetPrompt(string text)
    {
        promptText = text;
    }

    /// <summary>Гарантирует маркер на объекте (нужно для объектов, созданных в рантайме).</summary>
    public static ProximityInteractable EnsureOn(GameObject target, float distance = 2.2f, int priority = 0)
    {
        if (target == null)
        {
            return null;
        }

        ProximityInteractable marker = target.GetComponent<ProximityInteractable>();
        if (marker == null)
        {
            marker = target.AddComponent<ProximityInteractable>();
        }

        marker.Setup(distance);
        marker.SetPriority(priority);
        return marker;
    }

    /// <summary>Подсказка: своя строка, иначе текст IInteractable на этом объекте.</summary>
    public string Prompt
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(promptText))
            {
                return promptText;
            }

            IInteractable target = ResolveTarget();
            return target != null ? target.GetInteractPrompt() : "";
        }
    }

    public IInteractable ResolveTarget()
    {
        if (interactable as Object != null)
        {
            return interactable;
        }

        MonoBehaviour[] components = GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i] != this && components[i] is IInteractable found)
            {
                interactable = found;
                return found;
            }
        }

        interactable = null;
        return null;
    }

    /// <summary>
    /// Проверка расстояния до игрока. out — горизонтальная дистанция до габаритов,
    /// 0 если игрок стоит вплотную к объекту.
    /// </summary>
    public bool TryGetRange(Vector3 playerPosition, out float distance)
    {
        // Габариты пересчитываем не каждый кадр: объект статичен, а обход детей дорогой.
        if (Time.unscaledTime >= nextBoundsRefresh)
        {
            nextBoundsRefresh = Time.unscaledTime + 0.5f;
            RefreshBounds();
        }

        if (!hasBounds)
        {
            Vector3 delta = playerPosition - transform.position;
            distance = new Vector2(delta.x, delta.z).magnitude;
            return distance <= activateDistance && Mathf.Abs(delta.y) <= maxVerticalDifference;
        }

        // Горизонталь: расстояние до прямоугольника габаритов (внутри — ноль).
        float dx = Mathf.Max(cachedBounds.min.x - playerPosition.x, 0f, playerPosition.x - cachedBounds.max.x);
        float dz = Mathf.Max(cachedBounds.min.z - playerPosition.z, 0f, playerPosition.z - cachedBounds.max.z);
        distance = Mathf.Sqrt(dx * dx + dz * dz);

        // Вертикаль: насколько игрок выше/ниже габаритов.
        float dy = Mathf.Max(cachedBounds.min.y - playerPosition.y, 0f, playerPosition.y - cachedBounds.max.y);

        return distance <= activateDistance && dy <= maxVerticalDifference;
    }

    /// <summary>Пересчитать габариты и точку плашки (объект мог сдвинуться или сменить масштаб).</summary>
    public void RefreshBounds()
    {
        CacheComponents();

        hasBounds = false;
        cachedBounds = new Bounds(transform.position, Vector3.zero);

        Accumulate(cachedColliders);
        Accumulate(cachedRenderers);

        plateAnchor = hasBounds && cachedBounds.size.sqrMagnitude > 1e-6f
            ? new Vector3(cachedBounds.center.x, cachedBounds.max.y + plateHeightOffset, cachedBounds.center.z)
            : transform.position + Vector3.up * fallbackHeight;
    }

    private void Accumulate(Component[] source)
    {
        for (int i = 0; i < source.Length; i++)
        {
            Component item = source[i];
            if (item == null || !item.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds itemBounds;
            if (item is Collider collider)
            {
                if (!collider.enabled)
                {
                    continue;
                }

                itemBounds = collider.bounds;
            }
            else if (item is Renderer renderer)
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                itemBounds = renderer.bounds;
            }
            else
            {
                continue;
            }

            if (itemBounds.size.sqrMagnitude <= 1e-6f)
            {
                continue;
            }

            if (hasBounds)
            {
                cachedBounds.Encapsulate(itemBounds);
            }
            else
            {
                cachedBounds = itemBounds;
                hasBounds = true;
            }
        }
    }

    private void CacheComponents()
    {
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        CacheComponents();
        RefreshBounds();
        PlayerInteraction.RegisterMarker(this);
    }

    private void OnDisable()
    {
        PlayerInRange = false;
        PlayerInteraction.UnregisterMarker(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, activateDistance);
    }
}
