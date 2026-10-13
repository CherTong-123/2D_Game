using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class move : MonoBehaviour
{
    [Header("旋转参数")]
    public float maxAngularSpeed = 6f;    // 最大角速度（弧度/秒）
    public float turnSpeedFactor = 0.8f;  // 变速系数：越大，大角度转向提速越明显

    [Header("移动参数")]
    public float moveForce = 5f;
    public Rigidbody2D rb;

    // 计时调试用（保留你原来的打印逻辑）
    private float printTimer = 0f;


    void Start()
    {
        // 自动获取身上的 Rigidbody2D
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }
    void Update()
    {
        // ========== 1. 鼠标坐标转换 ==========
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // ========== 2. 方向向量 ==========
        Vector2 dir = (mouseWorld - transform.position).normalized;

        // ========== 3. 目标角度（核心修正：-90度让物体上方对准鼠标） ==========
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

        // ========== 4. 当前角度 + 最短角度差 ==========
        float currentAngle = transform.eulerAngles.z;
        float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);
        // ========== 5. 变速转向逻辑（角度差越大，转得越快） ==========
        // 基础最大步长
        float baseStep = maxAngularSpeed * Mathf.Rad2Deg * Time.deltaTime;
        // 变速：角度差越大，步长越大（0~1倍的比例）
        float speedMultiplier = Mathf.Clamp01(Mathf.Abs(angleDiff) / 90f * turnSpeedFactor);
        float rotateStep = baseStep * (1f + speedMultiplier);
        // 限制步长，保证走最短路径，不会绕大圈
        float newAngle = currentAngle + Mathf.Clamp(angleDiff, -rotateStep, rotateStep);
        // ========== 6. 应用旋转 ==========
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        // ========== 原有 WASD 移动逻辑 ==========
        if (Input.GetKey(KeyCode.W))
            rb.AddForce(Vector2.up * moveForce);
        if (Input.GetKey(KeyCode.S))
            rb.AddForce(Vector2.down * moveForce);
        if (Input.GetKey(KeyCode.D))
            rb.AddForce(Vector2.right * moveForce);
        if (Input.GetKey(KeyCode.A))
            rb.AddForce(Vector2.left * moveForce);
    }
}
