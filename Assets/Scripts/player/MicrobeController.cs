using System.Collections.Generic;
using System.Linq;                 // Grid.All.Contains(...) 需要它
using UnityEngine;

/// <summary>
/// 玩家的微生物（细胞团）。持有网格数据、能量、以及所有细胞的显示物体。
///
/// 职责边界：
///   - 规则判定交给 CellGrid
///   - 显示只是"数据 → GameObject"的同步
///   - 界面通过公开方法操作这里，不直接改数据
/// </summary>
public class MicrobeController : MonoBehaviour, INutrientEater
{
    [Header("细胞类型库")]
    [Tooltip("分裂界面右侧列表会用这个数组，加新细胞只在这里加")]
    public CellType[] availableTypes;

    [Header("能量")]
    [Tooltip("当前能量。开局 50")]
    public float energy = 50f;
    [Tooltip("能量上限。文档：100 + 100*储能细胞数（储能细胞还没做，先用 100）")]
    public float energyLimit = 100f;
    [Tooltip("每秒基础消耗。实际 = energyUse × 64/(63+细胞数) × 移动系数")]
    public float energyUse = 1f;
    [Tooltip("静止时的消耗系数（移动时为 1）。文档：移动消耗最多、静止最少")]
    [Range(0f, 1f)] public float idleCostFactor = 0.25f;
    [Tooltip("是否随时间掉能量。想暂停测试就取消勾选")]
    public bool drainEnergy = true;

    [Header("主细胞")]
    [Tooltip("主细胞直接用现有的 player 时，把 player 拖到这里。\n填了之后：不再生成主细胞的显示物体，网格原点每帧跟随它，整个细胞团一起移动。\n留空则自己生成一个主细胞显示物体（固定在世界上）")]
    public Transform mainCellBody;

    [Header("表现")]
    [Tooltip("细胞显示物体的父节点，留空就用自己")]
    public Transform cellRoot;
    [Tooltip("贴图的基础 sortingOrder，优先级会在这个基础上叠加")]
    public int baseSortingOrder = 0;

    [Header("起始")]
    [Tooltip("开局主细胞的类型")]
    public CellType mainCellType;
    [Tooltip("开局主细胞所在格")]
    public Vector2Int mainCellGrid = Vector2Int.zero;

    public CellGrid Grid { get; private set; }

    /// <summary>细胞数据变化时触发（分裂、回收、重算），界面可以订阅刷新</summary>
    public event System.Action Changed;

    private readonly Dictionary<PlacedCell, GameObject> views = new Dictionary<PlacedCell, GameObject>();
    private static Sprite whiteSprite;
    private Rigidbody2D mainRb;

    /// <summary>
    /// Main Cell Body 自己有没有贴图。
    /// player 有 SpriteRenderer → 主细胞就用它本身，不再生成。
    /// 测试用的空物体没有 → 生成一个占位方块，方便看到主细胞在哪。
    /// </summary>
    private bool MainBodyHasSprite =>
        mainCellBody != null && mainCellBody.GetComponent<SpriteRenderer>() != null;

    // ------------------------------------------------------------------

    void Awake()
    {
        Grid = new CellGrid();
        if (cellRoot == null) cellRoot = transform;
        if (mainCellBody == null) mainCellBody = transform;   // 默认主细胞就是自己
        mainRb = mainCellBody.GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (Grid.CellCount > 0) return;

        // 主细胞 = 玩家自己（mainCellBody）。
        // 网格里只是登记一条"主细胞数据"，用来算 attach / 尺寸 / 优先级，
        // 它的显示物体就是 player，不会额外生成。
        CellType mainType = mainCellType;

        if (mainType == null)
        {
            // 没配置就运行时造一个默认主细胞，保证"player 就是主细胞"零配置可用
            mainType = ScriptableObject.CreateInstance<CellType>();
            mainType.id = "main";
            mainType.displayName = "主细胞";
            mainType.hp = 100;
            mainType.cellsize = 1;
            mainType.pmt = 0;
            mainType.attach = true;
            mainType.cost = 0;
            mainType.placeholderColor = new Color(1f, 0.65f, 0.2f, 0.95f);

            Debug.Log("[Microbe] 未配置 Main Cell Type，已自动使用默认主细胞（1×1 / 可附着 / 花费 0）");
        }

        InitMain(mainType, mainCellGrid);
    }

    void Update()
    {
        // 主细胞 = 玩家物体时，网格原点跟着玩家走，整个细胞团一起移动
        if (mainCellBody != null && Grid != null && Grid.Main != null)
            SyncOriginToMainBody();

        if (!drainEnergy) return;

        // 和原来 em 一样的算法：移动时消耗最多，静止时最少
        float use = energyUse * (64f / (63f + Mathf.Max(1, Grid.CellCount)));
        bool moving = mainRb != null && mainRb.velocity.magnitude > 0.01f;
        energy -= use * (moving ? 1f : idleCostFactor) * Time.deltaTime;

        energy = Mathf.Clamp(energy, 0f, energyLimit);
    }

    /// <summary>吃到营养球时由 Nutrient 调用</summary>
    public void GainEnergy(int amount)
    {
        energy = Mathf.Min(energy + amount, energyLimit);
        Debug.Log($"[Microbe] 吃到 +{amount}，能量 {energy:F1}/{energyLimit:F0}，细胞数 {Grid.CellCount}");
    }

    /// <summary>把网格原点对齐到主细胞位置</summary>
    void SyncOriginToMainBody()
    {
        Vector2 desired = (Vector2)mainCellBody.position
                        - CellGrid.SquareLocal(Grid.Main.anchor);

        if ((desired - Grid.origin).sqrMagnitude > 0.000001f)
        {
            Grid.origin = desired;
            RepositionViews();
        }
    }

    /// <summary>只更新位置，不重建物体（每帧调用也很便宜）</summary>
    void RepositionViews()
    {
        foreach (var c in Grid.All)
        {
            if (c.isMain && MainBodyHasSprite) continue;
            if (!views.TryGetValue(c, out var go) || go == null) continue;

            Vector2Int size = c.type.FootprintSize(c.rotation);
            go.transform.position = Grid.GridToWorld(c.anchor)
                                  + new Vector2((size.x - 1) * CellGrid.CellSize * 0.5f,
                                                (size.y - 1) * CellGrid.CellSize * 0.5f);
        }
    }

    // ------------------------------------------------------------------
    // 对外操作
    // ------------------------------------------------------------------

    /// <summary>放下主细胞（开局调用）</summary>
    public void InitMain(CellType type, Vector2Int origin)
    {
        Grid.Place(type, origin, 0, asMain: true);
        RefreshAll();
    }

    /// <summary>
    /// 尝试分裂：先查能量，再查网格规则。
    /// 返回 false 时 reason 可以直接显示在界面上（预览变红的判断也用它）。
    /// </summary>
    public bool TryPlace(CellType type, Vector2Int origin, int rotation, out string reason)
    {
        if (type == null) { reason = "未选择细胞"; return false; }

        if (Grid.CanPlace(type, origin, rotation, out reason) == false)
            return false;

        if (energy < type.cost)
        {
            reason = "能量不足";
            return false;
        }

        energy -= type.cost;
        Grid.Place(type, origin, rotation);
        RefreshAll();
        return true;
    }

    /// <summary>回收一个细胞，返还它的花费</summary>
    public void Recycle(PlacedCell cell)
    {
        if (cell == null) return;

        // 主细胞不能回收
        if (cell.isMain) return;

        int refund = cell.type.cost;
        var removedSquares = Grid.Remove(cell);

        energy = Mathf.Min(energy + refund, energyLimit);

        // 文档：若有细胞的位置因此不可用，则一同回收
        RecycleInvalidCells();

        RefreshAll();
    }

    /// <summary>
    /// 回收"悬空"的细胞：如果不与任何 attach 细胞相邻就一起回收。
    /// </summary>
    private void RecycleInvalidCells()
    {
        bool changed = true;
        while (changed)
        {
            changed = false;

            List<PlacedCell> snapshot = new List<PlacedCell>(Grid.All);
            foreach (var c in snapshot)
            {
                if (c.isMain) continue;

                bool attached = false;
                foreach (var s in c.squares)
                {
                    foreach (var n in CellGrid.Neighbours(s))
                    {
                        var other = Grid.CellAt(n);
                        if (other != null && other != c && other.type.attach)
                        {
                            attached = true;
                            break;
                        }
                    }
                    if (attached) break;
                }

                if (!attached)
                {
                    energy = Mathf.Min(energy + c.type.cost, energyLimit);
                    Grid.Remove(c);
                    changed = true;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // 显示同步
    // ------------------------------------------------------------------

    /// <summary>数据和显示全部重建一次</summary>
    public void RefreshAll()
    {
        Grid.RecomputeLogic();

        // 删掉已经不存在的细胞
        var dead = new List<PlacedCell>();
        foreach (var kv in views)
            if (!Grid.All.Contains(kv.Key)) dead.Add(kv.Key);

        foreach (var k in dead)
        {
            if (views[k] != null) Destroy(views[k]);
            views.Remove(k);
        }

        // 创建/更新
        foreach (var c in Grid.All)
        {
            // 主细胞：只有当 Main Cell Body 自己有 SpriteRenderer（比如 player）时才跳过，
            // 否则给它生成一个占位显示物体，方便在没贴图的测试场景里看到主细胞在哪
            if (c.isMain && MainBodyHasSprite) continue;

            if (!views.TryGetValue(c, out var go) || go == null)
            {
                go = CreateView(c);
                views[c] = go;
            }
            UpdateView(c, go);
        }

        Changed?.Invoke();
    }

    private GameObject CreateView(PlacedCell c)
    {
        var go = new GameObject($"Cell_{c.type.id}");
        go.transform.SetParent(cellRoot, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = c.type.body != null ? c.type.body : PlaceholderSprite();
        sr.color = c.type.body != null ? c.type.tint : c.type.placeholderColor;

        return go;
    }

    private void UpdateView(PlacedCell c, GameObject go)
    {
        Vector2Int size = c.type.FootprintSize(c.rotation);

        // 位置：外接矩形的中心
        Vector2 center = Grid.GridToWorld(c.anchor)
                       + new Vector2((size.x - 1) * CellGrid.CellSize * 0.5f,
                                     (size.y - 1) * CellGrid.CellSize * 0.5f);
        go.transform.position = center;

        // 缩放：让贴图铺满整个 footprint（占位方块是 1x1 单位）
        go.transform.localScale = new Vector3(size.x * CellGrid.CellSize,
                                              size.y * CellGrid.CellSize, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = baseSortingOrder + Grid.SortingOrderOf(c);
            // 沟通没连上时暗一点，方便肉眼看出 link 传播结果
            Color tint = c.type.body != null ? c.type.tint : c.type.placeholderColor;
            if (!c.link) tint *= new Color(0.6f, 0.6f, 0.6f, 1f);
            sr.color = tint;
        }
    }

    /// <summary>运行期生成一个 1x1 白色方块贴图（美术资源到位后就不用了）</summary>
    public static Sprite PlaceholderSprite()
    {
        if (whiteSprite != null) return whiteSprite;

        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        var px = new Color32[16 * 16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();

        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        whiteSprite.name = "PlaceholderSquare";
        return whiteSprite;
    }
}
