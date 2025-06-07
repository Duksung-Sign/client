using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ItemUIManager : MonoBehaviour
{
    public static ItemUIManager Instance;

    [Header("Item Description UI")]
    public GameObject descriptionUI;
    public TextMeshProUGUI descriptionText;

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

    public void ShowItemDescription(string description)
    {
        descriptionUI.SetActive(true);
        descriptionText.text = description;
    }

    public void HideItemDescription()
    {
        descriptionUI.SetActive(false);
    }

    void Update()
    {
        if (descriptionUI != null)
        {
            if (descriptionUI.activeInHierarchy && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
            {
                HideItemDescription();
            }
        }
    }
}