using UnityEngine;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string npcName = "Перси";
    [TextArea(2, 5)]
    [SerializeField] private string dialogueText = "Снег усиливается... Проверь грузовик и загляни к Пекарю.";

    public string GetInteractPrompt()
    {
        return $"[E] Поговорить с {npcName}";
    }

    public void Interact()
    {
        Debug.Log($"[{npcName}]: {dialogueText}");
    }
}