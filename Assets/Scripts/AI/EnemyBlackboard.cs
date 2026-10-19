using UnityEngine;
/// <summary>
/// 敌方细胞 AI 的"黑板"——所有节点共享的数据 + 感知结果。
///
/// 行为树本身不存状态，节点需要的数据都从这里读、往这里写。
/// 把数据集中在一处，是行为树能保持"节点无状态、可复用"的关键。
/// </summary>
public class EnemyBlackboard : MonoBehaviour
{
    [Header("引用（留空会自动获取）")]
    public Rigidbody2D rb;
    [Tooltip("用来做朝向旋转的物体。一般就是自己。")]
    public Transform body;
    [Header("感知范围")]
    [Tooltip("找玩家 / 营养球的最大距离")]
    public float senseRadius = 15f;
    [Tooltip("危险物靠到多近才躲 —— 进入逃跑状态的距离")]
    public float escapeRadius = 5f;
    [Tooltip("已经在逃跑后，要跑出多远才脱离。必须 > Escape Radius，否则会在边界反复横跳")]
    public float fleeExitRadius = 12f;
    [Tooltip("危险物的图层（伤害蛋白、敌意细胞等）")]
    public LayerMask dangerMask;
    [Tooltip("玩家算不算危险源")]
    public bool playerIsDanger = true;
    [Tooltip("勾上 = 只有挂了 ThreatMarker 的玩家才算危险（策划要求：带攻击细胞的玩家）\n取消 = 任何玩家都算危险（临时测试用）")]
    public bool playerNeedsThreatMarker = true;
    [Header("地图边界")]
    [Tooltip("勾上后 AI 不会跑出下面的矩形范围")]
    public bool useBoundary = true;
    [Tooltip("和 NutrientSpawner 的 mapMin/mapMax 保持一致")]
    public float mapMinX = -50f;
    public float mapMaxX = 50f;
    public float mapMinY = -50f;
    public float mapMaxY = 50f;
    [Tooltip("离边界多近就开始往回拐（软引导的宽度）")]
    public float boundaryMargin = 3f;
    [Header("饥饿值")]
    [Tooltip("当前饥饿值 0~100，越高越饿")]
    public float hunger = 50f;
    [Tooltip("每秒增加多少")]
    public float hungerRate = 4f;
    [Tooltip("超过这个值就去攻击玩家")]
    public float attackHunger = 70f;
    [Tooltip("吃掉一个营养球减多少饥饿值")]
    public float hungerPerMeal = 25f;
    [Header("能量")]
    public float energy = 100f;
    public float maxEnergy = 400f;
    [Header("生命值")]
    public int hp = 100;
    public void TakeDamage(int amount)
    {
        hp = Mathf.Max(0, hp - amount);
        if (hp == 0) { gameObject.SetActive(false); Destroy(gameObject); }
    }
    // ---------- 运行时感知结果（AI 每帧刷新，节点只读） ----------
    [HideInInspector] public Transform targetPlayer;
    [HideInInspector] public Nutrient targetNutrient;
    [HideInInspector] public Transform danger;
    // ---------- 由 EnemyAI 按类型写入的行为开关 ----------
    [HideInInspector] public bool avoidDanger = true;   // 是否远离危险
    [HideInInspector] public bool alwaysHunt = false;  // 是否无视饥饿、主动找玩家
    // ---------- 内部状态 ----------
    private float playerRefreshTimer;
    private Transform cachedPlayer;
    private ThreatMarker cachedPlayerThreat;
    private bool fleeing;               // 是否正处于逃跑状态（迟滞用）
    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (body == null) body = transform;
    }
    void Update()
    {
        // 饥饿值随时间上升
        hunger = Mathf.Clamp(hunger + hungerRate * Time.deltaTime, 0f, 100f);
    }
    /// <summary>刷新感知结果。AI 每帧调用一次。</summary>
    public void Sense()
    {
        targetPlayer = FindPlayer();
        targetNutrient = FindNearestNutrient();
        danger = FindDanger();
    }
    /// <summary>吃到东西：加能量、减饥饿。</summary>
    public void AddEnergy(int amount)
    {
        energy = Mathf.Min(energy + amount, maxEnergy);
        hunger = Mathf.Max(hunger - hungerPerMeal, 0f);
    }
    /// <summary>玩家是否在感知范围内。找不到就返回 null。</summary>
    Transform FindPlayer()
    {
        // 玩家只有一个，但 FindGameObjectWithTag 有开销，缓存 0.3 秒刷一次
        playerRefreshTimer -= Time.deltaTime;
        if (playerRefreshTimer > 0f && cachedPlayer != null)
            return InRange(cachedPlayer);
        playerRefreshTimer = 0.3f;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        cachedPlayer = go != null ? go.transform : null;
        // 顺手缓存"威胁标记"组件，用来判断玩家有没有攻击细胞
        cachedPlayerThreat = (go != null) ? go.GetComponent<ThreatMarker>() : null;
        return InRange(cachedPlayer);
    }
    Transform InRange(Transform t)
    {
        if (t == null) return null;
        var health = t.GetComponent<maincell>();
        if (health != null && health.IsDead) return null;
        float sqr = ((Vector2)t.position - (Vector2)transform.position).sqrMagnitude;
        return sqr <= senseRadius * senseRadius ? t : null;
    }
    /// <summary>找最近的营养球。遍历静态列表，不做任何内存分配。</summary>
    Nutrient FindNearestNutrient()
    {
        Nutrient best = null;
        float bestSqr = senseRadius * senseRadius;
        Vector2 me = transform.position;
        var list = Nutrient.All;
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            if (n == null) continue;
            float sqr = ((Vector2)n.transform.position - me).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = n;
            }
        }
        return best;
    }
    /// <summary>
    /// 找最近的危险物。
    ///
    /// 两个关键设计：
    /// 1) 双阈值（迟滞）：没在逃时用小半径进入，已在逃时用大半径才脱离。
    ///    两个半径相同的话，AI 会在边界线上"追→逃→追→逃"每帧切换，
    ///    两帧速度互相抵消，看起来就像卡死在那个圆圈上。
    /// 2) 玩家必须"带攻击细胞"（挂了且启用了 ThreatMarker）才算危险，对应策划文档的设定。
    /// </summary>
    Transform FindDanger()
    {
        if (!avoidDanger) return null;
        // ---- 双阈值：逃跑状态切换用不同的扫描半径 ----
        float radius = fleeing ? Mathf.Max(fleeExitRadius, escapeRadius + 1f) : escapeRadius;
        float sqrMax = radius * radius;
        Transform best = null;
        float bestSqr = sqrMax;
        // ---- 玩家：只有带攻击细胞时才算危险 ----
        bool playerArmed = !playerNeedsThreatMarker
                           || (cachedPlayerThreat != null && cachedPlayerThreat.enabled);
        if (playerIsDanger && playerArmed && targetPlayer != null)
        {
            float sqr = ((Vector2)targetPlayer.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = targetPlayer; }
        }
        // ---- 其它危险物（伤害蛋白等），按图层找 ----
        if (dangerMask.value != 0)
        {
            Collider2D hit = Physics2D.OverlapCircle(transform.position, radius, dangerMask);
            if (hit != null)
            {
                float sqr = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < bestSqr) best = hit.transform;
            }
        }
        // 记录逃跑状态，供下一帧决定用哪个扫描半径
        fleeing = (best != null);
        return best;
    }
}
