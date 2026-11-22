using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Низкое разрешение в духе PS1: трёхмерный мир рендерится в маленький
/// RenderTexture и растягивается на экран точечной фильтрацией.
///
/// Отличие от URP render scale: там разрешение считается как доля от окна,
/// поэтому картинка «плывёт» вместе с размером монитора и пиксельные блоки
/// получаются неровными при нецелом коэффициенте. Здесь высота кадра
/// фиксирована, поэтому эффект выглядит одинаково на любом экране.
///
/// Кадр показывается отдельным uGUI-канвасом на максимальной сортировке:
/// интерфейс игры нарисован через IMGUI, а IMGUI всегда ложится поверх
/// uGUI, поэтому HUD остаётся чётким и не пикселизуется.
/// </summary>
[DisallowMultipleComponent]
public sealed class PsxLowResolution : MonoBehaviour
{
    public enum AspectMode
    {
        /// <summary>Ширина считается от соотношения сторон окна: кадр без искажений и без полей.</summary>
        FollowScreen = 0,

        /// <summary>Ровно pillarboxWidth на targetHeight, поля по краям. Как на настоящей приставке.</summary>
        Pillarbox = 1,
    }

    private const string ScreenObjectName = "PsxScreen";
    private const string CanvasObjectName = "PsxCanvas";
    private const string DisplayCameraName = "PsxDisplayCamera";
    private const string BufferName = "PSX_LowRes";

    private static PsxLowResolution instance;

    /// <summary>
    /// При старте подхватываем существующий объект. Ленивая проверка нужна ещё и
    /// потому, что после перекомпиляции скриптов в Play Mode статика обнуляется,
    /// а Awake у уже живого компонента повторно не вызывается.
    /// </summary>
    public static PsxLowResolution Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PsxLowResolution>(FindObjectsInactive.Include);
            }

            return instance;
        }
        private set => instance = value;
    }

    /// <summary>Эффект включён: буфер создан и камера рендерит в него.</summary>
    public static bool IsActive => Instance != null && Instance.applied;

    /// <summary>
    /// Куда на экране попадает кадр: при полях это центральный прямоугольник.
    /// Когда эффект выключен, картинка занимает весь экран.
    /// </summary>
    public static Rect ScreenRect => IsActive ? Instance.DestinationRect() : new Rect(0f, 0f, Screen.width, Screen.height);

    /// <summary>
    /// Проекция мира в пиксели экрана. Нужна потому, что камера с targetTexture
    /// возвращает WorldToScreenPoint в координатах маленького буфера, а IMGUI
    /// рисуется в полном разрешении — без пересчёта плашки уезжают в угол.
    /// </summary>
    public static Vector3 WorldToScreenPoint(Camera camera, Vector3 worldPoint)
    {
        if (camera == null)
        {
            return Vector3.zero;
        }

        Vector3 viewport = camera.WorldToViewportPoint(worldPoint);
        Rect frame = ScreenRect;
        return new Vector3(
            frame.x + viewport.x * frame.width,
            frame.y + viewport.y * frame.height,
            viewport.z);
    }

    [Header("Разрешение")]
    [SerializeField, Min(60)]
    [Tooltip("Строк по вертикали. 240 — классика PS1, 180 — жёстче, ниже 180 — совсем грубо.")]
    private int targetHeight = 240;

    [SerializeField]
    [Tooltip("FollowScreen — кадр занимает весь экран, число строк фиксировано, ширина следует за окном. " +
             "Pillarbox — ровно заданный кадр 4:3 с чёрными полями по краям.")]
    private AspectMode aspectMode = AspectMode.FollowScreen;

    [SerializeField, Min(60)]
    [Tooltip("Ширина кадра в режиме Pillarbox. 320 при высоте 240 даёт классические 4:3.")]
    private int pillarboxWidth = 320;

    [Header("Отладка")]
    [SerializeField]
    [Tooltip("Включать эффект при старте.")]
    private bool startEnabled = true;

    [SerializeField]
    [Tooltip("Клавиша включения и выключения эффекта.")]
    private KeyCode toggleKey = KeyCode.F9;

    [SerializeField]
    [Tooltip("Перевернуть кадр по вертикали. Нужен только если картинка вверх ногами на конкретном графическом API.")]
    private bool flipVertically;

    private RenderTexture buffer;
    private Camera hookedCamera;
    private RenderTexture previousTarget;
    private bool previousAllowMsaa;
    private bool previousAllowHdr;
    private bool applied;
    private int builtScreenWidth;
    private int builtScreenHeight;
    private Canvas presenterCanvas;
    private RawImage presenterImage;
    private Image pillarboxImage;
    private Camera displayCamera;

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

        // В новой сцене другая камера: подхватываем её до первого кадра,
        // иначе сцена успевает мигнуть полным разрешением.
        if (Instance != null && Instance.applied)
        {
            Instance.Rebuild();
            Instance.HookCamera(force: true);
        }
    }

    private static void Adopt()
    {
        if (Instance != null)
        {
            return;
        }

        PsxLowResolution found = FindFirstObjectByType<PsxLowResolution>();
        if (found == null)
        {
            GameObject go = new GameObject(ScreenObjectName);
            found = go.AddComponent<PsxLowResolution>();
        }

        Instance = found;
        if (found.transform.parent == null)
        {
            DontDestroyOnLoad(found.gameObject);
        }
    }

    private void Awake()
    {
        Instance = this;
        BuildPresenter();
        RecoverIfHooked();

        if (startEnabled)
        {
            SetEnabled(true);
        }
    }

    /// <summary>
    /// Канвас для показа кадра. Screen Space - Overlay с максимальной
    /// сортировкой гарантирует, что uGUI ляжет под IMGUI-интерфейс:
    /// IMGUI рисуется всегда последним.
    /// </summary>
    private void BuildPresenter()
    {
        if (presenterImage != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(CanvasObjectName);
        canvasObject.transform.SetParent(transform, false);

        presenterCanvas = canvasObject.AddComponent<Canvas>();
        presenterCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        presenterCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        GameObject backgroundObject = new GameObject("Pillarbox");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        pillarboxImage = backgroundObject.AddComponent<Image>();
        pillarboxImage.color = Color.black;
        pillarboxImage.raycastTarget = false;
        RectTransform backgroundRect = pillarboxImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject imageObject = new GameObject("Frame");
        imageObject.transform.SetParent(canvasObject.transform, false);
        presenterImage = imageObject.AddComponent<RawImage>();
        presenterImage.raycastTarget = false;
        presenterImage.enabled = false;
        RectTransform rect = presenterImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Перекомпиляция скриптов в Play Mode стирает приватные поля компонента,
    /// хотя камера остаётся с нашей текстурой. Восстанавливаемся, иначе эффект
    /// «залипает» — буфер уже некому отпустить.
    /// </summary>
    private void RecoverIfHooked()
    {
        Camera cam = Camera.main;
        if (cam == null || cam.targetTexture == null)
        {
            return;
        }

        if (cam.targetTexture.name != BufferName)
        {
            return;
        }

        buffer = cam.targetTexture;
        hookedCamera = cam;
        applied = true;
        builtScreenWidth = Screen.width;
        builtScreenHeight = Screen.height;
    }

    private void OnDestroy()
    {
        SetEnabled(false);
        ReleaseBuffer();

        if (presenterCanvas != null)
        {
            Destroy(presenterCanvas.gameObject);
            presenterCanvas = null;
            presenterImage = null;
            pillarboxImage = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (TogglePressed())
        {
            SetEnabled(!applied);
        }

        if (!applied)
        {
            return;
        }

        if (Screen.width != builtScreenWidth || Screen.height != builtScreenHeight)
        {
            // Окно растянули — пересобираем буфер под новый размер.
            Rebuild();
            HookCamera(force: true);
        }
        else if (hookedCamera == null || Camera.main != hookedCamera)
        {
            // Сменилась сцена — у неё своя камера.
            HookCamera(force: true);
        }
    }

    private bool TogglePressed()
    {
        if (toggleKey == KeyCode.None)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        if (!System.Enum.TryParse(toggleKey.ToString(), out Key key))
        {
            return false;
        }

        KeyControl control = keyboard[key];
        return control != null && control.wasPressedThisFrame;
#else
        return Input.GetKeyDown(toggleKey);
#endif
    }

    /// <summary>Включить или выключить эффект. Безопасно вызывать сколько угодно раз.</summary>
    public void SetEnabled(bool on)
    {
        if (on == applied)
        {
            return;
        }

        if (on)
        {
            EnsureBuffer();
            applied = true;
            HookCamera(force: true);
            ShowFrame(true);
        }
        else
        {
            applied = false;
            UnhookCamera();
            ShowFrame(false);

            // Игровая камера снова рисует в дисплей — заглушка больше не нужна.
            ReleaseDisplayCamera();
        }
    }

    /// <summary>Сменить высоту кадра на лету — для подбора «на глаз».</summary>
    public void SetTargetHeight(int height)
    {
        int clamped = Mathf.Clamp(height, 60, 2160);
        if (clamped == targetHeight)
        {
            return;
        }

        targetHeight = clamped;
        if (applied)
        {
            Rebuild();
            HookCamera(force: true);
        }
    }

    public int TargetHeight => targetHeight;

    public AspectMode CurrentAspectMode => aspectMode;

    /// <summary>Сменить формат кадра: с полями 4:3 или во весь экран.</summary>
    public void SetAspectMode(AspectMode mode)
    {
        if (mode == aspectMode)
        {
            return;
        }

        aspectMode = mode;
        if (applied)
        {
            Rebuild();
            HookCamera(force: true);
        }
    }

    /// <summary>
    /// Показать или спрятать кадр. Скрытый канвас не трогает экран вовсе,
    /// поэтому при выключенном эффекте игра выглядит как обычно.
    /// </summary>
    private void ShowFrame(bool visible)
    {
        if (presenterImage == null)
        {
            return;
        }

        presenterImage.texture = visible ? buffer : null;
        presenterImage.enabled = visible && buffer != null;
        presenterImage.uvRect = flipVertically ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);

        if (pillarboxImage != null)
        {
            // Чёрная подложка нужна только когда кадр не занимает весь экран.
            pillarboxImage.enabled = visible && aspectMode == AspectMode.Pillarbox;
        }

        if (presenterCanvas != null)
        {
            presenterCanvas.enabled = visible;
        }
    }

    private void HookCamera(bool force)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        if (!force && cam == hookedCamera)
        {
            return;
        }

        if (hookedCamera != null && hookedCamera != cam)
        {
            UnhookCamera();
        }

        if (cam == hookedCamera)
        {
            // Та же камера, но буфер пересобрали — достаточно подменить текстуру.
            cam.targetTexture = buffer;
            EnsureDisplayCamera();
            return;
        }

        hookedCamera = cam;
        previousTarget = cam.targetTexture;
        previousAllowMsaa = cam.allowMSAA;
        previousAllowHdr = cam.allowHDR;

        // Сглаживание размыло бы край пикселя, а HDR вынес бы яркость за пределы
        // буфера — для PS1-вида нужен обычный LDR-кадр.
        cam.allowMSAA = false;
        cam.allowHDR = false;
        cam.targetTexture = buffer;

        EnsureDisplayCamera();
    }

    /// <summary>
    /// Камера-заглушка для дисплея. Игровая камера рисует в RenderTexture, и
    /// тогда на экран не рендерит никто — Unity пишет «Display 1 No cameras
    /// rendering». Пустая камера с чёрным фоном закрывает эту дыру: канвас
    /// рисуется поверх неё, картинку она не портит.
    /// </summary>
    private void EnsureDisplayCamera()
    {
        if (displayCamera != null)
        {
            displayCamera.enabled = true;
            return;
        }

        // После перекомпиляции полей ссылка теряется, а объект остаётся жив.
        Transform existing = transform.Find(DisplayCameraName);
        if (existing != null)
        {
            displayCamera = existing.GetComponent<Camera>();
            if (displayCamera != null)
            {
                displayCamera.enabled = true;
                return;
            }
        }

        GameObject go = new GameObject(DisplayCameraName);
        go.transform.SetParent(transform, false);

        displayCamera = go.AddComponent<Camera>();
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = Color.black;
        displayCamera.cullingMask = 0;
        displayCamera.depth = -100f;
        displayCamera.orthographic = true;
        displayCamera.orthographicSize = 0.5f;
        displayCamera.nearClipPlane = 0.1f;
        displayCamera.farClipPlane = 1f;
        displayCamera.allowMSAA = false;
        displayCamera.allowHDR = false;
        displayCamera.useOcclusionCulling = false;
        displayCamera.targetTexture = null;

        // Свой AudioListener у заглушки не нужен: он уже есть у игровой камеры.
        AudioListener listener = go.GetComponent<AudioListener>();
        if (listener != null)
        {
            Destroy(listener);
        }
    }

    /// <summary>
    /// Гасим заглушку, но не уничтожаем объект: Destroy отложен до конца кадра,
    /// и повторное включение эффекта успело бы найти «живой» Transform и
    /// оставить дисплей без камеры.
    /// </summary>
    private void ReleaseDisplayCamera()
    {
        if (displayCamera == null)
        {
            // После перекомпиляции ссылка потеряна, а объект жив.
            Transform existing = transform.Find(DisplayCameraName);
            if (existing != null)
            {
                displayCamera = existing.GetComponent<Camera>();
            }
        }

        if (displayCamera == null)
        {
            return;
        }

        displayCamera.enabled = false;
    }

    private void UnhookCamera()
    {
        if (hookedCamera == null)
        {
            return;
        }

        hookedCamera.targetTexture = previousTarget;
        hookedCamera.allowMSAA = previousAllowMsaa;
        hookedCamera.allowHDR = previousAllowHdr;
        hookedCamera = null;
        previousTarget = null;
    }

    private void EnsureBuffer()
    {
        if (buffer != null)
        {
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        int height = Mathf.Clamp(targetHeight, 60, 2160);
        int width;

        if (aspectMode == AspectMode.Pillarbox)
        {
            width = Mathf.Clamp(pillarboxWidth, 60, 3840);
        }
        else
        {
            // Ширина следует за окном, поэтому кадр не растягивается по диагонали.
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
            width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 60, 3840);
        }

        ReleaseBuffer();

        RenderTexture created = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
        {
            name = BufferName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
        };
        created.Create();

        buffer = created;
        builtScreenWidth = Screen.width;
        builtScreenHeight = Screen.height;

        if (applied)
        {
            // Буфер пересоздан — старую текстуру канвас уже не покажет.
            ShowFrame(true);
        }
    }

    private void ReleaseBuffer()
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Release();
        Destroy(buffer);
        buffer = null;
    }

    private Rect DestinationRect()
    {
        if (buffer == null)
        {
            return new Rect(0f, 0f, Screen.width, Screen.height);
        }

        if (aspectMode == AspectMode.Pillarbox)
        {
            float scale = Mathf.Min(Screen.width / (float)buffer.width, Screen.height / (float)buffer.height);
            float width = buffer.width * scale;
            float height = buffer.height * scale;
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        return new Rect(0f, 0f, Screen.width, Screen.height);
    }

    private void LateUpdate()
    {
        if (!applied || buffer == null || presenterImage == null)
        {
            return;
        }

        // Канвас в Screen Space Overlay живёт в пикселях экрана, поэтому
        // прямоугольник кадра задаём напрямую от DestinationRect.
        Rect destination = DestinationRect();
        RectTransform rect = presenterImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(destination.x, Screen.height - destination.yMax);
        rect.sizeDelta = new Vector2(destination.width, destination.height);
    }
}
