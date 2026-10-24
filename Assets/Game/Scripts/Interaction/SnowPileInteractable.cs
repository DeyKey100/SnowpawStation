using UnityEngine;

public class SnowPileInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private DayLoopManager dayLoopManager;
    [SerializeField] private Renderer snowRenderer;
    [SerializeField] private Collider snowCollider;

    private bool cleared;

    public string GetInteractPrompt()
    {
        return cleared ? string.Empty : "[E] Расчистить сугроб";
    }

    public void Interact()
    {
        if (cleared)
        {
            return;
        }

        if (dayLoopManager == null)
        {
            Debug.LogError("SnowPileInteractable не назначил DayLoopManager.", this);
            return;
        }

        if (dayLoopManager.currentState != DayLoopManager.DayState.Morning)
        {
            Debug.Log("Сейчас не утренняя фаза. Сугроб можно будет убрать утром.", this);
            return;
        }

        cleared = true;

        if (snowRenderer != null)
        {
            snowRenderer.enabled = false;
        }

        if (snowCollider != null)
        {
            snowCollider.enabled = false;
        }

        Debug.Log("[Snowpaw Station] Сугроб расчищен. Утро завершено.", this);
        dayLoopManager.CompleteMorningTask();
    }
}
