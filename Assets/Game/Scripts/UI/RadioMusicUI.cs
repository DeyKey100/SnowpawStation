using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Music controls opened from the station radio, without pausing playback or the world.</summary>
[DefaultExecutionOrder(-150)] // Close before AdminPanel (-100) can capture the radio's unlocked cursor.
[DisallowMultipleComponent]
public sealed class RadioMusicUI : MonoBehaviour
{
    public const string InteractPrompt = "[E] Настроить радио";
    private const string LockId = "radio_music_ui";
    private const float PanelWidth = 420f;
    private const float PanelHeight = 398f;

    private static RadioMusicUI activeMenu;
    private static int closedFrame = -1;

    private RadioInteractable radio;
    private AudioManager audioManager;
    private StarterAssetsInputs playerInputs;
    private bool open;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool previousInputCursorLock;
    private bool previousInputLook;
    private float nextSliderFeedbackTime;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle trackStyle;
    private GUIStyle buttonStyle;

    public static bool IsOpen => activeMenu != null && activeMenu.open;
    // Esc must not close this window and open PauseMenu later in the same frame.
    public static bool BlocksPauseInput => IsOpen || closedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeMenu = null;
        closedFrame = -1;
    }

    private void Awake()
    {
        TryGetComponent(out radio);
        audioManager = AudioManager.EnsureInstance();
    }

    public void Open()
    {
        if (open || IsOpen || !isActiveAndEnabled || radio == null || !radio.isActiveAndEnabled ||
            PauseMenu.IsPaused || AdminPanel.IsOpen || DialogueManager.IsActive ||
            SceneLoadingScreen.IsLoading || InteractionLock.IsLocked || PlayerControlLock.IsLocked)
        {
            return;
        }

        if (playerInputs == null) { playerInputs = FindFirstObjectByType<StarterAssetsInputs>(); }
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (playerInputs != null)
        {
            previousInputCursorLock = playerInputs.cursorLocked;
            previousInputLook = playerInputs.cursorInputForLook;
            playerInputs.cursorLocked = false;
            playerInputs.cursorInputForLook = false;
            ClearPlayerInput();
        }

        open = true;
        activeMenu = this;
        nextSliderFeedbackTime = 0f;
        InteractionLock.Add(LockId);
        PlayerControlLock.Add(LockId);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        radio.PlaySwitchClick();
    }

    public void Close()
    {
        if (!open) { return; }
        open = false;
        if (activeMenu == this) { activeMenu = null; }
        closedFrame = Time.frameCount;
        InteractionLock.Remove(LockId);
        PlayerControlLock.Remove(LockId);
        if (playerInputs != null)
        {
            playerInputs.cursorLocked = previousInputCursorLock;
            playerInputs.cursorInputForLook = previousInputLook;
            ClearPlayerInput();
        }
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (audioManager != null) { audioManager.SavePreferences(); }
    }

    private void ClearPlayerInput()
    {
        playerInputs.LookInput(Vector2.zero);
        playerInputs.MoveInput(Vector2.zero);
        playerInputs.JumpInput(false);
        playerInputs.SprintInput(false);
    }

    private void Update()
    {
        if (!open) { return; }
        if (radio == null || !radio.isActiveAndEnabled || PauseMenu.IsPaused || AdminPanel.IsOpen ||
            DialogueManager.IsActive || SceneLoadingScreen.IsLoading || !PlayerControlLock.IsHeldBy(LockId))
        {
            Close();
            return;
        }

        bool closePressed;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        closePressed = keyboard != null &&
            (keyboard.escapeKey.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame);
#else
        closePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.BackQuote);
#endif
        if (closePressed)
        {
            audioManager.PlayUiBack();
            Close();
        }
    }

    private void OnDisable() => Close();
    private void OnDestroy() => Close();

    private void OnGUI()
    {
        if (!open || PauseMenu.IsPaused || AdminPanel.IsOpen || DialogueManager.IsActive ||
            SceneLoadingScreen.IsLoading)
        {
            return;
        }

        EnsureStyles();
        Matrix4x4 previousMatrix = GUI.matrix;
        int previousDepth = GUI.depth;
        float scale = Mathf.Min(1f, Mathf.Min(Screen.width / (PanelWidth + 32f), Screen.height / (PanelHeight + 32f)));
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - PanelWidth * scale) * 0.5f,
            (Screen.height - PanelHeight * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        GUI.depth = -20;
        try
        {
            DrawPanel();
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
        }
    }

    private void DrawPanel()
    {
        const float x = 30f;
        const float width = PanelWidth - x * 2f;
        UiTheme.Frame(new Rect(0f, 0f, PanelWidth, PanelHeight), UiTheme.Night, UiTheme.Frost);
        GUI.Label(new Rect(x, 14f, width, 40f), "Радио", titleStyle);

        bool musicEnabled = audioManager.IsMusicEnabled;
        if (GUI.Button(new Rect(x, 66f, width, 40f), $"Музыка: {(musicEnabled ? "Вкл" : "Выкл")}", buttonStyle))
        {
            audioManager.SetMusicEnabled(!musicEnabled);
            audioManager.PlayUiClick();
        }

        float volume = audioManager.MusicVolume;
        GUI.Label(new Rect(x, 116f, width, 30f), $"Громкость музыки: {volume * 100f:0}%", labelStyle);
        float newVolume = GUI.HorizontalSlider(new Rect(x, 150f, width, 20f), volume, 0f, 1f);
        if (!Mathf.Approximately(newVolume, volume))
        {
            audioManager.SetMusicVolume(newVolume);
            if (Time.unscaledTime >= nextSliderFeedbackTime)
            {
                nextSliderFeedbackTime = Time.unscaledTime + 0.12f;
                audioManager.PlayUiSelect();
            }
        }

        GUI.Label(new Rect(x, 184f, width, 56f),
            audioManager.IsReady ? $"Трек: {audioManager.CurrentTrackName}" : "Музыка: загрузка…", trackStyle);
        float trackButtonWidth = (width - 12f) * 0.5f;
        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && audioManager.IsReady && audioManager.PlaylistCount > 1;
        if (GUI.Button(new Rect(x, 244f, trackButtonWidth, 34f), "Предыдущий", buttonStyle))
        {
            audioManager.PreviousTrack();
            audioManager.PlayUiClick();
        }
        if (GUI.Button(new Rect(x + trackButtonWidth + 12f, 244f, trackButtonWidth, 34f), "Следующий", buttonStyle))
        {
            audioManager.NextTrack();
            audioManager.PlayUiClick();
        }
        GUI.enabled = previousEnabled;

        if (audioManager.IsMusicSuppressed)
        {
            GUI.Label(new Rect(x, 294f, width, 34f), "Музыка временно приглушена", labelStyle);
        }
        if (GUI.Button(new Rect(x, 338f, width, 40f), "Закрыть  [Esc]", buttonStyle))
        {
            audioManager.PlayUiBack();
            Close();
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) { return; }
        titleStyle = UiTheme.Label(26f, UiTheme.Text, FontStyle.Bold, scaleWithScreen: false);
        labelStyle = UiTheme.Label(18f, UiTheme.Text, scaleWithScreen: false);
        trackStyle = new GUIStyle(labelStyle) { wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            font = UiTheme.Font,
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };
    }
}
