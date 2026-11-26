using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class PauseMenu : MonoBehaviour
{
    private static PauseMenu persistent;

    [Header("References")]
    [SerializeField] private MusicPlayer musicPlayer;
    [Tooltip("Назначать не обязательно: BlizzardManager создаётся кодом при запуске игры, ссылка подтягивается автоматически (Instance / FindFirstObjectByType).")]
    [SerializeField] private BlizzardManager blizzardManager;

    [Header("Layout")]
    [SerializeField] private float panelWidth = 420f;

    public static bool IsPaused { get; private set; }

    private AudioManager audioManager;
    private StarterAssetsInputs playerInputs;
    private bool previousAudioListenerPause;
    private float nextSliderFeedbackTime;
    private float lastSliderFeedbackTime;
    private bool previousCursorLocked = true;
    private bool previousCursorInputForLook = true;
    private bool settingsOpen;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private Texture2D panelTexture;

    /// <summary>
    /// Меню паузы живёт на StationHUD из уличной сцены, поэтому в интерьере,
    /// запущенном напрямую, его не было вовсе. Поднимаем своё, если в сцене
    /// такого компонента нет, и перевозим между сценами.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        Adopt();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Adopt();
    }

    private static void Adopt()
    {
        if (persistent != null)
        {
            return;
        }

        PauseMenu found = FindFirstObjectByType<PauseMenu>();
        if (found == null)
        {
            GameObject go = new GameObject("PauseMenu");
            found = go.AddComponent<PauseMenu>();
        }

        persistent = found;
        if (found.transform.parent == null)
        {
            DontDestroyOnLoad(found.gameObject);
        }
    }

    private void Awake()
    {
        // Дубль из загруженной сцены не нужен: меню уже переехало сюда.
        if (persistent != null && persistent != this)
        {
            Destroy(this);
            return;
        }

        persistent = this;

        // musicPlayer remains serialized only to preserve existing scene references.
        audioManager = AudioManager.EnsureInstance();
        RefreshBlizzardReference();
    }

    private void Update()
    {
        // Дубль из загруженной сцены не должен перехватывать Esc: иначе
        // нажатие обрабатывают два меню, и пауза ставится снова сразу после снятия.
        if (persistent != this)
        {
            return;
        }

        if (AdminPanel.IsOpen || RadioMusicUI.BlocksPauseInput || FuelingMinigame.BlocksPauseInput ||
            !IsEscapePressed())
        {
            return;
        }

        if (!IsPaused)
        {
            Pause();
        }
        else if (settingsOpen)
        {
            settingsOpen = false;
            audioManager.PlayUiBack();
            audioManager.SavePreferences();
        }
        else
        {
            Resume();
        }
    }

    private void OnDisable()
    {
        if (persistent == this && IsPaused)
        {
            RestoreTimeScale();
        }
    }

    private void OnDestroy()
    {
        if (persistent == this)
        {
            persistent = null;
        }

        if (panelTexture != null)
        {
            Destroy(panelTexture);
            panelTexture = null;
        }
    }

    private bool IsEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void Pause()
    {
        if (IsPaused)
        {
            return;
        }

        if (audioManager == null) { audioManager = AudioManager.EnsureInstance(); }
        audioManager.PlayUiSelect();
        previousAudioListenerPause = AudioListener.pause;
        AudioListener.pause = true;
        IsPaused = true;
        settingsOpen = false;
        Time.timeScale = 0f;

        if (playerInputs == null)
        {
            playerInputs = FindFirstObjectByType<StarterAssetsInputs>();
        }

        if (playerInputs != null)
        {
            previousCursorLocked = playerInputs.cursorLocked;
            previousCursorInputForLook = playerInputs.cursorInputForLook;
            playerInputs.cursorLocked = false;
            playerInputs.cursorInputForLook = false;
            playerInputs.LookInput(Vector2.zero);
            playerInputs.MoveInput(Vector2.zero);
            playerInputs.JumpInput(false);
            playerInputs.SprintInput(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Resume()
    {
        if (!IsPaused)
        {
            return;
        }

        if (audioManager != null) { audioManager.PlayUiBack(); }
        IsPaused = false;
        settingsOpen = false;
        RestoreTimeScale();

        if (playerInputs != null)
        {
            playerInputs.cursorLocked = previousCursorLocked;
            playerInputs.cursorInputForLook = previousCursorInputForLook;
        }

        Cursor.lockState = previousCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !previousCursorLocked;
    }

    private void RestoreTimeScale()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = previousAudioListenerPause;
        if (audioManager != null) { audioManager.SavePreferences(); }
    }

    private void OnGUI()
    {
        if (!IsPaused || persistent != this)
        {
            return;
        }

        EnsureStyles();

        float width = Mathf.Min(panelWidth, Screen.width - 40f);
        float height = settingsOpen ? 432f : 250f;
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);

        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(panel.x + 24f, panel.y + 16f, width - 48f, 40f),
            settingsOpen ? "Настройки" : "Пауза",
            titleStyle);

        float buttonWidth = width - 80f;
        float buttonHeight = 40f;
        float x = panel.x + 40f;
        float y = panel.y + 70f;

        if (!settingsOpen)
        {
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Продолжить", buttonStyle))
            {
                Resume();
            }

            y += buttonHeight + 14f;

            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Настройки", buttonStyle))
            {
                settingsOpen = true;
                audioManager.PlayUiClick();
            }

            return;
        }

        if (audioManager == null) { audioManager = AudioManager.EnsureInstance(); }
        float currentSfxVolume = audioManager.SfxVolume;
        // Строка «Громкость звуков» с запасом по высоте: у пиксельного шрифта
        // нижние выносные элементы глифов упирались в 24 px и текст подрезался.
        const float sfxLabelHeight = 30f;
        GUI.Label(new Rect(x, y, buttonWidth, sfxLabelHeight),
            $"Громкость звуков: {currentSfxVolume * 100f:0}%", labelStyle);
        y += sfxLabelHeight + 4f;
        float newSfxVolume = GUI.HorizontalSlider(new Rect(x, y, buttonWidth, 20f), currentSfxVolume, 0f, 1f);
        if (!Mathf.Approximately(newSfxVolume, currentSfxVolume))
        {
            audioManager.SetSfxVolume(newSfxVolume);
            PlaySliderFeedback();
        }
        y += 44f;

        float currentMusicVolume = audioManager.MusicVolume;
        const float musicLabelHeight = 30f;
        GUI.Label(new Rect(x, y, buttonWidth, musicLabelHeight),
            $"Громкость музыки: {currentMusicVolume * 100f:0}%", labelStyle);
        y += musicLabelHeight + 4f;
        float newMusicVolume = GUI.HorizontalSlider(new Rect(x, y, buttonWidth, 20f), currentMusicVolume, 0f, 1f);
        if (!Mathf.Approximately(newMusicVolume, currentMusicVolume))
        {
            audioManager.SetMusicVolume(newMusicVolume);
            PlaySliderFeedback();
        }
        y += 44f;

        RefreshBlizzardReference();
        bool snowEnabled = blizzardManager != null && blizzardManager.IsEnabled;
        string snowLabel = blizzardManager == null
            ? "Метель: недоступна"
            : $"Метель: {(snowEnabled ? "Вкл" : "Выкл")}";

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), snowLabel, buttonStyle) &&
            blizzardManager != null)
        {
            blizzardManager.SetEnabled(!snowEnabled);
            audioManager.PlayUiClick();
        }

        y += buttonHeight + 14f;

        float currentIntensity = blizzardManager != null ? blizzardManager.Intensity : 0f;
        GUI.Label(
            new Rect(x, y, buttonWidth, 24f),
            blizzardManager == null
                ? "Сила метели: недоступна"
                : $"Сила метели: {currentIntensity * 100f:0}%",
            labelStyle);

        y += 28f;

        float newIntensity = GUI.HorizontalSlider(new Rect(x, y, buttonWidth, 20f), currentIntensity, 0f, 1f);
        if (blizzardManager != null && !Mathf.Approximately(newIntensity, currentIntensity))
        {
            blizzardManager.SetIntensity(newIntensity);
        }

        y += 44f;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Назад", buttonStyle))
        {
            settingsOpen = false;
            audioManager.PlayUiBack();
            audioManager.SavePreferences();
        }
    }

    private void PlaySliderFeedback()
    {
        float now = Time.unscaledTime;
        if (now < lastSliderFeedbackTime) { nextSliderFeedbackTime = 0f; }
        lastSliderFeedbackTime = now;
        if (now < nextSliderFeedbackTime) { return; }
        nextSliderFeedbackTime = now + 0.12f;
        audioManager.PlayUiSelect();
    }

    private void RefreshBlizzardReference()
    {
        if (blizzardManager != null)
        {
            return;
        }

        // Менеджер метели создаётся кодом при загрузке игры — на момент Awake
        // он может ещё не существовать, поэтому добираем ссылку здесь.
        blizzardManager = BlizzardManager.Instance != null
            ? BlizzardManager.Instance
            : FindFirstObjectByType<BlizzardManager>();
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
        {
            return;
        }

        panelTexture = new Texture2D(1, 1);
        panelTexture.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.09f, 0.96f));
        panelTexture.Apply();

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = panelTexture }
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = UiTheme.Font,
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            font = UiTheme.Font,
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            font = UiTheme.Font,
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
        };
    }
}
