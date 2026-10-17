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
    [Tooltip("危险物靠到多近才躲")]
    public float escapeRadius = 5f;
    [Tooltip("危险物的图层（伤害蛋白、敌意细胞等）")]
    public LayerMask dangerMask;
    [Tooltip("玩家也算危险吗（近距离时）")]
    public bool playerIsDanger = true;
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
    // ---------- 运行时感知结果（AI 每帧刷新，节点只读） ----------
    [HideInInspector] public Transform targetPlayer;
    [HideInInspector] public Nutrient targetNutrient;
    [HideInInspector] public Transform danger;
    // ---------- 由 EnemyAI 按类型写入的行为开关 ----------
    [HideInInspector] public bool avoidDanger = true;   // 是否远离危险
    [HideInInspector] public bool alwaysHunt = false;  // 是否无视饥饿、主动找玩家
    private float playerRefreshTimer;
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
        if (playerRefreshTimer > 0f && cachedPlayer != null) return InRange(cachedPlayer);
        playerRefreshTimer = 0.3f;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        cachedPlayer = go != null ? go.transform : null;
        return InRange(cachedPlayer);
    }
    private Transform cachedPlayer;
    Transform InRange(Transform t)
    {
        if (t == null) return null;
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
    /// 找最近的危险物。用 OverlapCircle + 图层，比按标签遍历快得多。
    /// 想加"伤害蛋白"？给它一个图层，填进 dangerMask 就行，代码不用改。
    /// </summary>
    Transform FindDanger()
    {
        if (!avoidDanger) return null;
        Transform best = null;
        float bestSqr = escapeRadius * escapeRadius;
        if (playerIsDanger && targetPlayer != null)
        {
            float sqr = ((Vector2)targetPlayer.position - (Vector2)transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = targetPlayer; }
        }
        if (dangerMask.value != 0)
        {
            Collider2D hit = Physics2D.OverlapCircle(transform.position, escapeRadius, dangerMask);
            if (hit != null)
            {
                float sqr = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < bestSqr) best = hit.transform;
            }
        }
        return best;
    }
}