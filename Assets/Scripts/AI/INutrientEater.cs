using UnityEngine;
/// <summary>
/// "能吃掉营养球"的东西都实现这个接口。
///
/// 为什么用接口而不是 if (other.CompareTag("Player")) {...} else if (CompareTag("Enemy")) {...}？
/// 因为营养球不需要知道对方是谁 —— 玩家挂的是 em.cs，敌人挂的是 EnemyAI.cs，
/// 将来可能还有"友方细胞""中立生物"。用接口的话，Nutrient.cs 一行都不用再改。
/// </summary>
public interface INutrientEater
{
    /// <summary>吃到营养球时被调用。</summary>
    void GainEnergy(int amount);
}
