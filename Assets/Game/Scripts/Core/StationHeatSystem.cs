using System;
using UnityEngine;

/// <summary>
/// Тепло станции. Канон нулевой версии (NEWstory):
/// Д1 начинается с 35%, Д2-Д3 с 75%. Потери: обычно −4% в минуту, в метель −7%.
/// Полено даёт +30%. Ниже 25% — иней по краям экрана, Рыжиков замедляется, радио шипит,
/// гости ворчат. Проиграть нельзя: тепло упирается в 0 и не убивает.
/// Среднее тепло за день нужно для чёткости письма №2 (не ниже 50%).
/// </summary>
public class StationHeatSystem : MonoBehaviour
{
    /// <summary>Потери при обычной погоде, % в минуту.</summary>
    public const float LossPerMinuteNormal = 4f;

    /// <summary>Потери в метель (день 3), % в минуту.</summary>
    public const float LossPerMinuteBlizzard = 7f;

    /// <summary>Прибавка от одного полена, %.</summary>
    public const float HeatPerLog = 30f;

    /// <summary>Порог холода: эффекты и «ворчащие» гости.</summary>
    public const float ColdThreshold = 25f;

    [SerializeField, Range(0f, 100f)] private float currentHeat = 75f;
    [SerializeField] private float maxHeat = 100f;

    public static StationHeatSystem Active { get; private set; }

    public float CurrentHeat => currentHeat;
    public float MaxHeat => maxHeat;

    /// <summary>Истинно в метель (день 3): потери выше.</summary>
    public bool BlizzardActive { get; private set; }

    /// <summary>Среднее тепло за текущий день — для ясности письма №2 (≥ 50%).</summary>
    public float AverageHeatToday => heatSamples > 0 ? heatSum / heatSamples : currentHeat;

    public bool IsCold => currentHeat < ColdThreshold;

    public event Action<float, float> HeatChanged;
    public event Action<bool> ColdChanged;
    public event Action<int> DayStarted;

    private float heatSum;
    private int heatSamples;
    private float sampleTimer;
    private bool wasCold;

    private void Awake()
    {
        if (Active != null && Active != this)
        {
            Destroy(gameObject);
            return;
        }

        Active = this;
    }

    private void OnEnable()
    {
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.DayStarted += OnDayStarted;
        }
    }

    private void OnDisable()
    {
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.DayStarted -= OnDayStarted;
        }
    }

    private void OnDestroy()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    private void OnValidate()
    {
        maxHeat = Mathf.Max(1f, maxHeat);
        currentHeat = Mathf.Clamp(currentHeat, 0f, maxHeat);
    }

    private void Start()
    {
        int day = DayLoopManager.Instance != null ? DayLoopManager.Instance.CurrentDay : 1;
        BlizzardActive = IsBlizzardDay(day);
        ApplyDayStart(day);
        HeatChanged?.Invoke(currentHeat, maxHeat);
    }

    private void Update()
    {
        if (currentHeat <= 0f)
        {
            SampleAverage();
            return;
        }

        float lossPerMinute = BlizzardActive ? LossPerMinuteBlizzard : LossPerMinuteNormal;
        SetHeat(currentHeat - lossPerMinute / 60f * Time.deltaTime);
        SampleAverage();
    }

    private void OnDayStarted(int day)
    {
        BlizzardActive = IsBlizzardDay(day);
        ApplyDayStart(day);
        DayStarted?.Invoke(day);
    }

    private void ApplyDayStart(int day)
    {
        float start = day == 1 ? 35f : 75f;
        heatSum = 0f;
        heatSamples = 0;
        sampleTimer = 0f;
        SetHeat(start);
        Debug.Log($"[Snowpaw Station] Тепло: день {day}, старт {start:0}%, потери {LossRate()}/мин.");
    }

    public float LossRate() => BlizzardActive ? LossPerMinuteBlizzard : LossPerMinuteNormal;

    public bool IsBlizzardDay(int day) => day >= 3;

    public void SetBlizzard(bool active)
    {
        if (BlizzardActive == active)
        {
            return;
        }

        BlizzardActive = active;
        Debug.Log($"[Snowpaw Station] Метель {(active ? "началась" : "закончилась")}: потери {LossRate():0}%/мин.");
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

    /// <summary>Положить полено в печь: канонические +30%.</summary>
    public void FeedStove() => AddHeat(HeatPerLog);

    public void ResetHeat(float value = 75f) => SetHeat(value);

    private void SetHeat(float value)
    {
        float newHeat = Mathf.Clamp(value, 0f, maxHeat);
        if (Mathf.Approximately(newHeat, currentHeat))
        {
            return;
        }

        currentHeat = newHeat;
        HeatChanged?.Invoke(currentHeat, maxHeat);

        bool cold = IsCold;
        if (cold != wasCold)
        {
            wasCold = cold;
            ColdChanged?.Invoke(cold);
            if (cold)
            {
                Debug.Log("[Snowpaw Station] Тепло ниже 25%: иней, Рыжиков замедляется, гости ворчат.");
            }
        }
    }

    private void SampleAverage()
    {
        sampleTimer += Time.deltaTime;
        if (sampleTimer < 1f)
        {
            return;
        }

        sampleTimer = 0f;
        heatSum += currentHeat;
        heatSamples++;
    }
}
