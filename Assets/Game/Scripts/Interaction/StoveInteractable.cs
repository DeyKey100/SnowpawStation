using UnityEngine;

public class StoveInteractable : MonoBehaviour, IInteractable
{
    private const string RuntimeGlowName = "RuntimeFireGlow";

    [SerializeField] private StationHeatSystem heatSystem;
    [SerializeField] private FirewoodStorage firewoodStorage;
    [SerializeField] private float heatPerInteraction = 30f;
    [SerializeField] private bool enableFireGlow = true;
    [SerializeField] private Color fireGlowColor = new Color(1f, 0.32f, 0.08f);
    [SerializeField, Min(0f)] private float fireGlowIntensity = 2.2f;
    [SerializeField, Min(0f)] private float fireGlowRange = 3.5f;
    [SerializeField, Tooltip("Смещение ауры от центра модели в мировых координатах (x — вправо).")]
    private Vector3 fireGlowOffset = Vector3.zero;
    [SerializeField, Tooltip("Ставить ауру вручную по Fire Glow Local Anchor вместо авто-позиции по границам модели.")]
    private bool useManualGlowAnchor;
    [SerializeField, Tooltip("Локальная позиция ауры относительно корня печи (при Use Manual Glow Anchor).")]
    private Vector3 fireGlowLocalAnchor = Vector3.zero;
    [SerializeField, Tooltip("Смещение ауры в локальных координатах корня, если у печи нет рендереров.")]
    private Vector3 fireGlowFallbackOffset = new Vector3(0f, 0.65f, 0.35f);

    private Light fireGlow;
    private float pulseTime;

    private void Awake()
    {
        if (enableFireGlow)
        {
            CreateFireGlow();
        }
    }

    private void Update()
    {
        if (fireGlow == null)
        {
            return;
        }

        pulseTime += Time.deltaTime;
        float flicker = Mathf.PerlinNoise(pulseTime * 4f, 0f);
        fireGlow.intensity = fireGlowIntensity * Mathf.Lerp(0.82f, 1.08f, flicker);
    }

    public string GetInteractPrompt()
    {
        if (heatSystem == null || firewoodStorage == null)
        {
            return string.Empty;
        }

        // Подсказка должна появляться при наведении всегда, иначе печь выглядит неинтерактивной.
        return firewoodStorage.HasFirewood
            ? "[E] Подбросить полено в печь"
            : "Нужны дрова - возьмите у поленницы";
    }

    public void Interact()
    {
        if (heatSystem == null)
        {
            Debug.LogError("StoveInteractable не назначил StationHeatSystem.", this);
            return;
        }

        if (firewoodStorage == null)
        {
            Debug.LogError("StoveInteractable не назначил FirewoodStorage.", this);
            return;
        }

        if (!firewoodStorage.TryConsumeFirewood())
        {
            return;
        }

        heatSystem.AddHeat(heatPerInteraction);
        DayLoopManager.Instance?.NotifyStoveFueled();
        pulseTime = 0f;
    }

    private void CreateFireGlow()
    {
        Transform existingGlow = transform.Find(RuntimeGlowName);
        if (existingGlow == null)
        {
            GameObject glowObject = new GameObject(RuntimeGlowName);
            glowObject.transform.SetParent(transform, false);
            existingGlow = glowObject.transform;
        }

        existingGlow.localPosition = ResolveGlowLocalAnchor();

        fireGlow = existingGlow.GetComponent<Light>();
        if (fireGlow == null)
        {
            fireGlow = existingGlow.gameObject.AddComponent<Light>();
        }

        fireGlow.type = LightType.Point;
        fireGlow.color = fireGlowColor;
        fireGlow.intensity = fireGlowIntensity;
        fireGlow.range = fireGlowRange;
        fireGlow.shadows = LightShadows.None;
    }

    /// <summary>
    /// Аура ставится по фактическим границам видимой модели, а не в origin корня.
    /// Если модель смещена относительно корня (перенос объекта по сцене, импорт FBX),
    /// свет, созданный в origin, оказывается далеко от печи и не виден.
    /// </summary>
    private Vector3 ResolveGlowLocalAnchor()
    {
        if (useManualGlowAnchor)
        {
            return fireGlowLocalAnchor;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return fireGlowFallbackOffset + ToLocalOffset(fireGlowOffset);
        }

        return transform.InverseTransformPoint(bounds.center + fireGlowOffset);
    }

    private Vector3 ToLocalOffset(Vector3 worldOffset)
    {
        return transform.InverseTransformVector(worldOffset);
    }
}
