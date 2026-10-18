using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DropArea : MonoBehaviour, IDropHandler
{
    public static DropArea Instance { get; private set; }

    Canvas canvas;
    RectTransform rectTransform;
    Image highlight;

    readonly List<RectTransform> occupants = new List<RectTransform>();
    readonly Stack<Placement> undoStack = new Stack<Placement>();

    void Awake()
    {
        Instance = this;
        canvas = GetComponentInParent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
        CreateHighlight();

        RectTransform mainCell = FindMainCell();
        if (mainCell != null)
        {
            occupants.Add(mainCell);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        bool hovering = DragImage.IsDragging && IsPointerOverGrid();
        if (highlight != null)
        {
            highlight.enabled = hovering;
        }

        // 未在拖动时右键：撤销最近一次成功放置
        if (Input.GetMouseButtonDown(1)
            && !DragImage.IsDragging
            && !DragImage.SuppressUndo)
        {
            UndoLastPlacement();
        }
    }

    bool IsPointerOverGrid()
    {
        Camera cam = canvas != null ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, cam);
    }

    void CreateHighlight()
    {
        GameObject go = new GameObject("DragHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, false);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        highlight = go.GetComponent<Image>();
        highlight.color = new Color(0.3f, 1f, 1f, 0.18f);
        highlight.raycastTarget = false;
        highlight.enabled = false;
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject dragged = eventData.pointerDrag;
        if (dragged == null) return;

        DragImage dragImage = dragged.GetComponent<DragImage>();
        if (dragImage == null) return;

        RectTransform ghost = dragImage.GhostRect;
        if (ghost == null) return;

        if (!dragImage.IsGhostFullyInside(rectTransform))
        {
            Debug.Log($"超出 Grid 边界，无法放置：{dragImage.EntityName}");
            return;
        }

        if (OverlapsOccupant(ghost))
        {
            Debug.Log($"与已有细胞重叠，无法放置：{dragImage.EntityName}");
            return;
        }

        Debug.Log($"识别到细胞：{dragImage.EntityName}，放置成功");
        dragImage.MarkConsumed();
        occupants.Add(ghost);
        undoStack.Push(new Placement { cell = dragImage, ghost = ghost });
    }

    RectTransform FindMainCell()
    {
        if (transform.parent != null)
        {
            Transform t = transform.parent.Find("MainCell");
            if (t != null)
            {
                return t as RectTransform;
            }
        }

        GameObject go = GameObject.Find("MainCell");
        return go != null ? go.GetComponent<RectTransform>() : null;
    }

    bool OverlapsOccupant(RectTransform rect)
    {
        for (int i = 0; i < occupants.Count; i++)
        {
            if (occupants[i] == null) continue;
            if (RectsOverlap(rect, occupants[i])) return true;
        }
        return false;
    }

    bool RectsOverlap(RectTransform a, RectTransform b)
    {
        Vector3[] ac = new Vector3[4];
        a.GetWorldCorners(ac);

        Vector3[] bc = new Vector3[4];
        b.GetWorldCorners(bc);

        float aMinX = Mathf.Min(ac[0].x, ac[1].x, ac[2].x, ac[3].x);
        float aMaxX = Mathf.Max(ac[0].x, ac[1].x, ac[2].x, ac[3].x);
        float aMinY = Mathf.Min(ac[0].y, ac[1].y, ac[2].y, ac[3].y);
        float aMaxY = Mathf.Max(ac[0].y, ac[1].y, ac[2].y, ac[3].y);

        float bMinX = Mathf.Min(bc[0].x, bc[1].x, bc[2].x, bc[3].x);
        float bMaxX = Mathf.Max(bc[0].x, bc[1].x, bc[2].x, bc[3].x);
        float bMinY = Mathf.Min(bc[0].y, bc[1].y, bc[2].y, bc[3].y);
        float bMaxY = Mathf.Max(bc[0].y, bc[1].y, bc[2].y, bc[3].y);

        return aMinX < bMaxX && aMaxX > bMinX && aMinY < bMaxY && aMaxY > bMinY;
    }

    void UndoLastPlacement()
    {
        if (undoStack.Count == 0) return;

        Placement placement = undoStack.Pop();
        if (placement.ghost != null)
        {
            occupants.Remove(placement.ghost);
        }

        if (placement.cell != null)
        {
            placement.cell.Restore();
            Debug.Log($"已撤销一次拖动：{placement.cell.EntityName}");
        }
    }

    class Placement
    {
        public DragImage cell;
        public RectTransform ghost;
    }
}
