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
    public int currentDay = 1;
    public DayState currentState = DayState.Morning;

    public bool MorningTaskCompleted { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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
        switch (currentState)
        {
            case DayState.Morning:
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
                break;
        }

        Debug.Log($"[Snowpaw Station] День {currentDay}. Фаза: {currentState}");
    }

    public void CompleteMorningTask()
    {
        if (currentState != DayState.Morning)
        {
            Debug.LogWarning("Утренняя задача недоступна вне фазы Morning.", this);
            return;
        }

        MorningTaskCompleted = true;
        AdvanceState();
    }

    private void StartNextDay()
    {
        currentDay++;
        currentState = DayState.Morning;
        MorningTaskCompleted = false;
        Debug.Log($" НАСТУПИЛ ДЕНЬ {currentDay}");
    }
}