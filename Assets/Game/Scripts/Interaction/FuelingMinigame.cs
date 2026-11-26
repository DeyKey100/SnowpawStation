using System;
using StarterAssets;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Результат мини-игры: сколько налито и сдана ли заправка.</summary>
public readonly struct FuelingResult
{
    public readonly float Liters;
    public readonly bool Committed;

    public FuelingResult(float liters, bool committed)
    {
        Liters = liters;
        Committed = committed;
    }
}

/// <summary>
/// Мини-игра заправки из fueling_v4.html: открыть лючок/крышку, взять пистолет,
/// вставить в горловину, удерживать ЛКМ, вернуть пистолет и закрыть лючок.
/// Пиксельный кадр 320×180 рисуется процедурно в Texture2D и показывается
/// IMGUI-оверлеем поверх затемнённого 3D-кадра.
/// </summary>
[DisallowMultipleComponent]
public sealed partial class FuelingMinigame : MonoBehaviour
{
    // ---------- Параметры заправки ----------
    private const float Rate = 8f;          // л/с
    private const float MinRatio = 0.75f;
    private const float GoodRatio = 0.91f;
    private const float OverRatio = 1.10f;

    // ---------- Геометрия лючка / горловины ----------
    private const float HubX = 142f;
    private const float HubY = 80f;
    private const float DoorW = 64f;
    private const float DoorH = 44f;
    private static readonly float HingeX = Mathf.Round(HubX - DoorW * 0.5f);
    private const float DoorThick = 4f;
    private const float HoleR = 15f;

    // ---------- Пистолет ----------
    private const float HomeX = 214f;
    private const float HomeY = 58f;
    private const float HomeRot = 0.16f;
    private const float HandRot = 0.14f;
    private const float NozzleScale = 1f;

    private static readonly Vector2 NozTip = new Vector2(-52f, -44f);
    private static readonly Vector2 NozBody = new Vector2(4f, 0f);
    private static readonly Vector2 NozPort = new Vector2(25.4f, 40.3f);
    private static readonly Vector2 NozPortDir = new Vector2(0.342f, 0.940f);

    private static readonly Vector2 PlugBody = new Vector2(HubX + 17f, HubY + 23f);
    private static readonly Vector2 PlugPort = new Vector2(HubX + 37.2f, HubY + 49.6f);
    private static readonly Vector2 PlugPortDir = new Vector2(0.6f, 0.8f);

    private const float HitBodyR = 25f;
    private static readonly Vector2 HoseAnchor = new Vector2(330f, 156f);

    // ---------- Шкала ----------
    private const float GaugeX = 18f;
    private const float GaugeTop = 48f;
    private const float GaugeBottom = 148f;
    private const float GaugeW = 10f;

    // ---------- Состояние ----------
    private bool active;
    private bool done;
    private bool capOpen;
    private float capAnim;
    private bool carrying;
    private bool plugged;
    private bool holding;
    private bool gaugeVisible;
    private float poured;
    private float capFlashTimer;
    private bool resultVisible;
    private string resultTitle = "ПРИНЯТО";
    private Color32 resultColor = Cream;

    private Vector2 pointer;
    private Vector2 tipWorld;
    private Vector2 tipVel;

    private bool hoverCap;
    private bool hoverNozzle;
    private bool hoverHome;
    private bool hoverNeck;

    private int carIndex;
    private CarDef currentCar;
    private float target = 35f;
    private string hintText = string.Empty;

    private Action<FuelingResult> onFinished;

    // ---------- Рендер ----------
    private Texture2D texture;
    private PixelCanvas background;
    private PixelCanvas frame;
    private Rect panelRect;
    private float panelScale = 1f;

    // ---------- Курсор / ввод ----------
    private StarterAssetsInputs playerInputs;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool previousInputCursorLocked;
    private bool previousInputLook;

    private static FuelingMinigame instance;

    public static bool IsActive => instance != null && instance.active;

    /// <summary>Пока идёт мини-игра, Esc принадлежит ей, а не меню паузы.</summary>
    public static bool BlocksPauseInput => IsActive;

    // ---------- Публичный запуск ----------

    public static bool Begin(int car, float targetLiters, Action<FuelingResult> finished)
    {
        FuelingMinigame game = EnsureInstance();
        return game.StartGame(car, targetLiters, finished);
    }

    public static void CancelActive()
    {
        if (instance != null && instance.active)
        {
            instance.EndGame(false);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private static FuelingMinigame EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        FuelingMinigame found = FindFirstObjectByType<FuelingMinigame>(FindObjectsInactive.Include);
        if (found != null)
        {
            instance = found;
            return found;
        }

        GameObject go = new GameObject("FuelingMinigame");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FuelingMinigame>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        background = new PixelCanvas();
        frame = new PixelCanvas();
        texture = new Texture2D(PixelCanvas.W, PixelCanvas.H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = "FuelingMinigameFrame",
        };
        texture.SetPixels32(new Color32[PixelCanvas.W * PixelCanvas.H]);
        texture.Apply(false, false);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }

    private void OnDisable()
    {
        // Компонент выключили или сцену перезагрузили посреди мини-игры:
        // курсор возвращаем, но заказчика не дёргаем — его замки снимет колонка.
        if (!active)
        {
            return;
        }

        active = false;
        done = false;
        resultVisible = false;
        onFinished = null;
        RestoreCursor();
    }

    private bool StartGame(int car, float targetLiters, Action<FuelingResult> finished)
    {
        if (active)
        {
            return false;
        }

        active = true;
        onFinished = finished;
        carIndex = Mathf.Clamp(car, 0, Cars.Length - 1);
        currentCar = Cars[carIndex];
        target = Mathf.Max(1f, targetLiters);

        ResetState();
        BuildBackground();
        FitPanel();
        CachePlayerInputs();
        UnlockCursor();
        UpdateHovers();
        UpdateHint();
        return true;
    }

    private void ResetState()
    {
        done = false;
        capOpen = false;
        capAnim = 0f;
        carrying = false;
        plugged = false;
        holding = false;
        gaugeVisible = false;
        poured = 0f;
        capFlashTimer = 0f;
        resultVisible = false;
        resultTitle = "ПРИНЯТО";
        resultColor = Cream;

        pointer = new Vector2(HomeX, HomeY);
        tipWorld = new Vector2(HomeX, HomeY);
        tipVel = Vector2.zero;

        hoverCap = false;
        hoverNozzle = false;
        hoverHome = false;
        hoverNeck = false;
        hintText = string.Empty;
    }

    private void EndGame(bool committed)
    {
        if (!active)
        {
            return;
        }

        active = false;
        done = false;
        resultVisible = false;

        Action<FuelingResult> callback = onFinished;
        onFinished = null;
        float liters = poured;

        RestoreCursor();
        callback?.Invoke(new FuelingResult(liters, committed));
    }

    private void Commit()
    {
        if (!done)
        {
            return;
        }

        EndGame(true);
    }

    // ---------- Кадр ----------

    private void Update()
    {
        if (!active)
        {
            return;
        }

        if (SceneLoadingScreen.IsLoading || PauseMenu.IsPaused)
        {
            EndGame(false);
            return;
        }

        FitPanel();
        ReadInput(out Vector2 pointerCanvas, out bool leftDown, out bool leftHeld, out bool rightDown,
            out bool spaceDown, out bool escapeDown);

        UpdateState(Mathf.Min(Time.deltaTime, 0.05f), pointerCanvas, leftDown, leftHeld, rightDown, spaceDown, escapeDown);

        if (active)
        {
            Render();
        }
    }

    private void ReadInput(out Vector2 pointerCanvas, out bool leftDown, out bool leftHeld, out bool rightDown,
        out bool spaceDown, out bool escapeDown)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        Vector2 screen = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        leftDown = mouse != null && mouse.leftButton.wasPressedThisFrame;
        leftHeld = mouse != null && mouse.leftButton.isPressed;
        rightDown = mouse != null && mouse.rightButton.wasPressedThisFrame;
        spaceDown = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        escapeDown = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
        Vector2 screen = Input.mousePosition;
        leftDown = Input.GetMouseButtonDown(0);
        leftHeld = Input.GetMouseButton(0);
        rightDown = Input.GetMouseButtonDown(1);
        spaceDown = Input.GetKeyDown(KeyCode.Space);
        escapeDown = Input.GetKeyDown(KeyCode.Escape);
#endif
        Vector2 gui = new Vector2(screen.x, Screen.height - screen.y);
        pointerCanvas = (gui - panelRect.position) / Mathf.Max(1f, panelScale);
        pointerCanvas.x = Mathf.Clamp(pointerCanvas.x, -40f, PixelCanvas.W + 40f);
        pointerCanvas.y = Mathf.Clamp(pointerCanvas.y, -40f, PixelCanvas.H + 40f);
    }

    private void UpdateState(float dt, Vector2 pointerCanvas, bool leftDown, bool leftHeld, bool rightDown,
        bool spaceDown, bool escapeDown)
    {
        pointer = pointerCanvas;

        float targetCap = capOpen ? 1f : 0f;
        capAnim = Mathf.Lerp(capAnim, targetCap, Mathf.Clamp01(dt * 12f));
        if (Mathf.Abs(capAnim - targetCap) < 0.005f)
        {
            capAnim = targetCap;
        }

        if (capFlashTimer > 0f)
        {
            capFlashTimer = Mathf.Max(0f, capFlashTimer - dt);
            if (capFlashTimer == 0f)
            {
                UpdateHint();
            }
        }

        if (!carrying && !plugged)
        {
            const float springK = 140f;
            const float damping = 18f;
            float ax = (HomeX - tipWorld.x) * springK - tipVel.x * damping;
            float ay = (HomeY - tipWorld.y) * springK - tipVel.y * damping;
            tipVel.x += ax * dt;
            tipVel.y += ay * dt;
            tipWorld.x += tipVel.x * dt;
            tipWorld.y += tipVel.y * dt;
        }
        else
        {
            tipVel = Vector2.zero;
        }

        if (carrying)
        {
            tipWorld = TipFromBodyWorld(pointer.x, pointer.y, HandRot, NozzleScale);
        }

        UpdateHovers();

        if (done)
        {
            if (spaceDown || leftDown || escapeDown)
            {
                Commit();
            }

            return;
        }

        if (escapeDown)
        {
            EndGame(false);
            return;
        }

        if (plugged)
        {
            holding = leftHeld;
            if (holding)
            {
                poured = Mathf.Clamp(poured + Rate * dt, 0f, target * 1.25f);
                UpdateHint();
            }

            if (rightDown)
            {
                plugged = false;
                carrying = true;
                holding = false;
                pointer = new Vector2(Mathf.Clamp(pointer.x, 20f, PixelCanvas.W - 20f),
                    Mathf.Clamp(pointer.y, 20f, PixelCanvas.H - 20f));
                tipWorld = TipFromBodyWorld(pointer.x, pointer.y, HandRot, NozzleScale);
                UpdateHovers();
                UpdateHint();
            }

            return;
        }

        if (!leftDown)
        {
            return;
        }

        if (carrying)
        {
            if (hoverNeck)
            {
                carrying = false;
                plugged = true;
                holding = true;
                gaugeVisible = true;
                UpdateHovers();
                UpdateHint();
                return;
            }

            if (hoverHome)
            {
                carrying = false;
                holding = false;
                UpdateHovers();
                UpdateHint();
            }

            return;
        }

        if (hoverNozzle)
        {
            if (!capOpen)
            {
                capFlashTimer = 1.3f;
                UpdateHint();
                return;
            }

            carrying = true;
            tipWorld = TipFromBodyWorld(pointer.x, pointer.y, HandRot, NozzleScale);
            UpdateHovers();
            UpdateHint();
            return;
        }

        if (!hoverCap)
        {
            return;
        }

        if (!capOpen)
        {
            capOpen = true;
            capFlashTimer = 0f;
            UpdateHovers();
            UpdateHint();
        }
        else
        {
            capOpen = false;
            if (poured >= target * MinRatio)
            {
                FinishOrder();
            }
            else
            {
                UpdateHovers();
                UpdateHint();
            }
        }
    }

    private void FinishOrder()
    {
        done = true;
        holding = false;
        carrying = false;
        plugged = false;
        UpdateHovers();
        UpdateHint();

        float ratio = poured / target;
        if (ratio > OverRatio)
        {
            resultTitle = "ПЕРЕЛИВ";
            resultColor = Bad;
        }
        else if (ratio >= GoodRatio)
        {
            resultTitle = "ТОЧНО В ЦЕЛЬ";
            resultColor = Ok;
        }
        else
        {
            resultTitle = "ПРИНЯТО";
            resultColor = Ember;
        }

        resultVisible = true;
    }

    // ---------- Наведение ----------

    private void UpdateHovers()
    {
        hoverCap = false;
        hoverNozzle = false;
        hoverHome = false;
        hoverNeck = false;

        if (done || plugged)
        {
            return;
        }

        Vector2 body = GetNozzleBodyWorld();

        if (carrying)
        {
            hoverNeck = capOpen && Dist(tipWorld.x, tipWorld.y, HubX, HubY) <= HoleR + 3f;
            Vector2 homeBody = NozzleLocalToWorld(NozBody, HomeRot, HomeX, HomeY, NozzleScale);
            bool nearHome = Dist(body.x, body.y, homeBody.x, homeBody.y) <= 55f || pointer.x >= 190f;
            bool awayFromNeck = Dist(tipWorld.x, tipWorld.y, HubX, HubY) > HoleR + 16f;
            hoverHome = !hoverNeck && (nearHome || (poured > 0.05f && awayFromNeck));
            return;
        }

        hoverNozzle = Dist(pointer.x, pointer.y, body.x, body.y) <= HitBodyR;
        hoverCap = IsCapHit(pointer.x, pointer.y);
    }

    private bool IsCapHit(float x, float y)
    {
        if (currentCar.IsExternalTank)
        {
            float open = capAnim;
            float capX = HubX - open * 42f;
            float capY = HubY + open * 10f;
            bool onCap = Dist(x, y, capX, capY) <= HoleR + 7f;
            bool onNeck = Dist(x, y, HubX, HubY) <= HoleR + 7f;
            return onCap || (capOpen && onNeck);
        }

        float wellX = HingeX;
        float wellY = Mathf.Round(HubY - DoorH * 0.5f);

        if (!capOpen)
        {
            return x >= wellX - 4f && x <= wellX + DoorW + 4f &&
                   y >= wellY - 4f && y <= wellY + DoorH + 4f;
        }

        bool onOpenLeaf = x >= wellX - 34f && x <= wellX + 12f &&
                          y >= wellY - 8f && y <= wellY + DoorH + 8f;
        bool onWell = x >= wellX - 4f && x <= wellX + DoorW + 4f &&
                      y >= wellY - 4f && y <= wellY + DoorH + 4f;
        return onOpenLeaf || onWell;
    }

    // ---------- Подсказки ----------

    private void UpdateHint()
    {
        if (done)
        {
            hintText = string.Empty;
            return;
        }

        if (capFlashTimer > 0f && !capOpen)
        {
            hintText = "СНАЧАЛА ОТКРОЙТЕ КРЫШКУ";
            return;
        }

        float ratio = poured / target;
        if (!capOpen && poured < 0.05f)
        {
            hintText = currentCar.IsExternalTank ? "ОТКРОЙТЕ КРЫШКУ БЕНЗОБАКА" : "ОТКРОЙТЕ ЛЮЧОК БЕНЗОБАКА";
        }
        else if (capOpen && !carrying && !plugged && poured < 0.05f)
        {
            hintText = "ВОЗЬМИТЕ ЗАПРАВОЧНЫЙ ПИСТОЛЕТ";
        }
        else if (carrying && poured < 0.05f)
        {
            hintText = hoverNeck
                ? "НАЖМИТЕ ЛКМ, ЧТОБЫ ВСТАВИТЬ ПИСТОЛЕТ"
                : "НАВЕДИТЕ НОСИК В ГОРЛОВИНУ И НАЖМИТЕ ЛКМ";
        }
        else if (plugged)
        {
            if (ratio > OverRatio)
            {
                hintText = "ПЕРЕЛИВ! ПКМ — ВЫНУТЬ ПИСТОЛЕТ";
            }
            else if (ratio >= GoodRatio)
            {
                hintText = "ДОСТАТОЧНО · ПКМ — ВЫНУТЬ ПИСТОЛЕТ";
            }
            else
            {
                hintText = "УДЕРЖИВАЙТЕ ЛКМ — ПОДАЧА БЕНЗИНА · ПКМ — ВЫНУТЬ";
            }
        }
        else if (carrying && poured >= 0.05f)
        {
            hintText = ratio >= MinRatio ? "ВЕРНИТЕ ПИСТОЛЕТ НА МЕСТО" : "ВСТАВЬТЕ НОСИК В БАК ИЛИ ВЕРНИТЕ ПИСТОЛЕТ";
        }
        else if (!carrying && !plugged && capOpen && poured >= 0.05f)
        {
            if (ratio >= MinRatio)
            {
                hintText = currentCar.IsExternalTank ? "ЗАКРОЙТЕ КРЫШКУ БЕНЗОБАКА" : "ЗАКРОЙТЕ ЛЮЧОК БЕНЗОБАКА";
            }
            else
            {
                hintText = "МАЛО ТОПЛИВА — ДОЛЕЙТЕ ЕЩЁ";
            }
        }
    }

    // ---------- Геометрия пистолета ----------

    private Vector2 GetNozzleBodyWorld()
    {
        if (plugged)
        {
            return PlugBody;
        }

        float rot = carrying ? HandRot : HomeRot;
        return NozzleLocalToWorld(NozBody, rot, tipWorld.x, tipWorld.y, NozzleScale);
    }

    private Vector2 GetNozzlePortWorld()
    {
        if (plugged)
        {
            return PlugPort;
        }

        float rot = carrying ? HandRot : HomeRot;
        return NozzleLocalToWorld(NozPort, rot, tipWorld.x, tipWorld.y, NozzleScale);
    }

    private Vector2 GetNozzlePortDirWorld()
    {
        if (plugged)
        {
            return PlugPortDir;
        }

        float rot = carrying ? HandRot : HomeRot;
        float cos = Mathf.Cos(rot);
        float sin = Mathf.Sin(rot);
        return new Vector2(NozPortDir.x * cos - NozPortDir.y * sin, NozPortDir.x * sin + NozPortDir.y * cos);
    }

    private static Vector2 NozzleLocalToWorld(Vector2 local, float rot, float tipX, float tipY, float scale)
    {
        float dx = (local.x - NozTip.x) * scale;
        float dy = (local.y - NozTip.y) * scale;
        float cos = Mathf.Cos(rot);
        float sin = Mathf.Sin(rot);
        return new Vector2(tipX + dx * cos - dy * sin, tipY + dx * sin + dy * cos);
    }

    private static Vector2 TipFromBodyWorld(float bodyX, float bodyY, float rot, float scale)
    {
        float dx = (NozBody.x - NozTip.x) * scale;
        float dy = (NozBody.y - NozTip.y) * scale;
        float cos = Mathf.Cos(rot);
        float sin = Mathf.Sin(rot);
        return new Vector2(bodyX - (dx * cos - dy * sin), bodyY - (dx * sin + dy * cos));
    }

    private static float Dist(float ax, float ay, float bx, float by)
    {
        float dx = ax - bx;
        float dy = ay - by;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // ---------- Оверлей ----------

    private void FitPanel()
    {
        int sx = Mathf.Max(1, Mathf.FloorToInt(Screen.width * 0.86f / PixelCanvas.W));
        int sy = Mathf.Max(1, Mathf.FloorToInt(Screen.height * 0.86f / PixelCanvas.H));
        panelScale = Mathf.Min(sx, sy);
        float w = PixelCanvas.W * panelScale;
        float h = PixelCanvas.H * panelScale;
        panelRect = new Rect(Mathf.Round((Screen.width - w) * 0.5f), Mathf.Round((Screen.height - h) * 0.5f), w, h);
    }

    private void OnGUI()
    {
        if (!active || texture == null)
        {
            return;
        }

        // Мини-игра рисуется поверх HUD и меню.
        GUI.depth = -100;

        UiTheme.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.05f, 0.5f));

        if (Event.current.type == EventType.Repaint)
        {
            Rect r = panelRect;
            UiTheme.Fill(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(0f, 0f, 0f, 0.55f));
            UiTheme.Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), new Color(0.05f, 0.07f, 0.11f, 1f));
            GUI.DrawTexture(r, texture, ScaleMode.StretchToFill, true);
        }
    }

    // ---------- Курсор и ввод персонажа ----------

    private void CachePlayerInputs()
    {
        playerInputs = FindFirstObjectByType<StarterAssetsInputs>();
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (playerInputs == null)
        {
            return;
        }

        previousInputCursorLocked = playerInputs.cursorLocked;
        previousInputLook = playerInputs.cursorInputForLook;
        playerInputs.cursorLocked = false;
        playerInputs.cursorInputForLook = false;
        playerInputs.LookInput(Vector2.zero);
        playerInputs.MoveInput(Vector2.zero);
        playerInputs.JumpInput(false);
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        if (playerInputs != null)
        {
            playerInputs.cursorLocked = previousInputCursorLocked;
            playerInputs.cursorInputForLook = previousInputLook;
            playerInputs.LookInput(Vector2.zero);
            playerInputs.MoveInput(Vector2.zero);
            playerInputs.JumpInput(false);
            playerInputs = null;
        }

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }
}
