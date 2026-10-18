// ============================================================================
// CellBody.cs：附属细胞的碰撞、血量与进食入口；使用玩家根物体的刚体。
// ============================================================================
using UnityEngine;

/// <summary>附属细胞共享根刚体，内部不互撞，对外保留碰撞。</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CellBody : MonoBehaviour, INutrientEater
{
    public MicrobeController Owner { get; private set; }
    public PlacedCell Cell { get; private set; }
    public bool CanEat => Owner != null && !Owner.IsDead && Cell.eat;
    private float nextSpikeHit;
    public void Initialize(MicrobeController owner, PlacedCell cell)
    {
        Owner = owner;
        Cell = cell;
        GetComponent<BoxCollider2D>().isTrigger = false;
    }
    public void GainEnergy(int amount) { if (CanEat) Owner.GainEnergy(amount); }
    public void TakeDamage(int damage) { Owner.DamageCell(Cell, damage); }
    private void OnCollisionStay2D(Collision2D collision) { HitEnemy(collision.collider); }
    private void OnTriggerStay2D(Collider2D other) { HitEnemy(other); }
    private void HitEnemy(Collider2D other)
    {
        if (Owner.IsDead || Time.timeScale == 0 || Cell.type.function != CellType.Function.Spike || Time.time < nextSpikeHit) return;
        var enemy = other.GetComponentInParent<EnemyBlackboard>();
        if (enemy == null) return;
        enemy.TakeDamage(Cell.type.contactDamage);
        nextSpikeHit = Time.time + 1f;
    }
    /// <summary>敌方攻击先命中附属细胞，主细胞单独扣血。</summary>
    public static bool HitPlayer(Collider2D target, int damage)
    {
        var body = target.GetComponent<CellBody>();
        if (body != null) { body.TakeDamage(damage); return true; }
        var main = target.GetComponent<maincell>();
        if (main == null || main.IsDead) return false;
        main.TakeDamage(damage);
        return true;
    }
}
