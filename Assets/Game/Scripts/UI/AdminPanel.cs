using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class AdminPanel : MonoBehaviour
{
    private const string LockId = "admin_panel";

    public static bool IsOpen { get; private set; }

    private bool open;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private StarterAssetsInputs playerInputs;
    private bool previousInputCursorLock;
    private bool previousInputLook;
    private string message;

    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle messageStyle;
    private GUIStyle buttonStyle;

    private void Update()
    {
        if (open && SceneLoadingScreen.IsLoading)
        {
            Close();
        }

        if (!TogglePressed())
        {
            return;
        }

        if (open)
        {
            Close();
        }
        else if (!PauseMenu.IsPaused && !SceneLoadingScreen.IsLoading &&
                 !PlayerControlLock.IsHeldBy("chopping") && !FuelingMinigame.IsActive)
        {
            // DayLoopManager живёт в уличной сцене и в интерьере, запущенном
            // напрямую, его нет — панель всё равно должна открываться.
            Open();
        }
    }

    private static bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.BackQuote);
#endif
    }

    private void Open()
    {
        open = IsOpen = true;
        message = string.Empty;
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        playerInputs = FindFirstObjectByType<StarterAssetsInputs>();
        if (playerInputs != null)
        {
            previousInputCursorLock = playerInputs.cursorLocked;
            previousInputLook = playerInputs.cursorInputForLook;
            playerInputs.cursorLocked = false;
            playerInputs.cursorInputForLook = false;
            playerInputs.LookInput(Vector2.zero);
            playerInputs.JumpInput(false);
        }

        InteractionLock.Add(LockId);
        PlayerControlLock.Add(LockId);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Close()
    {
        if (!open)
        {
            return;
        }

        open = IsOpen = false;
        InteractionLock.Remove(LockId);
        PlayerControlLock.Remove(LockId);
        Time.timeScale = previousTimeScale;
        if (playerInputs != null)
        {
            playerInputs.cursorLocked = previousInputCursorLock;
            playerInputs.cursorInputForLook = previousInputLook;
            playerInputs.LookInput(Vector2.zero);
        }

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable() => Close();

    private void OnGUI()
    {
        if (!open)
        {
            return;
        }

        EnsureStyles();
        int previousDepth = GUI.depth;
        GUI.depth = -100;
        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = Mathf.Min(1f, Mathf.Min(Screen.width / 480f, Screen.height / 460f));
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 440f * scale) * 0.5f,
            (Screen.height - 548f * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

        DayLoopManager loop = DayLoopManager.Instance;
        StationHeatSystem heat = StationHeatSystem.Active;
        StoryDirector story = StoryDirector.Instance;

        UiTheme.Frame(new Rect(0f, 0f, 440f, 548f), UiTheme.Night, UiTheme.Frost);
        GUI.Label(new Rect(20f, 14f, 400f, 32f), "Админ-панель  [~]", titleStyle);
        if (loop != null)
        {
            string heatText = heat != null ? $"{heat.CurrentHeat:0.#}%" : "—";
            GUI.Label(new Rect(20f, 50f, 400f, 26f),
                $"День {loop.CurrentDay}/{DayPlans.DayCount}   |   Тепло: {heatText}", labelStyle);
        }

        string dayButtonText = loop != null && loop.CurrentDay >= DayPlans.DayCount
            ? "Пропустить день (последний день)" : "Пропустить день";
        if (Button(82f, dayButtonText, loop != null && loop.CurrentDay < DayPlans.DayCount))
        {
            story?.Night?.CancelForDaySkip();
            story?.Dialogue?.DiscardForDaySkip();
            Close();
            loop.StartDay(loop.CurrentDay + 1);
        }
        else if (Button(122f, "Пропустить текущий заказ"))
        {
            if (story?.Service?.TrySkipCurrentOrder() == true)
            {
                message = "Заказ пропущен и засчитан.";
                if (loop != null && loop.CurrentState != DayLoopManager.DayState.WorkShift)
                {
                    Close();
                }
            }
            else
            {
                message = DialogueManager.IsActive ? "Сначала завершите диалог." : "Нет заказа для пропуска.";
            }
        }
        else if (Button(162f, "Быстро подобрать письмо"))
        {
            if (story?.Kotov?.TryTakeDoorstepLetter() == true)
            {
                Close();
            }
            else
            {
                message = "На пороге нет доступного письма.";
            }
        }
        else if (Button(202f, "Быстро расчистить снег"))
        {
            SnowPileSpawner spawner = FindAnyObjectByType<SnowPileSpawner>();
            int count = spawner != null ? spawner.ClearActivePilesImmediately() : 0;
            message = count > 0 ? $"Расчищено сугробов: {count}." : "Нет доступных сугробов. Проверьте задачи утра или выйдите во двор.";
        }
        else if (Button(242f, "Быстро нарубить одно полено"))
        {
            WoodPileInteractable woodPile = FindAnyObjectByType<WoodPileInteractable>();
            message = woodPile != null && woodPile.TryChopImmediately()
                ? "Полено добавлено в запас." : "Колода недоступна, лапы полны или нужно прочитать записку.";
        }

        if (heat != null)
        {
            if (GUI.Button(new Rect(20f, 282f, 196f, 34f), "Тепло −10", buttonStyle))
            {
                heat.ResetHeat(heat.CurrentHeat - 10f);
                message = "Тепло уменьшено.";
            }
            if (GUI.Button(new Rect(224f, 282f, 196f, 34f), "Тепло +10", buttonStyle))
            {
                heat.ResetHeat(heat.CurrentHeat + 10f);
                message = "Тепло увеличено.";
            }
        }

        // Bayer Dither включён в PC_Renderer.asset; старый PS1-переключатель удалён.
        PsxLowResolution psx = PsxLowResolution.Instance;
        if (psx != null)
        {
            string psxLabel = PsxLowResolution.IsActive
                ? $"PS1-эффект: вкл ({psx.TargetHeight}p)  [F9]"
                : "PS1-эффект: выкл  [F9]";
            if (GUI.Button(new Rect(20f, 322f, 400f, 30f), psxLabel, buttonStyle))
            {
                psx.SetEnabled(!PsxLowResolution.IsActive);
                message = PsxLowResolution.IsActive ? "Эффект включён." : "Эффект выключен.";
            }

            if (GUI.Button(new Rect(20f, 356f, 400f, 30f), "Сменить разрешение кадра", buttonStyle))
            {
                psx.SetTargetHeight(NextPsxHeight(psx.TargetHeight));
                message = $"Кадр: {psx.TargetHeight} строк.";
            }

            string aspectLabel = psx.CurrentAspectMode == PsxLowResolution.AspectMode.Pillarbox
                ? "Кадр: 4:3 с полями"
                : "Кадр: во весь экран";
            if (GUI.Button(new Rect(20f, 390f, 400f, 30f), aspectLabel, buttonStyle))
            {
                psx.SetAspectMode(psx.CurrentAspectMode == PsxLowResolution.AspectMode.Pillarbox
                    ? PsxLowResolution.AspectMode.FollowScreen
                    : PsxLowResolution.AspectMode.Pillarbox);
                message = "Формат кадра изменён.";
            }
        }

        GUI.Label(new Rect(20f, 428f, 400f, 44f), message, messageStyle);
        if (Button(474f, "Закрыть"))
        {
            Close();
        }

        GUI.matrix = previousMatrix;
        GUI.depth = previousDepth;
    }

    private bool Button(float y, string text, bool enabled = true)
    {
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && enabled;
        bool pressed = GUI.Button(new Rect(20f, y, 400f, 34f), text, buttonStyle);
        GUI.enabled = previousEnabled;
        return pressed;
    }

    /// <summary>Следующая ступень разрешения кадра по кругу: от самого грубого к 240.</summary>
    private static int NextPsxHeight(int current)
    {
        switch (current)
        {
            case 96: return 120;
            case 120: return 144;
            case 144: return 180;
            case 180: return 240;
            default: return 96;
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = UiTheme.Label(24f, UiTheme.Text, FontStyle.Bold, scaleWithScreen: false);
        labelStyle = UiTheme.Label(17f, UiTheme.Ice, scaleWithScreen: false);
        messageStyle = UiTheme.Label(16f, UiTheme.Cream, wordWrap: true, scaleWithScreen: false);
        buttonStyle = new GUIStyle(GUI.skin.button) { font = UiTheme.Font, fontSize = 17 };
    }
}
