using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 玩家细胞的主属性脚本：血量、分裂相关参数。
/// 敌方近战冲撞和远程子弹都通过 TakeDamage() 扣血。
/// </summary>
public class maincell : MonoBehaviour
{
    public int hp = 100, cost = 100;
    public enum cellsize { size1 = 1, size2 = 2, size3 = 4, size4 = 6, size5 = 9 }
    public enum pmt { None = 0, Weak = 1, Strong = 2 }
    public bool attach, divity, link = true, eat;
    public bool IsDead => hp <= 0;
    void Start() { }
    void Update() { }
    /// <summary>
    /// 受到伤害。敌方冲刺撞击、子弹都调用这里。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (hp <= 0) return;              // 已经死了就不重复扣
        hp -= amount;
        if (hp < 0) hp = 0;
        Debug.Log($"[MainCell] 受到 {amount} 点伤害，剩余 HP {hp}");
        if (hp == 0) Die();
    }
    /// <summary>回血（吃营养球、回血道具时调用）</summary>
    public void Heal(int amount)
    {
        if (hp <= 0) return;
        hp += amount;
        Debug.Log($"[MainCell] 回复 {amount} 点 HP，当前 HP {hp}");
    }
    void Die()
    {
        Debug.Log("[MainCell] 玩家死亡");
        // TODO: 这里接死亡表现 —— 爆炸特效、死亡界面、重生
        // 注意：把 GameObject 设为 inactive 后，敌人就找不到玩家了（FindGameObjectWithTag 会忽略 inactive 对象）
        // gameObject.SetActive(false);
    }
}