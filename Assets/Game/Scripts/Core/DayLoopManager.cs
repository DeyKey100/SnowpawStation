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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

    private void StartNextDay()
    {
        currentDay++;
        currentState = DayState.Morning;
        Debug.Log($" НАСТУПИЛ ДЕНЬ {currentDay}");
    }
}