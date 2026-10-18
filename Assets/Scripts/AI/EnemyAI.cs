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
/// 五棵树的结构：
///   ReactiveSelector（优先级从上到下，每帧重选）
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
    [Header("移动（常态速度）")]
    [Tooltip("觅食、游荡、追击时用的速度")]
    public float moveSpeed = 3f;
    [Tooltip("逃离危险时的速度")]
    public float fleeSpeed = 4.5f;
    [Tooltip("转向速度（度/秒）")]
    public float turnSpeed = 320f;
    [Tooltip("闲逛时每隔多久换个方向")]
    public float wanderChangeTime = 2f;
    [Header("近战（冲撞速度，和上面完全独立）")]
    [Tooltip("冲刺时的速度。想让它比常态明显快，就设大一点")]
    public float dashSpeed = 20f;
    public float dashDuration = 1f;
    public float meleeCooldown = 2f;
    [Tooltip("进入这个距离就开始冲刺")]
    public float meleeRange = 6f;
    public int meleeDamage = 30;
    [Tooltip("冲刺中每隔多久造成一次伤害（秒）。设成 >= Dash Duration 就是一次冲刺只打一次")]
    public float meleeDamageInterval = 0.5f;
    [Header("远程")]
    public GameObject bulletPrefab;
    public float fireCooldown = 1.2f;
    public float attackRange = 9f;
    public float keepDistance = 6f;
    public int bulletDamage = 15;
    [Header("混合型")]
    public int dashTimesBeforeRetreat = 2;
    public float retreatDuration = 1.5f;
    [Header("调试")]
    [Tooltip("勾上后每 30 帧打印一次状态，方便看 AI 到底在干什么")]
    public bool debugState = false;
    // ================= 内部状态 =================
    private EnemyBlackboard bb;
    private BTNode tree;
    private EnemyType lastBuiltType;
    private float cooldown;        // 攻击冷却
    private float slowTimer;       // 射击前后减速
    private float retreatTimer;    // 混合型撤退
    private float wanderTimer;
    private float damageTimer;     // 冲刺伤害节流
    private Vector2 wanderDir;
    private int dashCount;         // 混合型本轮已冲刺次数
    private bool dashing;
    private float dashTimer;
    private float effectSlowTimer;
    private float effectSlowMultiplier = 1f;
    /// <summary>减速蛋白命中后，在指定秒数内降低移动速度。</summary>
    public void ApplySlow(float multiplier, float duration)
    {
        effectSlowMultiplier = Mathf.Min(effectSlowMultiplier, multiplier);
        effectSlowTimer = Mathf.Max(effectSlowTimer, duration);
    }
    private Vector2 dashDir;
    // ================= 生命周期 =================
    void Awake()
    {
        bb = GetComponent<EnemyBlackboard>();
        Rebuild();
        wanderDir = Random.insideUnitCircle.normalized;
    }
    void Update()
    {
        // 调试便利：运行时在 Inspector 改 Type 也能立刻重建行为树
        if (type != lastBuiltType) Rebuild();
        float dt = Time.deltaTime;
        if (dt == 0f) return;
        bb.Sense();
        TickTimers(dt);
        tree.Tick();
        // 冲刺期间速度由这里统一接管，覆盖动作里设的值
        if (dashing) bb.rb.velocity = dashDir * dashSpeed * effectSlowMultiplier;
        if (debugState && Time.frameCount % 30 == 0) PrintState();
    }
    void FixedUpdate()
    {
        // 硬钳制：保证绝对跑不出地图
        if (!bb.useBoundary) return;
        Vector2 p = bb.rb.position;
        Vector2 c = new Vector2(
            Mathf.Clamp(p.x, bb.mapMinX, bb.mapMaxX),
            Mathf.Clamp(p.y, bb.mapMinY, bb.mapMaxY));
        if (c != p)
        {
            bb.rb.position = c;
            Vector2 v = bb.rb.velocity;
            if ((p.x <= bb.mapMinX && v.x < 0f) || (p.x >= bb.mapMaxX && v.x > 0f)) v.x = 0f;
            if ((p.y <= bb.mapMinY && v.y < 0f) || (p.y >= bb.mapMaxY && v.y > 0f)) v.y = 0f;
            bb.rb.velocity = v;
        }
    }
    // ================= 类型配置 =================
    void Rebuild()
    {
        lastBuiltType = type;
        if (tree != null) tree.Reset();
        ApplyTypeSettings();
        tree = BuildTree();
    }
    /// <summary>五种类型的行为开关。改这里就等于改行为，不用动树的结构。</summary>
    void ApplyTypeSettings()
    {
        switch (type)
        {
            case EnemyType.Wanderer:
                bb.avoidDanger = true; bb.alwaysHunt = false; break;
            case EnemyType.Melee:
                bb.avoidDanger = false; bb.alwaysHunt = false; break;
            case EnemyType.Ranged:
                bb.avoidDanger = true; bb.alwaysHunt = false; break;
            case EnemyType.Hybrid:
                bb.avoidDanger = true; bb.alwaysHunt = false; break;
            case EnemyType.Danger:
                bb.avoidDanger = true; bb.alwaysHunt = true; break;
        }
    }
    // ================= 拼装行为树 =================
    BTNode BuildTree()
    {
        var branches = new List<BTNode>();
        // ---- ① 逃离危险（近战型故意没有：策划要求"进攻时不会远离危险"）----
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
        Vector2 away = (Vector2)transform.position - (Vector2)bb.danger.position;
        if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle;
        dashing = false;
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
            // 冷却中 / 还太远：用常态速度靠近
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
        else Drive(dir, 0f);
        if (cooldown <= 0f && bulletPrefab != null && dist <= attackRange)
        {
            Fire(dir);
            cooldown = fireCooldown;
            slowTimer = 0.6f;
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
        damageTimer = 0f;                  // 冲刺开始立刻能造成第一次伤害
        bb.rb.velocity = dashDir * dashSpeed;
        FaceDirection(dashDir, 1f);
        Debug.Log($"[Enemy/{type}] 开始冲刺！");
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
            speedMul = 0f;          // 冲刺时速度由 Update 接管
            turnMul = 0.25f;       // 冲刺时转向速度大幅下降
        }
        else if (slowTimer > 0f)
        {
            speedMul = 0.35f;       // 射击前后降速
            turnMul = 0.4f;
        }
        // 靠近边界时把朝外的分量压掉，让 AI 沿墙滑行
        Vector2 moveDir = (speed > 0.01f) ? SteerInsideBounds(dir) : dir;
        bb.rb.velocity = moveDir.normalized * speed * speedMul * effectSlowMultiplier;
        FaceDirection(dir, turnMul);   // 朝向仍按原目标，不受边界影响
    }
    /// <summary>靠近地图边界时压掉朝外的方向分量，避免 AI 顶在墙上来回抖。</summary>
    Vector2 SteerInsideBounds(Vector2 dir)
    {
        if (!bb.useBoundary) return dir;
        Vector2 p = bb.rb.position;
        float m = bb.boundaryMargin;
        if (p.x < bb.mapMinX + m) dir.x = Mathf.Max(dir.x, 0.4f);
        if (p.x > bb.mapMaxX - m) dir.x = Mathf.Min(dir.x, -0.4f);
        if (p.y < bb.mapMinY + m) dir.y = Mathf.Max(dir.y, 0.4f);
        if (p.y > bb.mapMaxY - m) dir.y = Mathf.Min(dir.y, -0.4f);
        return dir.sqrMagnitude < 0.0001f ? Vector2.right : dir;
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
        if (effectSlowTimer > 0f) effectSlowTimer -= dt;
        if (effectSlowTimer <= 0f) effectSlowMultiplier = 1f;
        if (cooldown > 0f) cooldown -= dt;
        if (slowTimer > 0f) slowTimer -= dt;
        if (retreatTimer > 0f) retreatTimer -= dt;
        if (damageTimer > 0f) damageTimer -= dt;
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
    /// <summary>
    /// 冲刺撞到玩家时造成伤害。
    /// 用 OnCollisionStay2D（实心碰撞体）：
    ///   - 有物理阻挡，不会穿模
    ///   - 接触期间每帧触发，配合 damageTimer 做伤害节流
    /// </summary>
    void OnCollisionStay2D(Collision2D collision) { HitPlayer(collision.collider); }
    void OnTriggerStay2D(Collider2D other) { HitPlayer(other); }
    void HitPlayer(Collider2D other)
    {
        if (!dashing || damageTimer > 0f) return;
        if (CellBody.HitPlayer(other, meleeDamage)) damageTimer = meleeDamageInterval;
    }
    void PrintState()
    {
        float d = bb.targetPlayer
            ? Vector2.Distance(transform.position, bb.targetPlayer.position)
            : -1f;
        string distText = d < 0f ? "无" : d.ToString("F1");
        Debug.Log($"[{type}] 饥饿={bb.hunger:F0} 玩家距离={distText} " +
                  $"危险={(bb.danger != null ? "是" : "否")} 冲刺中={dashing} 冷却={cooldown:F1}");
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
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, b.fleeExitRadius);
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, keepDistance);
        if (b.useBoundary)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Vector3 center = new Vector3((b.mapMinX + b.mapMaxX) * 0.5f,
                                         (b.mapMinY + b.mapMaxY) * 0.5f, 0f);
            Vector3 size = new Vector3(b.mapMaxX - b.mapMinX,
                                         b.mapMaxY - b.mapMinY, 0f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
