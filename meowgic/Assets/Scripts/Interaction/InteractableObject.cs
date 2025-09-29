using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    [Tooltip("StoryAction.csv의 Target과 일치해야 하는 고유 ID")]
    public string interactionID;

    private Collider2D objectCollider;

    void Awake()
    {
        objectCollider = GetComponent<Collider2D>();
        DisableInteraction();
    }

    void OnEnable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RegisterObject(this);
        }
    }

    void OnDisable()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.UnregisterObject(this);
        }
    }

    public void EnableInteraction()
    {
        if (objectCollider != null) objectCollider.enabled = true;
        Debug.Log($"[{interactionID}] 상호작용이 활성화되었습니다.");
    }

    public void DisableInteraction()
    {
        if (objectCollider != null) objectCollider.enabled = false;
    }
}