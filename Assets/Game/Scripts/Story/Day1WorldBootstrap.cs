using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Рантайм-объекты дня 1: записка под кружкой (интерьер) и дровник (двор StationMain).
/// Сцены не правятся: объекты создаются при загрузке сцены и живут в ней.
/// </summary>
[DisallowMultipleComponent]
public sealed class Day1WorldBootstrap : MonoBehaviour
{
    private GameObject note;
    private GameObject shed;
    private DayLoopManager dayManager;

    private void Start()
    {
        // Координатор сохраняется вместе с bootstrap, сами сугробы живут в StationMain.
        if (!TryGetComponent<SnowPileSpawner>(out _))
        {
            gameObject.AddComponent<SnowPileSpawner>();
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (dayManager != null)
        {
            dayManager.DayStarted -= OnDayStarted;
        }
    }

    private void BindDayManager()
    {
        DayLoopManager current = DayLoopManager.Instance;
        if (dayManager == current)
        {
            return;
        }
        if (dayManager != null)
        {
            dayManager.DayStarted -= OnDayStarted;
        }
        dayManager = current;
        if (dayManager != null)
        {
            dayManager.DayStarted += OnDayStarted;
        }
    }

    private void OnDayStarted(int day)
    {
        if (day < 3)
        {
            return;
        }
        // Дровник нужен и при переходе Д2 -> Д3 во дворе без перезагрузки сцены.
        Scene station = SceneManager.GetSceneByName("StationMain");
        if (station.IsValid() && station.isLoaded)
        {
            SpawnShed(station);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        BindDayManager();
        string name = scene.name.ToLowerInvariant();
        bool interior = name.Contains("interior");

        // Записка лежит только в доме: на улице её нет, пока игрок не зайдёт внутрь.
        if (interior)
        {
            SpawnNote(scene);
        }

        if (scene.name == "StationMain" && dayManager != null && dayManager.CurrentDay >= 3)
        {
            SpawnShed(scene);
        }
    }

    private void SpawnNote(Scene scene)
    {
        // Записку читают один раз за прохождение: после перезагрузки сцены (магазин → улица)
        // она не должна появляться снова.
        if (GameState.IsDayEventDone(KeeperNoteInteractable.ReadEventId))
        {
            return;
        }

        if (FindInScene(scene, "keepernote") != null)
        {
            return;
        }

        // Записка лежит на прилавке с кассой внутри дома: на улице её нет,
        // пока игрок не зайдёт внутрь. Стойка тоже интерактивна, поэтому записке выдан
        // приоритет: иначе подсказку кассы получал бы тот, кто ближе к стойке, а не к записке.
        // Точку берём у самой модели магазина: у неё прилавок стоит не там, где стоял
        // куб старого интерьера, и жёсткая координата оставляла записку в воздухе.
        const float noteLift = 0.03f / 2f;
        if (!InteriorShopAnchors.TryGetCashDeskSurface(noteLift, out Vector3 pos))
        {
            Debug.LogWarning("[Snowpaw Station] Прилавок магазина не найден — записка не создана.");
            return;
        }

        note = GameObject.CreatePrimitive(PrimitiveType.Cube);
        note.name = "KeeperNote";
        note.transform.position = pos;
        note.transform.localScale = new Vector3(0.34f, 0.03f, 0.46f);
        note.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        ApplyInteractableLayer(note);

        // Записка лежит на прилавке — чтобы не провалиться сквозь него, убираем у куба
        // столкновения: маркеру хватает габаритов рендерера, а игроку мешать нечему.
        Collider noteCollider = note.GetComponent<Collider>();
        if (noteCollider != null)
        {
            Destroy(noteCollider);
        }

        Renderer renderer = note.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(0.95f, 0.9f, 0.72f);
            renderer.sharedMaterial = mat;
        }

        note.AddComponent<KeeperNoteInteractable>();
        // Приоритет 4 = «важнее всего на столе»: записка выигрывает у стойки и полки.
        ProximityInteractable.EnsureOn(note, 2.2f, 4);
        Debug.Log($"[Snowpaw Station] Записка смотрителя: {pos}.");
    }

    private void SpawnShed(Scene scene)
    {
        if (shed != null || FindInScene(scene, "woodshed") != null)
        {
            return;
        }

        Transform woodPile = FindInScene(scene, "wood");
        shed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SceneManager.MoveGameObjectToScene(shed, scene);
        shed.name = "WoodShed";
        Vector3 pos = woodPile != null
            ? woodPile.position + woodPile.right * 2.5f
            : new Vector3(8f, 0.6f, 3f);
        shed.transform.position = new Vector3(pos.x, 0.6f, pos.z);
        shed.transform.localScale = new Vector3(1.4f, 1.2f, 0.8f);
        ApplyInteractableLayer(shed);

        Renderer renderer = shed.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.28f, 0.19f, 0.13f);
            renderer.sharedMaterial = mat;
        }

        shed.AddComponent<WoodShedInteractable>();
        ProximityInteractable.EnsureOn(shed, 2.4f);
        Debug.Log("[Snowpaw Station] Дровник создан у колоды (дровник до 6 поленьев).");
    }

    private static Transform FindInScene(Scene scene, string namePart)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindRecursive(roots[i].transform, namePart);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindRecursive(Transform t, string namePart)
    {
        if (t.name.ToLowerInvariant().Contains(namePart))
        {
            return t;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindRecursive(t.GetChild(i), namePart);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void ApplyInteractableLayer(GameObject go)
    {
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0)
        {
            go.layer = layer;
        }
    }
}

/// <summary>
/// Мини-игра колки: бегунок бегает, зелёная зона в центре. ЛКМ/Пробел в зоне = полено.
/// Промах мягко возвращает бегунок в начало и подсказывает направление. На время игры вешается InteractionLock "chopping".
/// </summary>
[DisallowMultipleComponent]
public sealed class WoodChoppingMinigame : MonoBehaviour
{
    /// <summary>Скорость бегунка на первом круге (доля полоски в секунду).</summary>
    private const float BaseSpeed = 1.4f;

    /// <summary>Скорость растёт за промах, но не бесконечно: иначе шаг за кадр
    /// становится больше полоски и она визуально «замирает» между крайними точками.</summary>
    private const float SpeedPerMiss = 0.05f;
    private const float MaxSpeed = 1.9f;

    /// <summary>Предел шага за кадр: при просадке fps бегунок не должен перескакивать полосу.</summary>
    private const float MaxStepPerFrame = 0.12f;

    private const float SweetSpotMin = 0.4f;
    private const float SweetSpotMax = 0.6f;

    private float marker;       // 0..1
    private float direction = 1f;
    private float speed = BaseSpeed;
    private int misses;
    private float feedbackTimer;
    private float graceTimer;
    private float resultTimer;
    private string feedbackText = "";
    private bool active;
    private bool success;
    private Transform cachedTransform;
    private AudioManager audioManager;

    private static AudioClip chopClip;
    private static AudioClip missClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAudio()
    {
        if (chopClip != null) { Object.Destroy(chopClip); }
        if (missClip != null) { Object.Destroy(missClip); }
        chopClip = null;
        missClip = null;
    }

    public bool IsActive => active;

    private void Awake()
    {
        cachedTransform = transform;
        audioManager = AudioManager.EnsureInstance();
    }

    /// <summary>Запуск. onSuccess — полено добыто.</summary>
    public void Begin(System.Action onSuccess)
    {
        if (active)
        {
            return;
        }

        active = true;
        success = false;
        marker = 0f;
        direction = 1f;
        speed = BaseSpeed;
        misses = 0;
        graceTimer = 0f;
        resultTimer = 0f;
        feedbackTimer = 0f;
        feedbackText = "";
        InteractionLock.Add("chopping");

        // Пока идёт колка — персонаж и камера стоят: игрок рубит, а не ходит вокруг колоды.
        PlayerControlLock.Add("chopping");
        StartCoroutine(Run(onSuccess));
    }

    private System.Collections.IEnumerator Run(System.Action onSuccess)
    {
        while (active)
        {
            yield return null;
        }

        InteractionLock.Remove("chopping");
        PlayerControlLock.Remove("chopping");
        if (success)
        {
            onSuccess?.Invoke();
        }

        while (resultTimer > 0f)
        {
            resultTimer = Mathf.Max(0f, resultTimer - Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>Колоду выгрузили/выключили посреди колки — иначе игрок остался бы замороженным.</summary>
    private void OnDisable()
    {
        active = false;
        InteractionLock.Remove("chopping");
        PlayerControlLock.Remove("chopping");
    }

    private void PlayCue(bool hit)
    {
        if (audioManager == null) { audioManager = AudioManager.EnsureInstance(); }
        if (audioManager == null) { return; }
        AudioClip clip = hit ? GetChopClip() : GetMissClip();
        if (clip == null) { return; }
        float pitch = hit ? UnityEngine.Random.Range(0.96f, 1.04f) : UnityEngine.Random.Range(0.92f, 1.02f);
        audioManager.PlaySfx(clip, cachedTransform != null ? cachedTransform.position : transform.position,
            hit ? 0.72f : 0.38f, 1f, pitch, false, 1.2f, 10f);
    }

    private static AudioClip GetChopClip()
    {
        if (chopClip == null) { chopClip = CreateCue("WoodChop_Cozy", 0.24f, true); }
        return chopClip;
    }

    private static AudioClip GetMissClip()
    {
        if (missClip == null) { missClip = CreateCue("WoodChop_Swish", 0.18f, false); }
        return missClip;
    }

    private static AudioClip CreateCue(string name, float seconds, bool impact)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.CeilToInt(sampleRate * seconds);
        float[] samples = new float[sampleCount];
        System.Random random = new System.Random(name.GetHashCode());
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float tone = Mathf.Sin(2f * Mathf.PI * (impact ? 155f : 500f) * t);
            float body = impact ? tone * Mathf.Exp(-12f * t) : 0f;
            samples[i] = Mathf.Clamp((noise * (impact ? 0.16f : 0.07f) + body * (impact ? 0.30f : 0.04f)) * envelope, -0.8f, 0.8f);
        }
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        clip.hideFlags = HideFlags.HideAndDontSave;
        return clip;
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        feedbackTimer = Mathf.Max(0f, feedbackTimer - Time.deltaTime);
        graceTimer = Mathf.Max(0f, graceTimer - Time.deltaTime);

        // Скорость держится постоянной, пока игрок не промахнётся: иначе полоска
        // разгоняется сама по себе и через полминуты перестаёт читаться.
        float step = Mathf.Clamp(speed * Time.deltaTime, 0f, MaxStepPerFrame);
        marker += direction * step;

        if (marker >= 1f)
        {
            marker = 1f;
            direction = -1f;
        }
        else if (marker <= 0f)
        {
            marker = 0f;
            direction = 1f;
        }

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
            (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
#else
        pressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
#endif

        if (pressed)
        {
            // Чуть более широкая «уютная» зона после промаха не наказывает за один неловкий кадр.
            float forgiveness = graceTimer > 0f ? 0.035f : 0f;
            bool hit = marker >= SweetSpotMin - forgiveness && marker <= SweetSpotMax + forgiveness;
            if (hit)
            {
                success = true;
                feedbackText = marker >= 0.45f && marker <= 0.55f ? "Точно в сучок!" : "Хороший замах!";
                feedbackTimer = 0.8f;
                resultTimer = 0.35f;
                PlayCue(true);
                active = false;
            }
            else
            {
                bool wasLeft = marker < SweetSpotMin;
                misses++;
                speed = Mathf.Min(BaseSpeed + SpeedPerMiss * misses, MaxSpeed);
                marker = 0f;
                direction = 1f;
                graceTimer = 0.18f;
                feedbackText = wasLeft ? "Чуть правее…" : "Чуть левее…";
                feedbackTimer = 0.7f;
                PlayCue(false);
            }
        }

        // Выход из мини-игры — Q (Tab занят записной книжкой, Space теперь просто ничего не делает).
        bool quit;
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        quit = kb != null && kb.qKey.wasPressedThisFrame;
#else
        quit = Input.GetKeyDown(KeyCode.Q);
#endif
        if (quit)
        {
            active = false;
        }
    }

    private void OnGUI()
    {
        if (!active && resultTimer <= 0f)
        {
            return;
        }

        float scale = UiTheme.Scale;
        float w = Mathf.Min(Screen.width - 32f, 460f * scale);
        float h = 30f * scale;
        Rect panel = new Rect((Screen.width - w - 28f) * 0.5f, Screen.height * 0.5f - 56f * scale, w + 28f, 128f * scale);
        UiTheme.Frame(panel, UiTheme.Night, UiTheme.Frost);
        GUI.Label(new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, 24f * scale),
            "КОЛКА ДРОВ", UiTheme.Label(18f, UiTheme.Cream, FontStyle.Bold, TextAnchor.MiddleCenter));

        Rect bar = new Rect(panel.x + 14f, panel.y + 42f * scale, w, h);
        UiTheme.Bar(bar, marker, UiTheme.Ember);
        Rect sweet = new Rect(bar.x + bar.width * SweetSpotMin, bar.y, bar.width * (SweetSpotMax - SweetSpotMin), h);
        UiTheme.Fill(sweet, new Color(0.32f, 0.75f, 0.42f, 0.55f));
        Rect knob = new Rect(bar.x + bar.width * marker - 4f * scale, bar.y - 5f * scale, 8f * scale, h + 10f * scale);
        UiTheme.Fill(knob, UiTheme.Cream);

        string hint = "ЛКМ или ПРОБЕЛ — замах   Q — выйти";
        GUI.Label(new Rect(panel.x + 14f, panel.y + 78f * scale, w, 24f * scale), hint,
            UiTheme.Label(15f, UiTheme.Muted, alignment: TextAnchor.MiddleCenter));
        if (feedbackTimer > 0f && !string.IsNullOrEmpty(feedbackText))
        {
            GUI.Label(new Rect(panel.x + 14f, panel.y + 101f * scale, w, 22f * scale), feedbackText,
                UiTheme.Label(16f, success ? UiTheme.Ember : UiTheme.Ice, FontStyle.Bold, TextAnchor.MiddleCenter));
        }
    }
}
