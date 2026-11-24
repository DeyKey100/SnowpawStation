using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Temporarily suppresses music while matching colliders are inside.
/// Suppression is owner-scoped: overlapping zones do not release each other's blockers.
/// Global playback commands are separate UnityEvent entry points.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Snowpaw/Audio/Music Trigger")]
public sealed class MusicTrigger : MonoBehaviour
{
    [Header("Trigger suppression")]
    [Tooltip("Requires an enabled trigger Collider on this object or its children. This script does not modify colliders.")]
    [SerializeField] private bool suppressWhileInside = true;
    [Tooltip("Matches the collider or any of its ancestors. Blank accepts all colliders; an undefined tag rejects them.")]
    [SerializeField] private string playerTag = "Player";

    [Header("First enter / last exit")]
    [SerializeField] private UnityEvent onPlayerEntered = new UnityEvent();
    [SerializeField] private UnityEvent onPlayerExited = new UnityEvent();

    private struct Occupant
    {
        public int PairCount;
        public double RecoveredStep;
    }

    // Counts duplicate enter/exit pairs, including multiple local trigger colliders.
    private readonly Dictionary<Collider, Occupant> occupants = new Dictionary<Collider, Occupant>(8);
    private readonly List<Collider> staleColliders = new List<Collider>(8);
    private Collider[] triggerColliders;
    private AudioManager manager;
    private bool validPlayerTag = true;
    private bool eventSuppressed;
    private bool suppressionApplied;

    public bool HasPlayersInside => occupants.Count > 0;

    private void Awake()
    {
        triggerColliders = GetComponentsInChildren<Collider>(true);
        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            try { CompareTag(playerTag); }
            catch (UnityException)
            {
                validPlayerTag = false;
                Debug.LogWarning("[Audio] MusicTrigger has an undefined player tag: " + playerTag, this);
            }
        }
        manager = AudioManager.EnsureInstance();
    }

    private void OnEnable()
    {
        ApplySuppression();
    }

    private void OnDisable()
    {
        // Do not invoke exit UnityEvents during teardown, or bootstrap a manager while quitting.
        occupants.Clear();
        staleColliders.Clear();
        eventSuppressed = false;
        if (manager != null) { manager.SuppressMusic(this, false); }
        suppressionApplied = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || other == null || !MatchesPlayer(other)) { return; }
        bool wasInside = HasPlayersInside;
        if (!occupants.TryGetValue(other, out Occupant occupant))
        {
            occupant = new Occupant { RecoveredStep = -1d };
        }
        occupant.PairCount++;
        occupants[other] = occupant;
        UpdateOccupancy(wasInside);
    }

    private void OnTriggerStay(Collider other)
    {
        // Restores all observed pairs after enabling this component while the player is already inside.
        if (!isActiveAndEnabled || other == null) { return; }
        double step = Time.fixedTimeAsDouble;
        if (occupants.TryGetValue(other, out Occupant occupant))
        {
            if (occupant.RecoveredStep == step)
            {
                occupant.PairCount++;
                occupants[other] = occupant;
            }
            return;
        }
        if (!MatchesPlayer(other)) { return; }
        bool wasInside = HasPlayersInside;
        occupants.Add(other, new Occupant { PairCount = 1, RecoveredStep = step });
        UpdateOccupancy(wasInside);
    }

    private void OnTriggerExit(Collider other)
    {
        // Do not retest the tag: a tracked object may have changed tags before leaving.
        if (other == null || !occupants.TryGetValue(other, out Occupant occupant)) { return; }
        // During initial Stay recovery, count only the remaining pairs; an unobserved Exit cannot consume one.
        if (occupant.RecoveredStep == Time.fixedTimeAsDouble) { return; }
        bool wasInside = HasPlayersInside;
        occupant.PairCount--;
        if (occupant.PairCount > 0) { occupants[other] = occupant; }
        else { occupants.Remove(other); }
        UpdateOccupancy(wasInside);
    }

    private void FixedUpdate()
    {
        bool wasInside = HasPlayersInside;
        if (wasInside)
        {
            if (!HasActiveTriggerCollider())
            {
                occupants.Clear();
            }
            else
            {
                // Unity may not send OnTriggerExit for a destroyed or disabled collider.
                staleColliders.Clear();
                foreach (KeyValuePair<Collider, Occupant> pair in occupants)
                {
                    Collider collider = pair.Key;
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    {
                        staleColliders.Add(collider);
                    }
                }
                for (int i = 0; i < staleColliders.Count; i++) { occupants.Remove(staleColliders[i]); }
                staleColliders.Clear();
            }
        }
        UpdateOccupancy(wasInside);
    }

    private bool HasActiveTriggerCollider()
    {
        for (int i = 0; i < triggerColliders.Length; i++)
        {
            Collider collider = triggerColliders[i];
            if (collider != null && collider.enabled && collider.isTrigger && collider.gameObject.activeInHierarchy)
            {
                return true;
            }
        }
        return false;
    }

    private bool MatchesPlayer(Collider collider)
    {
        if (!validPlayerTag) { return false; }
        if (string.IsNullOrWhiteSpace(playerTag)) { return true; }
        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag)) { return true; }
            current = current.parent;
        }
        return false;
    }

    private void UpdateOccupancy(bool wasInside)
    {
        ApplySuppression();
        bool inside = HasPlayersInside;
        if (!wasInside && inside) { onPlayerEntered?.Invoke(); }
        else if (wasInside && !inside) { onPlayerExited?.Invoke(); }
    }

    private void ApplySuppression()
    {
        bool suppress = isActiveAndEnabled && (eventSuppressed || (suppressWhileInside && HasPlayersInside));
        AudioManager audio = GetManager();
        if (audio == null || suppressionApplied == suppress) { return; }
        audio.SuppressMusic(this, suppress);
        suppressionApplied = suppress;
    }

    private AudioManager GetManager()
    {
        if (manager == null)
        {
            suppressionApplied = false;
            manager = AudioManager.EnsureInstance();
        }
        return manager;
    }

    private AudioManager GetActiveManager()
    {
        if (this == null || !isActiveAndEnabled) { return null; }
        AudioManager audio = GetManager();
        return audio != null && audio.isActiveAndEnabled ? audio : null;
    }

    /// <summary>Adds this component's explicit event blocker without changing music preferences.</summary>
    public void SuppressMusic() => SetMusicSuppressed(true);

    /// <summary>Releases the event blocker; an occupied trigger still keeps its own suppression.</summary>
    public void ReleaseMusic() => SetMusicSuppressed(false);

    public void SetMusicSuppressed(bool suppress)
    {
        if (this == null || !isActiveAndEnabled) { return; }
        eventSuppressed = suppress;
        ApplySuppression();
    }

    /// <summary>Global playback request; does not release occupied zones or explicit blockers.</summary>
    public void PlayMusic()
    {
        AudioManager audio = GetActiveManager();
        if (audio != null) { audio.PlayMusic(); }
    }

    public void PauseMusic()
    {
        AudioManager audio = GetActiveManager();
        if (audio != null) { audio.PauseMusic(); }
    }

    public void StopMusic()
    {
        AudioManager audio = GetActiveManager();
        if (audio != null) { audio.StopMusic(); }
    }

    public void NextTrack()
    {
        AudioManager audio = GetActiveManager();
        if (audio != null) { audio.NextTrack(); }
    }

    public void PlayTrack(int index)
    {
        AudioManager audio = GetActiveManager();
        if (audio != null) { audio.PlayTrack(index); }
    }

    public void SetPlaylistFolder(string folder)
    {
        AudioManager audio = GetActiveManager();
        if (audio != null) { audio.SetPlaylistFolder(folder); }
    }

    public void SetMusicEnabled(bool enabled)
    {
        AudioManager audio = GetActiveManager();
        if (audio != null) { audio.SetMusicEnabled(enabled); }
    }
}
