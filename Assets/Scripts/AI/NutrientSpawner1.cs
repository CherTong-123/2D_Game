using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 营养球生成器：开局 / 重置时调用 Generate()。
/// 地图 100x100，区块 20x20 -> 5x5 = 25 个区块。
/// 每个区块随机生成 3~4 个营养球，类型比例 小:中:大 = 6:3:1。
/// 能量：小=4，中=16，大=64。
/// 三种营养球使用各自的美术 Sprite（小营养球.png / 中营养球.png / 大营养球.png）。
/// 小营养球用 CircleCollider2D，中/大营养球用 PolygonCollider2D。
/// </summary>
public class NutrientSpawner : MonoBehaviour
{
    [Header("地图范围（世界坐标）")]
    public float mapMinX = -50f;
    public float mapMaxX = 50f;
    public float mapMinY = -50f;
    public float mapMaxY = 50f;

    [Header("区块设置")]
    public float blockSize = 20f;        // 区块边长
    public int minPerBlock = 3;           // 每区块最少营养球
    public int maxPerBlock = 4;           // 每区块最多营养球

    [Header("基础预制体（挂 Nutrient + CircleCollider2D + PolygonCollider2D + Rigidbody2D(Kinematic)）")]
    public GameObject nutrientPrefab;

    [Header("三种营养球的美术 Sprite")]
    public Sprite smallSprite;   // 小营养球.png
    public Sprite mediumSprite;  // 中营养球.png
    public Sprite largeSprite;   // 大营养球.png

    [Header("【小营养球】")]
    public float smallSize = 1f;
    public float smallColliderRadius = 0.4f;

    [Header("【中营养球】")]
    public float mediumSize = 1.5f;
    public float mediumColliderRadius = 0.6f;

    [Header("【大营养球】")]
    public float largeSize = 2.5f;
    public float largeColliderRadius = 1.0f;

    [Header("【碰撞箱偏移（一般保持 0,0 就行）】")]
    public Vector2 smallColliderOffset = Vector2.zero;
    public Vector2 mediumColliderOffset = Vector2.zero;
    public Vector2 largeColliderOffset = Vector2.zero;

    // 权重：小:中:大 = 6:3:1
    private const float wSmall = 6f;
    private const float wMedium = 3f;
    private const float wLarge = 1f;

    private readonly List<GameObject> spawned = new List<GameObject>();

    void Start()
    {
        Generate();
    }

    public void Generate()
    {
        ClearAll();

        int cols = Mathf.RoundToInt((mapMaxX - mapMinX) / blockSize);
        int rows = Mathf.RoundToInt((mapMaxY - mapMinY) / blockSize);

        for (int cx = 0; cx < cols; cx++)
        {
            for (int cy = 0; cy < rows; cy++)
            {
                float bxMin = mapMinX + cx * blockSize;
                float bxMax = bxMin + blockSize;
                float byMin = mapMinY + cy * blockSize;
                float byMax = byMin + blockSize;

                int count = Random.Range(minPerBlock, maxPerBlock + 1);

                for (int i = 0; i < count; i++)
                {
                    NutrientType type = RollType();
                    Vector2 pos = FindFreePosition(bxMin, bxMax, byMin, byMax, type);
                    SpawnOne(pos, type);
                }
            }
        }

        Debug.Log($"[NutrientSpawner] 共生成 {spawned.Count} 个营养球");
    }

    private NutrientType RollType()
    {
        float total = wSmall + wMedium + wLarge;
        float r = Random.Range(0f, total);
        if (r < wSmall) return NutrientType.Small;
        if (r < wSmall + wMedium) return NutrientType.Medium;
        return NutrientType.Large;
    }

    // ===== 按类型大小自动算安全间距，大球留的空间更大 =====
    private Vector2 FindFreePosition(float xMin, float xMax, float yMin, float yMax, NutrientType newType)
    {
        float newRadius = GetSafeRadius(newType);

        // 最多尝试 30 次，找不到空位就直接用随机位置（防止死循环）
        for (int attempt = 0; attempt < 100; attempt++)
        {
            float x = Random.Range(xMin, xMax);
            float y = Random.Range(yMin, yMax);
            Vector2 pos = new Vector2(x, y);

            bool tooClose = false;
            foreach (var go in spawned)
            {
                if (go == null) continue;

                float existingRadius = 0.5f;
                var nutrient = go.GetComponent<Nutrient>();
                if (nutrient != null)
                {
                    existingRadius = GetSafeRadius(nutrient.type);
                }

                // 中心距 >= 两个半径之和，才算不重叠
                if (Vector2.Distance(go.transform.position, pos) < newRadius + existingRadius)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose) return pos;
        }

        return new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
    }

    private float GetSafeRadius(NutrientType type)
    {
        switch (type)
        {
            case NutrientType.Small: return smallSize * 0.5f + 0.3f;
            case NutrientType.Medium: return mediumSize * 0.5f + 0.3f;
            case NutrientType.Large: return largeSize * 0.5f + 2.0f; // 大球形状不规则，余量给多
            default: return 0.5f;
        }
    }

    private void SpawnOne(Vector2 pos, NutrientType type)
    {
        if (nutrientPrefab == null)
        {
            Debug.LogError("[NutrientSpawner] 未指定 nutrientPrefab！");
            return;
        }

        GameObject go = Instantiate(nutrientPrefab, pos, Quaternion.identity, transform);
        go.name = $"Nutrient_{type}_{pos.x:F1}_{pos.y:F1}";

        Nutrient n = go.GetComponent<Nutrient>();
        if (n == null) n = go.AddComponent<Nutrient>();
        n.type = type;
        n.energy = EnergyOf(type);

        // 换 Sprite
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = SpriteOf(type);
            sr.color = Color.white;
        }

        // 视觉缩放
        float scale = SizeOf(type);
        go.transform.localScale = Vector3.one * scale;

        // 设置碰撞箱
        ApplyCollider(go, type, scale);

        spawned.Add(go);
    }

    private void ApplyCollider(GameObject go, NutrientType type, float scale)
    {
        // 小营养球用 Circle，中/大营养球用 Polygon
        bool useCircle = (type == NutrientType.Small);

        var circle = go.GetComponent<CircleCollider2D>();
        var poly = go.GetComponent<PolygonCollider2D>();

        if (circle != null) circle.enabled = useCircle;
        if (poly != null) poly.enabled = !useCircle;

        // 小营养球：CircleCollider2D
        if (useCircle && circle != null)
        {
            circle.radius = smallColliderRadius / scale;
            circle.offset = smallColliderOffset / scale;
            circle.isTrigger = true;
        }

        // 中/大营养球：PolygonCollider2D（从 Sprite 读 Physics Shape）
        if (!useCircle && poly != null)
        {
            Sprite sprite = SpriteOf(type);
            if (sprite != null)
            {
                int shapeCount = sprite.GetPhysicsShapeCount();
                if (shapeCount > 0)
                {
                    List<Vector2> points = new List<Vector2>();
                    sprite.GetPhysicsShape(0, points);

                    if (points.Count > 2)
                    {
                        poly.pathCount = 1;
                        poly.SetPath(0, points);
                        poly.offset = Vector2.zero;
                        poly.isTrigger = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[NutrientSpawner] Sprite '{sprite.name}' 没有 Physics Shape，请在 Sprite Editor → Custom Physics Shape 里 Generate 一下");
                }
            }
        }
    }

    public static int EnergyOf(NutrientType t)
    {
        switch (t)
        {
            case NutrientType.Small: return 4;
            case NutrientType.Medium: return 16;
            case NutrientType.Large: return 64;
            default: return 0;
        }
    }

    private Sprite SpriteOf(NutrientType t)
    {
        switch (t)
        {
            case NutrientType.Small: return smallSprite;
            case NutrientType.Medium: return mediumSprite;
            case NutrientType.Large: return largeSprite;
            default: return null;
        }
    }

    private float SizeOf(NutrientType t)
    {
        switch (t)
        {
            case NutrientType.Small: return smallSize;
            case NutrientType.Medium: return mediumSize;
            case NutrientType.Large: return largeSize;
            default: return 1f;
        }
    }

    public void ClearAll()
    {
        foreach (var go in spawned)
        {
            if (go != null) Destroy(go);
        }
        spawned.Clear();
    }

    public void Despawn(GameObject go)
    {
        if (go != null)
        {
            spawned.Remove(go);
            Destroy(go);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        int cols = Mathf.RoundToInt((mapMaxX - mapMinX) / blockSize);
        int rows = Mathf.RoundToInt((mapMaxY - mapMinY) / blockSize);
        for (int cx = 0; cx <= cols; cx++)
        {
            float x = mapMinX + cx * blockSize;
            Gizmos.DrawLine(new Vector3(x, mapMinY, 0), new Vector3(x, mapMaxY, 0));
        }
        for (int cy = 0; cy <= rows; cy++)
        {
            float y = mapMinY + cy * blockSize;
            Gizmos.DrawLine(new Vector3(mapMinX, y, 0), new Vector3(mapMaxX, y, 0));
        }
    }
}
