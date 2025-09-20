public interface IInteractable
{
    void Interact();
    string GetInteractionPromptText();
    bool IsCurrentlyInteractable();
}