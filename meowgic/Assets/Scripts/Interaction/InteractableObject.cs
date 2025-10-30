using UnityEngine;
using System.Collections;

public class InteractableObject : MonoBehaviour
{
    [Tooltip("StoryAction.csv의 Target과 일치해야 하는 고유 ID")]
    public string interactionID;

    private Collider2D objectCollider;
    private bool isInitialized = false;

    void Awake()
    {
        objectCollider = GetComponent<Collider2D>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.1f);

        DisableInteraction();
        isInitialized = true;
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
        if (!isInitialized)
        {
            Debug.LogWarning($"[{interactionID}] 아직 초기화되지 않았습니다.");
            return;
        }

        if (objectCollider != null)
        {
            objectCollider.enabled = true;
            Debug.Log($"[{interactionID}] 상호작용이 활성화되었습니다.");
        }
    }

    public void DisableInteraction()
    {
        if (objectCollider != null) objectCollider.enabled = false;
    }

    public bool IsInteractable()
    {
        return isInitialized && objectCollider != null && objectCollider.enabled;
    }
}