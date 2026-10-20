// ============================================================================
// ScoreHUD.cs：右上角六位分数显示，与 TimeHUD 处于同一水平线。
// 素材与 TimeNumber 一样是整屏 1920x1080 透明 PNG，每个字形位于整屏图的
// 固定像素位置，因此按字形中心做偏移，把整屏贴图当作“图层”叠在画布上。
// 暂定六位、不足六位补 0；提供接收分数的 Score 变量，未赋值时为 0。
// ============================================================================
using UnityEngine;
using UnityEngine.UI;

public class ScoreHUD : MonoBehaviour
{
    private const float RefWidth = 1920f;
    private const float RefHeight = 1080f;

    private const int DigitCount = 6;

    // 每个数字在整屏源图里的字形中心（左上角像素坐标）。
    private static readonly float[] DigitFileX =
    {
        1181f, 1255f, 1325f, 1400f, 1476f,
        1553f, 1628f, 1708f, 1780f, 1859f
    };
    private static readonly float[] DigitFileY =
    {
        83f, 83f, 84f, 83f, 83f,
        83f, 83f, 80f, 83f, 83f
    };

    // 界面上的槽位（1920x1080 参考空间，原点在左下），右上角从右往左排 6 位。
    private static readonly float[] SlotX = { 1475f, 1550f, 1625f, 1700f, 1775f, 1850f };
    private const float RowY = 1010f; // 与 TimeHUD 的 RowY 对齐，保证同一水平线

    [SerializeField] private Sprite[] digits = new Sprite[10];

    private readonly Image[] digitImages = new Image[DigitCount];
    private int score;
    private bool initialized;

    // 接收分数的变量：未设置时返回 0。
    public int Score
    {
        get => score;
        set
        {
            score = Mathf.Max(0, value);
            if (initialized) Refresh();
        }
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        LoadSprites();

        for (int i = 0; i < digitImages.Length; i++)
            digitImages[i] = CreateLayer("ScoreDigit" + i, DigitFileX[0], DigitFileY[0], SlotX[i]);

        Refresh();
    }

    // 显示 score 对应的六位数字，不足六位补 0，超过六位截断到 999999。
    private void Refresh()
    {
        int value = Mathf.Min(score, 999999);
        for (int i = 0; i < digitImages.Length; i++)
        {
            int place = DigitCount - 1 - i; // i=0 是最高位（十万位）
            int divisor = 1;
            for (int p = 0; p < place; p++) divisor *= 10;
            int digit = (value / divisor) % 10;
            SetDigit(digitImages[i], digit, SlotX[i]);
        }
    }

    private void SetDigit(Image image, int value, float sx)
    {
        if (image == null) return;
        if (value < 0 || value > 9 || digits == null || digits[value] == null)
        {
            image.enabled = false;
            return;
        }

        image.enabled = true;
        image.sprite = digits[value];
        image.rectTransform.anchoredPosition = ScreenToAnchored(sx, RowY, DigitFileX[value], DigitFileY[value]);
    }

    private Image CreateLayer(string name, float fileX, float fileY, float sx)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(RefWidth, RefHeight);
        rect.anchoredPosition = ScreenToAnchored(sx, RowY, fileX, fileY);

        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.enabled = false;
        return image;
    }

    private static Vector2 ScreenToAnchored(float sx, float sy, float fileX, float fileY)
    {
        return new Vector2(sx - fileX, sy - RefHeight + fileY);
    }

    private void LoadSprites()
    {
        Sprite[] loaded = Resources.LoadAll<Sprite>("Score");
        foreach (Sprite sprite in loaded)
        {
            if (sprite == null) continue;
            if (sprite.name.Length == 1 && sprite.name[0] >= '0' && sprite.name[0] <= '9')
                digits[sprite.name[0] - '0'] = sprite;
        }

        for (int i = 0; i < digits.Length; i++)
            if (digits[i] == null) Debug.LogWarning($"[ScoreHUD] 未找到数字资源 {i}（Resources/Score/{i}.png）");
    }
}
