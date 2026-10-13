using UnityEngine;

/// <summary>
/// 挂在营养球预制体上。记录类型与能量，被玩家碰到时把能量加到玩家身上。
/// 能量：Small=4, Medium=16, Large=64。
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Nutrient : MonoBehaviour
{
    public em.NutrientType type;
    public int energy;

    /// <summary>
    /// 玩家吃到营养球时触发。要求：
    /// 1) 玩家层为 "Player"，Player 物体上挂了 em.cs（含 public float initialenergy）
    /// 2) 营养球 Collider2D 勾选 Is Trigger
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 给玩家加能量（反射式调用，避免强耦合；也可改成 GetComponent<em>()）
        em player = other.GetComponent<em>();
        if (player != null)
        {
            player.initialenergy += energy;
            Debug.Log($"[Nutrient] 吃到 {type} +{energy} 能量，当前总能量 {player.initialenergy:F1}");
        }

        // 从生成器里回收并销毁
        NutrientSpawner spawner = FindObjectOfType<NutrientSpawner>();
        if (spawner != null) spawner.Despawn(gameObject);
        else Destroy(gameObject);
    }
}
