// 玩家输入在 Update 采样，移动和转向在固定物理帧执行。
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class move : MonoBehaviour
{
    [Header("转向")]
    public float maxAngularSpeed = 6f;
    public float turnSpeedFactor = 0.8f;
    [Header("移动")]
    [Tooltip("基础加速度，世界单位/秒平方")]
    public float moveForce = 5f;
    public float maxSpeed = 12f;
    public Rigidbody2D rb;
    public Vector2 mapMin = new Vector2(-100, -100);
    public Vector2 mapMax = new Vector2(100, 100);
    public float MaxSpeed => maxSpeed * (microbe != null ? microbe.MovementMultiplier : 1f);
    private Vector2 input;
    private float targetAngle;
    private Camera viewCamera;
    private MicrobeController microbe;
    private maincell health;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        microbe = GetComponent<MicrobeController>();
        health = GetComponent<maincell>();
        viewCamera = Camera.main;
    }
    private void Update()
    {
        if (Time.timeScale == 0 || (health != null && health.IsDead)) { input = Vector2.zero; return; }
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        Vector2 direction = viewCamera.ScreenToWorldPoint(Input.mousePosition) - transform.position;
        targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    }
    private void FixedUpdate()
    {
        if (health != null && health.IsDead) return;
        float multiplier = microbe != null ? microbe.MovementMultiplier : 1f;
        rb.AddForce(input * moveForce * rb.mass * multiplier);
        rb.velocity = Vector2.ClampMagnitude(rb.velocity, MaxSpeed);
        float delta = Mathf.DeltaAngle(rb.rotation, targetAngle);
        float step = maxAngularSpeed * Mathf.Rad2Deg * Time.fixedDeltaTime * (1f + Mathf.Clamp01(Mathf.Abs(delta) / 90f * turnSpeedFactor));
        rb.MoveRotation(rb.rotation + Mathf.Clamp(delta, -step, step));
        Vector2 position = rb.position;
        Vector2 clamped = new Vector2(Mathf.Clamp(position.x, mapMin.x + 7.5f, mapMax.x - 7.5f),
            Mathf.Clamp(position.y, mapMin.y + 7.5f, mapMax.y - 7.5f));
        if (position != clamped)
        {
            rb.position = clamped;
            Vector2 velocity = rb.velocity;
            if (position.x != clamped.x) velocity.x = 0;
            if (position.y != clamped.y) velocity.y = 0;
            rb.velocity = velocity;
        }
    }
}
