using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class InteractionDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRange = 2f;
    public LayerMask interactionLayers = -1;

    [Header("UI")]
    public GameObject promptUI;
    public TextMeshProUGUI promptText;

    private IInteractable currentTarget;
    private bool canInteract = true;

    void Awake()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnDialogueStarted += (type, id) => canInteract = false;
            GameEventManager.Instance.OnDialogueFinished += (type, id) => canInteract = true;
        }
    }

    void Start()
    {
        if (promptUI != null) promptUI.SetActive(false);
    }

    void Update()
    {
        if (!canInteract)
        {
            ClearTarget();
            return;
        }

        CheckForInteractables();
        HandleInput();
    }

    void CheckForInteractables()
    {
        var colliders = Physics2D.OverlapCircleAll(transform.position, detectionRange, interactionLayers);

        IInteractable closest = colliders
            .Select(col => col.GetComponent<IInteractable>())
            .Where(interactable => interactable != null && interactable.IsCurrentlyInteractable())
            .OrderBy(interactable => Vector3.Distance(transform.position, ((MonoBehaviour)interactable).transform.position))
            .FirstOrDefault();

        if (closest != currentTarget)
        {
            currentTarget = closest;
            UpdatePrompt();
        }
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.E) && currentTarget != null)
        {
            currentTarget.Interact();
            ClearTarget();
        }
    }

    void UpdatePrompt()
    {
        if (currentTarget != null)
        {
            promptUI.SetActive(true);
            promptText.text = currentTarget.GetInteractionPromptText();
        }
        else
        {
            promptUI.SetActive(false);
        }
    }

    void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget = null;
            UpdatePrompt();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}