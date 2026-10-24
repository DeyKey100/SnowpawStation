using UnityEngine;

public class WoodPileInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private FirewoodStorage storage;

    public string GetInteractPrompt()
    {
        return "[E] Взять полено";
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
