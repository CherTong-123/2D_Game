using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 挂在营养球预制体上。记录类型与能量，被玩家或敌方细胞碰到时把能量加上去。
/// 能量：Small=4, Medium=16, Large=64。
///
/// 【相比原版改了两处】
/// 1) 用 INutrientEater 接口代替 CompareTag("Player")，敌人也能吃
/// 2) 增加静态列表 All，让 AI 找"最近的营养球"不用每帧 FindObjectsOfType
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Nutrient : MonoBehaviour
{
    /// <summary>场上所有活着的营养球。AI 遍历这个列表找最近目标，比 FindObjectsOfType 快很多。</summary>
    public static readonly List<Nutrient> All = new List<Nutrient>();
    public NutrientType type;
    public int energy;
    private bool consumed;
    private NutrientSpawner owner;
    void OnEnable()
    {
        consumed = false;
        owner = GetComponentInParent<NutrientSpawner>();
        All.Add(this);
    }
    void OnDisable()
    {
        All.Remove(this);
    }
    /// <summary>
    /// 被吃掉时触发。要求：
    /// 1) 营养球 Collider2D 勾选 Is Trigger（由 NutrientSpawner 运行时设置）
    /// 2) 吃的一方身上挂了实现 INutrientEater 的脚本。
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        // 谁吃都行 —— 只要它实现了 INutrientEater
        if (consumed) return;
        var body = other.GetComponent<CellBody>();
        if (body != null && !body.CanEat) return;
        INutrientEater eater = other.GetComponent<INutrientEater>();
        if (eater == null) return;
        consumed = true;
        All.Remove(this);
        eater.GainEnergy(energy);
        // 从生成器里回收并销毁；没有生成器就直接销毁
        if (owner != null) owner.Despawn(gameObject);
        else Destroy(gameObject);
    }
}
