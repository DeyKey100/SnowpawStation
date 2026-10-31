using System;
using UnityEngine;

public class DayLoopManager : MonoBehaviour
{
    public static DayLoopManager Instance { get; private set; }

    [System.Serializable]
    public enum DayState
    {
        Morning,
        WorkShift,
        Evening,
        NightTransition
    }

    [Header("Current Progress")]
    [SerializeField, Min(1)] private int currentDay = 1;
    [SerializeField] private DayState currentState = DayState.Morning;
    [SerializeField, Min(0f)] private float dayTransitionDelay = 10f;

    public int CurrentDay => currentDay;
    public DayState CurrentState => currentState;
    public bool MorningTaskCompleted { get; private set; }
    public bool StoveFueledToday { get; private set; }
    public float TransitionTimeRemaining { get; private set; }

    public event Action<int, DayState> PhaseChanged;
    public event Action<int> DayChanged;

    private bool transitionStarted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        currentDay = Mathf.Max(1, currentDay);
    }

    private void Start()
    {
        PhaseChanged?.Invoke(currentDay, currentState);
    }

    private void Update()
    {
        if (!transitionStarted)
        {
            return;
        }

        TransitionTimeRemaining = Mathf.Max(0f, TransitionTimeRemaining - Time.deltaTime);
        if (TransitionTimeRemaining <= 0f)
        {
            transitionStarted = false;
            StartNextDay();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void AdvanceState()
    {
        if (transitionStarted)
        {
            return;
        }

        switch (currentState)
        {
            case DayState.Morning:
                if (!MorningTaskCompleted)
                {
                    Debug.LogWarning("Утро нельзя завершить до расчистки сугроба и растопки печи.", this);
                    return;
                }
                currentState = DayState.WorkShift;
                break;
            case DayState.WorkShift:
                currentState = DayState.Evening;
                break;
            case DayState.Evening:
                currentState = DayState.NightTransition;
                break;
            case DayState.NightTransition:
                StartNextDay();
                return;
        }

        Debug.Log($"[Snowpaw Station] День {currentDay}. Фаза: {currentState}");
        PhaseChanged?.Invoke(currentDay, currentState);
        TryStartDayTransition();
    }

    public void NotifyStoveFueled()
    {
        if ((currentState != DayState.Morning && currentState != DayState.WorkShift) || StoveFueledToday)
        {
            return;
        }

        StoveFueledToday = true;
        TryCompleteMorning();
    }

    private void TryCompleteMorning()
    {
        if (currentState != DayState.Morning || !MorningTaskCompleted || !StoveFueledToday)
        {
            return;
        }

        currentState = DayState.WorkShift;
        PhaseChanged?.Invoke(currentDay, currentState);
        TryStartDayTransition();
    }

    private void TryStartDayTransition()
    {
        if (currentState != DayState.WorkShift || !MorningTaskCompleted || !StoveFueledToday || transitionStarted)
        {
            return;
        }

        transitionStarted = true;
        TransitionTimeRemaining = dayTransitionDelay;
        Debug.Log($"Подготовка выполнена. Следующий день через {dayTransitionDelay:0} сек.", this);
    }

    public void CompleteMorningTask()
    {
        if (currentState != DayState.Morning)
        {
            Debug.LogWarning("Утренняя задача недоступна вне фазы Morning.", this);
            return;
        }

        MorningTaskCompleted = true;
        TryCompleteMorning();
    }

    private void StartNextDay()
    {
        currentDay++;
        currentState = DayState.Morning;
        MorningTaskCompleted = false;
        StoveFueledToday = false;
        TransitionTimeRemaining = 0f;
        Debug.Log($" НАСТУПИЛ ДЕНЬ {currentDay}");
        DayChanged?.Invoke(currentDay);
        PhaseChanged?.Invoke(currentDay, currentState);
    }
}