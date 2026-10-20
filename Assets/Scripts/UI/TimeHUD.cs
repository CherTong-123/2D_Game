// ============================================================================
// TimeHUD.cs：用 TimeNumber 里的数字与冒号拼出时间 HUD。
// 支持正向计时与循环倒计时两种模式，可配置位置、缩放和是否显示背景。
// 资源以整屏 1920x1080 透明 PNG 提供，每个字形位于整屏图的固定像素位置，
// 因此这里按字形中心做偏移，把整屏贴图当作“图层”叠在画布上。
// ============================================================================
using UnityEngine;
using UnityEngine.UI;

public class TimeHUD : MonoBehaviour
{
    private const float RefWidth = 1920f;
    private const float RefHeight = 1080f;

    // 每个数字在其整屏源图里的字形中心（左上角像素坐标）。
    private static readonly float[] DigitFileX =
    {
        1182f, 1254f, 1324f, 1400f, 1476f,
        1552f, 1628f, 1708f, 1780f, 1860f
    };
    private const float DigitFileY = 84f;
    private const float ColonFileX = 1098f;
    private const float ColonFileY = 84f;
    private const float BgFileX = 1116f;
    private const float BgFileY = 176f;

    // MM:SS 各槽位相对 HUD 中心的水平偏移（全尺寸单位）。
    private static readonly float[] SlotOffsets = { -122f, -54f, 54f, 122f };

    [Header("布局")]
    [SerializeField] private Vector2 center = new Vector2(960f, 960f);
    [SerializeField] private float scale = 1f;
    [SerializeField] private bool showBackground = true;

    [Header("计时")]
    [SerializeField] private bool countdown;
    [SerializeField] private float countdownSeconds = 180f;

    [SerializeField] private Sprite[] digits = new Sprite[10];
    [SerializeField] private Sprite colon;
    [SerializeField] private Sprite background;

    private readonly Image[] digitImages = new Image[4];
    private float elapsed;
    private float secondAccumulator;
    private int countdownRemaining;
    private string lastShown = "";
    private bool initialized;

    public void Configure(Vector2 screenCenter, float hudScale, bool withBackground, bool isCountdown, float countdownDuration)
    {
        center = screenCenter;
        scale = hudScale;
        showBackground = withBackground;
        countdown = isCountdown;
        countdownSeconds = Mathf.Max(1f, countdownDuration);
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        LoadSprites();

        countdownRemaining = Mathf.RoundToInt(countdownSeconds);
        elapsed = 0f;
        secondAccumulator = 0f;

        if (showBackground)
            CreateLayer("TimeBG", background, BgFileX, BgFileY, center.x, center.y - 42f * scale);

        CreateLayer("TimeColon", colon, ColonFileX, ColonFileY, center.x, center.y);
        for (int i = 0; i < digitImages.Length; i++)
        {
            float sx = center.x + SlotOffsets[i] * scale;
            digitImages[i] = CreateLayer("TimeDigit" + i, null, DigitFileX[0], DigitFileY, sx, center.y);
        }

        Display(countdown ? countdownRemaining : 0);
    }

    private void Update()
    {
        if (!initialized) return;

        if (countdown)
        {
            secondAccumulator += Time.deltaTime;
            while (secondAccumulator >= 1f)
            {
                secondAccumulator -= 1f;
                countdownRemaining--;
                if (countdownRemaining < 0)
                    countdownRemaining = Mathf.RoundToInt(countdownSeconds);
            }
            Display(countdownRemaining);
        }
        else
        {
            elapsed += Time.deltaTime;
            Display(Mathf.FloorToInt(elapsed));
        }
    }

    private void Display(int totalSeconds)
    {
        int minutes = Mathf.Min(totalSeconds / 60, 99);
        int seconds = totalSeconds % 60;
        string text = minutes.ToString("D2") + ":" + seconds.ToString("D2");
        if (text == lastShown) return;
        lastShown = text;

        int[] values = { minutes / 10, minutes % 10, seconds / 10, seconds % 10 };
        for (int i = 0; i < digitImages.Length; i++)
            SetDigit(digitImages[i], values[i], center.x + SlotOffsets[i] * scale, center.y);
    }

    private void SetDigit(Image image, int value, float sx, float sy)
    {
        if (image == null) return;
        if (value < 0 || value > 9 || digits == null || digits[value] == null)
        {
            image.enabled = false;
            return;
        }

        image.enabled = true;
        image.sprite = digits[value];
        image.rectTransform.anchoredPosition = ScreenToAnchored(sx, sy, DigitFileX[value], DigitFileY);
    }

    private Image CreateLayer(string name, Sprite sprite, float fileX, float fileY, float sx, float sy)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(RefWidth * scale, RefHeight * scale);
        rect.anchoredPosition = ScreenToAnchored(sx, sy, fileX, fileY);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (sprite == null) image.enabled = false;
        return image;
    }

    private Vector2 ScreenToAnchored(float sx, float sy, float fileX, float fileY)
    {
        float dx = (fileX - 960f) * scale;
        float dy = (540f - fileY) * scale;
        return new Vector2(sx - 960f - dx, sy - 540f - dy);
    }

    private void LoadSprites()
    {
        Sprite[] loaded = Resources.LoadAll<Sprite>("TimeNumber");
        foreach (Sprite sprite in loaded)
        {
            if (sprite == null) continue;
            if (sprite.name.Length == 1 && sprite.name[0] >= '0' && sprite.name[0] <= '9')
                digits[sprite.name[0] - '0'] = sprite;
            else if (sprite.name == "：")
                colon = sprite;
            else if (sprite.name == "BG")
                background = sprite;
        }

        if (colon == null) Debug.LogWarning("[TimeHUD] 未找到冒号资源（Resources/TimeNumber/：.png）");
        if (background == null) Debug.LogWarning("[TimeHUD] 未找到背景资源（Resources/TimeNumber/BG.png）");
        for (int i = 0; i < digits.Length; i++)
            if (digits[i] == null) Debug.LogWarning($"[TimeHUD] 未找到数字资源 {i}（Resources/TimeNumber/{i}.png）");
    }
}
