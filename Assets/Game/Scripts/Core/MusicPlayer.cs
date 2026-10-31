using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MusicPlayer : MonoBehaviour
{
    private const string EnabledKey = "snowpaw.music.enabled";
    private const string VolumeKey = "snowpaw.music.volume";

    // Фоновая музыка не должна быть громче половины громкости источника,
    // даже если игрок выкрутил ползунок на 100%.
    private const float MasterGain = 0.3f;

    [Header("Clip")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float defaultVolume = 0.3f;
    [SerializeField] private bool musicEnabledByDefault = true;

    private AudioSource source;
    private bool musicEnabled = true;
    private float volume = 0.2f;

    public bool IsEnabled => musicEnabled;
    public float Volume => volume;
    public float EffectiveVolume => volume * MasterGain;
    public bool HasClip => musicClip != null;

    public event Action<bool> EnabledChanged;
    public event Action<float> VolumeChanged;

    private void Awake()
    {
        musicEnabled = PlayerPrefs.GetInt(EnabledKey, musicEnabledByDefault ? 1 : 0) == 1;
        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, defaultVolume));

        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;

        Apply();
    }

    public void SetEnabled(bool enabled)
    {
        if (musicEnabled == enabled)
        {
            return;
        }

        musicEnabled = enabled;
        PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
        EnabledChanged?.Invoke(musicEnabled);
    }

    public void SetVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, volume))
        {
            return;
        }

        volume = clamped;
        PlayerPrefs.SetFloat(VolumeKey, volume);
        PlayerPrefs.Save();
        Apply();
        VolumeChanged?.Invoke(volume);
    }

    private void Apply()
    {
        if (source == null)
        {
            return;
        }

        source.clip = musicClip;
        source.volume = volume * MasterGain;

        if (musicEnabled && musicClip != null)
        {
            if (!source.isPlaying)
            {
                source.Play();
            }
        }
        else if (source.isPlaying)
        {
            source.Stop();
        }
    }
}
