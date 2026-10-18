using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 GameView 的 StatePanel 上，根据玩家当前能量和血量，让进度条从左到右按比例变黑。
/// 黑色遮罩覆盖的比例 = 1 - 数值/100，数值越低，黑色从左侧越向右覆盖。
/// </summary>
public class StatePanel : MonoBehaviour
{
    [Header("进度条")]
    [SerializeField] private Image energyFill;   // EnergyBar 下的 EnergyFill
    [SerializeField] private Image hpFill;       // HPBar 下的 HPFill

    [Header("数据来源（留空会自动按 Player 标签查找）")]
    [SerializeField] private em playerEnergy;    // 玩家能量脚本
    [SerializeField] private maincell playerCell; // 玩家血量脚本

    [Header("满值（1/100 比例）")]
    [SerializeField] private float energyMax = 100f;
    [SerializeField] private float hpMax = 100f;

    [Header("变黑效果")]
    [SerializeField] private Color darkColor = Color.black;   // 变黑后的颜色（默认纯黑）

    private Image energyMask;   // 能量条上的黑色遮罩
    private Image hpMask;       // 血条上的黑色遮罩

    private void Start()
    {
        if (playerEnergy == null || playerCell == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (playerEnergy == null) playerEnergy = player.GetComponent<em>();
                if (playerCell == null) playerCell = player.GetComponent<maincell>();
            }
        }

        energyMask = CreateDarkMask(energyFill);
        hpMask = CreateDarkMask(hpFill);

        // 暂停（按 Esc）时隐藏 HUD，恢复时再显示
        if (PauseController.Instance != null)
            PauseController.Instance.OnPauseChanged += HandlePauseChanged;
    }

    private void Update()
    {
        float energyRatio = playerEnergy != null
            ? Mathf.Clamp01((float)playerEnergy.initialenergy / energyMax)
            : 1f;
        ApplyMask(energyMask, energyRatio);

        float hpRatio = playerCell != null
            ? Mathf.Clamp01(playerCell.hp / hpMax)
            : 1f;
        ApplyMask(hpMask, hpRatio);
    }

    /// <summary>在 fill 上创建一个同尺寸的黑色遮罩，从左侧按比例覆盖。</summary>
    private Image CreateDarkMask(Image fill)
    {
        if (fill == null) return null;

        // 主进度条保持完整显示
        fill.type = Image.Type.Simple;
        fill.fillAmount = 1f;

        GameObject maskGo = new GameObject("DarkMask", typeof(RectTransform));
        maskGo.transform.SetParent(fill.transform, false);

        RectTransform rt = (RectTransform)maskGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image mask = maskGo.AddComponent<Image>();
        mask.sprite = fill.sprite;
        mask.type = Image.Type.Filled;
        mask.fillMethod = Image.FillMethod.Horizontal;
        mask.fillOrigin = (int)Image.OriginHorizontal.Right;   // 从右开始
        mask.fillClockwise = true;
        mask.color = darkColor;
        mask.raycastTarget = false;

        return mask;
    }

    /// <summary>遮罩覆盖比例 = 1 - 当前比例，数值越低，黑色从右向左覆盖越多。</summary>
    private void ApplyMask(Image mask, float ratio)
    {
        if (mask == null) return;
        mask.fillAmount = Mathf.Clamp01(1f - ratio);
    }

    private void HandlePauseChanged(bool paused)
    {
        gameObject.SetActive(!paused);
    }

    private void OnDestroy()
    {
        if (PauseController.Instance != null)
            PauseController.Instance.OnPauseChanged -= HandlePauseChanged;
    }
}
