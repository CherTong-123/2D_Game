using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏流程管理：
///   ① 每 20 秒从 5 种 AI 里随机生成一个敌人
///   ② 每 3 分钟重置一次场景 —— 只清除【场上的营养体】和【敌对 AI】
///      保留：分数、时间、玩家信息（能量、已附着的细胞）
///
/// 用法：
///   1. 挂在一个空物体上（比如 GameFlow）
///   2. Enemy Prefab 拖 Enemy.prefab
///   3. 其余参数按需调
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    [Header("敌人生成")]
    [Tooltip("敌人预制体（没有就用下面的 Prefab 列表）")]
    public GameObject enemyPrefab;
    [Tooltip("敌人实例的父节点，留空则放在根节点")]
    public Transform enemyRoot;
    [Tooltip("每隔多少秒生成一个敌人")]
    public float spawnInterval = 20f;
    [Tooltip("场上最多同时存在多少敌人")]
    public int maxEnemies = 30;
    [Tooltip("生成距离玩家多远（最小/最大）")]
    public float spawnMinDistance = 25f;
    public float spawnMaxDistance = 45f;

    [Header("阶段重置")]
    [Tooltip("每隔多少秒重置一次场景（3 分钟 = 180）")]
    public float resetInterval = 180f;
    [Tooltip("重置时清除营养体")]
    public bool clearNutrients = true;
    [Tooltip("重置时清除敌对 AI")]
    public bool clearEnemies = true;
    [Tooltip("重置时是否重新生成营养球（如果场上没有 NutrientSpawner 就忽略）")]
    public bool respawnNutrients = true;

    [Header("调试")]
    public bool logEvents = true;

    // ------------------------------------------------------------------

    private float spawnTimer;
    private float resetTimer;
    private Transform player;
    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();

    void Start()
    {
        var pm = FindObjectOfType<MicrobeController>();
        if (pm != null) player = pm.transform;

        spawnTimer = 0f;
        resetTimer = 0f;

        if (logEvents)
            Debug.Log($"[GameFlow] 启动：每 {spawnInterval}s 生成敌人，每 {resetInterval}s 重置场景");
    }

    void Update()
    {
        // ① 定时生成敌人
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnRandomEnemy();
        }

        // ② 定时重置场景
        resetTimer += Time.deltaTime;
        if (resetTimer >= resetInterval)
        {
            resetTimer = 0f;
            ResetStage();
        }
    }

    // ------------------------------------------------------------------
    // 生成敌人
    // ------------------------------------------------------------------

    void SpawnRandomEnemy()
    {
        if (enemyPrefab == null)
        {
            if (logEvents) Debug.LogWarning("[GameFlow] Enemy Prefab 没有配置，无法生成敌人");
            return;
        }

        // 清理已被销毁的引用
        spawnedEnemies.RemoveAll(e => e == null);

        if (spawnedEnemies.Count >= maxEnemies)
        {
            if (logEvents) Debug.Log($"[GameFlow] 敌人已达上限 {maxEnemies}，本次跳过");
            return;
        }

        Vector3 pos = RandomSpawnPosition();
        GameObject go = Instantiate(enemyPrefab, pos, Quaternion.identity, enemyRoot);
        spawnedEnemies.Add(go);

        // 随机 5 种类型之一（EnemyAI 会在 Update 里检测 type 变化并重建行为树）
        var ai = go.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.type = (EnemyType)Random.Range(0, 5);
            if (logEvents) Debug.Log($"[GameFlow] 生成敌人 {ai.type} @ {pos}");
        }
        else if (logEvents)
        {
            Debug.LogWarning("[GameFlow] 敌人预制体上没有 EnemyAI 组件");
        }
    }

    Vector3 RandomSpawnPosition()
    {
        Vector2 center = player != null ? (Vector2)player.position : Vector2.zero;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist  = Random.Range(spawnMinDistance, spawnMaxDistance);

        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
    }

    // ------------------------------------------------------------------
    // 阶段重置
    // ------------------------------------------------------------------

    /// <summary>
    /// 重置场景：只清除营养体和敌对 AI。
    /// 分数、时间、玩家能量、已附着的细胞一律不动。
    /// </summary>
    public void ResetStage()
    {
        int nNutrient = 0;
        int nEnemy = 0;

        NutrientSpawner spawner = FindObjectOfType<NutrientSpawner>();

        if (clearNutrients)
        {
            // 先让生成器清空（它有内部记账），再兜底清一次静态列表
            if (spawner != null) spawner.ClearAll();

            // 找到生成器用的模板，绝不能销毁它（否则 Nutrient Prefab 引用会变成 Missing）
            Nutrient template = null;
            if (spawner != null && spawner.nutrientPrefab != null)
                template = spawner.nutrientPrefab.GetComponent<Nutrient>();

            // 直接搜场景里的 Nutrient 组件（不依赖 Nutrient.All 静态列表，更可靠）
            var nutrients = FindObjectsOfType<Nutrient>(true);
            foreach (var n in nutrients)
            {
                if (n == null) continue;
                if (n == template) continue;                              // 跳过模板
                if (n.gameObject.scene.name == "DontDestroyOnLoad") continue; // 跳过常驻对象
                Destroy(n.gameObject);
                nNutrient++;
            }

            // 重新铺满营养球
            if (respawnNutrients && spawner != null) spawner.Generate();
        }

        if (clearEnemies)
        {
            var ais = FindObjectsOfType<EnemyAI>();
            foreach (var ai in ais)
            {
                if (ai == null) continue;
                Destroy(ai.gameObject);
                nEnemy++;
            }
            spawnedEnemies.Clear();
        }

        if (logEvents)
            Debug.Log($"[GameFlow] 阶段重置：清除 {nNutrient} 个营养体、{nEnemy} 个敌人（分数/时间/玩家信息保留）");
    }
}
