// ============================================================================
// CellSlotInput.cs：列表点击选择、右键详情和拖入预览。
// ============================================================================
using UnityEngine;
using UnityEngine.EventSystems;

public class CellSlotInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private SplitScreenUI screen;
    private int index;
    public void Initialize(SplitScreenUI owner, int slotIndex) { screen = owner; index = slotIndex; }
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Right) screen.ShowDetail(index);
        else if (data.button == PointerEventData.InputButton.Left) screen.SelectType(index);
    }
    public void OnBeginDrag(PointerEventData data)
    { if (data.button == PointerEventData.InputButton.Left) screen.SelectType(index, false); }
    public void OnDrag(PointerEventData data) { }
    public void OnEndDrag(PointerEventData data) { }
}
