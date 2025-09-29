using System.Collections.Generic;
using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance;

    private Dictionary<string, InteractableObject> interactableObjects = new Dictionary<string, InteractableObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterObject(InteractableObject obj)
    {
        if (!interactableObjects.ContainsKey(obj.interactionID))
        {
            interactableObjects.Add(obj.interactionID, obj);
        }
    }

    public void UnregisterObject(InteractableObject obj)
    {
        if (interactableObjects.ContainsKey(obj.interactionID))
        {
            interactableObjects.Remove(obj.interactionID);
        }
    }

    public void EnableObject(string id)
    {
        if (interactableObjects.TryGetValue(id, out InteractableObject obj))
        {
            obj.EnableInteraction();
        }
        else
        {
            Debug.LogWarning($"[InteractionManager] ID가 '{id}'인 오브젝트를 찾을 수 없습니다.");
        }
    }

    public void DisableObject(string id)
    {
        if (interactableObjects.TryGetValue(id, out InteractableObject obj))
        {
            obj.DisableInteraction();
        }
        else
        {
            Debug.LogWarning($"[InteractionManager] ID가 '{id}'인 오브젝트를 찾을 수 없습니다.");
        }
    }
}