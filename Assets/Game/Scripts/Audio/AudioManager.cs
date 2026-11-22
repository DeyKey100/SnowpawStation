using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Persistent music playlist and a bounded, reusable SFX voice pool.
/// User preferences and temporary story/trigger suppression are independent.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
[AddComponentMenu("Snowpaw/Audio/Audio Manager")]
public sealed class AudioManager : MonoBehaviour
{
    private const string MusicEnabledKey = "snowpaw.music.enabled";
    private const string MusicVolumeKey = "snowpaw.music.volume";
    private const string SfxVolumeKey = "snowpaw.sfx.volume";
    private static readonly Predicate<UnityEngine.Object> MissingOwner = owner => owner == null;
    private static int playSession;

    [Header("Music playlist")]
    [Tooltip("Folder inside Assets/Game/Audio/Music (subfolders included). Packaged automatically for standalone builds.")]
    [SerializeField] private string playlistFolder = AudioLibrary.MusicFolder;
    [Tooltip("Optional explicit playlist. Takes precedence over the folder; also works on non-standalone platforms.")]
    [SerializeField] private AudioClip[] playlistClips = Array.Empty<AudioClip>();
    [SerializeField] private bool shufflePlaylist = true;
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Min(0f)] private float fadeSeconds = 1.2f;

    [Header("Volume defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 0.1f;
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
    [SerializeField] private bool musicEnabledByDefault = true;
    [Tooltip("Keeps the old player's music headroom. The settings slider itself is still 0..100%.")]
    [SerializeField, Range(0f, 1f)] private float musicGain = 0.3f;

    [Header("SFX pool / optional mixer routing")]
    [SerializeField, Range(4, 64)] private int sfxPoolSize = 24;
    [SerializeField] private AudioMixerGroup musicOutput;
    [SerializeField] private AudioMixerGroup sfxOutput;

    private sealed class Voice
    {
        public AudioSource Source;
        public int Handle;
        public float Gain;
        public float StartedAt;
    }

    private readonly List<AudioLibrary.Entry> playlist = new List<AudioLibrary.Entry>();
    private readonly List<int> playOrder = new List<int>();
    private readonly HashSet<UnityEngine.Object> suppressors = new HashSet<UnityEngine.Object>();
    private readonly AudioSource[] musicSources = new AudioSource[2];
    private readonly AudioLibrary.Entry[] playingEntries = new AudioLibrary.Entry[2];
    private readonly float[] trackGains = new float[2];
    private Voice[] voices;
    private AudioClip legacyClip;
    private bool legacyAdopted;
    private int adoptedSession;
    private bool initialized;
    private bool initializing;
    private bool musicRequested;
    private bool musicPaused;
    private bool sourcesPaused;
    private bool stopWhenSilent;
    private bool crossfading;
    private float envelope;
    private float fadeProgress;
    private float outgoingStartGain;
    private int activeSlot;
    private int currentTrack = -1;
    private int orderCursor;
    private int nextVoiceHandle;
    private int failedTracks;
    private float nextTrackAttempt;
    private bool loadingTrack;
    private Coroutine loadRoutine;
    private AudioLibrary.Entry pendingEntry;
    private bool preferencesDirty;
    private float saveAt;

    public static AudioManager Instance { get; private set; }
    public bool IsReady => initialized;
    public bool IsMusicEnabled { get; private set; }
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }
    public float EffectiveMusicVolume => MusicVolume * musicGain;
    public bool IsMusicSuppressed => suppressors.Count > 0;
    public bool IsMusicSilent => !initialized || !isActiveAndEnabled || envelope <= 0f;
    public bool IsMusicPlaying => initialized && !sourcesPaused && envelope > 0f &&
        (musicSources[0].isPlaying || musicSources[1].isPlaying);
    public int PlaylistCount => playlist.Count;
    public int CurrentTrackIndex => currentTrack;
    public string CurrentTrackName => currentTrack >= 0 ? GetTrackName(currentTrack) : "—";
    public string PlaylistFolder => playlistFolder;

    public event Action Ready;
    public event Action<float> MusicVolumeChanged;
    public event Action<float> SfxVolumeChanged;
    public event Action<bool> MusicEnabledChanged;
    public event Action<AudioClip> TrackChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        playSession++;
        AudioLibrary.Reset();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    public static AudioManager EnsureInstance()
    {
        if (Instance != null) { return Instance; }
        AudioManager found = FindFirstObjectByType<AudioManager>();
        if (found == null) { return new GameObject("AudioManager").AddComponent<AudioManager>(); }
        Instance = found;
        found.AdoptPlaySession();
        return found;
    }

    private void AdoptPlaySession()
    {
        if (adoptedSession == playSession || voices == null) { return; }
        // With both reload options disabled, Awake is not guaranteed to run again.
        adoptedSession = playSession;
        StopAllCoroutines();
        AudioLibrary.CancelPendingLoads();
        CancelTrackLoad();
        StopAndReleaseMusic();
        StopAllSfx();
        suppressors.RemoveWhere(MissingOwner); // Retained active triggers keep their owner-scoped blockers.
        SavePreferences();
        MusicVolume = ClampVolume(PlayerPrefs.GetFloat(MusicVolumeKey, legacyAdopted ? MusicVolume : defaultMusicVolume));
        SfxVolume = ClampVolume(PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume));
        IsMusicEnabled = PlayerPrefs.GetInt(MusicEnabledKey, (legacyAdopted ? IsMusicEnabled : musicEnabledByDefault) ? 1 : 0) == 1;
        musicRequested = playOnStart;
        musicPaused = false;
        stopWhenSilent = false;
        envelope = 0f;
        nextTrackAttempt = 0f;
        initialized = false;
        initializing = false;
        BeginInitialize();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this); // Do not destroy an unrelated scene object's other components.
            return;
        }
        Instance = this;
        adoptedSession = playSession;
        if (transform.parent == null) { DontDestroyOnLoad(gameObject); }
        else { Debug.LogWarning("[Audio] Put AudioManager on a root object to persist across scenes.", this); }

        MusicVolume = ClampVolume(PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume));
        SfxVolume = ClampVolume(PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume));
        IsMusicEnabled = PlayerPrefs.GetInt(MusicEnabledKey, musicEnabledByDefault ? 1 : 0) == 1;
        musicRequested = playOnStart;
        for (int i = 0; i < musicSources.Length; i++)
        {
            musicSources[i] = CreateSource("Music_" + i, musicOutput);
            musicSources[i].ignoreListenerPause = true; // Music and settings feedback continue on menu pause.
        }
        voices = new Voice[Mathf.Clamp(sfxPoolSize, 4, 64)];
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = new Voice { Source = CreateSource("Sfx_" + i, sfxOutput) };
        }
    }

    private AudioSource CreateSource(string objectName, AudioMixerGroup output)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = 0f;
        source.dopplerLevel = 0f;
        source.outputAudioMixerGroup = output;
        return source;
    }

    private void OnEnable()
    {
        if (Instance != this) { return; }
        BeginInitialize();
    }

    private void BeginInitialize()
    {
        if (initialized || initializing || !isActiveAndEnabled) { return; }
        initializing = true;
        StartCoroutine(InitializeAudio());
    }

    private IEnumerator InitializeAudio()
    {
        yield return AudioLibrary.Initialize();
        initialized = true;
        initializing = false;
        BuildPlaylist();
        Ready?.Invoke();
        if (musicRequested && playlist.Count > 0 && currentTrack < 0 && !loadingTrack)
        {
            RequestTrack(TakeNextIndex());
        }
    }

    private void OnDisable()
    {
        if (Instance != this) { return; }
        StopAllCoroutines();
        initializing = false;
        AudioLibrary.CancelPendingLoads();
        loadingTrack = false;
        loadRoutine = null;
        ReleasePendingEntry();
        if (!initialized) { AudioLibrary.Reset(); }
        PauseSources();
        StopAllSfx();
        SavePreferences();
    }

    private void OnDestroy()
    {
        if (Instance != this) { return; }
        Instance = null;
        StopAndReleaseMusic();
        AudioLibrary.Reset();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) { SavePreferences(); }
    }

    private void OnApplicationQuit() => SavePreferences();

    /// <summary>Migration bridge: retains the existing scene clip and preference defaults.</summary>
    public void AdoptLegacyPlayer(AudioClip clip, float volume, bool enabledByDefault)
    {
        if (legacyAdopted) { return; }
        legacyAdopted = true;
        legacyClip = clip;
        if (!PlayerPrefs.HasKey(MusicVolumeKey))
        {
            MusicVolume = ClampVolume(volume);
            MusicVolumeChanged?.Invoke(MusicVolume);
        }
        if (!PlayerPrefs.HasKey(MusicEnabledKey))
        {
            IsMusicEnabled = enabledByDefault;
            MusicEnabledChanged?.Invoke(IsMusicEnabled);
        }
        if (initialized && playlist.Count == 0)
        {
            BuildPlaylist();
            if (musicRequested && playlist.Count > 0) { RequestTrack(TakeNextIndex()); }
        }
    }

    public void SetMusicVolume(float value)
    {
        float clamped = ClampVolume(value);
        if (Mathf.Approximately(MusicVolume, clamped)) { return; }
        MusicVolume = clamped;
        PlayerPrefs.SetFloat(MusicVolumeKey, clamped);
        MarkPreferencesDirty();
        ApplyMusicVolumes();
        MusicVolumeChanged?.Invoke(clamped);
    }

    public void SetSfxVolume(float value)
    {
        float clamped = ClampVolume(value);
        if (Mathf.Approximately(SfxVolume, clamped)) { return; }
        SfxVolume = clamped;
        PlayerPrefs.SetFloat(SfxVolumeKey, clamped);
        MarkPreferencesDirty();
        for (int i = 0; i < voices.Length; i++) { voices[i].Source.volume = voices[i].Gain * SfxVolume; }
        SfxVolumeChanged?.Invoke(clamped);
    }

    public void SetMusicEnabled(bool value)
    {
        if (IsMusicEnabled == value) { return; }
        IsMusicEnabled = value;
        PlayerPrefs.SetInt(MusicEnabledKey, value ? 1 : 0);
        MarkPreferencesDirty();
        MusicEnabledChanged?.Invoke(value);
    }

    private static float ClampVolume(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
    private void MarkPreferencesDirty() { preferencesDirty = true; saveAt = Time.unscaledTime + 0.4f; }

    public void SavePreferences()
    {
        if (!preferencesDirty) { return; }
        PlayerPrefs.Save();
        preferencesDirty = false;
    }

    /// <summary>Owner-scoped blockers do not change the user's Music Enabled preference.</summary>
    public void SuppressMusic(UnityEngine.Object owner, bool suppress)
    {
        if (owner == null) { return; }
        if (suppress) { suppressors.Add(owner); }
        else { suppressors.Remove(owner); }
    }

    public void PlayMusic()
    {
        musicRequested = true;
        musicPaused = false;
        stopWhenSilent = false;
        failedTracks = 0;
    }

    public void PauseMusic() { musicPaused = true; stopWhenSilent = false; }
    public void StopMusic() { musicRequested = false; musicPaused = false; stopWhenSilent = true; CancelTrackLoad(); }

    public void NextTrack()
    {
        if (!initialized || playlist.Count == 0) { return; }
        PlayMusic();
        RequestTrack(TakeNextIndex());
    }

    public void PreviousTrack()
    {
        if (!initialized || playlist.Count == 0) { return; }
        PlayTrack((currentTrack - 1 + playlist.Count) % playlist.Count);
    }

    public void PlayTrack(int index)
    {
        if (!initialized || index < 0 || index >= playlist.Count) { return; }
        PlayMusic();
        RequestTrack(index);
    }

    public string GetTrackName(int index) => index >= 0 && index < playlist.Count ? playlist[index].Name : "";

    public void SetPlaylistFolder(string folder)
    {
        if (!AudioLibrary.IsMusicFolder(folder))
        {
            Debug.LogWarning("[Audio] Playlist folder must be inside " + AudioLibrary.MusicFolder, this);
            return;
        }
        playlistFolder = AudioLibrary.NormalizeFolder(folder);
        playlistClips = Array.Empty<AudioClip>();
        CancelTrackLoad();
        if (!initialized) { return; }
        BuildPlaylist();
        if (playlist.Count > 0 && musicRequested) { RequestTrack(TakeNextIndex()); }
        else { StopMusic(); }
    }

    private void BuildPlaylist()
    {
        playlist.Clear();
        if (playlistClips != null)
        {
            for (int i = 0; i < playlistClips.Length; i++)
            {
                AudioClip clip = playlistClips[i];
                if (clip != null) { playlist.Add(new AudioLibrary.Entry(clip)); }
            }
        }
        if (playlist.Count == 0) { AudioLibrary.FillPlaylist(playlistFolder, playlist); }
        if (playlist.Count == 0 && legacyClip != null) { playlist.Add(new AudioLibrary.Entry(legacyClip)); }
        currentTrack = -1;
        failedTracks = 0;
        BuildOrder();
    }

    private void BuildOrder()
    {
        playOrder.Clear();
        for (int i = 0; i < playlist.Count; i++) { playOrder.Add(i); }
        if (shufflePlaylist)
        {
            for (int i = playOrder.Count - 1; i > 0; i--)
            {
                int other = UnityEngine.Random.Range(0, i + 1);
                int value = playOrder[i];
                playOrder[i] = playOrder[other];
                playOrder[other] = value;
            }
        }
        if (playOrder.Count > 1 && playOrder[0] == currentTrack)
        {
            int value = playOrder[0];
            playOrder[0] = playOrder[1];
            playOrder[1] = value;
        }
        orderCursor = 0;
    }

    private int TakeNextIndex()
    {
        if (orderCursor >= playOrder.Count) { BuildOrder(); }
        int index = playOrder[orderCursor++];
        if (playlist.Count > 1 && index == currentTrack)
        {
            if (orderCursor >= playOrder.Count) { BuildOrder(); }
            index = playOrder[orderCursor++];
        }
        return index;
    }

    private void RequestTrack(int index)
    {
        if (!isActiveAndEnabled || index < 0 || index >= playlist.Count) { return; }
        AudioLibrary.Entry entry = playlist[index];
        if (playingEntries[activeSlot] == entry && musicSources[activeSlot].clip != null) { return; }
        CancelTrackLoad();
        pendingEntry = entry;
        loadingTrack = true;
        loadRoutine = StartCoroutine(LoadAndPlay(entry, index));
    }

    private IEnumerator LoadAndPlay(AudioLibrary.Entry entry, int index)
    {
        yield return AudioLibrary.LoadClip(entry);
        loadingTrack = false;
        loadRoutine = null;
        pendingEntry = null;
        if (entry.Clip == null)
        {
            failedTracks++;
            nextTrackAttempt = Time.unscaledTime + 1f;
            if (failedTracks >= playlist.Count)
            {
                musicRequested = false;
                Debug.LogWarning("[Audio] No playable tracks in the current playlist.", this);
            }
            yield break;
        }
        failedTracks = 0;
        StartTrack(entry);
        currentTrack = index;
        TrackChanged?.Invoke(entry.Clip);
    }

    private void CancelTrackLoad()
    {
        if (loadRoutine != null) { StopCoroutine(loadRoutine); loadRoutine = null; }
        if (loadingTrack) { AudioLibrary.CancelPendingLoads(); }
        loadingTrack = false;
        ReleasePendingEntry();
    }

    private void ReleasePendingEntry()
    {
        if (pendingEntry != null && pendingEntry != playingEntries[0] && pendingEntry != playingEntries[1])
        {
            pendingEntry.Release();
        }
        pendingEntry = null;
    }

    private void StartTrack(AudioLibrary.Entry entry)
    {
        int oldSlot = activeSlot;
        int newSlot = musicSources[oldSlot].clip == null ? oldSlot : 1 - oldSlot;
        ReleaseSlot(newSlot, entry);
        AudioSource incoming = musicSources[newSlot];
        incoming.clip = entry.Clip;
        incoming.loop = playlist.Count == 1;
        playingEntries[newSlot] = entry;
        activeSlot = newSlot;
        crossfading = oldSlot != newSlot && musicSources[oldSlot].clip != null && fadeSeconds > 0f;
        fadeProgress = 0f;
        outgoingStartGain = trackGains[oldSlot];
        trackGains[newSlot] = crossfading ? 0f : 1f;
        if (!crossfading && oldSlot != newSlot) { ReleaseSlot(oldSlot); }
        ApplyMusicVolumes();
        incoming.Play();
        if (sourcesPaused) { incoming.Pause(); }
    }

    private void Update()
    {
        if (Instance != this) { return; }
        if (preferencesDirty && Time.unscaledTime >= saveAt) { SavePreferences(); }
        UpdateSfxPool();
        if (!initialized) { return; }
        suppressors.RemoveWhere(MissingOwner);
        bool audible = IsMusicEnabled && musicRequested && !musicPaused && suppressors.Count == 0;
        envelope = fadeSeconds > 0f
            ? Mathf.MoveTowards(envelope, audible ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds)
            : (audible ? 1f : 0f);
        if (audible && sourcesPaused)
        {
            for (int i = 0; i < musicSources.Length; i++) { musicSources[i].UnPause(); }
            sourcesPaused = false;
        }
        if (!audible && envelope <= 0f)
        {
            if (stopWhenSilent) { StopAndReleaseMusic(); stopWhenSilent = false; }
            else { PauseSources(); }
        }
        if (crossfading && !sourcesPaused)
        {
            fadeProgress = Mathf.Min(1f, fadeProgress + Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
            trackGains[activeSlot] = fadeProgress;
            trackGains[1 - activeSlot] = outgoingStartGain * (1f - fadeProgress);
            if (fadeProgress >= 1f) { ReleaseSlot(1 - activeSlot); crossfading = false; }
        }
        ApplyMusicVolumes();
        if (!audible || loadingTrack || crossfading || playlist.Count == 0 || Time.unscaledTime < nextTrackAttempt) { return; }
        AudioSource active = musicSources[activeSlot];
        if (active.clip == null || !active.isPlaying ||
            (!active.loop && active.clip.length - active.time <= Mathf.Min(fadeSeconds, active.clip.length * 0.25f)))
        {
            RequestTrack(TakeNextIndex());
        }
    }

    private void ApplyMusicVolumes()
    {
        for (int i = 0; i < musicSources.Length; i++)
        {
            if (musicSources[i] != null) { musicSources[i].volume = trackGains[i] * envelope * EffectiveMusicVolume; }
        }
    }

    private void PauseSources()
    {
        if (sourcesPaused) { return; }
        for (int i = 0; i < musicSources.Length; i++)
        {
            if (musicSources[i] != null) { musicSources[i].Pause(); }
        }
        sourcesPaused = true;
    }

    private void StopAndReleaseMusic()
    {
        ReleaseSlot(0);
        ReleaseSlot(1);
        crossfading = false;
        currentTrack = -1;
        sourcesPaused = false;
    }

    private void ReleaseSlot(int slot, AudioLibrary.Entry keep = null)
    {
        if (musicSources[slot] != null)
        {
            musicSources[slot].Stop();
            musicSources[slot].clip = null;
        }
        if (playingEntries[slot] != null && playingEntries[slot] != playingEntries[1 - slot] && playingEntries[slot] != keep)
        {
            playingEntries[slot].Release();
        }
        playingEntries[slot] = null;
        trackGains[slot] = 0f;
    }

    /// <returns>A generation handle, or -1 if muted, disabled or no clip was supplied.</returns>
    public int PlaySfx(AudioClip clip, Vector3 position, float volume = 1f, float spatialBlend = 1f,
        float pitch = 1f, bool ignoreListenerPause = false, float minDistance = 1f, float maxDistance = 20f)
    {
        if (!isActiveAndEnabled || voices == null || clip == null || SfxVolume <= 0f || volume <= 0f) { return -1; }
        Voice voice = voices[0];
        for (int i = 0; i < voices.Length; i++)
        {
            Voice candidate = voices[i];
            if (candidate.Handle == 0 || (!candidate.Source.isPlaying && (!AudioListener.pause || candidate.Source.ignoreListenerPause)))
            {
                voice = candidate;
                break;
            }
            if (candidate.StartedAt < voice.StartedAt) { voice = candidate; }
        }
        AudioSource source = voice.Source;
        source.Stop();
        nextVoiceHandle = nextVoiceHandle == int.MaxValue ? 1 : nextVoiceHandle + 1;
        voice.Handle = nextVoiceHandle;
        voice.Gain = ClampVolume(volume);
        voice.StartedAt = Time.unscaledTime;
        source.transform.position = position;
        source.clip = clip;
        source.spatialBlend = ClampVolume(spatialBlend);
        source.pitch = float.IsNaN(pitch) ? 1f : Mathf.Clamp(pitch, 0.1f, 3f);
        source.minDistance = Mathf.Max(0.01f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.ignoreListenerPause = ignoreListenerPause;
        source.volume = voice.Gain * SfxVolume;
        source.Play();
        return voice.Handle;
    }

    public void StopSfx(int handle)
    {
        if (voices == null || handle <= 0) { return; }
        for (int i = 0; i < voices.Length; i++)
        {
            if (voices[i].Handle == handle) { ReleaseVoice(voices[i]); return; }
        }
    }

    public bool IsSfxPlaying(int handle)
    {
        if (voices == null || handle <= 0) { return false; }
        for (int i = 0; i < voices.Length; i++)
        {
            Voice voice = voices[i];
            if (voice.Handle == handle)
            {
                return voice.Source.isPlaying || (AudioListener.pause && !voice.Source.ignoreListenerPause);
            }
        }
        return false;
    }

    public void StopAllSfx()
    {
        if (voices == null) { return; }
        for (int i = 0; i < voices.Length; i++) { ReleaseVoice(voices[i]); }
    }

    private void UpdateSfxPool()
    {
        for (int i = 0; i < voices.Length; i++)
        {
            Voice voice = voices[i];
            if (voice.Handle != 0 && !voice.Source.isPlaying && (!AudioListener.pause || voice.Source.ignoreListenerPause))
            {
                ReleaseVoice(voice);
            }
        }
    }

    private static void ReleaseVoice(Voice voice)
    {
        voice.Source.Stop();
        voice.Source.clip = null;
        voice.Handle = 0;
    }

    public void PlayUiClick() => PlayUiSound("WAV/Winter UI/ui_confirm_01.wav");
    public void PlayUiBack() => PlayUiSound("WAV/Winter UI/ui_back_01.wav");
    public void PlayUiSelect() => PlayUiSound("WAV/Winter UI/ui_select_01.wav");

    private void PlayUiSound(string path)
    {
        AudioClip clip = AudioLibrary.FindSfx(path) ?? AudioLibrary.FindSfx("Button.wav");
        PlaySfx(clip, Vector3.zero, 0.7f, 0f, 1f, true);
    }
}
