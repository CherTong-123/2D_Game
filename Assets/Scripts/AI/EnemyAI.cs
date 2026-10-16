using System.Collections.Generic;
using UnityEngine;

/// <summary>策划文档里的五种细胞类型。</summary>
public enum EnemyType
{
    Wanderer,   // 游荡型：只觅食，不攻击，会躲危险
    Melee,      // 近战型：饿了就冲玩家，进攻时不躲危险
    Ranged,     // 远程型：饿了就射击，会躲危险
    Hybrid,     // 混合型：先远程，锁定后切近战，冲刺 1~2 次后拉开
    Danger      // 危险型：混合型的底子，但主动找玩家、不觅食
}

/// <summary>
/// 敌方细胞 AI。用行为树驱动：Awake 里按类型拼好一棵树，每帧 Tick 一次。
///
/// 五棵树的结构见 README。核心思想：
///   Selector（优先级从上到下）
///     ├─ 逃离危险
///     ├─ 攻击
///     ├─ 觅食
///     └─ 闲逛
/// 每种类型只是【少几个分支】或【换个攻击动作】。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyBlackboard))]
public class EnemyAI : MonoBehaviour, INutrientEater
{
    [Header("类型")]
    public EnemyType type = EnemyType.Wanderer;

    [Header("移动")]
    public float moveSpeed = 3f;
    public float fleeSpeed = 4.5f;
    [Tooltip("转向速度（度/秒）")]
    public float turnSpeed = 320f;
    [Tooltip("闲逛时每隔多久换个方向")]
    public float wanderChangeTime = 2f;

    [Header("近战")]
    public float dashSpeed = 12f;
    public float dashDuration = 0.35f;
    public float meleeCooldown = 2f;
    public float meleeRange = 6f;
    public int meleeDamage = 30;

    [Header("远程")]
    public GameObject bulletPrefab;
    public float fireCooldown = 1.2f;
    public float attackRange = 9f;
    public float keepDistance = 6f;
    public int bulletDamage = 15;

    [Header("混合型")]
    public int dashTimesBeforeRetreat = 2;
    public float retreatDuration = 1.5f;

    private EnemyBlackboard bb;
    private BTNode tree;

    // ---- 计时器 ----
    private float cooldown;        // 攻击冷却
    private float slowTimer;       // 射击前后减速
    private float retreatTimer;    // 混合型撤退
    private float wanderTimer;
    private Vector2 wanderDir;
    private int dashCount;         // 混合型本轮已冲刺次数

    // ---- 冲刺状态 ----
    private bool dashing;
    private float dashTimer;
    private Vector2 dashDir;

    // ================= 生命周期 =================

    void Awake()
    {
        bb = GetComponent<EnemyBlackboard>();
        ApplyTypeSettings();
        tree = BuildTree();
        wanderDir = Random.insideUnitCircle.normalized;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        bb.Sense();
        TickTimers(dt);
        tree.Tick();

        // 冲刺期间速度由这里统一接管，覆盖动作里设的值
        if (dashing) bb.rb.velocity = dashDir * dashSpeed;
    }

    // ================= 类型配置 =================

    /// <summary>五种类型的行为开关。改动这里就等于改行为，不用动树的结构。</summary>
    void ApplyTypeSettings()
    {
        switch (type)
        {
            case EnemyType.Wanderer:
                bb.avoidDanger = true;
                bb.alwaysHunt = false;
                break;

            case EnemyType.Melee:
                bb.avoidDanger = false;   // 进攻时不会远离危险
                bb.alwaysHunt = false;
                break;

            case EnemyType.Ranged:
                bb.avoidDanger = true;
                bb.alwaysHunt = false;
                break;

            case EnemyType.Hybrid:
                bb.avoidDanger = true;
                bb.alwaysHunt = false;
                break;

            case EnemyType.Danger:
                bb.avoidDanger = true;
                bb.alwaysHunt = true;    // 无视饥饿，主动找玩家
                break;
        }
    }

    // ================= 拼装行为树 =================
    //
    // 这就是"五种类型"的全部差异 —— 一段 switch，没有任何新节点类型。

    BTNode BuildTree()
    {
        var branches = new List<BTNode>();

        // ---- ① 逃离危险（游荡型、远程型、混合型、危险型 都有）----
        // 近战型故意不加：策划写的是"进攻时不会远离危险"
        if (type != EnemyType.Melee)
        {
            branches.Add(new ReactiveSequence(
                new Condition(() => bb.danger != null),
                new Action(FleeAction)
            ));
        }

        // ---- ② 攻击分支 ----
        switch (type)
        {
            case EnemyType.Melee:
                branches.Add(new ReactiveSequence(
                    new Condition(ShouldHunt),
                    new Condition(() => bb.targetPlayer != null),
                    new Action(MeleeAction)
                ));
                break;

            case EnemyType.Ranged:
                branches.Add(new ReactiveSequence(
                    new Condition(ShouldHunt),
                    new Condition(() => bb.targetPlayer != null),
                    new Action(RangedAction)
                ));
                break;

            case EnemyType.Hybrid:
            case EnemyType.Danger:
                branches.Add(new ReactiveSequence(
                    new Condition(ShouldHunt),
                    new Condition(() => bb.targetPlayer != null),
                    new Action(HybridAction)
                ));
                break;

                // 游荡型没有攻击分支
        }

        // ---- ③ 觅食（危险型不用觅食）----
        if (type != EnemyType.Danger)
        {
            branches.Add(new ReactiveSequence(
                new Condition(() => bb.targetNutrient != null),
                new Action(ForageAction)
            ));
        }

        // ---- ④ 兜底：闲逛 ----
        branches.Add(new Action(WanderAction));

        return new ReactiveSelector(branches.ToArray());
    }

    /// <summary>饥饿值够了才去打玩家；危险型永远返回 true。</summary>
    bool ShouldHunt()
    {
        return bb.alwaysHunt || bb.hunger >= bb.attackHunger;
    }

    // ================= 动作 =================

    NodeState FleeAction()
    {
        Vector2 away = ((Vector2)transform.position - (Vector2)bb.danger.position).normalized;
        if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle.normalized;

        Drive(away, fleeSpeed);
        return NodeState.Running;
    }

    NodeState ForageAction()
    {
        Vector2 to = (Vector2)bb.targetNutrient.transform.position - (Vector2)transform.position;
        if (to.sqrMagnitude < 0.04f) return NodeState.Running;   // 快贴上了，等触发器

        Drive(to, moveSpeed);
        return NodeState.Running;
    }

    NodeState WanderAction()
    {
        Drive(wanderDir, moveSpeed * 0.55f);
        return NodeState.Running;
    }

    NodeState MeleeAction()
    {
        if (dashing) return NodeState.Running;

        Vector2 toPlayer = (Vector2)bb.targetPlayer.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        if (cooldown > 0f || dist > meleeRange)
        {
            // 冷却中 / 还太远：先靠近，但不冲
            Drive(toPlayer, moveSpeed);
            return NodeState.Running;
        }

        StartDash(toPlayer.normalized, meleeCooldown);
        return NodeState.Running;
    }

    NodeState RangedAction()
    {
        Vector2 toPlayer = (Vector2)bb.targetPlayer.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer.normalized;

        // 保持距离：太近后退、太远靠近、正好就停住瞄准
        if (dist < keepDistance - 1f) Drive(-dir, moveSpeed);
        else if (dist > keepDistance + 1f) Drive(dir, moveSpeed);
        else Drive(dir, 0f);       // 速度 0，但仍然转向瞄准

        if (cooldown <= 0f && bulletPrefab != null && dist <= attackRange)
        {
            Fire(dir);
            cooldown = fireCooldown;
            slowTimer = 0.6f;      // 射击前后大幅降速、降转向
        }

        return NodeState.Running;
    }

    /// <summary>混合型：先远程，贴近后切近战，冲刺若干次再拉开。</summary>
    NodeState HybridAction()
    {
        Vector2 toPlayer = (Vector2)bb.targetPlayer.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = toPlayer.normalized;

        // ---- 撤退阶段 ----
        if (retreatTimer > 0f)
        {
            Drive(-dir, moveSpeed * 1.2f);
            return NodeState.Running;
        }

        // ---- 远程阶段 ----
        if (dashCount == 0)
        {
            if (dist < keepDistance - 1f) Drive(-dir, moveSpeed);
            else if (dist > keepDistance + 1f) Drive(dir, moveSpeed);
            else Drive(dir, 0f);

            if (cooldown <= 0f && bulletPrefab != null && dist <= attackRange)
            {
                Fire(dir);
                cooldown = fireCooldown;
                slowTimer = 0.5f;
            }

            // 贴到近战距离 → 切换到近战
            if (dist <= meleeRange)
            {
                dashCount = 1;
                cooldown = 0f;
            }
            return NodeState.Running;
        }

        // ---- 近战阶段 ----
        if (!dashing && cooldown <= 0f)
        {
            StartDash(dir, meleeCooldown * 0.6f);

            dashCount++;
            if (dashCount > dashTimesBeforeRetreat)
            {
                retreatTimer = retreatDuration;
                dashCount = 0;
            }
        }
        return NodeState.Running;
    }

    // ================= 工具 =================

    void StartDash(Vector2 dir, float cd)
    {
        dashDir = dir;
        dashing = true;
        dashTimer = dashDuration;
        cooldown = cd;

        bb.rb.velocity = dashDir * dashSpeed;
        FaceDirection(dashDir, 1f);        // 起步瞬间就对准
    }

    void Fire(Vector2 dir)
    {
        GameObject go = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        EnemyBullet b = go.GetComponent<EnemyBullet>();
        if (b != null)
        {
            b.damage = bulletDamage;
            b.Launch(dir);
        }
    }

    /// <summary>设置速度并转向。速度/转向会被"冲刺中"和"射击减速"两个状态影响。</summary>
    void Drive(Vector2 dir, float speed)
    {
        float speedMul = 1f;
        float turnMul = 1f;

        if (dashing)
        {
            // 冲刺时速度由 Update 接管，这里只负责转向
            speedMul = 0f;
            turnMul = 0.25f;          // 策划要求：冲刺时转向速度大幅下降
        }
        else if (slowTimer > 0f)
        {
            speedMul = 0.35f;          // 射击前后降速
            turnMul = 0.4f;           // 射击时转向也变慢
        }

        bb.rb.velocity = dir.normalized * speed * speedMul;
        FaceDirection(dir, turnMul);
    }

    void FaceDirection(Vector2 dir, float turnMul)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        float curAngle = bb.body.eulerAngles.z;
        float next = Mathf.MoveTowardsAngle(curAngle, targetAngle, turnSpeed * turnMul * Time.deltaTime);

        bb.body.rotation = Quaternion.Euler(0f, 0f, next);
    }

    void TickTimers(float dt)
    {
        if (cooldown > 0f) cooldown -= dt;
        if (slowTimer > 0f) slowTimer -= dt;
        if (retreatTimer > 0f) retreatTimer -= dt;

        wanderTimer -= dt;
        if (wanderTimer <= 0f)
        {
            wanderDir = Random.insideUnitCircle.normalized;
            wanderTimer = wanderChangeTime;
        }

        if (dashing)
        {
            dashTimer -= dt;
            if (dashTimer <= 0f)
            {
                dashing = false;
                bb.rb.velocity = Vector2.zero;
            }
        }
    }

    // ================= 吃 & 打 =================

    /// <summary>实现 INutrientEater：营养球会调用这里。</summary>
    public void GainEnergy(int amount)
    {
        bb.AddEnergy(amount);
        Debug.Log($"[Enemy/{type}] 吃到 +{amount}，能量 {bb.energy:F1}，饥饿 {bb.hunger:F1}");
    }

    /// <summary>冲刺撞到玩家时造成伤害。一次冲刺最多命中一次。</summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!dashing) return;
        if (!other.CompareTag("Player")) return;

        em player = other.GetComponent<em>();
        if (player != null)
        {
            player.initialenergy -= meleeDamage;
            Debug.Log($"[Enemy/{type}] 冲撞玩家 -{meleeDamage}");
        }
    }

    // 在 Scene 视图里画出感知范围，方便调参
    void OnDrawGizmosSelected()
    {
        var b = GetComponent<EnemyBlackboard>();
        if (b == null) return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, b.senseRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, b.escapeRadius);

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, keepDistance);
    }
}
