using UnityEngine;

public class WoodPileInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private FirewoodStorage storage;

    public string GetInteractPrompt()
    {
        return storage != null && storage.CanAddFirewood
            ? "[E] Взять полено"
            : string.Empty;
    }

    public void Interact()
    {
        if (storage == null)
        {
            Debug.LogError("WoodPileInteractable не назначил FirewoodStorage.", this);
            return;
        }

        storage.TryAddFirewood();
    }
}
