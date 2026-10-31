using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class PauseMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MusicPlayer musicPlayer;

    [Header("Layout")]
    [SerializeField] private float panelWidth = 420f;

    public static bool IsPaused { get; private set; }

    private StarterAssetsInputs playerInputs;
    private bool previousCursorLocked = true;
    private bool previousCursorInputForLook = true;
    private bool settingsOpen;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private Texture2D panelTexture;

    private void Awake()
    {
        if (musicPlayer == null)
        {
            musicPlayer = FindFirstObjectByType<MusicPlayer>();
        }
    }

    private void Update()
    {
        if (!IsEscapePressed())
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
        }
        else
        {
            Resume();
        }
    }

    private void OnDisable()
    {
        if (IsPaused)
        {
            RestoreTimeScale();
        }
    }

    private void OnDestroy()
    {
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
    }

    private void OnGUI()
    {
        if (!IsPaused)
        {
            return;
        }

        EnsureStyles();

        float width = Mathf.Min(panelWidth, Screen.width - 40f);
        float height = settingsOpen ? 300f : 250f;
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
            }

            return;
        }

        bool musicEnabled = musicPlayer != null && musicPlayer.IsEnabled;
        string musicLabel = musicPlayer == null
            ? "Музыка: недоступна"
            : $"Музыка: {(musicEnabled ? "Вкл" : "Выкл")}";

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), musicLabel, buttonStyle) &&
            musicPlayer != null)
        {
            musicPlayer.SetEnabled(!musicEnabled);
        }

        y += buttonHeight + 14f;

        float currentVolume = musicPlayer != null ? musicPlayer.Volume : 0f;
        GUI.Label(
            new Rect(x, y, buttonWidth, 24f),
            $"Громкость музыки: {currentVolume * 100f:0}%",
            labelStyle);

        y += 28f;

        float newVolume = GUI.HorizontalSlider(new Rect(x, y, buttonWidth, 20f), currentVolume, 0f, 1f);
        if (musicPlayer != null && !Mathf.Approximately(newVolume, currentVolume))
        {
            musicPlayer.SetVolume(newVolume);
        }

        y += 44f;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Назад", buttonStyle))
        {
            settingsOpen = false;
        }
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
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.85f, 0.9f, 0.95f) }
        };
    }
}
