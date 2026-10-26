using UnityEngine;

public class StationHeatSystem : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] private float currentHeat = 75f;
    [SerializeField] private float maxHeat = 100f;
    [SerializeField, Min(0f)] private float heatLossPerSecond = 0.2f;

    public float CurrentHeat => currentHeat;
    public float MaxHeat => maxHeat;

    private void OnValidate()
    {
        maxHeat = Mathf.Max(1f, maxHeat);
        currentHeat = Mathf.Clamp(currentHeat, 0f, maxHeat);
    }

    private void Update()
    {
        if (heatLossPerSecond <= 0f || currentHeat <= 0f)
        {
            return;
        }

        currentHeat = Mathf.Max(0f, currentHeat - heatLossPerSecond * Time.deltaTime);
    }

    public void AddHeat(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogError("StationHeatSystem получил отрицательное значение для AddHeat.", this);
            return;
        }

        currentHeat = Mathf.Clamp(currentHeat + amount, 0f, maxHeat);
        Debug.Log($"[Snowpaw Station] Тепло станции: {currentHeat:0}%.", this);
    }
}
