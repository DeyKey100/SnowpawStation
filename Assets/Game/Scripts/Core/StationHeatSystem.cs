using System;
using UnityEngine;

public class StationHeatSystem : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] private float currentHeat = 75f;
    [SerializeField] private float maxHeat = 100f;
    [SerializeField, Min(0f)] private float heatLossPerSecond = 0.2f;

    public float CurrentHeat => currentHeat;
    public float MaxHeat => maxHeat;

    public event Action<float, float> HeatChanged;

    private void OnValidate()
    {
        maxHeat = Mathf.Max(1f, maxHeat);
        currentHeat = Mathf.Clamp(currentHeat, 0f, maxHeat);
    }

    private void Start()
    {
        HeatChanged?.Invoke(currentHeat, maxHeat);
    }

    private void Update()
    {
        if (heatLossPerSecond <= 0f || currentHeat <= 0f)
        {
            return;
        }

        SetHeat(Mathf.Max(0f, currentHeat - heatLossPerSecond * Time.deltaTime));
    }

    public void AddHeat(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogError("StationHeatSystem получил отрицательное значение для AddHeat.", this);
            return;
        }

        SetHeat(currentHeat + amount);
        Debug.Log($"[Snowpaw Station] Тепло станции: {currentHeat:0}%.", this);
    }

    public void ResetHeat(float value = 75f)
    {
        SetHeat(value);
    }

    private void SetHeat(float value)
    {
        float newHeat = Mathf.Clamp(value, 0f, maxHeat);
        if (Mathf.Approximately(newHeat, currentHeat))
        {
            return;
        }

        currentHeat = newHeat;
        HeatChanged?.Invoke(currentHeat, maxHeat);
    }
}
