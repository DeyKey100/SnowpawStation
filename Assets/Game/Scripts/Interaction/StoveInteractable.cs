using UnityEngine;

public class StoveInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private StationHeatSystem heatSystem;
    [SerializeField] private FirewoodStorage firewoodStorage;
    [SerializeField] private float heatPerInteraction = 30f;

    public string GetInteractPrompt()
    {
        return "[E] Подбросить полено в печь";
    }

    public void Interact()
    {
        if (heatSystem == null)
        {
            Debug.LogError("StoveInteractable не назначил StationHeatSystem.", this);
            return;
        }

        if (firewoodStorage == null)
        {
            Debug.LogError("StoveInteractable не назначил FirewoodStorage.", this);
            return;
        }

        if (!firewoodStorage.TryConsumeFirewood())
        {
            return;
        }

        heatSystem.AddHeat(heatPerInteraction);
    }
}
