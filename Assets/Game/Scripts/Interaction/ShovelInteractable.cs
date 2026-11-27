using UnityEngine;

/// <summary>Лопата на стене магазина: доступна после записки и остаётся у игрока.</summary>
[DisallowMultipleComponent]
public sealed class ShovelInteractable : MonoBehaviour, IInteractable, IInteractionAvailability
{
    private Renderer[] cachedRenderers;
    private ProximityInteractable marker;

    public bool CanInteractNow => isActiveAndEnabled && !GameState.HasShovel &&
        GameState.IsDayEventDone(KeeperNoteInteractable.ReadEventId) &&
        !DialogueManager.IsActive && !InteractionLock.IsLocked &&
        !PauseMenu.IsPaused && !SceneLoadingScreen.IsLoading;

    private void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        TryGetComponent(out marker);
    }

    private void OnEnable()
    {
        GameState.FlagsChanged += RefreshVisibility;
        RefreshVisibility();
    }

    private void OnDisable()
    {
        GameState.FlagsChanged -= RefreshVisibility;
    }

    public string GetInteractPrompt() => CanInteractNow ? "[E] Взять лопату" : string.Empty;

    public void Interact()
    {
        if (!CanInteractNow)
        {
            return;
        }

        GameState.TryTakeShovel();
    }

    private void RefreshVisibility()
    {
        // Keep this component active so ResetRun can put the shovel back on the wall.
        bool visible = !GameState.HasShovel;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = visible;
            }
        }
        if (marker != null)
        {
            marker.enabled = visible;
        }
    }
}
