// ============================================================================
// CellProteinSystem.cs：蛋白的合成、传递、释放和喷射。
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

/// <summary>可储存和发射的蛋白种类。</summary>
public enum ProteinKind { Damage, Slow }

/// <summary>由细胞团每帧更新，单个细胞最多储存三枚蛋白。</summary>
public class CellProteinSystem
{
    private readonly MicrobeController owner;
    public CellProteinSystem(MicrobeController owner) { this.owner = owner; }
    public void Tick(float dt)
    {
        foreach (var cell in owner.Grid.All)
        {
            cell.productionTimer -= dt;
            cell.transferTimer -= dt;
            bool producer = IsProducer(cell);
            if (producer && cell.productionTimer <= 0 && cell.proteins.Count < 3)
            {
                cell.proteins.Add(cell.type.function == CellType.Function.DamageProtein ? ProteinKind.Damage : ProteinKind.Slow);
                cell.productionTimer = cell.type.productionInterval;
            }
            if (cell.proteins.Count == 0 || cell.transferTimer > 0) continue;
            PlacedCell next = cell.type.divity ? owner.Grid.FrontCell(cell) : NextSprayerHop(cell);
            if (next != null && Receive(next, cell.proteins[0]))
            {
                cell.proteins.RemoveAt(0);
                cell.transferTimer = 0.25f;
            }
        }
    }
    public bool Receive(PlacedCell cell, ProteinKind kind)
    {
        if (!owner.Contains(cell) || cell.type.pmt == 0 || cell.proteins.Count >= 3) return false;
        cell.proteins.Add(kind);
        cell.productionTimer = Mathf.Max(cell.productionTimer, cell.type.receivingPause);
        cell.transferTimer = 0.25f;
        return true;
    }
    public void ReleaseProducers()
    {
        foreach (var cell in owner.Grid.All) if (IsProducer(cell)) Release(cell, 4f);
    }
    public void FireSprayers()
    {
        foreach (var cell in owner.Grid.All)
        {
            if (cell.type.function != CellType.Function.Spray || owner.Grid.FrontCell(cell) != null) continue;
            if (cell.proteins.Count > 0) Release(cell, cell.type.spraySpeed);
            else owner.WaterImpulse(-WorldDirection(cell));
        }
    }
    private void Release(PlacedCell cell, float speed)
    {
        if (owner.Grid.FrontCell(cell) != null) return;
        Vector2 direction = WorldDirection(cell);
        Vector2Int footprint = cell.type.FootprintSize();
        Vector2 start = owner.Grid.CellCenter(cell) + direction * (footprint.y * CellGrid.CellSize * 0.5f + 0.8f);
        for (int i = 0; i < cell.proteins.Count; i++)
            ProteinProjectile.Spawn(owner, cell, cell.proteins[i], start + direction * i * 0.7f, direction, speed);
        cell.proteins.Clear();
    }
    private Vector2 WorldDirection(PlacedCell cell) => CellGrid.Rotate(CellGrid.Facing(cell.rotation), owner.Grid.angle);
    private static bool IsProducer(PlacedCell cell) => cell.type.function == CellType.Function.DamageProtein || cell.type.function == CellType.Function.SlowProtein;

    // 强通透细胞沿最短路径把蛋白送往有空位的喷射细胞，避免在储能格间来回传递。
    private PlacedCell NextSprayerHop(PlacedCell source)
    {
        var queue = new Queue<PlacedCell>();
        var firstHop = new Dictionary<PlacedCell, PlacedCell> { [source] = null };
        queue.Enqueue(source);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in owner.Grid.Adjacent(current))
            {
                if (firstHop.ContainsKey(next) || next.type.pmt == 0 || next.proteins.Count >= 3) continue;
                if (current.type.divity && owner.Grid.FrontCell(current) != next) continue;
                firstHop[next] = current == source ? next : firstHop[current];
                if (next.type.function == CellType.Function.Spray) return firstHop[next];
                if (next.type.pmt == 2) queue.Enqueue(next);
            }
        }
        return null;
    }
}
