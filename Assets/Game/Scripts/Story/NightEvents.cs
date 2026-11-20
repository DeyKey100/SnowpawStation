using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ночные события. День 1: кресло у печи, радио шипит «Слушай метель...», заметка в блокнот, сон.
/// Дни 2-3 — сюжетные радио/ночи, после сна вызывается NotifyNightCompleted.
/// </summary>
[DisallowMultipleComponent]
public sealed class NightEvents : MonoBehaviour
{
    public static NightEvents Instance { get; private set; }

    private DialogueManager dialogue;
    private bool running;

    public bool IsNightRunning => running;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        dialogue = StoryDirector.Instance != null ? StoryDirector.Instance.Dialogue : null;
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged += OnPhaseChanged;
            DayLoopManager.Instance.DayStarted += OnDayStarted;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged -= OnPhaseChanged;
            DayLoopManager.Instance.DayStarted -= OnDayStarted;
        }
    }

    private void OnDayStarted(int day)
    {
        running = false;
    }

    private void OnPhaseChanged(int day, DayLoopManager.DayState state)
    {
        if (state != DayLoopManager.DayState.NightTransition || running)
        {
            return;
        }

        running = true;
        Debug.Log($"[Snowpaw Station] Ночь дня {day}: эфир готов, ждём взаимодействия с радио/кроватью.");
    }

    /// <summary>Взаимодействие с радиоприёмником в ночную фазу.</summary>
    public void TriggerNightRadio()
    {
        int day = DayLoopManager.Instance != null ? DayLoopManager.Instance.CurrentDay : 1;
        switch (day)
        {
            case 1:
                StartNight1();
                break;
            case 2:
                StartNight2();
                break;
            default:
                DayLoopManager.Instance?.NotifyNightTaskDone("radio");
                break;
        }
    }

    private void StartNight1()
    {
        running = true;
        DialogueScript s = StoryDialogues.Night1Radio();
        if (dialogue != null)
        {
            dialogue.Show(s);
            dialogue.DialogueFinished += OnNight1Finished;
        }
    }

    private void OnNight1Finished(DialogueScript finished)
    {
        if (finished == null || finished.id != "night1_radio")
        {
            return;
        }

        if (dialogue != null)
        {
            dialogue.DialogueFinished -= OnNight1Finished;
        }

        DayLoopManager.Instance?.NotifyNightTaskDone("radio");
        GameState.AddShiftLog(StoryDialogues.NightNote(1));
        Debug.Log("[Snowpaw Station] Ночное радио дня 1 прослушано. Задача 'radio' закрыта.");
    }

    private void StartNight2()
    {
        running = true;
        DialogueScript s = StoryDialogues.Night2Radio();
        if (dialogue != null)
        {
            dialogue.Show(s);
            dialogue.DialogueFinished += OnNight2Finished;
        }
    }

    private void OnNight2Finished(DialogueScript finished)
    {
        if (finished == null || finished.id != "night2_radio")
        {
            return;
        }

        if (dialogue != null)
        {
            dialogue.DialogueFinished -= OnNight2Finished;
        }

        DayLoopManager.Instance?.NotifyNightTaskDone("radio");
        GameState.AddShiftLog(StoryDialogues.NightNote(2));
        Debug.Log("[Snowpaw Station] Ночное радио дня 2 прослушано. Задача 'radio' закрыта.");
    }

    /// <summary>Ночь завершена (сон в кровати) → следующий день.</summary>
    public void FinishNight()
    {
        if (!running && DayLoopManager.Instance != null && DayLoopManager.Instance.CurrentState != DayLoopManager.DayState.NightTransition)
        {
            return;
        }

        running = false;
        if (dialogue != null)
        {
            dialogue.DialogueFinished -= OnNight1Finished;
            dialogue.DialogueFinished -= OnNight2Finished;
        }

        DayLoopManager.Instance?.NotifyNightCompleted();
    }

    internal void CancelForDaySkip()
    {
        running = false;
        if (dialogue != null)
        {
            dialogue.DialogueFinished -= OnNight1Finished;
            dialogue.DialogueFinished -= OnNight2Finished;
        }
    }

    // Заглушки ночи дня 3 (силуэт/следы/финальная карта) — вне текущего этапа (день 1).
    public void HideSilhouette() { }
    public void GoOutAtNight3() { }
    public void ShowSliceEnd() { }
}
