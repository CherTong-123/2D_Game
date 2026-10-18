// ============================================================================
// CellGrid.cs：细胞占格、尖刺朝向、连接和回收规则。
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

/// <summary>已放置的细胞及运行状态；旋转角按顺时针记录。</summary>
public class PlacedCell
{
    public CellType type;
    public int hp, rotation;
    public Vector2Int anchor;
    public List<Vector2Int> squares;
    public bool isMain, link, eat;
    public GameObject view;
    public readonly List<ProteinKind> proteins = new List<ProteinKind>();
    public float productionTimer, transferTimer;
}

/// <summary>以主细胞为中心的网格，随细胞团平移、转向。</summary>
public class CellGrid
{
    public const float CellSize = 5f;
    public Vector2 origin;
    public float angle;
    private readonly Dictionary<Vector2Int, PlacedCell> map = new Dictionary<Vector2Int, PlacedCell>();
    private readonly List<PlacedCell> all = new List<PlacedCell>();
    public IReadOnlyList<PlacedCell> All => all;
    public int CellCount => all.Count;
    public int SquareCount => map.Count;
    public PlacedCell Main { get; private set; }
    public static Vector2 SquareLocal(Vector2Int g) => new Vector2(g.x + 0.5f, g.y + 0.5f) * CellSize;
    public static Vector2 Rotate(Vector2 p, float degrees) => Quaternion.Euler(0, 0, degrees) * p;
    public Vector2 GridToWorld(Vector2Int g) => origin + Rotate(SquareLocal(g), angle);
    public Vector2 CellCenter(PlacedCell c) => Center(c.type, c.anchor, c.rotation);
    public Vector2 Center(CellType type, Vector2Int anchor, int rotation)
        => origin + Rotate(((Vector2)anchor + (Vector2)type.FootprintSize(rotation) * 0.5f) * CellSize, angle);
    public Vector2Int WorldToGrid(Vector2 w)
    {
        Vector2 p = Rotate(w - origin, -angle) / CellSize;
        return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
    }
    public PlacedCell CellAt(Vector2Int g) => map.TryGetValue(g, out var c) ? c : null;
    public bool IsFree(Vector2Int g) => !map.ContainsKey(g);
    public static Vector2Int Facing(int rotation)
    {
        switch ((rotation % 360 + 360) % 360)
        {
            case 90: return Vector2Int.right;
            case 180: return Vector2Int.down;
            case 270: return Vector2Int.left;
            default: return Vector2Int.up;
        }
    }

    // ================================================================
    // 放置和旋转
    // ================================================================
    public bool CanPlace(CellType type, Vector2Int anchor, int rotation, out string reason)
        => CanPlace(type, anchor, rotation, null, out reason);
    private bool CanPlace(CellType type, Vector2Int anchor, int rotation, PlacedCell ignore, out string reason)
    {
        reason = "";
        var squares = SquaresOf(anchor, type.FootprintSize(rotation));
        foreach (var s in squares)
            if (CellAt(s) != null && CellAt(s) != ignore) { reason = "位置已被占用"; return false; }
        foreach (var other in all)
        {
            if (other == ignore || other.type.function != CellType.Function.Spike) continue;
            foreach (var front in FrontSquares(other.type, other.anchor, other.rotation))
                if (squares.Contains(front)) { reason = "不能占用尖刺前方"; return false; }
        }
        if (type.function == CellType.Function.Spike)
            foreach (var front in FrontSquares(type, anchor, rotation))
                if (CellAt(front) != null && CellAt(front) != ignore)
                { reason = "尖刺不能朝向细胞"; return false; }
        if (all.Count == 0) return true;
        foreach (var s in squares)
            foreach (var n in Neighbours(s))
            {
                var other = CellAt(n);
                if (other != null && other != ignore && other.type.attach) return true;
            }
        reason = "必须与可附着细胞相邻";
        return false;
    }
    public PlacedCell Place(CellType type, Vector2Int anchor, int rotation, bool asMain = false)
    {
        var c = new PlacedCell { type = type, hp = type.hp, anchor = anchor, rotation = rotation,
            squares = SquaresOf(anchor, type.FootprintSize(rotation)), isMain = asMain,
            productionTimer = type.productionInterval };
        all.Add(c);
        foreach (var s in c.squares) map.Add(s, c);
        if (asMain) Main = c;
        RecomputeLogic();
        return c;
    }
    /// <summary>保留细胞实例和蛋白，阻止会造成重叠或断开的旋转。</summary>
    public bool TryRotate(PlacedCell c, out string reason)
    {
        reason = "主细胞不能旋转";
        if (c == null || c.isMain || !all.Contains(c)) return false;
        int next = (c.rotation + 90) % 360;
        if (!CanPlace(c.type, c.anchor, next, c, out reason)) return false;
        int previous = c.rotation;
        var previousSquares = c.squares;
        foreach (var s in c.squares) map.Remove(s);
        c.rotation = next;
        c.squares = SquaresOf(c.anchor, c.type.FootprintSize(next));
        foreach (var s in c.squares) map.Add(s, c);
        if (DisconnectedCells().Count > 0)
        {
            foreach (var s in c.squares) map.Remove(s);
            c.rotation = previous;
            c.squares = previousSquares;
            foreach (var s in c.squares) map.Add(s, c);
            reason = "旋转后有细胞失去连接";
            return false;
        }
        RecomputeLogic();
        return true;
    }
    public List<Vector2Int> Remove(PlacedCell c)
    {
        if (c == null || c.isMain || !all.Remove(c)) return new List<Vector2Int>();
        foreach (var s in c.squares) map.Remove(s);
        RecomputeLogic();
        return c.squares;
    }
    public void Clear() { map.Clear(); all.Clear(); Main = null; }

    /// <summary>从主细胞沿可附着细胞向外查找，孤立的细胞环也会被回收。</summary>
    public List<PlacedCell> DisconnectedCells()
    {
        var reached = new HashSet<PlacedCell>();
        var queue = new Queue<PlacedCell>();
        if (Main != null) { reached.Add(Main); queue.Enqueue(Main); }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (!c.type.attach) continue;
            foreach (var other in Adjacent(c)) if (reached.Add(other)) queue.Enqueue(other);
        }
        var result = new List<PlacedCell>();
        foreach (var c in all) if (!reached.Contains(c)) result.Add(c);
        return result;
    }
    public IEnumerable<PlacedCell> Adjacent(PlacedCell c)
    {
        var seen = new HashSet<PlacedCell>();
        foreach (var s in c.squares)
            foreach (var n in Neighbours(s))
            {
                var other = CellAt(n);
                if (other != null && other != c && seen.Add(other)) yield return other;
            }
    }
    public PlacedCell FrontCell(PlacedCell c)
    {
        foreach (var s in FrontSquares(c.type, c.anchor, c.rotation))
            if (CellAt(s) != null) return CellAt(s);
        return null;
    }
    public void RecomputeLogic()
    {
        foreach (var c in all) { c.link = c.isMain; c.eat = c.isMain; }
        var queue = new Queue<PlacedCell>();
        if (Main != null) queue.Enqueue(Main);
        while (queue.Count > 0)
            foreach (var other in Adjacent(queue.Dequeue()))
                if (!other.link && other.type.pmt == 2) { other.link = true; queue.Enqueue(other); }
        foreach (var c in all)
        {
            if (!c.link || c.isMain) continue;
            foreach (var s in c.squares)
                foreach (var n in Neighbours(s)) if (IsFree(n)) c.eat = true;
        }
    }
    public int SortingOrderOf(PlacedCell c)
    {
        var center = MainCenterSquare();
        float sum = 0;
        foreach (var s in c.squares) sum += Mathf.Abs(s.x - center.x) + Mathf.Abs(s.y - center.y);
        return 1000 - Mathf.RoundToInt(sum / c.squares.Count * 10f);
    }
    public Vector2Int MainCenterSquare() => Main == null ? Vector2Int.zero : Main.anchor + Main.type.FootprintSize(Main.rotation) / 2;
    public RectInt Bounds()
    {
        if (map.Count == 0) return new RectInt();
        var min = new Vector2Int(int.MaxValue, int.MaxValue);
        var max = new Vector2Int(int.MinValue, int.MinValue);
        foreach (var s in map.Keys) { min = Vector2Int.Min(min, s); max = Vector2Int.Max(max, s); }
        return new RectInt(min, max - min + Vector2Int.one);
    }
    public static List<Vector2Int> SquaresOf(Vector2Int anchor, Vector2Int size)
    {
        var result = new List<Vector2Int>();
        for (int x = 0; x < size.x; x++) for (int y = 0; y < size.y; y++) result.Add(anchor + new Vector2Int(x, y));
        return result;
    }
    public static IEnumerable<Vector2Int> FrontSquares(CellType type, Vector2Int anchor, int rotation)
    {
        var size = type.FootprintSize(rotation);
        var facing = Facing(rotation);
        for (int i = 0; i < (facing.x == 0 ? size.x : size.y); i++)
            yield return anchor + (facing.x == 0 ? new Vector2Int(i, facing.y > 0 ? size.y : -1)
                : new Vector2Int(facing.x > 0 ? size.x : -1, i));
    }
    public static IEnumerable<Vector2Int> Neighbours(Vector2Int g)
    {
        yield return g + Vector2Int.right; yield return g + Vector2Int.left;
        yield return g + Vector2Int.up; yield return g + Vector2Int.down;
    }
}
