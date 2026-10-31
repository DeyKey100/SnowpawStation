using UnityEngine;

public class StoveInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private StationHeatSystem heatSystem;
    [SerializeField] private FirewoodStorage firewoodStorage;
    [SerializeField] private float heatPerInteraction = 30f;
    [SerializeField] private bool enableFireGlow = true;
    [SerializeField] private Color fireGlowColor = new Color(1f, 0.32f, 0.08f);
    [SerializeField, Min(0f)] private float fireGlowIntensity = 2.2f;
    [SerializeField, Min(0f)] private float fireGlowRange = 3.5f;

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
        return firewoodStorage != null && firewoodStorage.HasFirewood
            ? "[E] Подбросить полено в печь"
            : string.Empty;
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
        Transform existingGlow = transform.Find("RuntimeFireGlow");
        if (existingGlow != null)
        {
            fireGlow = existingGlow.GetComponent<Light>();
            return;
        }

        GameObject glowObject = new GameObject("RuntimeFireGlow");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = new Vector3(0f, 0.65f, 0.35f);

        fireGlow = glowObject.AddComponent<Light>();
        fireGlow.type = LightType.Point;
        fireGlow.color = fireGlowColor;
        fireGlow.intensity = fireGlowIntensity;
        fireGlow.range = fireGlowRange;
        fireGlow.shadows = LightShadows.None;
    }
}
