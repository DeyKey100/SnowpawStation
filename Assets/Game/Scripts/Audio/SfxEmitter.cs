using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays one-shots through AudioManager's pool, not through a source on this object.
/// Button clicks and ProximityInteractable interactions are wired while enabled.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Snowpaw/Audio/SFX Emitter")]
public sealed class SfxEmitter : MonoBehaviour
{
    private const string DefaultFallbackPath = "WAV/Winter UI/ui_select_01.wav";

    [Header("Sound")]
    [SerializeField] private AudioClip clip;
    [Tooltip("A random non-null variation takes priority over Clip. Empty entries are ignored.")]
    [SerializeField] private AudioClip[] variations = Array.Empty<AudioClip>();
    [Tooltip("Path relative to Assets/Game/Audio/Sfx, or a clip name. Blank uses the runtime fallback, then the default UI select sound.")]
    [SerializeField] private string fallbackSfxPath = "";
    [SerializeField] private bool avoidRepeatingVariation = true;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField, Range(0.1f, 3f)] private float pitch = 1f;
    [SerializeField, Range(0f, 1f)] private float pitchVariation;
    [SerializeField, Min(0f)] private float cooldown = 0.075f;
    [SerializeField, Min(0.01f)] private float minDistance = 1f;
    [SerializeField, Min(0.01f)] private float maxDistance = 20f;
    [Tooltip("Button sounds are always 2D and ignore AudioListener.pause, regardless of this setting.")]
    [SerializeField] private bool ignoreListenerPause;

    [Header("Automatic events")]
    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool playOnInteract = true;
    [SerializeField] private bool playOnButtonClick = true;
    [Tooltip("Off by default: an already-playing pooled sound survives disabling or destroying the emitter.")]
    [SerializeField] private bool stopOnDisable;

    private readonly List<int> voiceHandles = new List<int>(4);
    private AudioManager manager;
    private Transform cachedTransform;
    private Button button;
    private ProximityInteractable interactionMarker;
    private AudioClip cachedFallbackClip;
    private string cachedFallbackPath;
    private string runtimeFallbackPath;
    private double nextPlayTime;
    private double lastObservedTime;
    private int lastVariationIndex = -1;
    private bool waitingForReady;
    private bool missingClipReported;

    public bool IsPlaying
    {
        get
        {
            if (manager == null) { return false; }
            for (int i = 0; i < voiceHandles.Count; i++)
            {
                if (manager.IsSfxPlaying(voiceHandles[i])) { return true; }
            }
            return false;
        }
    }

    private void Awake()
    {
        cachedTransform = transform;
        TryGetComponent(out button);
        TryGetComponent(out interactionMarker);
        manager = AudioManager.EnsureInstance();
    }

    private void OnEnable()
    {
        nextPlayTime = 0d;
        lastObservedTime = Time.unscaledTimeAsDouble;
        if (button != null) { button.onClick.AddListener(OnButtonClicked); }
        if (interactionMarker != null) { interactionMarker.Interacted += OnInteract; }
        if (playOnEnable) { Play(); }
    }

    private void OnDisable()
    {
        if (button != null) { button.onClick.RemoveListener(OnButtonClicked); }
        if (interactionMarker != null) { interactionMarker.Interacted -= OnInteract; }
        CancelDeferredPlay();
        if (stopOnDisable) { Stop(); }
    }

    /// <summary>
    /// Supplies a semantic fallback for runtime bootstrap without replacing an Inspector sound override.
    /// Does not change any serialized field.
    /// </summary>
    public void ConfigureFallback(string relativePathOrName)
    {
        if (clip != null || CountVariations() > 0 || !string.IsNullOrWhiteSpace(fallbackSfxPath)) { return; }
        runtimeFallbackPath = relativePathOrName;
        cachedFallbackPath = null;
        cachedFallbackClip = null;
        missingClipReported = false;
    }

    /// <summary>UnityEvent entry point. Requests before library warm-up are coalesced until Ready.</summary>
    public void Play()
    {
        if (this == null) { return; }
        AudioManager audio = GetManager();
        if (audio == null || !audio.isActiveAndEnabled) { return; }
        if (!audio.IsReady)
        {
            if (isActiveAndEnabled && !waitingForReady)
            {
                waitingForReady = true;
                audio.Ready += OnAudioReady;
            }
            return;
        }

        CancelDeferredPlay();
        double now = Time.unscaledTimeAsDouble;
        if (now < lastObservedTime) { nextPlayTime = 0d; }
        lastObservedTime = now;
        if (now < nextPlayTime) { return; }
        AudioClip selected = ResolveClip();
        if (selected == null) { return; }

        PruneVoiceHandles();
        bool isButton = button != null;
        float selectedPitch = Mathf.Clamp(pitch + UnityEngine.Random.Range(-pitchVariation, pitchVariation), 0.1f, 3f);
        Vector3 position = !isButton && interactionMarker != null
            ? interactionMarker.CenterWorldPoint
            : cachedTransform != null ? cachedTransform.position : Vector3.zero;
        int handle = audio.PlaySfx(selected, position, volume, isButton ? 0f : spatialBlend,
            selectedPitch, ignoreListenerPause || isButton, minDistance, Mathf.Max(minDistance, maxDistance));
        if (handle <= 0) { return; }
        voiceHandles.Add(handle);
        nextPlayTime = now + Mathf.Max(0f, cooldown);
    }

    /// <summary>Stops all of this emitter's still-owned voices, including a deferred request.</summary>
    public void Stop()
    {
        CancelDeferredPlay();
        if (manager != null)
        {
            for (int i = 0; i < voiceHandles.Count; i++) { manager.StopSfx(voiceHandles[i]); }
        }
        voiceHandles.Clear();
        nextPlayTime = 0d;
    }

    /// <summary>Called automatically by ProximityInteractable.Interacted, or explicitly by other interaction systems.</summary>
    public void OnInteract()
    {
        if (isActiveAndEnabled && playOnInteract) { Play(); }
    }

    private void OnButtonClicked()
    {
        // A button may close its panel during this same click. Already-ready audio must still play.
        if (playOnButtonClick) { Play(); }
    }

    private void OnAudioReady()
    {
        if (!waitingForReady) { return; }
        CancelDeferredPlay();
        if (isActiveAndEnabled) { Play(); }
    }

    private void CancelDeferredPlay()
    {
        if (waitingForReady && manager != null) { manager.Ready -= OnAudioReady; }
        waitingForReady = false;
    }

    private AudioManager GetManager()
    {
        if (manager == null)
        {
            waitingForReady = false;
            voiceHandles.Clear();
            manager = AudioManager.EnsureInstance();
        }
        return manager;
    }

    private void PruneVoiceHandles()
    {
        for (int i = voiceHandles.Count - 1; i >= 0; i--)
        {
            if (!manager.IsSfxPlaying(voiceHandles[i])) { voiceHandles.RemoveAt(i); }
        }
    }

    private int CountVariations()
    {
        int count = 0;
        if (variations != null)
        {
            for (int i = 0; i < variations.Length; i++)
            {
                if (variations[i] != null) { count++; }
            }
        }
        return count;
    }

    private AudioClip ResolveClip()
    {
        int count = CountVariations();
        if (count > 0)
        {
            bool skipLast = avoidRepeatingVariation && count > 1 && lastVariationIndex >= 0 &&
                lastVariationIndex < variations.Length && variations[lastVariationIndex] != null;
            int selected = UnityEngine.Random.Range(0, skipLast ? count - 1 : count);
            for (int i = 0; i < variations.Length; i++)
            {
                if (variations[i] == null || (skipLast && i == lastVariationIndex)) { continue; }
                if (selected-- == 0)
                {
                    lastVariationIndex = i;
                    return variations[i];
                }
            }
        }
        if (clip != null) { return clip; }

        string path = !string.IsNullOrWhiteSpace(fallbackSfxPath) ? fallbackSfxPath :
            !string.IsNullOrWhiteSpace(runtimeFallbackPath) ? runtimeFallbackPath : DefaultFallbackPath;
        if (!string.Equals(cachedFallbackPath, path, StringComparison.Ordinal))
        {
            cachedFallbackPath = path;
            cachedFallbackClip = null;
            missingClipReported = false;
        }
        if (cachedFallbackClip == null && AudioLibrary.IsReady) { cachedFallbackClip = AudioLibrary.FindSfx(path); }
        if (cachedFallbackClip == null && AudioLibrary.IsReady && !missingClipReported)
        {
            missingClipReported = true;
            Debug.LogWarning("[Audio] SfxEmitter could not find SFX: " + path, this);
        }
        return cachedFallbackClip;
    }
}
