using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DragImage : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static bool IsDragging { get; private set; }
    public static bool SuppressUndo { get; private set; }

    [Header("实体照片")]
    [SerializeField] Sprite entityPhotoSprite;
    [SerializeField] string entityName;
    [SerializeField] Vector2Int gridSize = Vector2Int.one; // 占用的格子数：宽×高，如 1×1、2×3

    public string EntityName =>
        string.IsNullOrEmpty(entityName) ? gameObject.name : entityName;

    public RectTransform GhostRect => ghostRect;

    Canvas canvas;
    Camera uiCamera;
    CanvasGroup canvasGroup;

    RectTransform ghostRect;
    EntityPhoto ghostPhoto;

    PointerEventData currentEventData;
    bool dragging;
    bool canceled;
    bool pendingConsume;

    void Awake()
    {
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

    void OnDisable()
    {
        if (!dragging) return;
        dragging = false;
        IsDragging = false;
        currentEventData = null;
        DestroyGhost();
        SetCellVisual(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        canceled = false;
        pendingConsume = false;
        currentEventData = eventData;
        IsDragging = true;

        // 原列表项留在原位，变淡并暂时不参与射线
        SetCellVisual(true);

        SpawnGhost(eventData.position);
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

        MoveGhost(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        IsDragging = false;
        currentEventData = null;

        if (canceled)
        {
            canceled = false;
            return;
        }

        if (pendingConsume)
        {
            pendingConsume = false;
            gameObject.SetActive(false); // 原列表项失活，幽灵图留在悬浮位置
            return;
        }

        // 未落入有效区域：销毁幽灵并恢复列表项
        DestroyGhost();
        SetCellVisual(false);
    }

    public void MarkConsumed()
    {
        pendingConsume = true;
    }

    public void Restore()
    {
        canceled = false;
        dragging = false;
        IsDragging = false;
        pendingConsume = false;
        currentEventData = null;

        DestroyGhost();
        gameObject.SetActive(true);
        SetCellVisual(false);
    }

    void CancelDrag(PointerEventData eventData)
    {
        if (eventData == null) return;

        canceled = true;
        dragging = false;
        IsDragging = false;
        SuppressUndo = true;
        currentEventData = null;

        DestroyGhost();
        SetCellVisual(false);

        eventData.pointerDrag = null;
        eventData.dragging = false;
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.endDragHandler);
    }

    void SpawnGhost(Vector2 screenPosition)
    {
        if (entityPhotoSprite == null || canvas == null) return;

        GameObject ghostGo = new GameObject("EntityPhotoGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        // 放在网格内，关闭分裂界面时跟随隐藏。
        ghostGo.transform.SetParent(DropArea.Instance != null ? DropArea.Instance.transform : canvas.transform, false);
        ghostGo.AddComponent<LayoutElement>().ignoreLayout = true;

        Image ghostImage = ghostGo.GetComponent<Image>();
        ghostImage.sprite = entityPhotoSprite;
        ghostImage.raycastTarget = false;

        ghostRect = ghostGo.GetComponent<RectTransform>();
        ghostRect.pivot = new Vector2(0.5f, 0.5f);
        ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRect.anchorMax = new Vector2(0.5f, 0.5f);

        if (DropArea.Instance != null)
        {
            GridLayoutGroup gridLayout = DropArea.Instance.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                int w = Mathf.Max(1, gridSize.x);
                int h = Mathf.Max(1, gridSize.y);
                ghostRect.sizeDelta = new Vector2(
                    w * (gridLayout.cellSize.x + gridLayout.spacing.x),
                    h * (gridLayout.cellSize.y + gridLayout.spacing.y));
            }
        }

        ghostGo.transform.SetAsLastSibling();

        CanvasGroup ghostGroup = ghostGo.GetComponent<CanvasGroup>();
        if (ghostGroup == null)
        {
            ghostGroup = ghostGo.AddComponent<CanvasGroup>();
        }
        ghostGroup.blocksRaycasts = false;

        ghostPhoto = ghostGo.GetComponent<EntityPhoto>();
        if (ghostPhoto == null)
        {
            ghostPhoto = ghostGo.AddComponent<EntityPhoto>();
        }
        ghostPhoto.PhotoName = EntityName;

        MoveGhost(screenPosition);
    }

    void MoveGhost(Vector2 screenPosition)
    {
        if (ghostRect == null) return;

        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            ghostRect,
            screenPosition,
            uiCamera,
            out Vector3 worldPos);
        ghostRect.position = worldPos;
    }

    public bool IsGhostFullyInside(RectTransform area)
    {
        if (ghostRect == null || area == null) return false;

        Vector3[] corners = new Vector3[4];
        ghostRect.GetWorldCorners(corners);

        Vector3[] areaCorners = new Vector3[4];
        area.GetWorldCorners(areaCorners);

        float minX = Mathf.Min(areaCorners[0].x, areaCorners[1].x, areaCorners[2].x, areaCorners[3].x);
        float maxX = Mathf.Max(areaCorners[0].x, areaCorners[1].x, areaCorners[2].x, areaCorners[3].x);
        float minY = Mathf.Min(areaCorners[0].y, areaCorners[1].y, areaCorners[2].y, areaCorners[3].y);
        float maxY = Mathf.Max(areaCorners[0].y, areaCorners[1].y, areaCorners[2].y, areaCorners[3].y);

        for (int i = 0; i < 4; i++)
        {
            Vector3 c = corners[i];
            if (c.x < minX || c.x > maxX || c.y < minY || c.y > maxY)
            {
                return false;
            }
        }

        return true;
    }

    void DestroyGhost()
    {
        if (ghostPhoto != null)
        {
            Destroy(ghostPhoto.gameObject);
            ghostPhoto = null;
        }
        ghostRect = null;
    }

    void SetCellVisual(bool draggingState)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = draggingState ? 0.5f : 1f;
        canvasGroup.blocksRaycasts = !draggingState;
    }
}
