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
    private Texture2D blackTexture;

    private void Awake()
    {
        blackTexture = MakeSolidTexture(Color.black);
    }

    private void OnDestroy()
    {
        if (blackTexture != null)
        {
            Destroy(blackTexture);
        }
    }

    private void OnDisable()
    {
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
        if (sleeping)
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
        if (fadeAlpha <= 0f)
        {
            return;
        }

        if (blackTexture == null)
        {
            blackTexture = MakeSolidTexture(Color.black);
        }

        Color prevColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), blackTexture);
        GUI.color = prevColor;
    }

    private static Texture2D MakeSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
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
