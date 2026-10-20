// ============================================================================
// SplitScreenUI.cs：分裂选择、拖入预览、旋转、回收、镜头和详情。
// 控制器挂在常驻 Canvas 上，panelRoot 可以单独关闭。
// ============================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SplitScreenUI : MonoBehaviour
{
    public GameObject panelRoot;
    public RectTransform gridArea, listContent;
    public GameObject slotTemplate;
    public Button exitButton;
    public Camera mainCamera;
    public MicrobeController microbe;
    public TMP_FontAsset font;
    public bool IsOpen { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    private CellType Selected => SelectedIndex < 0 ? null : microbe.availableTypes[SelectedIndex];
    private readonly List<Image> slots = new List<Image>();
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private SpriteRenderer preview;
    private SpriteRenderer previewArrow;
    private GameObject gridRoot, detailPanel;
    private TMP_Text detailText, status;
    private CameraFollow follow;
    private Vector3 savedPosition;
    private float savedSize, nextRotation;
    private bool followWasEnabled;
    private int rotation;
    private PlacedCell selectedCell;
    private string reason = "";
    private Material gridMaterial;

    /// <summary>场景入口绑定完引用后调用一次。</summary>
    public void Initialize()
    {
        follow = mainCamera.GetComponent<CameraFollow>();
        BuildSlots();
        var previewObject = new GameObject("SplitPreview");
        preview = previewObject.AddComponent<SpriteRenderer>();
        preview.sortingOrder = 2000;
        previewArrow = new GameObject("Facing").AddComponent<SpriteRenderer>();
        previewArrow.transform.SetParent(preview.transform, false);
        previewArrow.sprite = MicrobeController.DirectionSprite();
        previewArrow.sortingOrder = 2001;
        previewObject.SetActive(false);
        BuildGrid();
        status = GameUIFactory.Text(panelRoot.transform, "SplitStatus", font, "", 20);
        GameUIFactory.Rect(status.rectTransform, new Vector2(0, 0), new Vector2(0.65f, 0), new Vector2(16, 12), new Vector2(-16, 90));
        BuildDetail();
        exitButton.onClick.AddListener(() => SetOpen(false));
        panelRoot.SetActive(false);
    }
    public void SetOpen(bool value)
    {
        if (value == IsOpen || (value && microbe.IsDead)) return;
        IsOpen = value;
        Intera_Get.Instance.SetSplitPaused(value);
        panelRoot.SetActive(value);
        gridRoot.SetActive(value);
        preview.gameObject.SetActive(false);
        detailPanel.SetActive(false);
        selectedCell = null;
        SelectedIndex = -1;
        UpdateSelection();
        if (value)
        {
            savedPosition = mainCamera.transform.position;
            savedSize = mainCamera.orthographicSize;
            followWasEnabled = follow != null && follow.enabled;
            if (follow != null) follow.enabled = false;
            mainCamera.orthographicSize = 28f;
        }
        else
        {
            mainCamera.transform.position = savedPosition;
            mainCamera.orthographicSize = savedSize;
            if (follow != null) follow.enabled = followWasEnabled;
        }
    }
    private void Update()
    {
        if (!IsOpen) return;
        bool inGrid = PointerInGrid() && !detailPanel.activeSelf;

        // WASD 平移在分裂期间始终生效，作用区域覆盖整个画面，必然包含玩家。
        Vector2 pan = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        mainCamera.transform.position += (Vector3)(pan * mainCamera.orthographicSize * Time.unscaledDeltaTime);

        if (inGrid)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            mainCamera.orthographicSize = Mathf.Clamp(mainCamera.orthographicSize - scroll * 12f, 10f, 65f);
            if (Input.GetMouseButtonDown(1)) { selectedCell = null; reason = ""; }
            if (Selected != null && Input.GetKeyDown(KeyCode.R)) rotation = (rotation + 90) % 360;
            if (Input.GetMouseButtonDown(0)) ClickGrid();
            if (Selected == null && selectedCell != null && Input.GetMouseButton(0) && Time.unscaledTime >= nextRotation)
            {
                microbe.TryRotate(selectedCell, out reason);
                nextRotation = Time.unscaledTime + 0.4f;
            }
        }
        UpdatePreview(inGrid);
        UpdateGrid();
        status.text = $"能量 {microbe.energy:F0} / {microbe.energyLimit:F0}    {reason}\n" +
            (selectedCell != null ? "点击主细胞回收；按住左键顺时针旋转；右键取消" : "选择细胞后点击网格放置；R 转向；WASD 平移；滚轮缩放；Tab / Esc 退出");
    }
    private void ClickGrid()
    {
        Vector2Int grid = MouseGrid();
        if (Selected != null)
        {
            Vector2Int anchor = grid - Selected.FootprintSize(rotation) / 2;
            if (microbe.TryPlace(Selected, anchor, rotation, out reason)) reason = "放置成功";
            return;
        }
        var hit = microbe.Grid.CellAt(grid);
        if (hit != null && hit.isMain)
        {
            if (selectedCell != null) { microbe.Recycle(selectedCell); reason = "已回收并返还能量"; }
            selectedCell = null;
        }
        else selectedCell = hit;
        nextRotation = Time.unscaledTime + 0.4f;
    }
    public void SelectType(int index, bool toggle = true)
    {
        SelectedIndex = toggle && SelectedIndex == index ? -1 : index;
        selectedCell = null;
        rotation = 0;
        reason = "";
        UpdateSelection();
    }
    private void UpdateSelection()
    {
        for (int i = 0; i < slots.Count; i++) slots[i].color = i == SelectedIndex ? new Color(0.4f, 0.8f, 1f) : Color.white;
    }
    private Vector2Int MouseGrid() => microbe.Grid.WorldToGrid(mainCamera.ScreenToWorldPoint(Input.mousePosition));
    private bool PointerInGrid()
    {
        Vector2 mouse = Input.mousePosition;
        if (mouse.x < 0f || mouse.x > Screen.width || mouse.y < 0f || mouse.y > Screen.height) return false;
        return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
    }
    private void UpdatePreview(bool inGrid)
    {
        if (Selected == null)
        {
            bool show = selectedCell != null && microbe.Contains(selectedCell);
            preview.gameObject.SetActive(show);
            if (!show) return;
            ShowPreview(selectedCell.type, selectedCell.anchor, selectedCell.rotation, new Color(0.3f, 0.85f, 1f, 0.45f));
            return;
        }
        preview.gameObject.SetActive(inGrid);
        if (!inGrid) return;
        Vector2Int anchor = MouseGrid() - Selected.FootprintSize(rotation) / 2;
        bool valid = microbe.Grid.CanPlace(Selected, anchor, rotation, out reason);
        if (microbe.energy < Selected.cost) { valid = false; reason = "能量不足"; }
        ShowPreview(Selected, anchor, rotation, valid ? new Color(0.35f, 0.8f, 1f, 0.5f) : new Color(1f, 0.25f, 0.25f, 0.55f));
    }
    private void ShowPreview(CellType type, Vector2Int anchor, int facing, Color color)
    {
        preview.sprite = type.body != null ? type.body : MicrobeController.PlaceholderSprite();
        preview.color = color;
        preview.transform.position = microbe.Grid.Center(type, anchor, facing);
        preview.transform.rotation = Quaternion.Euler(0, 0, microbe.Grid.angle - facing);
        Vector2 size = type.FootprintSize();
        preview.transform.localScale = new Vector3(size.x * CellGrid.CellSize * 1.08f / preview.sprite.bounds.size.x,
            size.y * CellGrid.CellSize * 1.08f / preview.sprite.bounds.size.y, 1);
        previewArrow.gameObject.SetActive(type.divity);
        previewArrow.color = new Color(color.r, color.g, color.b, 0.9f);
        previewArrow.transform.localPosition = new Vector3(0, preview.sprite.bounds.size.y * 0.3f, 0);
        previewArrow.transform.localScale = new Vector3(1.4f / preview.transform.lossyScale.x, 1.4f / preview.transform.lossyScale.y, 1);
    }

    // ================================================================
    // 列表、详情和网格显示
    // ================================================================
    private void BuildSlots()
    {
        for (int i = 0; i < microbe.availableTypes.Length; i++)
        {
            var type = microbe.availableTypes[i];
            var slot = Instantiate(slotTemplate, listContent);
            slot.name = "Slot_" + type.id;
            foreach (var legacy in slot.GetComponents<DragImage>()) { legacy.enabled = false; Destroy(legacy); }
            foreach (var legacy in slot.GetComponents<ImageHover>()) { legacy.enabled = false; Destroy(legacy); }
            foreach (Transform child in slot.transform) child.gameObject.SetActive(false);
            var layout = slot.AddComponent<LayoutElement>();
            layout.preferredHeight = 86;
            var background = slot.GetComponent<Image>();
            slots.Add(background);
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(slot.transform, false);
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = type.icon != null ? type.icon : MicrobeController.PlaceholderSprite();
            icon.color = type.icon != null ? Color.white : type.placeholderColor;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            GameUIFactory.Rect(icon.rectTransform, Vector2.zero, new Vector2(0, 1), new Vector2(8, 8), new Vector2(70, -8));
            var label = GameUIFactory.Text(slot.transform, "Label", font, $"{type.displayName}\n<size=16>{type.cost} 能量 · {type.hp} 生命</size>", 22);
            label.color = new Color(0.1f, 0.15f, 0.2f);
            GameUIFactory.Rect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(80, 4), new Vector2(-8, -4));
            slot.AddComponent<CellSlotInput>().Initialize(this, i);
        }
    }
    private void BuildDetail()
    {
        detailPanel = GameUIFactory.Panel(transform, "CellDetail", new Color(0.06f, 0.12f, 0.18f, 0.98f));
        var rect = detailPanel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.sizeDelta = new Vector2(520, 540);
        detailText = GameUIFactory.Text(detailPanel.transform, "Description", font, "", 23);
        GameUIFactory.Rect(detailText.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 68), new Vector2(-24, -24));
        var close = GameUIFactory.Button(detailPanel.transform, "Close", font, "关闭");
        GameUIFactory.Rect(close.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), new Vector2(24, 16), new Vector2(-24, 58));
        close.onClick.AddListener(() => detailPanel.SetActive(false));
        detailPanel.SetActive(false);
    }
    public void ShowDetail(int index)
    {
        var type = microbe.availableTypes[index];
        detailText.text = $"<b>{type.displayName}</b>\n\n{type.description}\n\n生命：{type.hp}\n占格：{type.FootprintSize().x} × {type.FootprintSize().y}\n分裂消耗：{type.cost}\n可附着：{(type.attach ? "是" : "否")}\n通透性：{new[] { "无", "弱", "强" }[type.pmt]}";
        detailPanel.SetActive(true);
        detailPanel.transform.SetAsLastSibling();
    }
    private void BuildGrid()
    {
        gridRoot = new GameObject("SplitGrid");
        gridMaterial = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < 82; i++)
        {
            var go = new GameObject("GridLine");
            go.transform.SetParent(gridRoot.transform);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = gridMaterial;
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.035f;
            line.startColor = line.endColor = new Color(0.35f, 0.7f, 0.8f, 0.35f);
            line.sortingOrder = -10;
            lines.Add(line);
        }
        gridRoot.SetActive(false);
    }
    private void UpdateGrid()
    {
        Vector2Int center = microbe.Grid.WorldToGrid(mainCamera.transform.position);
        for (int i = 0; i <= 40; i++)
        {
            Vector2 a = new Vector2(center.x - 20 + i, center.y - 20) * CellGrid.CellSize;
            Vector2 b = a + Vector2.up * 40 * CellGrid.CellSize;
            lines[i].SetPosition(0, microbe.Grid.origin + CellGrid.Rotate(a, microbe.Grid.angle));
            lines[i].SetPosition(1, microbe.Grid.origin + CellGrid.Rotate(b, microbe.Grid.angle));
            a = new Vector2(center.x - 20, center.y - 20 + i) * CellGrid.CellSize;
            b = a + Vector2.right * 40 * CellGrid.CellSize;
            lines[41 + i].SetPosition(0, microbe.Grid.origin + CellGrid.Rotate(a, microbe.Grid.angle));
            lines[41 + i].SetPosition(1, microbe.Grid.origin + CellGrid.Rotate(b, microbe.Grid.angle));
        }
    }
    private void OnDestroy()
    {
        if (preview != null) Destroy(preview.gameObject);
        if (gridRoot != null) Destroy(gridRoot);
        if (gridMaterial != null) Destroy(gridMaterial);
        if (IsOpen) Intera_Get.Instance.SetSplitPaused(false);
    }
}
