using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ночные события. День 1: кресло у печи, радио шипит «Слушай метель...», заметка в блокнот, сон.
/// Дни 2-3 — нулевые заглушки (следующий этап), после ночи вызывается NotifyNightCompleted.
/// </summary>
[DisallowMultipleComponent]
public sealed class NightEvents : MonoBehaviour
{
    private DialogueManager dialogue;
    private bool running;

    public bool IsNightRunning => running;

    private void Start()
    {
        dialogue = StoryDirector.Instance != null ? StoryDirector.Instance.Dialogue : null;
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged += OnPhaseChanged;
        }
    }

    private void OnDestroy()
    {
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged -= OnPhaseChanged;
        }
    }

    private void OnPhaseChanged(int day, DayLoopManager.DayState state)
    {
        if (state != DayLoopManager.DayState.NightTransition || running)
        {
            return;
        }

        switch (day)
        {
            case 1: StartNight1(); break;
            default:
                // Дни 2-3: пока заглушка-ночь, сразу следующий день (следующий этап работы).
                Debug.Log($"[Snowpaw Station] Ночь дня {day}: событие ещё не реализовано (вне текущего этапа).");
                DayLoopManager.Instance.NotifyNightCompleted();
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

        dialogue.DialogueFinished -= OnNight1Finished;
        GameState.AddShiftLog(StoryDialogues.NightNote(1));
        FinishNight();
    }

    /// <summary>Ночь завершена (сон) → следующий день.</summary>
    public void FinishNight()
    {
        if (!running)
        {
            return;
        }

        running = false;
        DayLoopManager.Instance.NotifyNightCompleted();
    }

    // Заглушки ночи дня 3 (силуэт/следы/финальная карта) — вне текущего этапа (день 1).
    public void HideSilhouette() { }
    public void GoOutAtNight3() { }
    public void ShowSliceEnd() { }
}
