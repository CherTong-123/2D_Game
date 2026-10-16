using System.
Collections;
using System.
Collections.Generic;
using UnityEngine;
public class em : MonoBehaviour, INutrientEater
{
    // Start is called before the first frame update
    public void GainEnergy(int amount)               // ← ② 加方法
    {
        initialenergy += amount;
    }
    Rigidbody2D rb;
    private em player;
    private float timer;
    public double initialenergy = 64;
    public double movecost = 1, maxenergy = 200, cellcount, skillcost;
    public bool skill = false;
    public enum NutrientType
    {
        Small,    // 小营养体
        Medium,   // 中营养体
        Large     // 大营养体
    }
    void Start()
    {
        rb = this.GetComponent<Rigidbody2D>();
    }
    // Update is called once per frame
    void Update()
    {
        if (initialenergy >= maxenergy)
        {
            initialenergy = maxenergy;
        }
        movecost = 1 + cellcount * 0.25;
        bool isMoving = rb.velocity.magnitude > 0.01f;
        timer += Time.deltaTime;
        if (timer >= 1f)
        {
            timer = 0f;
            string state = isMoving ? "移动中" : "静止";
            Debug.Log($"[{state}] 能量：{initialenergy:F1}");
        }
        if (isMoving == true)
        {
            initialenergy -= movecost * Time.deltaTime;
        }
        else if (isMoving == false)
        {
            initialenergy -= 0.25 * movecost * Time.deltaTime;
        }
        if (skill == true)
        {
            initialenergy -= skillcost;
            skill = false;
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
    }
}