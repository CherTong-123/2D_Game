using UnityEngine;

/// <summary>
/// 细胞类型定义（数据驱动）。
///
/// 策划表里的属性全部在这里，**所有贴图都是 Inspector 字段**——
/// 美术给了图之后直接拖进 Body / Icon 即可，代码一行不用改。
///
/// 用法：Project 窗口右键 → 创建 → 细胞战舰 → 细胞类型
/// </summary>
[CreateAssetMenu(fileName = "CellType", menuName = "细胞战舰/细胞类型", order = 0)]
public class CellType : ScriptableObject
{
    /// <summary>细胞的功能；数值与贴图仍由资产配置。</summary>
    public enum Function { Main, Spike, Storage, Power, DamageProtein, SlowProtein, Spray, Armor, Transport }

    [Header("标识")]
    public string id = "cell";
    public string displayName = "细胞";
    [TextArea(2, 4)] public string description = "";

    [Header("属性（对应策划表）")]
    [Tooltip("生命值")]
    public int hp = 100;
    [Tooltip("细胞大小：1 / 2 / 4 / 6 / 9，对应占用的格子数")]
    public int cellsize = 1;
    [Tooltip("通透性：0 无 / 1 弱 / 2 强")]
    [Range(0, 2)] public int pmt = 0;
    [Tooltip("可附着：分裂时新细胞必须与 attach=1 的细胞相邻")]
    public bool attach = false;
    [Tooltip("分裂消耗的能量")]
    public int cost = 100;
    [Tooltip("方向性：分裂预览时可以旋转")]
    public bool divity = false;
    [Tooltip("沟通：主细胞默认为 1")]
    public bool link = false;
    [Tooltip("进食：主细胞默认为 1")]
    public bool eat = false;

    [Header("细胞功能")]
    public Function function;
    [Tooltip("储能细胞增加的能量上限")]
    public float storageBonus = 100f;
    [Tooltip("动力细胞增加的移动速度和加速度比例")]
    public float movementBonus = 0.15f;
    [Tooltip("尖刺接触伤害")]
    public int contactDamage = 100;
    [Tooltip("蛋白合成间隔，秒；系统细则补充前使用此默认值")]
    public float productionInterval = 3f;
    [Tooltip("收到外部蛋白后暂停合成的时间，秒")]
    public float receivingPause = 1f;
    [Tooltip("伤害蛋白的伤害；系统细则补充前使用此默认值")]
    public int proteinDamage = 15;
    [Tooltip("减速后保留的速度比例")]
    public float slowMultiplier = 0.5f;
    [Tooltip("减速持续时间，秒")]
    public float slowDuration = 2f;
    [Tooltip("喷射细胞发射蛋白的速度，世界单位/秒")]
    public float spraySpeed = 20f;

    [Header("表现 —— 美术后期直接替换这几张图")]
    [Tooltip("右侧列表里的图标")]
    public Sprite icon;
    [Tooltip("场景中的细胞贴图。留空会用下面的占位颜色自动生成一个方块")]
    public Sprite body;
    [Tooltip("染色")]
    public Color tint = Color.white;

    [Header("占位")]
    [Tooltip("没填 body 时自动生成的颜色（后期填了 body 就用不到了）")]
    public Color placeholderColor = new Color(0.35f, 0.8f, 1f, 0.9f);

    /// <summary>cellsize → 占用的格子尺寸</summary>
    public Vector2Int FootprintSize()
    {
        switch (cellsize)
        {
            case 2: return new Vector2Int(1, 2);
            case 4: return new Vector2Int(2, 2);
            case 6: return new Vector2Int(2, 3);
            case 9: return new Vector2Int(3, 3);
            default: return new Vector2Int(1, 1);   // 1
        }
    }

    /// <summary>方向性细胞旋转后，宽高可能互换</summary>
    public Vector2Int FootprintSize(int rotation)
    {
        Vector2Int s = FootprintSize();
        bool swap = rotation % 180 != 0;
        return swap ? new Vector2Int(s.y, s.x) : s;
    }
}
