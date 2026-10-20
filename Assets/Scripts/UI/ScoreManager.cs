using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 分数系统。对应文档：
///   获取 1 能量 +1
///   摧毁其他分裂细胞 +10
///   击杀其他主细胞 +100
///
/// 用法：
///   1. 挂在一个空物体上（比如 HUD 下的 ScoreManager）
///   2. 把分数 HUD 的 Text 拖到 Score Text 字段
///   3. 在需要加分的地方调用下面三个方法
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("分数 HUD（三种二选一）")]
    [Tooltip("队友写的位图分数显示（ScoreHUD）。拖 HUD 根物体到这里，脚本会自动调 Initialize 并写 Score")]
    public ScoreHUD spriteScoreHUD;
    [Tooltip("分数显示用的 TextMeshPro 文本")]
    public TMP_Text scoreText;
    [Tooltip("如果 HUD 用的是旧版 Text，拖这里")]
    public Text legacyScoreText;
    [Tooltip("分数前面显示的文字")]
    public string prefix = "分数：";

    [Header("分值配置（对应文档）")]
    [Tooltip("每获得 1 点能量加多少分")]
    public int pointPerEnergy = 1;
    [Tooltip("摧毁一个分裂细胞加多少分")]
    public int pointPerSplitCell = 10;
    [Tooltip("击杀一个主细胞加多少分")]
    public int pointPerMainCell = 100;

    [Header("调试")]
    public bool logChanges = true;

    /// <summary>当前总分</summary>
    public int Score { get; private set; }

    // ------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        AutoFindHUD();
        RefreshHUD();
    }

    /// <summary>
    /// 自动找场景里的 ScoreHUD（包括未激活的、运行期生成的）。
    /// 这样即使 HUD 在编辑器里看不到/是预制体实例，也能接上。
    /// </summary>
    void AutoFindHUD()
    {
        if (spriteScoreHUD != null) return;

        spriteScoreHUD = FindObjectOfType<ScoreHUD>(true);   // true = 包含未激活的物体
        if (spriteScoreHUD != null)
        {
            spriteScoreHUD.Initialize();
            Debug.Log("[Score] 自动找到 ScoreHUD：" + spriteScoreHUD.name);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // 三个加分入口（对应文档三条规则）
    // ------------------------------------------------------------------

    /// <summary>吃到营养球 / 获得能量：每 1 能量 +1 分</summary>
    public void AddEnergyPoints(int energyAmount)
    {
        if (energyAmount <= 0) return;
        Add(energyAmount * pointPerEnergy, $"获得 {energyAmount} 能量");
    }

    /// <summary>摧毁其他分裂细胞：+10</summary>
    public void OnSplitCellDestroyed()
    {
        Add(pointPerSplitCell, "摧毁分裂细胞");
    }

    /// <summary>击杀其他主细胞：+100</summary>
    public void OnMainCellKilled()
    {
        Add(pointPerMainCell, "击杀主细胞");
    }

    // ------------------------------------------------------------------
    // 通用
    // ------------------------------------------------------------------

    public void Add(int points, string reason = null)
    {
        if (points == 0) return;

        Score += points;
        if (Score < 0) Score = 0;

        if (logChanges && !string.IsNullOrEmpty(reason))
            Debug.Log($"[Score] +{points}（{reason}）→ 总分 {Score}");

        RefreshHUD();
    }

    public void ResetScore()
    {
        Score = 0;
        RefreshHUD();
    }

    void RefreshHUD()
    {
        string text = prefix + Score;

        // HUD 可能是运行期才生成的，第一次用到时再找一次
        if (spriteScoreHUD == null) AutoFindHUD();

        // 队友的位图分数显示
        if (spriteScoreHUD != null) spriteScoreHUD.Score = Score;

        if (scoreText != null) scoreText.text = text;
        if (legacyScoreText != null) legacyScoreText.text = text;
    }

    // ------------------------------------------------------------------
    // 静态快捷方法：不用判空，没有 ScoreManager 也不会报错
    // ------------------------------------------------------------------

    public static void GainEnergy(int energy)
    {
        if (Instance != null) Instance.AddEnergyPoints(energy);
    }

    public static void SplitCellDestroyed()
    {
        if (Instance != null) Instance.OnSplitCellDestroyed();
    }

    public static void MainCellKilled()
    {
        if (Instance != null) Instance.OnMainCellKilled();
    }
}
