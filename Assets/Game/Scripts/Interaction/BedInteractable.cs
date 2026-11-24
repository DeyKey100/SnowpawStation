using System.Collections;
using UnityEngine;

/// <summary>
/// Кровать: в конце дня (фаза NightTransition) игрок взаимодействует с ней [E],
/// чтобы лечь спать и перейти к следующему дню.
/// </summary>
[DisallowMultipleComponent]
public class BedInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private string prompt = "[E] Лечь спать";

    [Header("Early Sleep Dialogue")]
    [SerializeField] private string speakerName = "РЫЖИКОВ";
    [SerializeField] private string earlyDialogueText = "Ещё рано спать. Сначала нужно закончить дела.";

    [Header("Sleep Fade")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float sleepDuration = 0.8f;

    private bool sleeping;
    private float fadeAlpha;
    private float sleepStartedAt;
    private GUIStyle sleepCaptionStyle;
    private readonly GUIContent sleepCaption = new GUIContent("zzz...");

    private void OnDisable()
    {
        StopAllCoroutines();
        if (sleeping)
        {
            sleeping = false;
            fadeAlpha = 0f;
            InteractionLock.Remove("sleeping");
            PlayerControlLock.Remove("sleeping");
        }
    }

    public string GetInteractPrompt()
    {
        return sleeping ? string.Empty : prompt;
    }

    public void Interact()
    {
        if (sleeping || !isActiveAndEnabled)
        {
            return;
        }

        if (!CanSleep())
        {
            ShowEarlyDialogue();
            return;
        }

        StartCoroutine(SleepRoutine());
    }

    private void ShowEarlyDialogue()
    {
        DialogueManager dialogue = DialogueManager.Instance;
        if (dialogue == null)
        {
            Debug.Log("[Snowpaw Station] " + earlyDialogueText);
            return;
        }

        if (DialogueManager.IsActive)
        {
            return;
        }

        DialogueScript script = DialogueBuilder.Create("bed_early")
            .Say(speakerName, earlyDialogueText);

        dialogue.Show(script);
    }

    private bool CanSleep()
    {
        DayLoopManager loop = DayLoopManager.Instance;
        return loop != null && loop.CurrentState == DayLoopManager.DayState.NightTransition && loop.NightTasksCompleted;
    }

    private IEnumerator SleepRoutine()
    {
        sleeping = true;
        sleepStartedAt = Time.time;
        InteractionLock.Add("sleeping");
        PlayerControlLock.Add("sleeping");

        // Плавное затемнение экрана
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        fadeAlpha = 1f;

        // Пауза сна
        yield return new WaitForSeconds(sleepDuration);

        // Запись в блокнот/журнал смены (если ещё не была добавлена)
        DayLoopManager loop = DayLoopManager.Instance;
        if (loop != null)
        {
            string note = StoryDialogues.NightNote(loop.CurrentDay);
            if (!string.IsNullOrEmpty(note) && !HasShiftLogNote(note))
            {
                GameState.AddShiftLog(note);
            }
        }

        // Переключение на следующий день
        if (StoryDirector.Instance != null && StoryDirector.Instance.Night != null)
        {
            StoryDirector.Instance.Night.FinishNight();
        }
        else if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.NotifyNightCompleted();
        }

        if (!sleeping || !isActiveAndEnabled)
        {
            yield break;
        }

        // BeginDay сбрасывает замки — держим управление до конца заставки.
        InteractionLock.Add("sleeping");
        PlayerControlLock.Add("sleeping");

        yield return new WaitForSeconds(0.4f);

        // Просветление экрана
        elapsed = fadeDuration;
        while (elapsed > 0f)
        {
            elapsed -= Time.deltaTime;
            fadeAlpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        fadeAlpha = 0f;

        InteractionLock.Remove("sleeping");
        PlayerControlLock.Remove("sleeping");
        sleeping = false;
        Debug.Log("[Snowpaw Station] Рыжиков проснулся. Наступило утро.");
    }

    private void OnGUI()
    {
        if (fadeAlpha <= 0f || Event.current.type != EventType.Repaint)
        {
            return;
        }

        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        try
        {
            // Закрываем HUD, но оставляем меню паузы и загрузку поверх заставки.
            GUI.depth = PauseMenu.IsPaused || SceneLoadingScreen.IsLoading ? 1 : -50;
            GUI.color = Color.white;

            Color background = UiTheme.Night;
            background.a = fadeAlpha;
            UiTheme.Fill(new Rect(0f, 0f, Screen.width, Screen.height), background);

            if (sleeping)
            {
                DrawSleepCaption();
            }
        }
        finally
        {
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }

    private void DrawSleepCaption()
    {
        float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, fadeAlpha));
        if (alpha <= 0f)
        {
            return;
        }

        if (sleepCaptionStyle == null)
        {
            sleepCaptionStyle = UiTheme.Label(38f, UiTheme.Ice, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        float scale = UiTheme.Scale;
        sleepCaptionStyle.fontSize = Mathf.RoundToInt(38f * scale);
        float bob = Mathf.Round(Mathf.Sin((Time.time - sleepStartedAt) * 2.5f) * 6f * scale);
        float height = 56f * scale;
        Rect rect = new Rect(0f, Screen.height * 0.5f - height * 0.5f + bob, Screen.width, height);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, sleepCaption, sleepCaptionStyle);
    }

    private static bool HasShiftLogNote(string note)
    {
        var log = GameState.GetShiftLog();
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] == note)
            {
                return true;
            }
        }
        return false;
    }
}
