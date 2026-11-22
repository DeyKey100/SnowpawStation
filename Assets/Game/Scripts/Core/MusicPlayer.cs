using System;
using UnityEngine;

/// <summary>
/// Compatibility bridge for existing scene references. Actual playback now belongs
/// to AudioManager. Keep this script/GUID until its serialized references are migrated.
/// </summary>
[DisallowMultipleComponent]
public sealed class MusicPlayer : MonoBehaviour
{
    // These names and types intentionally remain unchanged: scenes already store them.
    [Header("Legacy defaults (AudioManager handles playback)")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.3f;
    [SerializeField] private bool musicEnabledByDefault = true;

    private AudioManager manager;

    public bool IsEnabled => manager != null && manager.IsMusicEnabled;
    public float Volume => manager != null ? manager.MusicVolume : defaultVolume;
    public float EffectiveVolume => manager != null ? manager.EffectiveMusicVolume : defaultVolume * 0.3f;
    public bool HasClip => manager != null && manager.IsReady ? manager.PlaylistCount > 0 : musicClip != null;

    public event Action<bool> EnabledChanged;
    public event Action<float> VolumeChanged;

    private void Awake()
    {
        manager = AudioManager.EnsureInstance();
        manager.AdoptLegacyPlayer(musicClip, defaultVolume, musicEnabledByDefault);
    }

    private void OnEnable()
    {
        if (manager == null) { return; }
        manager.MusicEnabledChanged += HandleEnabledChanged;
        manager.MusicVolumeChanged += HandleVolumeChanged;
    }

    private void OnDisable()
    {
        if (manager == null) { return; }
        manager.MusicEnabledChanged -= HandleEnabledChanged;
        manager.MusicVolumeChanged -= HandleVolumeChanged;
    }

    public void SetEnabled(bool enabled)
    {
        if (manager == null) { manager = AudioManager.EnsureInstance(); }
        manager.SetMusicEnabled(enabled);
    }

    public void SetVolume(float value)
    {
        if (manager == null) { manager = AudioManager.EnsureInstance(); }
        manager.SetMusicVolume(value);
    }

    private void HandleEnabledChanged(bool value) => EnabledChanged?.Invoke(value);
    private void HandleVolumeChanged(float value) => VolumeChanged?.Invoke(value);
}
