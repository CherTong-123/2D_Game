using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlacedCellDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static bool IsDragging { get; private set; }
    public static bool SuppressUndo { get; private set; }

    RectTransform rectTransform;
    Canvas canvas;
    Camera uiCamera;
    CanvasGroup canvasGroup;

    PointerEventData currentEventData;
    Vector3 originalWorldPosition;
    bool dragging;
    bool canceled;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null ? canvas.worldCamera : null;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Update()
    {
        if (dragging && Input.GetMouseButtonDown(1))
        {
            CancelDrag(currentEventData);
        }
    }

    void LateUpdate()
    {
        SuppressUndo = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        canceled = false;
        currentEventData = eventData;
        IsDragging = true;
        originalWorldPosition = rectTransform.position;

        // 拖动期间临时移除自身占用，避免和自己做重叠判定
        if (DropArea.Instance != null)
        {
            DropArea.Instance.RemoveOccupant(rectTransform);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canceled) return;

        currentEventData = eventData;

        if (Input.GetMouseButtonDown(1))
        {
            CancelDrag(eventData);
            return;
        }

        MoveTo(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        IsDragging = false;
        currentEventData = null;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        if (canceled)
        {
            canceled = false;
            return;
        }

        if (DropArea.Instance != null
            && DropArea.Instance.IsFullyInsideGrid(rectTransform)
            && !DropArea.Instance.OverlapsOccupant(rectTransform))
        {
            DropArea.Instance.AddOccupant(rectTransform);
        }
        else
        {
            Revert();
        }
    }

    void CancelDrag(PointerEventData eventData)
    {
        if (eventData == null) return;

        canceled = true;
        dragging = false;
        IsDragging = false;
        SuppressUndo = true;
        currentEventData = null;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        Revert();

        eventData.pointerDrag = null;
        eventData.dragging = false;
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.endDragHandler);
    }

    void Revert()
    {
        rectTransform.position = originalWorldPosition;
        if (DropArea.Instance != null)
        {
            DropArea.Instance.AddOccupant(rectTransform);
        }
    }

    void MoveTo(Vector2 screenPosition)
    {
        if (rectTransform == null) return;

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            rectTransform,
            screenPosition,
            uiCamera,
            out Vector3 worldPos);
        rectTransform.position = worldPos;
    }
}
