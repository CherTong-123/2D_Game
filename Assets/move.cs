using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class move : MonoBehaviour
{
    // Start is called before the first frame update
    Rigidbody2D rb;
    void Start()
    {
        rb = this.GetComponent<Rigidbody2D>();
    }
    public float maxSpeed = 10f;
    public float maxAngularSpeed = 2f;
    public float playerAddSpeed = 50f;

    // Update is called once per frame
    void Update()
    {
        // ---- 鼠标朝向（放 Update 里，输入读取适合帧更新）----
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f; // 2D 平面，z 清零

        // 方向向量：鼠标位置 - 角色位置
        Vector2 dir = (mouseWorld - transform.position).normalized;

        // 目标角度：Mathf.Atan2 输出弧度（-π 到 π）
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 当前角度
        float currentAngle = transform.eulerAngles.z;

        // 计算角度差，处理 360° 环绕（比如 350° 转到 10° 只转 20°，不是 340°）
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);

        // 限速：最多转 maxAngularSpeed * Time.deltaTime
        float rotateStep = maxAngularSpeed * Mathf.Rad2Deg * Time.deltaTime;
        float newAngle = currentAngle + Mathf.Clamp(angleDiff, -rotateStep, rotateStep);

        // 应用旋转
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        if (Input.GetKey(KeyCode.W))
        {
            rb.AddForce(Vector2.up * 5);
        }
        if (Input.GetKey(KeyCode.D))
        {
            rb.AddForce(Vector2.right * 5);
        }
        if (Input.GetKey(KeyCode.A))
        {
            rb.AddForce(Vector2.left * 5);
        }
        if (Input.GetKey(KeyCode.S))
        {
            rb.AddForce(Vector2.down * 5);
        }
        }
    void FixedUpdate()  // 物理相关用 FixedUpdate，不要用 Update
    {
        // 1. 获取输入方向
        float moveX = Input.GetAxisRaw("Horizontal"); // A/D 或左右方向键
        float moveY = Input.GetAxisRaw("Vertical");   // W/S 或上下方向键
        Vector2 input = new Vector2(moveX, moveY).normalized;

        // 2. 施加力 = 方向 × 加速度
        //    这就是"最大加速度"——每帧施加的力被 playerAddSpeed 限制死
        rb.AddForce(input * playerAddSpeed);
        // 3. 钳制速度 = "最大速度"
        //    只钳制水平方向（如果只做横版），或钳制整体大小（顶视角）
        if (rb.velocity.magnitude > maxSpeed)
        {
            // 保留当前方向，把长度压回 maxSpeed
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
    }
}
