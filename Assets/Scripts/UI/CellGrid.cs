using System.Collections.Generic;
using UnityEngine;

/// <summary>一个已经放在网格上的细胞实例（运行时数据）。</summary>
public class PlacedCell
{
    public CellType type;
    public int hp;
    public int rotation;                 // 0 / 90 / 180 / 270
    public Vector2Int anchor;            // 左下角格坐标
    public List<Vector2Int> squares = new List<Vector2Int>();

    public bool isMain;                  // 是否主细胞
    public bool link;                    // 运行时计算出的沟通值
    public bool eat;                     // 运行时计算出的进食值

    public GameObject view;              // 对应的显示物体
}

/// <summary>
/// 细胞网格：纯数据 + 规则，不依赖 MonoBehaviour，方便单测和复用。
///
/// 坐标约定：
///   - 格子边长 = 5（策划文档「细胞正方形网格边长 5」）
///   - 格坐标 (gx, gy) 表示第 gx 列第 gy 行，世界坐标取该格**中心**
/// </summary>
public class CellGrid
{
    public const float CellSize = 5f;

    /// <summary>
    /// 网格原点（世界坐标）。
    ///
    /// 主细胞用玩家物体时，每帧把它设为「玩家位置 − 主细胞中心偏移」，
    /// 整个细胞团就会跟着玩家一起移动。
    /// </summary>
    public Vector2 origin;

    /// <summary>格子在「网格本地坐标系」里的中心（不含 origin）</summary>
    public static Vector2 SquareLocal(Vector2Int g)
    {
        return new Vector2(g.x * CellSize + CellSize * 0.5f,
                           g.y * CellSize + CellSize * 0.5f);
    }

    /// <summary>格坐标 → 该格子中心的世界坐标</summary>
    public Vector2 GridToWorld(Vector2Int g) => origin + SquareLocal(g);

    /// <summary>世界坐标 → 所在格坐标</summary>
    public Vector2Int WorldToGrid(Vector2 w)
    {
        Vector2 local = w - origin;
        return new Vector2Int(Mathf.FloorToInt(local.x / CellSize),
                              Mathf.FloorToInt(local.y / CellSize));
    }

    // ------------------------------------------------------------------

    private readonly Dictionary<Vector2Int, PlacedCell> map = new Dictionary<Vector2Int, PlacedCell>();
    private readonly List<PlacedCell> all = new List<PlacedCell>();

    public IReadOnlyList<PlacedCell> All => all;
    public int CellCount => all.Count;

    /// <summary>所有被占用的格子总数（= cellnum_b）</summary>
    public int SquareCount => map.Count;

    public PlacedCell Main { get; private set; }

    /// <summary>查询某个格子被哪个细胞占了</summary>
    public PlacedCell CellAt(Vector2Int g)
    {
        return map.TryGetValue(g, out var c) ? c : null;
    }

    public bool IsFree(Vector2Int g) => !map.ContainsKey(g);

    // ------------------------------------------------------------------
    // 放置 / 移除
    // ------------------------------------------------------------------

    /// <summary>
    /// 尝试在 origin 处放置一个细胞。
    /// 返回 false 时 failReason 里是人话说明（可以直接显示在 UI 上）。
    /// </summary>
    public bool CanPlace(CellType type, Vector2Int origin, int rotation, out string failReason)
    {
        failReason = null;

        Vector2Int size = type.FootprintSize(rotation);
        var squares = SquaresOf(origin, size);

        // 1) 不能和已有细胞重叠
        foreach (var s in squares)
        {
            if (map.ContainsKey(s))
            {
                failReason = "位置已被占用";
                return false;
            }
        }

        // 2) 第一个细胞（主细胞）可以随便放；之后必须与 attach=1 的细胞相邻
        if (all.Count > 0)
        {
            bool adjacentToAttach = false;

            foreach (var s in squares)
            {
                foreach (var n in Neighbours(s))
                {
                    var other = CellAt(n);
                    if (other != null && other.type.attach)
                    {
                        adjacentToAttach = true;
                        break;
                    }
                }
                if (adjacentToAttach) break;
            }

            if (!adjacentToAttach)
            {
                failReason = "必须与可附着细胞相邻";
                return false;
            }
        }

        return true;
    }

    /// <summary>放置细胞（调用前先 CanPlace 校验）。asMain 只由 InitMain 传 true</summary>
    public PlacedCell Place(CellType type, Vector2Int origin, int rotation, bool asMain = false)
    {
        Vector2Int size = type.FootprintSize(rotation);

        var cell = new PlacedCell
        {
            type = type,
            hp = type.hp,
            rotation = rotation,
            anchor = origin,
            squares = SquaresOf(origin, size),
            isMain = asMain,          // 主细胞必须显式指定，不会"第一个放下就自动当主细胞"
        };

        foreach (var s in cell.squares) map[s] = cell;
        all.Add(cell);

        if (cell.isMain) Main = cell;

        RecomputeLogic();
        return cell;
    }

    /// <summary>移除一个细胞，返回它占用的格子（供回收逻辑用）</summary>
    public List<Vector2Int> Remove(PlacedCell cell)
    {
        if (cell == null) return null;

        foreach (var s in cell.squares) map.Remove(s);
        all.Remove(cell);

        if (cell.isMain) Main = all.Count > 0 ? all[0] : null;

        RecomputeLogic();
        return cell.squares;
    }

    /// <summary>清空</summary>
    public void Clear()
    {
        map.Clear();
        all.Clear();
        Main = null;
    }

    // ------------------------------------------------------------------
    // 规则计算
    // ------------------------------------------------------------------

    /// <summary>
    /// 重算「沟通(link)」和「进食(eat)」的传播。
    ///
    /// 文档规则：
    ///   - 主细胞 link=1、eat=1，其余默认 0
    ///   - link=1 的细胞，会让周围 link=0 且 pmt=2 的细胞 link=1（反复传播）
    ///   - link=1 且周围有空位的细胞，eat=1
    /// </summary>
    public void RecomputeLogic()
    {
        foreach (var c in all)
        {
            c.link = c.isMain;
            c.eat = c.isMain;
        }

        // ---- link 传播（BFS）----
        var queue = new Queue<PlacedCell>();
        foreach (var c in all) if (c.link) queue.Enqueue(c);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();

            foreach (var s in cur.squares)
            {
                foreach (var n in Neighbours(s))
                {
                    var other = CellAt(n);
                    if (other == null || other.link) continue;
                    if (other.type.pmt != 2) continue;      // 只有通透性强(2)的才会被点亮

                    other.link = true;
                    queue.Enqueue(other);
                }
            }
        }

        // ---- eat：link=1 且周围有空位 ----
        foreach (var c in all)
        {
            if (c.isMain) { c.eat = true; continue; }
            if (!c.link) { c.eat = false; continue; }

            bool hasEmpty = false;
            foreach (var s in c.squares)
            {
                foreach (var n in Neighbours(s))
                {
                    if (IsFree(n)) { hasEmpty = true; break; }
                }
                if (hasEmpty) break;
            }
            c.eat = hasEmpty;
        }
    }

    /// <summary>
    /// 贴图显示优先级。
    /// 文档：以主细胞中央格为 0，其余格子 = 曼哈顿距离；细胞优先级 = 所占格子的平均值；
    /// 优先级低的贴图在上。Unity 里 sortingOrder 越大越靠前，所以用负相关。
    /// </summary>
    public int SortingOrderOf(PlacedCell cell)
    {
        if (cell == null) return 0;

        Vector2Int center = MainCenterSquare();
        float sum = 0f;
        foreach (var s in cell.squares)
            sum += Mathf.Abs(s.x - center.x) + Mathf.Abs(s.y - center.y);

        float priority = sum / Mathf.Max(1, cell.squares.Count);
        return 1000 - Mathf.RoundToInt(priority * 10f);
    }

    /// <summary>主细胞的中央格（用于优先级计算）</summary>
    public Vector2Int MainCenterSquare()
    {
        if (Main == null) return Vector2Int.zero;
        Vector2Int size = Main.type.FootprintSize(Main.rotation);
        return new Vector2Int(Main.anchor.x + size.x / 2,
                              Main.anchor.y + size.y / 2);
    }

    /// <summary>整个细胞团的外接矩形（用于相机/描边环）</summary>
    public RectInt Bounds()
    {
        if (all.Count == 0) return new RectInt(0, 0, 0, 0);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var s in map.Keys)
        {
            if (s.x < minX) minX = s.x;
            if (s.y < minY) minY = s.y;
            if (s.x > maxX) maxX = s.x;
            if (s.y > maxY) maxY = s.y;
        }
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // ------------------------------------------------------------------
    // 工具
    // ------------------------------------------------------------------

    public static List<Vector2Int> SquaresOf(Vector2Int origin, Vector2Int size)
    {
        var list = new List<Vector2Int>(size.x * size.y);
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                list.Add(new Vector2Int(origin.x + x, origin.y + y));
        return list;
    }

    /// <summary>四邻格（文档说的「网格相邻」按边相邻算）</summary>
    public static IEnumerable<Vector2Int> Neighbours(Vector2Int g)
    {
        yield return new Vector2Int(g.x + 1, g.y);
        yield return new Vector2Int(g.x - 1, g.y);
        yield return new Vector2Int(g.x, g.y + 1);
        yield return new Vector2Int(g.x, g.y - 1);
    }
}
