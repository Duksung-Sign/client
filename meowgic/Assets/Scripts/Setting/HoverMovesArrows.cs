using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class HoverMovesArrows : MonoBehaviour, IPointerEnterHandler
{
    public SettingsController controller;          // 드래그 할당
    public RectTransform overrideTarget;           // 비워두면 자기 자신

    RectTransform self;

    void Awake()
    {
        self = GetComponent<RectTransform>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!controller) return;
        var tgt = overrideTarget ? overrideTarget : self;
        //controller.HoverTo(tgt, eventData.enterEventCamera);
        controller.HoverToSameParent(tgt);
    }
}
