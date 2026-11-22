using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Настройки сюжетной передачи для конкретного дня.
/// </summary>
[Serializable]
public class RadioStoryBroadcast
{
    [Tooltip("Игровой день (1, 2, 3...)")]
    public int day = 1;

    [Tooltip("Аудиоклип сюжетной передачи или музыки (джаз, речь, сигнал)")]
    public AudioClip broadcastClip;

    [Range(0f, 1f), Tooltip("Громкость воспроизведения")]
    public float volume = 0.8f;

    [Tooltip("Зацикливать ли аудио")]
    public bool loop = false;

    [Tooltip("Остановить аудио при закрытии сюжетного диалога")]
    public bool stopOnDialogueEnd = true;
}

/// <summary>
/// Интерактивное радио на станции:
/// - Воспроизводит либо статический шум (Static), либо сюжетную передачу (Story Broadcast) через единую функцию PlayBroadcastClip.
/// - При выключении или смене звука воспроизведение обрывается мгновенно, без затухания.
/// - 3D-звучание, привязанное к объекту радио.
/// </summary>
[DisallowMultipleComponent]
public class RadioInteractable : MonoBehaviour, IInteractable
{
    public static RadioInteractable Instance { get; private set; }
    private const float PostVoiceStaticDuration = 3f;
    private const float PostVoiceStaticVolume = 0.12f;

    [Header("Prompts")]
    [SerializeField] private string turnOnPrompt = "[E] Включить радио";
    [SerializeField] private string turnOffPrompt = "[E] Выключить радио";
    [SerializeField] private string storyRadioPrompt = "[E] Слушать радио";

    [Header("General Audio & SFX")]
    [Tooltip("Статический шум (помехи) радио")]
    [SerializeField] private AudioClip staticClip;
    [Range(0f, 1f), Tooltip("Громкость статического шума")]
    [SerializeField] private float staticVolume = 0.5f;

    [Tooltip("Щелчок тумблера/кнопки включения")]
    [SerializeField] private AudioClip switchClickClip;

    [Header("Story Broadcasts")]
    [Tooltip("Список сюжетных радиопередач по дням")]
    [SerializeField] private List<RadioStoryBroadcast> storyBroadcasts = new List<RadioStoryBroadcast>
    {
        new RadioStoryBroadcast { day = 1, volume = 0.7f },
        new RadioStoryBroadcast { day = 2, volume = 0.8f },
    };

    [Header("3D Sound Settings")]
    [SerializeField] private bool spatial3D = true;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 12f;
    [Tooltip("Temporarily pause background music during a story broadcast, without changing the user's music preference.")]
    [SerializeField] private bool suppressBackgroundDuringBroadcast = true;

    private AudioManager audioManager;
    private RadioMusicUI musicUI;
    private float radioLocalVolume = 1f;
    private double broadcastEndDsp;
    private bool ownsMusicSuppression;
    private Coroutine pendingBroadcast;
    private bool postVoiceStaticPending;
    private bool timedStaticPlaying;

    // Основной динамик радио и источник для щелчка тумблера
    private AudioSource radioSource;
    private AudioSource sfxSource;

    private AudioClip StaticNoiseClip => staticClip != null ? staticClip : AudioLibrary.FindSfx("Radio_Static_Loop.ogg");
    public bool IsPlaying => radioSource != null && radioSource.isPlaying;
    public bool IsStaticOn => IsPlaying && radioSource.clip == StaticNoiseClip;
    public bool IsBroadcastPlaying => IsPlaying && radioSource.clip != StaticNoiseClip;
    public bool IsNoiseOn => IsStaticOn; // Обратная совместимость

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        audioManager = AudioManager.EnsureInstance();
        EnsureAudioSources();
        if (!TryGetComponent(out musicUI)) { musicUI = gameObject.AddComponent<RadioMusicUI>(); }
    }

    private void OnEnable()
    {
        if (Instance != this) { return; }
        audioManager.SfxVolumeChanged += ApplySfxVolume;
        ApplySfxVolume(audioManager.SfxVolume);
    }

    private void Update()
    {
        // DSP time freezes with listener pause, including a delayed broadcast and the static tail.
        if (pendingBroadcast != null || radioSource == null || AudioListener.pause) { return; }

        double dspTime = AudioSettings.dspTime;
        if (timedStaticPlaying)
        {
            if (dspTime >= broadcastEndDsp) { StopRadioAudio(); }
            return;
        }

        if (!radioSource.loop && dspTime >= broadcastEndDsp && !radioSource.isPlaying)
        {
            if (postVoiceStaticPending)
            {
                BeginPostVoiceStatic();
            }
            else if (ownsMusicSuppression)
            {
                SetBackgroundSuppressed(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeDialogueEvents();
    }

    private void OnDisable()
    {
        if (musicUI != null) { musicUI.Close(); }
        if (audioManager != null) { audioManager.SfxVolumeChanged -= ApplySfxVolume; }
        StopRadioAudio();
        UnsubscribeDialogueEvents();
    }

    private void EnsureAudioSources()
    {
        if (radioSource != null && sfxSource != null) { return; }
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 2)
        {
            radioSource = sources[0];
            sfxSource = sources[1];
        }
        else if (sources.Length == 1)
        {
            radioSource = sources[0];
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            radioSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        radioSource.playOnAwake = false;
        radioSource.loop = false;
        Apply3DSettings(radioSource);

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        Apply3DSettings(sfxSource);
    }

    private void Apply3DSettings(AudioSource src)
    {
        if (src == null)
        {
            return;
        }

        if (spatial3D)
        {
            src.spatialBlend = 1f;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
        }
        else
        {
            src.spatialBlend = 0f;
        }
    }

    // ================= IInteractable =================

    public string GetInteractPrompt()
    {
        if (IsNightStoryWaiting())
        {
            return storyRadioPrompt;
        }

        return RadioMusicUI.InteractPrompt;
    }

    public void Interact()
    {
        if (IsNightStoryWaiting())
        {
            TriggerNightStory();
            return;
        }

        musicUI.Open();
    }

    // ================= Ночной сюжет =================

    private bool IsNightStoryWaiting()
    {
        DayLoopManager loop = DayLoopManager.Instance;
        if (loop == null || loop.CurrentState != DayLoopManager.DayState.NightTransition)
        {
            return false;
        }

        return loop.GetTaskProgress("radio") == 0 && HasNightRadioTask(loop.CurrentDay);
    }

    private static bool HasNightRadioTask(int day)
    {
        List<DayPlan.NightTask> tasks = DayPlans.GetNightTasks(day);
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].id == "radio" && tasks[i].required)
            {
                return true;
            }
        }

        return false;
    }

    private void TriggerNightStory()
    {
        PlaySwitchClick();

        int day = DayLoopManager.Instance != null ? DayLoopManager.Instance.CurrentDay : 1;
        PlayStoryBroadcast(day);

        NightEvents night = StoryDirector.Instance != null ? StoryDirector.Instance.Night : NightEvents.Instance;
        if (night != null)
        {
            night.TriggerNightRadio();
        }
    }

    // ================= Воспроизведение =================

    private AudioClip SwitchClick => switchClickClip != null ? switchClickClip :
        AudioLibrary.FindSfx("Button.wav") ?? AudioLibrary.FindSfx("WAV/Winter UI/ui_select_01.wav");
    private float SwitchClickDuration => SwitchClick != null ? SwitchClick.length : 0f;

    /// <summary>Воспроизвести сюжетный эфир для указанного игрового дня.</summary>
    public void PlayStoryBroadcast(int day)
    {
        RadioStoryBroadcast broadcast = GetBroadcastForDay(day);
        if (day == 2 && (broadcast == null || broadcast.broadcastClip == null))
        {
            // The second night's transmission comes through interference, not the music playlist.
            AudioClip noise = StaticNoiseClip;
            PlayClip(noise, staticVolume, loop: true, delay: SwitchClickDuration);
            if (noise != null)
            {
                SetBackgroundSuppressed(suppressBackgroundDuringBroadcast);
                SubscribeDialogueEvents(); // A noise loop must stop when this story dialogue ends.
            }
            return;
        }

        if (broadcast == null || broadcast.broadcastClip == null)
        {
            return;
        }

        StopRadioAudio();
        SetBackgroundSuppressed(suppressBackgroundDuringBroadcast && broadcast.broadcastClip != StaticNoiseClip);
        if (broadcast.stopOnDialogueEnd)
        {
            SubscribeDialogueEvents();
        }
        pendingBroadcast = StartCoroutine(PlayStoryBroadcastWhenReady(
            broadcast.broadcastClip, broadcast.volume, broadcast.loop, broadcast.stopOnDialogueEnd, day == 1));
    }

    private IEnumerator PlayStoryBroadcastWhenReady(AudioClip clip, float volume, bool loop, bool stopOnDialogueEnd, bool appendStatic)
    {
        double clickEndDsp = AudioSettings.dspTime + SwitchClickDuration;
        yield return null; // Assign the pending handle before any completion or cancellation.

        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        while (clip != null && (clip.loadState == AudioDataLoadState.Loading || AudioListener.pause ||
            AudioSettings.dspTime < clickEndDsp ||
            (ownsMusicSuppression && audioManager != null && !audioManager.IsMusicSilent)))
        {
            yield return null;
        }

        pendingBroadcast = null; // PlayClip must not cancel its own coroutine.
        if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
        {
            StopRadioAudio();
            Debug.LogWarning("[Snowpaw Station] Не удалось загрузить аудио сюжетного радио.", this);
            yield break;
        }

        PlayClip(clip, volume, loop);
        postVoiceStaticPending = appendStatic && !loop && clip != StaticNoiseClip;
        if (stopOnDialogueEnd)
        {
            SubscribeDialogueEvents();
        }
    }

    private void BeginPostVoiceStatic()
    {
        postVoiceStaticPending = false;
        pendingBroadcast = StartCoroutine(PlayPostVoiceStaticWhenReady(StaticNoiseClip));
    }

    private IEnumerator PlayPostVoiceStaticWhenReady(AudioClip noise)
    {
        yield return null; // The tail uses the same cancellable pending handle as the voice.
        if (noise != null && noise.loadState == AudioDataLoadState.Unloaded)
        {
            noise.LoadAudioData();
        }
        while (noise != null && (noise.loadState == AudioDataLoadState.Loading || AudioListener.pause))
        {
            yield return null;
        }

        pendingBroadcast = null;
        if (noise == null || noise.loadState != AudioDataLoadState.Loaded)
        {
            StopRadioAudio();
            Debug.LogWarning("[Snowpaw Station] Не удалось загрузить помехи радио.", this);
            yield break;
        }

        // Continue the same broadcast: retain its music blocker and dialogue subscription.
        radioSource.clip = noise;
        radioSource.loop = true;
        radioLocalVolume = PostVoiceStaticVolume;
        ApplySfxVolume(audioManager.SfxVolume);
        broadcastEndDsp = AudioSettings.dspTime + PostVoiceStaticDuration;
        timedStaticPlaying = true;
        radioSource.Play();
    }

    private void CancelPendingBroadcast()
    {
        if (pendingBroadcast == null) { return; }
        StopCoroutine(pendingBroadcast);
        pendingBroadcast = null;
    }

    /// <summary>
    /// Единая функция воспроизведения любого звука на радио (статика, сюжетная запись, музыка).
    /// Обрывает текущий звук и начинает воспроизведение переданного клипа.
    /// </summary>
    public void PlayClip(AudioClip clip, float volume = 0.8f, bool loop = false, float delay = 0f)
    {
        CancelPendingBroadcast();
        postVoiceStaticPending = false;
        timedStaticPlaying = false;
        EnsureAudioSources();

        UnsubscribeDialogueEvents();
        radioSource.Stop();
        SetBackgroundSuppressed(false);

        if (clip == null)
        {
            Debug.LogWarning("[Snowpaw Station] RadioInteractable: аудиоклип не назначен.", this);
            return;
        }

        radioSource.clip = clip;
        radioSource.loop = loop;
        radioLocalVolume = Mathf.Clamp01(volume);
        ApplySfxVolume(audioManager.SfxVolume);
        broadcastEndDsp = AudioSettings.dspTime + Mathf.Max(0f, delay) + clip.length;
        SetBackgroundSuppressed(suppressBackgroundDuringBroadcast && clip != StaticNoiseClip);

        if (delay > 0f)
        {
            radioSource.PlayDelayed(delay);
        }
        else
        {
            radioSource.Play();
        }

        Debug.Log($"[Snowpaw Station] Радио: воспроизведение '{clip.name}'.");
    }

    public RadioStoryBroadcast GetBroadcastForDay(int day)
    {
        for (int i = 0; i < storyBroadcasts.Count; i++)
        {
            if (storyBroadcasts[i].day == day)
            {
                return storyBroadcasts[i];
            }
        }

        return null;
    }

    public void ToggleStatic()
    {
        PlaySwitchClick();

        if (IsPlaying)
        {
            StopRadioAudio();
        }
        else
        {
            PlayClip(StaticNoiseClip, staticVolume, loop: true, SwitchClickDuration);
        }
    }

    public void PlaySwitchClick()
    {
        PlaySfx(SwitchClick, 0.7f);
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        if (audioManager == null) { audioManager = AudioManager.EnsureInstance(); }
        audioManager.PlaySfx(clip, transform.position, Mathf.Clamp01(volume), spatial3D ? 1f : 0f,
            1f, false, minDistance, maxDistance);
    }

    /// <summary>Мгновенный обрыв звучания радио.</summary>
    public void StopRadioAudio()
    {
        CancelPendingBroadcast();
        postVoiceStaticPending = false;
        timedStaticPlaying = false;
        UnsubscribeDialogueEvents();
        if (radioSource != null) { radioSource.Stop(); } // Also cancels a delayed or paused broadcast.
        SetBackgroundSuppressed(false);
        Debug.Log("[Snowpaw Station] Радио выключено.");
    }

    private void ApplySfxVolume(float value)
    {
        if (radioSource != null) { radioSource.volume = radioLocalVolume * value; }
        if (sfxSource != null) { sfxSource.volume = value; }
    }

    private void SetBackgroundSuppressed(bool value)
    {
        ownsMusicSuppression = value;
        if (audioManager != null) { audioManager.SuppressMusic(this, value); }
    }

    // ================= Синхронизация с диалогами =================

    private void SubscribeDialogueEvents()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm != null)
        {
            dm.DialogueFinished -= OnDialogueFinished;
            dm.DialogueFinished += OnDialogueFinished;
        }
    }

    private void UnsubscribeDialogueEvents()
    {
        DialogueManager dm = DialogueManager.Instance;
        if (dm != null)
        {
            dm.DialogueFinished -= OnDialogueFinished;
        }
    }

    private void OnDialogueFinished(DialogueScript finished)
    {
        if (finished == null)
        {
            return;
        }

        // Если завершился ночной эфир радио — звук мгновенно обрывается
        if (finished.id.StartsWith("night") && finished.id.Contains("radio"))
        {
            UnsubscribeDialogueEvents();
            StopRadioAudio();
        }
    }
}
