public interface IInteractable
{
    string GetInteractPrompt();
    void Interact();
}

/// <summary>Необязательный контракт: информационная плашка не забирает клавишу E.</summary>
public interface IInteractionAvailability
{
    bool CanInteractNow { get; }
}