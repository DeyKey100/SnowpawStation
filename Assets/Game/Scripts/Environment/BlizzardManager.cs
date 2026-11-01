using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Метель-снегопад в PSX-стиле: процедурная система частиц, созданная кодом
/// (без ассетов и правок сцены), следует за активной камерой.
/// Включается/выключается из настроек меню паузы; состояние сохраняется
/// в PlayerPrefs между запусками (как у MusicPlayer).
/// В закрытых сценах (интерьер станции) снег автоматически скрывается.
/// </summary>
[DisallowMultipleComponent]
public sealed class BlizzardManager : MonoBehaviour
{
    private const string EnabledKey = "snowpaw.blizzard.enabled";
    private const string IntensityKey = "snowpaw.blizzard.intensity";
    private const float QualitySampleInterval = 0.5f;

    public static BlizzardManager Instance { get; private set; }

    [Header("Поведение")]
    [SerializeField] private bool enabledByDefault = true;
    [SerializeField] private List<string> indoorScenes = new List<string> { "GasStationInterior" };

    [Header("Сила метели")]
    [SerializeField, Range(0f, 1f)] private float defaultIntensity = 0.7f;

    [Header("Производительность")]
    [Tooltip("Автоматически уменьшает число частиц при просадках FPS и восстанавливает их с гистерезисом.")]
    [SerializeField] private bool adaptiveQuality = true;
    [SerializeField, Range(0.2f, 1f)] private float minQualityScale = 0.35f;
    [SerializeField, Range(15f, 60f)] private float minAcceptableFps = 45f;
    [SerializeField, Range(20f, 90f)] private float recoverFps = 55f;

    [Header("Снег")]
    [SerializeField, Range(100f, 2000f)] private float emissionRate = 750f;
    [SerializeField, Range(1000, 8000)] private int maxParticles = 5000;
    [Tooltip("Ширина поля снега вокруг камеры, м. Меньше — снег плотнее рядом с игроком.")]
    [SerializeField, Range(20f, 140f)] private float fieldSize = 34f;
    [Tooltip("Высота рождения снега над камерой, м.")]
    [SerializeField, Range(4f, 40f)] private float spawnHeight = 7f;
    [SerializeField, Range(0.5f, 8f)] private float fallSpeed = 2.8f;
    [SerializeField, Range(0f, 16f)] private float windStrength = 7f;
    [SerializeField, Range(0f, 4f)] private float gustStrength = 1.6f;

    private ParticleSystem snowParticles;
    private GameObject snowObject;
    private ParticleSystem.VelocityOverLifetimeModule velocityModule;
    private ParticleSystem.NoiseModule noiseModule;
    private Texture2D flakeTexture;
    private Material flakeMaterial;
    private Transform followTarget;
    private Vector3 windDirection = new Vector3(0.85f, 0f, 0.52f);
    private bool snowEnabled = true;
    private bool indoorScene;
    private float intensity = 0.7f;
    private float qualityScale = 1f;
    private float windScale = 1f;
    private float fpsWindowTime;
    private int fpsWindowFrames;

    public bool IsEnabled => snowEnabled;
    public bool IsSnowing => snowObject != null && snowObject.activeSelf;

    /// <summary>Сила метели 0..1 (0% — снега нет, 100% — полная буря).</summary>
    public float Intensity => intensity;

    public event Action<bool> EnabledChanged;
    public event Action<float> IntensityChanged;

    /// <summary>
    /// Самосоздание при загрузке игры: объект не нужно размещать в сцене вручную.
    /// Если BlizzardManager уже добавлен на сцене — используется он.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null)
        {
            new GameObject("BlizzardManager").AddComponent<BlizzardManager>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        snowEnabled = PlayerPrefs.GetInt(EnabledKey, enabledByDefault ? 1 : 0) == 1;
        intensity = Mathf.Clamp01(PlayerPrefs.GetFloat(IntensityKey, defaultIntensity));
        windDirection = windDirection.normalized;

        BuildSnow();

        SceneManager.sceneLoaded += HandleSceneLoaded;
        indoorScene = IsIndoorScene(SceneManager.GetActiveScene().name);
        ApplyIntensity();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (flakeMaterial != null)
        {
            Destroy(flakeMaterial);
            flakeMaterial = null;
        }

        if (flakeTexture != null)
        {
            Destroy(flakeTexture);
            flakeTexture = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetEnabled(bool value)
    {
        if (snowEnabled == value)
        {
            return;
        }

        snowEnabled = value;
        PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
        EnabledChanged?.Invoke(snowEnabled);
        Debug.Log($"[Snowpaw Station] Метель: {(snowEnabled ? "вкл" : "выкл")}", this);
    }

    /// <summary>
    /// Сила метели в долях: 0 — снега нет, 1 — полная буря.
    /// Масштабирует плотность, количество частиц и силу ветра.
    /// </summary>
    public void SetIntensity(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, intensity))
        {
            return;
        }

        intensity = clamped;
        PlayerPrefs.SetFloat(IntensityKey, intensity);
        PlayerPrefs.Save();
        ApplyIntensity();
        IntensityChanged?.Invoke(intensity);
        Debug.Log($"[Snowpaw Station] Сила метели: {Mathf.RoundToInt(intensity * 100f)}%", this);
    }

    private void LateUpdate()
    {
        if (snowParticles == null || !snowObject.activeSelf)
        {
            return;
        }

        RefreshFollowTarget();
        if (followTarget != null)
        {
            transform.position = followTarget.position;
        }

        UpdateAdaptiveQuality();

        // Плавные порывы: сила и направление ветра дрейфуют по Perlin-шуму.
        float gust = 0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 0.05f, 0.37f);
        float swayDegrees = (Mathf.PerlinNoise(Time.time * 0.03f, 9.1f) - 0.5f) * 50f;
        Vector3 wind = Quaternion.Euler(0f, swayDegrees, 0f) * windDirection * (windStrength * windScale * gust);

        velocityModule.x = wind.x;
        velocityModule.y = -fallSpeed;
        velocityModule.z = wind.z;
    }

    /// <summary>
    /// Адаптивное качество: раз в полсекунды меряем средний FPS и с гистерезисом
    /// ужимаем/восстанавливаем бюджет частиц, чтобы метель не роняла кадры.
    /// </summary>
    private void UpdateAdaptiveQuality()
    {
        if (!adaptiveQuality)
        {
            return;
        }

        // На паузе и под экраном загрузки FPS нерепрезентативен — не меряем.
        if (PauseMenu.IsPaused || SceneLoadingScreen.IsLoading)
        {
            fpsWindowTime = 0f;
            fpsWindowFrames = 0;
            return;
        }

        fpsWindowTime += Time.unscaledDeltaTime;
        fpsWindowFrames++;

        if (fpsWindowTime < QualitySampleInterval)
        {
            return;
        }

        float averageFps = fpsWindowFrames / Mathf.Max(0.0001f, fpsWindowTime);
        fpsWindowTime = 0f;
        fpsWindowFrames = 0;

        float previousScale = qualityScale;
        if (averageFps < minAcceptableFps && qualityScale > minQualityScale + 0.001f)
        {
            qualityScale = Mathf.Max(minQualityScale, qualityScale - 0.15f);
        }
        else if (averageFps > recoverFps && qualityScale < 1f - 0.001f)
        {
            qualityScale = Mathf.Min(1f, qualityScale + 0.05f);
        }

        if (!Mathf.Approximately(previousScale, qualityScale))
        {
            ApplyParticleBudget();
        }
    }

    private void RefreshFollowTarget()
    {
        if (followTarget != null)
        {
            return;
        }

        Camera viewCamera = Camera.main;
        followTarget = viewCamera != null ? viewCamera.transform : null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        followTarget = null;
        indoorScene = IsIndoorScene(scene.name);
        Apply();
    }

    private bool IsIndoorScene(string sceneName)
    {
        return indoorScenes != null && indoorScenes.Contains(sceneName);
    }

    private void Apply()
    {
        if (snowObject == null || snowParticles == null)
        {
            return;
        }

        bool shouldSnow = snowEnabled && !indoorScene && intensity > 0.001f;
        snowObject.SetActive(shouldSnow);

        if (shouldSnow && !snowParticles.isEmitting)
        {
            snowParticles.Play();
        }
        else if (!shouldSnow)
        {
            snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>Применяет силу метели: ветер, турбулентность и бюджет частиц.</summary>
    private void ApplyIntensity()
    {
        if (snowParticles == null)
        {
            return;
        }

        // Слабый снег — слабый ветер; на максимуме ветер чуть сильнее базового.
        windScale = Mathf.Lerp(0.25f, 1.3f, intensity);
        noiseModule.strength = gustStrength * Mathf.Lerp(0.4f, 1f, intensity);

        ApplyParticleBudget();
        Apply();
    }

    /// <summary>
    /// Бюджет частиц = базовые значения × сила × адаптивный масштаб качества.
    /// Один ParticleSystem = один draw call; частицы масштабируются только числом.
    /// </summary>
    private void ApplyParticleBudget()
    {
        if (snowParticles == null)
        {
            return;
        }

        float effective = intensity * qualityScale;

        ParticleSystem.EmissionModule emission = snowParticles.emission;
        emission.rateOverTime = emissionRate * effective;

        ParticleSystem.MainModule main = snowParticles.main;
        main.maxParticles = Mathf.Max(128, Mathf.CeilToInt(maxParticles * effective));
    }

    private void BuildSnow()
    {
        flakeTexture = CreateFlakeTexture();

        Shader flakeShader = FindFlakeShader();
        if (flakeShader == null)
        {
            Debug.LogError("BlizzardManager: не найден подходящий шейдер для снега, метель отключена.", this);
            return;
        }

        flakeMaterial = new Material(flakeShader)
        {
            name = "BlizzardFlake (runtime)",
            mainTexture = flakeTexture,
            renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
        };

        snowObject = new GameObject("BlizzardSnow");
        snowObject.transform.SetParent(transform, false);

        snowParticles = snowObject.AddComponent<ParticleSystem>();

        // AddComponent сам запускает систему (playOnAwake по умолчанию true),
        // а duration у играющей системы менять нельзя — сначала гасим её.
        snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = snowParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 60f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            spawnHeight / Mathf.Max(0.1f, fallSpeed),
            (spawnHeight + 8f) / Mathf.Max(0.1f, fallSpeed));
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.8f, 0.87f, 0.96f, 0.9f),
            Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = snowParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = snowParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, spawnHeight, 0f);
        shape.scale = new Vector3(fieldSize, 3f, fieldSize);

        velocityModule = snowParticles.velocityOverLifetime;
        velocityModule.enabled = true;
        velocityModule.space = ParticleSystemSimulationSpace.World;
        velocityModule.x = windDirection.x * windStrength;
        velocityModule.y = -fallSpeed;
        velocityModule.z = windDirection.z * windStrength;

        noiseModule = snowParticles.noise;
        noiseModule.enabled = gustStrength > 0f;
        noiseModule.strength = gustStrength;
        noiseModule.frequency = 0.18f;
        noiseModule.scrollSpeed = 0.6f;
        noiseModule.damping = true;

        ParticleSystemRenderer snowRenderer = snowObject.GetComponent<ParticleSystemRenderer>();
        snowRenderer.material = flakeMaterial;
        snowRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        snowRenderer.sortMode = ParticleSystemSortMode.None;
        snowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        snowRenderer.receiveShadows = false;
    }

    private static Shader FindFlakeShader()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            return shader;
        }

        return Shader.Find("Legacy Shaders/Particles/Alpha Blended");
    }

    /// <summary>
    /// «Квадратная» снежинка-крест 8×8 в PSX-стиле: point-фильтрация, без мипмапов.
    /// </summary>
    private static Texture2D CreateFlakeTexture()
    {
        const int size = 8;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "BlizzardFlake (runtime)",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool horizontalBar = y >= 3 && y <= 4 && x >= 1 && x <= 6;
                bool verticalBar = x >= 3 && x <= 4 && y >= 1 && y <= 6;
                pixels[y * size + x] = (horizontalBar || verticalBar) ? solid : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
