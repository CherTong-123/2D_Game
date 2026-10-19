// 主细胞血量与死亡入口。死亡后保留显示，停止移动、进食和碰撞。
using UnityEngine;
public class maincell : MonoBehaviour
{
    public int hp = 300, maxHp = 300, cost = 0;
    public bool attach = true, divity = true, link = true, eat = true;
    public bool IsDead => hp <= 0;
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;
        hp = Mathf.Max(0, hp - amount);
        if (hp == 0)
        {
            var microbe = GetComponent<MicrobeController>();
            if (microbe != null) microbe.NotifyDeath();
            else
            {
                var movement = GetComponent<move>();
                if (movement != null) movement.enabled = false;
                GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            }
        }
    }
    public void Heal(int amount) { if (!IsDead) hp = Mathf.Min(maxHp, hp + Mathf.Max(0, amount)); }
}
