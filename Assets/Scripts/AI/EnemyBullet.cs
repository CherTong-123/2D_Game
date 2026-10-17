using UnityEngine;
/// <summary>
/// 敌方远程子弹。挂在一个圆形 Sprite 预制体上，预制体需要：
///   - SpriteRenderer
///   - CircleCollider2D（勾选 Is Trigger）
///   - Rigidbody2D（Body Type = Kinematic，Gravity Scale = 0）
///   - EnemyBullet 脚本
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class EnemyBullet : MonoBehaviour
{
    public float speed = 12f;
    public float lifeTime = 3f;
    public int damage = 15;
    private Vector2 dir = Vector2.right;
    private bool launched;
    /// <summary>由 EnemyAI 调用，指定飞行方向并开始计时销毁。</summary>
    public void Launch(Vector2 direction)
    {
        dir = direction.normalized;
        launched = true;
        Destroy(gameObject, lifeTime);
    }
    void Update()
    {
        if (!launched) return;
        transform.position += (Vector3)(dir * speed * Time.deltaTime);
        // 让子弹朝向飞行方向（贴图如果是箭头的话）
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        // 主伤害：扣 HP（和近战冲撞走同一套逻辑）
        maincell cell = other.GetComponent<maincell>();
        if (cell != null) cell.TakeDamage(damage);
        // 附带：也扣一点能量（不需要就删掉这两行）
        em energy = other.GetComponent<em>();
        if (energy != null) energy.initialenergy -= damage * 0.5;
        Debug.Log($"[Bullet] 命中玩家 -{damage}");
        Destroy(gameObject);
    }
}