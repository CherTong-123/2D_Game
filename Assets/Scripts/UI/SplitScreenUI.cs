using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 分裂界面。**接在你现有的 Divid_Scene UI 上**，不自己建 Canvas。
///
/// 场景对应关系：
///   panelRoot     = DividScene          （Tab 开关整个界面）
///   gridArea      = Top_Left            （左侧网格区域，用来判断点击范围）
///   listContent   = Top_Right/Divid_Menu/Viewport/Content  （列表容器）
///   slotTemplate  = Content/Image       （槽位模板，运行时会被复制并隐藏原件）
///   detailButton  = Top_Right/Detail
///   exitButton    = Exit
///
/// 操作：
///   Tab / Esc      进入 / 退出
///   左键点右侧槽位  选中 / 取消选中
///   鼠标在左侧网格  显示预览投影
///   右键           旋转（仅方向性细胞）
///   左键           在预览处分裂
///
/// 美术接入：
///   - 槽位图标 = CellType.icon（没填就用占位色方块）
///   - 想做正式槽位时，把 slotTemplate 换成你做的槽位预制体即可
///   - 预览投影颜色在 previewValid / previewInvalid 里调
/// </summary>
public class SplitScreenUI : MonoBehaviour
{
    [Header("界面根节点")]
    [Tooltip("Tab 开关的根物体，一般是 DividScene")]
    public GameObject panelRoot;

    [Header("左侧网格区域")]
    [Tooltip("用来判断鼠标是否在网格区内的 UI 区域，一般是 Top_Left。留空则用下面的屏幕比例")]
    public RectTransform gridArea;
    [Tooltip("gridArea 留空时，鼠标横坐标小于这个屏幕比例才算在网格区（0.55 = 左 55%）。\n预览和点击都用它")]
    [Range(0.1f, 1f)] public float gridAreaScreenSplitX = 0.55f;
    [Tooltip("留空则用 Camera.main")]
    public Camera mainCamera;

    [Header("右侧细胞列表")]
    [Tooltip("列表容器，一般是 Content")]
    public RectTransform listContent;
    [Tooltip("槽位模板，一般是 Content/Image。运行时会隐藏原件并复制出多个")]
    public GameObject slotTemplate;
    [Tooltip("槽位里显示名字的文字（模板里没有的话会运行时自动创建）")]
    public Text slotLabelPrototype;

    [Header("按钮")]
    public Button detailButton;
    public Button exitButton;

    [Header("详情弹窗（可选）")]
    [Tooltip("点\"详细\"时显示的弹窗。留空则只在 Console 打印")]
    public GameObject detailPanel;
    public Text detailText;
    public Button detailCloseButton;

    [Header("背景")]
    [Tooltip("进入分裂界面时把这个背景图调淡，让下面的游戏世界透出来。一般是 DividScene/BG。留空则不动")]
    public Image backgroundDim;
    [Tooltip("打开时背景的 alpha（1 = 完全不透明，看不到游戏世界）")]
    [Range(0f, 1f)] public float backgroundAlphaWhenOpen = 0.25f;

    [Header("镜头")]
    [Tooltip("打开分裂界面时把相机整体挪动多少（世界单位）。\n用来把主细胞摆到左侧网格区的正中间。\nX 为正 → 相机右移 → 画面内容左移")]
    public Vector2 cameraOffsetWhenOpen = Vector2.zero;
    [Tooltip("打开时相机放大到多大（0 = 不动，用当前 orthographicSize）")]
    public float cameraSizeWhenOpen = 0f;

    [Header("数据")]
    public MicrobeController microbe;

    [Header("按键")]
    public KeyCode toggleKey = KeyCode.Tab;
    public KeyCode exitKey = KeyCode.Escape;
    public bool startOpen = false;

    [Header("外观（占位配色，后期换贴图）")]
    public Color slotNormal = new Color(1f, 1f, 1f, 0.10f);
    public Color slotSelected = new Color(0.30f, 0.70f, 1f, 0.55f);
    public Color previewValid = new Color(0.40f, 0.80f, 1f, 0.45f);
    public Color previewInvalid = new Color(1f, 0.30f, 0.30f, 0.45f);

    // ------------------------------------------------------------------

    private bool open;
    private int selectedIndex = -1;
    private int rotation;
    private string failReason = "";
    private bool lastValid;

    private readonly List<Image> slotImages = new List<Image>();
    private readonly List<GameObject> slotObjects = new List<GameObject>();

    private GameObject previewGO;
    private SpriteRenderer previewRenderer;
    private Canvas canvas;

    private CellType Selected =>
        (selectedIndex >= 0 && microbe != null && microbe.availableTypes != null
         && selectedIndex < microbe.availableTypes.Length)
        ? microbe.availableTypes[selectedIndex] : null;

    // ------------------------------------------------------------------

    void Awake()
    {
        if (microbe == null) microbe = FindObjectOfType<MicrobeController>();
        if (mainCamera == null) mainCamera = Camera.main;
        canvas = GetComponentInParent<Canvas>();

        BuildSlots();
        CreatePreview();
        WireButtons();
    }

    void Start()
    {
        SetOpen(startOpen);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) { SetOpen(!open); return; }
        if (!open) return;

        if (Input.GetKeyDown(exitKey)) { SetOpen(false); return; }

        // 右键旋转（方向性细胞）
        if (Input.GetMouseButtonDown(1) && Selected != null && Selected.divity)
            rotation = (rotation + 90) % 360;

        UpdatePreview();

        if (Input.GetMouseButtonDown(0))
        {
            bool inArea = IsPointerInGridArea();
            string selName = Selected != null ? Selected.displayName : "无";
            Debug.Log($"[Split] 左键 | 界面开={open} 选中={selName} 在网格区={inArea} 能量={(microbe != null ? microbe.energy : -1):F1}");

            if (inArea) TrySplitAtMouse();
        }
    }

    // ------------------------------------------------------------------
    // 开关
    // ------------------------------------------------------------------

    public void SetOpen(bool value)
    {
        bool wasOpen = open;
        open = value;

        if (panelRoot != null) panelRoot.SetActive(value);
        if (previewGO != null) previewGO.SetActive(value && selectedIndex >= 0);

        if (!value && detailPanel != null) detailPanel.SetActive(false);

        // 背景调淡：让下面的游戏世界透出来，否则 Screen Space Overlay 的 UI 会挡住细胞
        if (backgroundDim != null)
        {
            Color c = backgroundDim.color;
            c.a = value ? backgroundAlphaWhenOpen : backgroundAlphaWhenOpen + 0.6f;
            backgroundDim.color = c;
        }

        // 镜头：打开时偏移到左侧网格区，关闭时还原
        if (value && !wasOpen)  ApplyCameraOpen();
        if (!value && wasOpen)  RestoreCamera();

        if (value && microbe != null && microbe.Grid != null)
            microbe.RefreshAll();
    }

    private Vector3 savedCamPos;
    private float savedCamSize;
    private bool camSaved;

    void ApplyCameraOpen()
    {
        Camera cam = mainCamera;
        if (cam == null) return;

        if (!camSaved)
        {
            savedCamPos = cam.transform.position;
            savedCamSize = cam.orthographicSize;
            camSaved = true;
        }

        cam.transform.position = savedCamPos + (Vector3)cameraOffsetWhenOpen;
        if (cameraSizeWhenOpen > 0.01f) cam.orthographicSize = cameraSizeWhenOpen;
    }

    void RestoreCamera()
    {
        Camera cam = mainCamera;
        if (cam == null || !camSaved) return;

        cam.transform.position = savedCamPos;
        cam.orthographicSize = savedCamSize;
        camSaved = false;
    }

    // ------------------------------------------------------------------
    // 右侧列表
    // ------------------------------------------------------------------

    void BuildSlots()
    {
        if (listContent == null || slotTemplate == null || microbe == null) return;

        // 模板原件隐藏，只当样板
        slotTemplate.SetActive(false);

        if (microbe.availableTypes == null) return;

        for (int i = 0; i < microbe.availableTypes.Length; i++)
        {
            CellType type = microbe.availableTypes[i];
            if (type == null) continue;

            GameObject slot = Instantiate(slotTemplate, listContent);
            slot.name = $"Slot_{type.id}";
            slot.SetActive(true);

            // 背景图
            Image bg = slot.GetComponent<Image>();
            if (bg == null) bg = slot.AddComponent<Image>();
            bg.color = slotNormal;

            // 图标：有 icon 用 icon，否则用占位色
            Transform iconT = slot.transform.Find("Icon");
            Image iconImg;
            if (iconT == null)
            {
                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(slot.transform, false);
                var rt = iconGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(8f, 0f);
                rt.sizeDelta = new Vector2(48f, 48f);
                iconImg = iconGO.GetComponent<Image>();
            }
            else iconImg = iconT.GetComponent<Image>();

            if (iconImg != null)
            {
                iconImg.sprite = type.icon != null ? type.icon : MicrobeController.PlaceholderSprite();
                iconImg.color = type.icon != null ? Color.white : type.placeholderColor;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            // 名字
            Text label = slot.GetComponentInChildren<Text>();
            if (label == null && slotLabelPrototype != null)
                label = Instantiate(slotLabelPrototype, slot.transform);
            if (label == null)
            {
                var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
                textGO.transform.SetParent(slot.transform, false);
                var rt = textGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(64f, 2f);
                rt.offsetMax = new Vector2(-8f, -2f);
                label = textGO.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            label.text = type.displayName;
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            // 点击
            Button btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.AddComponent<Button>();
            btn.targetGraphic = bg;
            int captured = i;
            btn.onClick.AddListener(() => OnSlotClicked(captured));

            slotImages.Add(bg);
            slotObjects.Add(slot);
        }
    }

    void OnSlotClicked(int index)
    {
        selectedIndex = (selectedIndex == index) ? -1 : index;   // 再点一次取消
        rotation = 0;

        for (int i = 0; i < slotImages.Count; i++)
        {
            if (slotImages[i] == null) continue;
            slotImages[i].color = (i == selectedIndex) ? slotSelected : slotNormal;
        }

        if (previewGO != null) previewGO.SetActive(selectedIndex >= 0);

        string nm = microbe != null && microbe.availableTypes != null && index < microbe.availableTypes.Length
                    ? microbe.availableTypes[index].displayName : "?";
        Debug.Log($"[Split] 点击槽位 {index}（{nm}）→ 当前选中 = {(selectedIndex == -1 ? "无" : selectedIndex.ToString())}");
    }

    // ------------------------------------------------------------------
    // 预览
    // ------------------------------------------------------------------

    void CreatePreview()
    {
        previewGO = new GameObject("SplitPreview");
        previewRenderer = previewGO.AddComponent<SpriteRenderer>();
        previewRenderer.sprite = MicrobeController.PlaceholderSprite();
        previewRenderer.sortingOrder = 5000;
        previewGO.SetActive(false);
    }

    void UpdatePreview()
    {
        CellType type = Selected;

        if (type == null)
        {
            if (previewGO != null) previewGO.SetActive(false);
            return;
        }

        // 鼠标在右侧面板上时不显示预览，也不允许放置
        if (!IsPointerInGridArea())
        {
            if (previewGO != null) previewGO.SetActive(false);
            return;
        }

        previewGO.SetActive(true);

        Vector2Int origin = MouseGridOrigin(type);
        Vector2Int size = type.FootprintSize(rotation);

        bool ok = microbe.Grid.CanPlace(type, origin, rotation, out failReason)
                  && microbe.energy >= type.cost;

        if (!ok && microbe.energy < type.cost) failReason = "能量不足";
        lastValid = ok;

        Vector2 center = microbe.Grid.GridToWorld(origin)
                       + new Vector2((size.x - 1) * CellGrid.CellSize * 0.5f,
                                     (size.y - 1) * CellGrid.CellSize * 0.5f);

        previewGO.transform.position = center;
        previewGO.transform.localScale = new Vector3(size.x * CellGrid.CellSize,
                                                     size.y * CellGrid.CellSize, 1f);
        previewRenderer.color = ok ? previewValid : previewInvalid;
    }

    /// <summary>鼠标所在格 → 以该格为中心的细胞锚点</summary>
    Vector2Int MouseGridOrigin(CellType type)
    {
        Vector2 world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int g = microbe.Grid.WorldToGrid(world);
        Vector2Int size = type.FootprintSize(rotation);
        return new Vector2Int(g.x - size.x / 2, g.y - size.y / 2);
    }

    bool IsPointerInGridArea()
    {
        // 1) 配了 UI 区域就用它
        if (gridArea != null)
        {
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                         ? canvas.worldCamera : null;

            return RectTransformUtility.RectangleContainsScreenPoint(gridArea, Input.mousePosition, cam);
        }

        // 2) 没配就用屏幕比例：只看左侧那一片，右侧面板不参与
        return Input.mousePosition.x <= Screen.width * gridAreaScreenSplitX;
    }

    void TrySplitAtMouse()
    {
        CellType type = Selected;
        if (type == null) return;

        Vector2Int origin = MouseGridOrigin(type);

        if (microbe.TryPlace(type, origin, rotation, out string reason))
        {
            Debug.Log($"[Split] 放置 {type.displayName} @ {origin}");
            failReason = "";
        }
        else
        {
            failReason = reason;
            Debug.Log($"[Split] 失败：{reason}");
        }
    }

    // ------------------------------------------------------------------
    // 按钮
    // ------------------------------------------------------------------

    void WireButtons()
    {
        if (exitButton != null)
            exitButton.onClick.AddListener(() => SetOpen(false));

        if (detailButton != null)
            detailButton.onClick.AddListener(ShowDetail);

        if (detailCloseButton != null)
            detailCloseButton.onClick.AddListener(() =>
            {
                if (detailPanel != null) detailPanel.SetActive(false);
            });

        if (detailPanel != null) detailPanel.SetActive(false);
    }

    void ShowDetail()
    {
        CellType type = Selected;
        if (type == null)
        {
            Debug.Log("[Split] 请先在右侧选中一个细胞");
            return;
        }

        if (detailPanel != null)
        {
            detailPanel.SetActive(true);
            if (detailText != null)
            {
                detailText.text =
                    $"<size=24><b>{type.displayName}</b></size>\n\n" +
                    $"{type.description}\n\n" +
                    $"生命值：{type.hp}\n" +
                    $"细胞大小：{type.cellsize} 格\n" +
                    $"可附着：{(type.attach ? "是" : "否")}\n" +
                    $"方向性：{(type.divity ? "是" : "否")}\n" +
                    $"通透性：{type.pmt}\n" +
                    $"花费：{type.cost}";
            }
        }
        else
        {
            Debug.Log($"[Split] {type.displayName}：{type.description} | HP {type.hp} | {type.cellsize}格 | 花费 {type.cost}");
        }
    }

    // ------------------------------------------------------------------
    // 调试信息（可选：挂到界面上显示）
    // ------------------------------------------------------------------

    public string StatusText()
    {
        CellType t = Selected;
        if (t == null) return "未选择细胞";
        if (lastValid) return $"可分裂：{t.displayName}（{t.cost} 能量）";
        return $"无法分裂：{failReason}";
    }
}
