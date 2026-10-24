using UnityEngine;

public class SimpleInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionPrompt = "[E] Взаимодействовать";
    [SerializeField] private string interactionMessage = "Взаимодействие сработало.";

    public string GetInteractPrompt()
    {
        return interactionPrompt;
    }

    public void Interact()
    {
        Debug.Log($"[Snowpaw Station] {interactionMessage}", this);
    }
}
