using UnityEngine;

public class StationHeatSystem : MonoBehaviour
{
    [SerializeField, Range(0f, 100f)] private float currentHeat = 75f;
    [SerializeField] private float maxHeat = 100f;

    public float CurrentHeat => currentHeat;
    public float MaxHeat => maxHeat;

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
