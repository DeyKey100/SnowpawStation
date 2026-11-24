using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional uGUI bridge for independent music/SFX volume sliders and a music-enabled toggle.
/// Slider ranges are mapped to 0..1 without changing Inspector min/max settings.
/// IMGUI PauseMenu does not use this component.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Snowpaw/Audio/Audio Settings Binder")]
public sealed class AudioSettingsBinder : MonoBehaviour
{
    [Header("Optional uGUI controls")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle musicEnabledToggle;

    [Header("Optional settings feedback")]
    [SerializeField] private bool playSettingsFeedback = true;
    [SerializeField] private AudioClip settingsFeedbackClip;
    [Tooltip("Path relative to Assets/Game/Audio/Sfx, or a clip name. Used when no feedback clip is assigned.")]
    [SerializeField] private string settingsFeedbackPath = "Button.wav";
    [SerializeField, Range(0f, 1f)] private float settingsFeedbackVolume = 0.7f;
    [SerializeField, Min(0f)] private float feedbackCooldown = 0.12f;

    private AudioManager manager;
    private Slider boundMusicSlider;
    private Slider boundSfxSlider;
    private Toggle boundMusicToggle;
    private AudioClip cachedFeedbackClip;
    private string cachedFeedbackPath;
    private double nextFeedbackTime;
    private double lastObservedTime;

    private void OnEnable()
    {
        Unbind();
        nextFeedbackTime = 0d;
        lastObservedTime = Time.unscaledTimeAsDouble;
        manager = AudioManager.EnsureInstance();
        if (manager == null) { return; }

        boundMusicSlider = musicVolumeSlider;
        boundSfxSlider = sfxVolumeSlider;
        boundMusicToggle = musicEnabledToggle;
        if (boundMusicSlider != null && boundMusicSlider == boundSfxSlider)
        {
            boundSfxSlider = null;
            Debug.LogWarning("[Audio] AudioSettingsBinder needs separate music and SFX sliders; the duplicate SFX binding was skipped.", this);
        }

        if (boundMusicSlider != null) { boundMusicSlider.onValueChanged.AddListener(OnMusicSliderChanged); }
        if (boundSfxSlider != null) { boundSfxSlider.onValueChanged.AddListener(OnSfxSliderChanged); }
        if (boundMusicToggle != null) { boundMusicToggle.onValueChanged.AddListener(SetMusicEnabled); }
        manager.MusicVolumeChanged += OnMusicVolumeChanged;
        manager.SfxVolumeChanged += OnSfxVolumeChanged;
        manager.MusicEnabledChanged += OnMusicEnabledChanged;
        manager.Ready += Synchronize;
        Synchronize();
    }

    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();

    private void Unbind()
    {
        if (boundMusicSlider != null) { boundMusicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged); }
        if (boundSfxSlider != null) { boundSfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged); }
        if (boundMusicToggle != null) { boundMusicToggle.onValueChanged.RemoveListener(SetMusicEnabled); }
        if (manager != null)
        {
            manager.MusicVolumeChanged -= OnMusicVolumeChanged;
            manager.SfxVolumeChanged -= OnSfxVolumeChanged;
            manager.MusicEnabledChanged -= OnMusicEnabledChanged;
            manager.Ready -= Synchronize;
        }
        boundMusicSlider = null;
        boundSfxSlider = null;
        boundMusicToggle = null;
        manager = null;
    }

    /// <summary>Refreshes the bound controls without firing UI callbacks or settings feedback.</summary>
    public void Synchronize()
    {
        if (this == null || !isActiveAndEnabled) { return; }
        AudioManager audio = GetManager();
        if (audio == null) { return; }
        OnMusicVolumeChanged(audio.MusicVolume);
        OnSfxVolumeChanged(audio.SfxVolume);
        OnMusicEnabledChanged(audio.IsMusicEnabled);
    }

    private void OnMusicSliderChanged(float value)
    {
        if (boundMusicSlider != null)
        {
            SetMusicVolume(Mathf.InverseLerp(boundMusicSlider.minValue, boundMusicSlider.maxValue, value));
        }
    }

    private void OnSfxSliderChanged(float value)
    {
        if (boundSfxSlider != null)
        {
            SetSfxVolume(Mathf.InverseLerp(boundSfxSlider.minValue, boundSfxSlider.maxValue, value));
        }
    }

    private void OnMusicVolumeChanged(float value) => SetSliderWithoutNotify(boundMusicSlider, value);
    private void OnSfxVolumeChanged(float value) => SetSliderWithoutNotify(boundSfxSlider, value);

    private void OnMusicEnabledChanged(bool enabled)
    {
        if (boundMusicToggle != null) { boundMusicToggle.SetIsOnWithoutNotify(enabled); }
    }

    private static void SetSliderWithoutNotify(Slider slider, float normalizedVolume)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, normalizedVolume));
        }
    }

    /// <summary>UnityEvent entry point. The argument is a normalized volume (0..1).</summary>
    public void SetMusicVolume(float normalizedVolume)
    {
        AudioManager audio = GetManager();
        if (audio == null) { return; }
        float value = ClampVolume(normalizedVolume);
        bool changed = !Mathf.Approximately(value, audio.MusicVolume);
        audio.SetMusicVolume(value);
        if (changed && playSettingsFeedback) { PlaySettingsSfx(); }
    }

    /// <summary>UnityEvent entry point. Feedback uses the newly selected SFX volume, including silence at zero.</summary>
    public void SetSfxVolume(float normalizedVolume)
    {
        AudioManager audio = GetManager();
        if (audio == null) { return; }
        float value = ClampVolume(normalizedVolume);
        bool changed = !Mathf.Approximately(value, audio.SfxVolume);
        audio.SetSfxVolume(value);
        if (changed && playSettingsFeedback) { PlaySettingsSfx(); }
    }

    public void SetMusicEnabled(bool enabled)
    {
        AudioManager audio = GetManager();
        if (audio == null) { return; }
        bool changed = enabled != audio.IsMusicEnabled;
        audio.SetMusicEnabled(enabled);
        if (changed && playSettingsFeedback) { PlaySettingsSfx(); }
    }

    public void ToggleMusicEnabled()
    {
        AudioManager audio = GetManager();
        if (audio != null) { SetMusicEnabled(!audio.IsMusicEnabled); }
    }

    /// <summary>Optional button/UnityEvent feedback: pooled, 2D, unscaled cooldown, audible during listener pause.</summary>
    public void PlaySettingsSfx()
    {
        AudioManager audio = GetManager();
        double now = Time.unscaledTimeAsDouble;
        if (now < lastObservedTime) { nextFeedbackTime = 0d; }
        lastObservedTime = now;
        if (audio == null || !audio.IsReady || now < nextFeedbackTime) { return; }

        AudioClip selected = settingsFeedbackClip;
        if (selected == null)
        {
            if (!string.Equals(cachedFeedbackPath, settingsFeedbackPath, StringComparison.Ordinal))
            {
                cachedFeedbackPath = settingsFeedbackPath;
                cachedFeedbackClip = null;
            }
            if (cachedFeedbackClip == null && AudioLibrary.IsReady)
            {
                cachedFeedbackClip = AudioLibrary.FindSfx(settingsFeedbackPath);
            }
            selected = cachedFeedbackClip;
        }
        if (selected == null) { return; }
        int handle = audio.PlaySfx(selected, Vector3.zero, settingsFeedbackVolume, 0f, 1f, true);
        if (handle > 0) { nextFeedbackTime = now + Mathf.Max(0f, feedbackCooldown); }
    }

    private AudioManager GetManager()
    {
        if (manager == null) { manager = AudioManager.EnsureInstance(); }
        return manager;
    }

    private static float ClampVolume(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
}
