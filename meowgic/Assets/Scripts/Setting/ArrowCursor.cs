using UnityEngine;
[DisallowMultipleComponent]
public class ArrowCursor : MonoBehaviour
{
    [Header("Arrows (UI)")]
    [SerializeField] private RectTransform leftArrow; // 왼쪽 화살표(UI)
    [SerializeField] private RectTransform rightArrow; // 오른쪽 화살표(UI)
    [Header("Movement")]
    [SerializeField] private float stepY = 32f; // ↑/↓ 한 번에 움직일 양
    [SerializeField] private float minY; // 최솟값(아래쪽 한계)
    [SerializeField] private float maxY; // 최댓값(위쪽 한계)
    [SerializeField] private bool clampToBounds = true; // 범위 밖 이동 방지
    [SerializeField] private bool wrapAtEdges = false; // 상/하 경계에서 랩(메뉴에 유용)
    [Header("Special Slots (optional)")]
    [SerializeField] private bool hasOkSlot = false;
    [SerializeField] private RectTransform okSlot; // OK 버튼 RectTransform (권장)
    [SerializeField] private float okY = 0f; // okSlot이 없을 때 사용할 Y 값
    [SerializeField] private bool hasBackSlot = false;
    [SerializeField] private RectTransform backSlot; // Back 버튼 RectTransform (권장)
    [SerializeField] private float backY = 0f; // backSlot이 없을 때 사용할 Y 값
    [Header("Runtime (읽기용)")]
    [SerializeField] private float currentY; // 현재 Y 위치(anchoredPosition.y)
    [SerializeField] private int currentIndex; // 현재 인덱스(0부터, step 기준)
    [SerializeField] private bool onOk = false;
    [SerializeField] private bool onBack = false;
    public float CurrentY => currentY;
    public int CurrentIndex => currentIndex;
    public int SlotCount => Mathf.RoundToInt((maxY - minY) / Mathf.Max(1f, stepY)) + 1;

public bool OnOk => onOk;
    public bool OnBack => onBack;
    const float EPS = 0.001f;
    public void SyncFromLeftArrow()
    {
        if (leftArrow) SetY(leftArrow.anchoredPosition.y);
    }
    public void SetIndex(int index)
    {
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        float y = maxY - index * Mathf.Max(1f, stepY);
        SetY(y);
    }
    /// <summary>dir: +1=위, -1=아래</summary>
    public void Move(int dir)
    {
        if (!leftArrow || !rightArrow) return;
        // 특수 슬롯에서의 이동 처리
        if (onOk)
        {
            onOk = false;
            if (dir > 0) EnterBack(); // OK에서 아래로 → Back
            else SetY(maxY); // OK에서 위로 → 최상단
            return;
        }
        if (onBack)
        {
            onBack = false;
            if (dir > 0) SetY(minY); // Back에서 아래로 → 최하단
            else EnterOk(); // Back에서 위로 → OK
            return;
        }
        bool atTop = Mathf.Abs(currentY - maxY) <= EPS;
        bool atBottom = Mathf.Abs(currentY - minY) <= EPS;
        // 경계에서 OK/Back 점프
        if (hasOkSlot)
        {
            if (dir > 0 && atTop) { EnterOk(); return; }
            if (dir < 0 && atBottom) { EnterBack(); return; }
        }
        float newY = currentY + dir * stepY;
        if (wrapAtEdges)
        {
            if (newY < minY) newY = maxY;
            if (newY > maxY) newY = minY;
        }
        else if (clampToBounds)
        {
            newY = Mathf.Clamp(newY, minY, maxY);
        }
        if (!Mathf.Approximately(newY, currentY))
            SetY(newY);
    }
    public void EnterOk()
    {
        if (!hasOkSlot) return;
        onOk = true; onBack = false;
        float targetY = okSlot ? okSlot.anchoredPosition.y : okY;
        SetArrowsY(targetY);
    }
    public void EnterBack()
    {
        if (!hasBackSlot) return;
        onBack = true; onOk = false;
        float targetY = backSlot ? backSlot.anchoredPosition.y : backY;
        SetArrowsY(targetY);
    }
    public void ExitSpecial()
    {
        onOk = onBack = false;
    }
    public void SetY(float y)
    {
        ExitSpecial();
        currentY = clampToBounds ? Mathf.Clamp(y, minY, maxY) : y;
        currentIndex = Mathf.RoundToInt((maxY - currentY) / Mathf.Max(1f, stepY));
        SetArrowsY(currentY);
    }
    /// <summary>UI 항목 위로 마우스 호버 시, 동일 부모 내에서 포커싱</summary>
    public void FocusSibling(RectTransform target)
    {
        if (!target) return;
        if (hasOkSlot && okSlot == target) { EnterOk(); return; }
        if (hasBackSlot && backSlot == target) { EnterBack(); return; }
        SetIndex(target.GetSiblingIndex());
    }

private void SetArrowsY(float y)
    {
        if (leftArrow)
        {
            var lp = leftArrow.anchoredPosition;
            lp.y = y; leftArrow.anchoredPosition = lp;
        }
        if (rightArrow)
        {
            var rp = rightArrow.anchoredPosition;
            rp.y = y; rightArrow.anchoredPosition = rp;
        }
    }
}