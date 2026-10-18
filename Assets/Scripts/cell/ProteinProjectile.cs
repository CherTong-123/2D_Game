// ============================================================================
// ProteinProjectile.cs：蛋白飞行、命中敌人和进入可通透细胞。
// ============================================================================
using UnityEngine;

/// <summary>蛋白使用刚体速度移动，一次碰撞只结算一次。</summary>
public class ProteinProjectile : MonoBehaviour
{
    private PlacedCell source;
    private ProteinKind kind;
    private bool consumed;
    public static ProteinProjectile Spawn(MicrobeController owner, PlacedCell source, ProteinKind kind, Vector2 position, Vector2 direction, float speed)
    {
        var go = new GameObject("Protein_" + kind);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.65f;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = MicrobeController.PlaceholderSprite();
        renderer.color = kind == ProteinKind.Damage ? new Color(1f, 0.35f, 0.3f) : new Color(0.3f, 0.8f, 1f);
        renderer.sortingOrder = 1100;
        if (kind == ProteinKind.Damage) go.AddComponent<ThreatMarker>();
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.velocity = direction * speed;
        var protein = go.AddComponent<ProteinProjectile>();
        protein.source = source;
        protein.kind = kind;
        Destroy(go, 8f);
        return protein;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        var enemy = other.GetComponentInParent<EnemyBlackboard>();
        if (enemy != null)
        {
            if (kind == ProteinKind.Damage) enemy.TakeDamage(source.type.proteinDamage);
            else enemy.GetComponent<EnemyAI>().ApplySlow(source.type.slowMultiplier, source.type.slowDuration);
            consumed = true;
        }
        else
        {
            var body = other.GetComponent<CellBody>();
            if (body != null && body.Cell != source && !body.Owner.IsDead)
                consumed = body.Owner.ReceiveProtein(body.Cell, kind);
        }
        if (consumed) Destroy(gameObject);
    }
}
