// ============================================================================
// MicrobeController.cs：细胞团的能量、分裂、回收、显示和功能入口。
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>挂在玩家根物体上，所有细胞共享同一个刚体。</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(maincell))]
public class MicrobeController : MonoBehaviour, INutrientEater
{
    [Header("细胞类型库")]
    public CellType[] availableTypes;
    public CellType mainCellType;
    [Header("能量")]
    public float energy = 50f;
    public float energyLimit = 300f;
    public float energyUse = 1f;
    [Range(0, 1)] public float idleCostFactor = 0.25f;
    public bool drainEnergy = true;
    [Header("显示")]
    public Transform mainCellBody;
    public Transform cellRoot;
    public int baseSortingOrder;
    public Vector2Int mainCellGrid = new Vector2Int(-1, -1);
    public CellGrid Grid { get; private set; }
    public bool IsDead => health.IsDead;
    public float MovementMultiplier { get; private set; } = 1f;
    public event System.Action Changed;
    public event System.Action Died;
    private readonly Dictionary<PlacedCell, GameObject> views = new Dictionary<PlacedCell, GameObject>();
    private static Sprite whiteSprite;
    private static Sprite arrowSprite;
    private Rigidbody2D mainRb;
    private maincell health;
    private ThreatMarker threat;
    private CellProteinSystem proteins;

    private void Awake()
    {
        Grid = new CellGrid();
        if (mainCellBody == null) mainCellBody = transform;
        if (cellRoot == null) cellRoot = transform;
        mainRb = GetComponent<Rigidbody2D>();
        health = GetComponent<maincell>();
        threat = GetComponent<ThreatMarker>();
        proteins = new CellProteinSystem(this);
    }
    private void Start() { if (Grid.Main == null) InitMain(mainCellType, mainCellGrid); }
    private void Update()
    {
        if (IsDead || Time.timeScale == 0f) return;
        if (drainEnergy)
        {
            float use = energyUse * 64f / (63f + Grid.CellCount);
            energy = Mathf.Max(0, energy - use * (mainRb.velocity.sqrMagnitude > 0.0001f ? 1 : idleCostFactor) * Time.deltaTime);
        }
        proteins.Tick(Time.deltaTime);
        if (Input.GetKeyDown(KeyCode.E)) proteins.ReleaseProducers();
        if (Input.GetKeyDown(KeyCode.F)) proteins.FireSprayers();
    }
    private void LateUpdate()
    {
        if (Grid.Main == null) return;
        SyncTransform();
        foreach (var pair in views) UpdateView(pair.Key, pair.Value);
    }
    private void SyncTransform()
    {
        Grid.angle = mainCellBody.eulerAngles.z;
        var main = Grid.Main;
        Vector2 localCenter = ((Vector2)main.anchor + (Vector2)main.type.FootprintSize(main.rotation) * 0.5f) * CellGrid.CellSize;
        Grid.origin = (Vector2)mainCellBody.position - CellGrid.Rotate(localCenter, Grid.angle);
    }

    // ================================================================
    // 分裂、旋转和回收
    // ================================================================
    public void InitMain(CellType type, Vector2Int anchor)
    {
        if (mainCellBody == transform)
        {
            // 主体显示单独缩放，根物体保持单位缩放，避免附属细胞转向时变形。
            transform.localScale = Vector3.one;
            var original = GetComponent<SpriteRenderer>();
            if (original != null) original.enabled = false;
            var visual = new GameObject("MainCellBody");
            visual.transform.SetParent(transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = type.body != null ? type.body : PlaceholderSprite();
            Vector2 size = type.FootprintSize();
            visual.transform.localScale = new Vector3(size.x * CellGrid.CellSize * 1.08f / renderer.sprite.bounds.size.x,
                size.y * CellGrid.CellSize * 1.08f / renderer.sprite.bounds.size.y, 1);
            mainCellBody = visual.transform;
            var circle = GetComponent<CircleCollider2D>();
            if (circle != null) circle.radius = size.x * CellGrid.CellSize * 0.5f;
        }
        Grid.Clear();
        Grid.Place(type, anchor, 0, true);
        health.hp = type.hp;
        health.maxHp = type.hp;
        SyncTransform();
        RefreshAll();
    }
    public bool TryPlace(CellType type, Vector2Int anchor, int rotation, out string reason)
    {
        reason = "未选择细胞";
        if (type == null || IsDead) return false;
        if (!Grid.CanPlace(type, anchor, rotation, out reason)) return false;
        if (energy < type.cost) { reason = "能量不足"; return false; }
        energy -= type.cost;
        Grid.Place(type, anchor, rotation);
        RefreshAll();
        return true;
    }
    public bool TryRotate(PlacedCell cell, out string reason)
    {
        if (!Grid.TryRotate(cell, out reason)) return false;
        RefreshAll();
        return true;
    }
    /// <summary>回收及断开的附属细胞全额返还；返还余额可暂时超过储能上限。</summary>
    public void Recycle(PlacedCell cell)
    {
        if (cell == null || cell.isMain || !Contains(cell)) return;
        energy += cell.type.cost;
        Grid.Remove(cell);
        foreach (var detached in Grid.DisconnectedCells())
        {
            energy += detached.type.cost;
            Grid.Remove(detached);
        }
        RefreshAll();
    }
    public bool Contains(PlacedCell cell)
    {
        foreach (var c in Grid.All) if (c == cell) return true;
        return false;
    }
    /// <summary>被击毁的细胞和脱落部分不返还能量。</summary>
    public void DamageCell(PlacedCell cell, int amount)
    {
        if (IsDead || !Contains(cell)) return;
        cell.hp = Mathf.Max(0, cell.hp - amount);
        if (cell.hp > 0) { Changed?.Invoke(); return; }
        Grid.Remove(cell);
        foreach (var detached in Grid.DisconnectedCells()) Grid.Remove(detached);
        RefreshAll();
    }
    public void GainEnergy(int amount)
    {
        if (IsDead) return;
        energy += Mathf.Min(Mathf.Max(0, amount), Mathf.Max(0, energyLimit - energy));
        Changed?.Invoke();
    }
    public void NotifyDeath()
    {
        mainRb.velocity = Vector2.zero;
        mainRb.angularVelocity = 0;
        foreach (var collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        if (threat != null) threat.enabled = false;
        Died?.Invoke();
    }
    public bool ReceiveProtein(PlacedCell cell, ProteinKind kind) => proteins.Receive(cell, kind);
    public void WaterImpulse(Vector2 direction) { mainRb.velocity += direction * GetComponent<move>().MaxSpeed; }

    // ================================================================
    // 显示和属性
    // ================================================================
    public void RefreshAll()
    {
        Grid.RecomputeLogic();
        energyLimit = 100f;
        MovementMultiplier = 1f;
        bool armed = false;
        foreach (var c in Grid.All)
        {
            if (c.type.function == CellType.Function.Storage) energyLimit += c.type.storageBonus;
            if (c.type.function == CellType.Function.Power) MovementMultiplier += c.type.movementBonus;
            armed |= c.type.function == CellType.Function.Spike || c.type.function == CellType.Function.DamageProtein;
        }
        if (threat != null) threat.enabled = armed;
        var removed = new List<PlacedCell>();
        foreach (var pair in views) if (!Contains(pair.Key)) removed.Add(pair.Key);
        foreach (var c in removed) { views[c].SetActive(false); Destroy(views[c]); views.Remove(c); }
        foreach (var c in Grid.All)
        {
            if (c.isMain)
            {
                c.view = mainCellBody.gameObject;
                var mainRenderer = mainCellBody.GetComponent<SpriteRenderer>();
                if (mainRenderer != null) mainRenderer.sortingOrder = baseSortingOrder + Grid.SortingOrderOf(c);
                continue;
            }
            if (!views.TryGetValue(c, out var go))
            {
                go = new GameObject("Cell_" + c.type.id);
                go.transform.SetParent(cellRoot, false);
                go.AddComponent<SpriteRenderer>();
                if (c.type.divity)
                {
                    var arrow = new GameObject("Facing").AddComponent<SpriteRenderer>();
                    arrow.transform.SetParent(go.transform, false);
                    arrow.sprite = DirectionSprite();
                    arrow.color = new Color(0.1f, 0.2f, 0.25f, 0.9f);
                }
                var body = go.AddComponent<CellBody>();
                body.Initialize(this, c);
                views.Add(c, go);
                c.view = go;
            }
            UpdateView(c, go);
        }
        Changed?.Invoke();
    }
    private void UpdateView(PlacedCell c, GameObject go)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = c.type.body != null ? c.type.body : PlaceholderSprite();
        sr.color = c.type.body != null ? c.type.tint : c.type.placeholderColor;
        sr.sortingOrder = baseSortingOrder + Grid.SortingOrderOf(c);
        go.transform.position = Grid.CellCenter(c);
        go.transform.rotation = Quaternion.Euler(0, 0, Grid.angle - c.rotation);
        // 按贴图原始尺寸归一化，保持相邻细胞略微重叠。
        Vector2 size = c.type.FootprintSize();
        Vector3 parentScale = cellRoot.lossyScale;
        go.transform.localScale = new Vector3(size.x * CellGrid.CellSize * 1.08f / sr.sprite.bounds.size.x / parentScale.x,
            size.y * CellGrid.CellSize * 1.08f / sr.sprite.bounds.size.y / parentScale.y, 1);
        go.GetComponent<BoxCollider2D>().size = (Vector2)sr.sprite.bounds.size / 1.08f;
        if (c.type.divity)
        {
            var arrow = go.transform.GetChild(0);
            arrow.localPosition = new Vector3(0, sr.sprite.bounds.size.y * 0.3f, 0);
            arrow.localScale = new Vector3(1.4f / go.transform.lossyScale.x, 1.4f / go.transform.lossyScale.y, 1);
            arrow.GetComponent<SpriteRenderer>().sortingOrder = sr.sortingOrder + 1;
        }
    }
    /// <summary>用三角标记显示有方向性细胞的朝向。</summary>
    public static Sprite DirectionSprite()
    {
        if (arrowSprite != null) return arrowSprite;
        var texture = new Texture2D(16, 16);
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            texture.SetPixel(x, y, Mathf.Abs(x - 7.5f) <= (15 - y) * 0.5f ? Color.white : Color.clear);
        texture.Apply();
        arrowSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
        return arrowSprite;
    }
    public static Sprite PlaceholderSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
        return whiteSprite;
    }
}
