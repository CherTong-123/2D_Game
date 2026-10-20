// ============================================================================
// TimeHUD.cs：用 TimeNumber 里的数字与冒号拼出游戏时间（MM:SS）。
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

    // 界面上的槽位（1920x1080 参考空间，原点在左下）。
    // 布局为 分十位 / 分个位 / 冒号 / 秒十位 / 秒个位。
    private static readonly float[] SlotX = { 838f, 906f, 1014f, 1082f };
    private const float RowY = 1010f;
    private const float BgY = 968f; // 底图比数字略低，复刻原图版式

    [SerializeField] private Sprite[] digits = new Sprite[10];
    [SerializeField] private Sprite colon;
    [SerializeField] private Sprite background;

    private readonly Image[] digitImages = new Image[4];
    private float elapsed;
    private string lastShown = "";
    private bool initialized;

    public float ElapsedSeconds => elapsed;

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        LoadSprites();

        CreateLayer("TimeBG", background, BgFileX, BgFileY, 960f, BgY);
        CreateLayer("TimeColon", colon, ColonFileX, ColonFileY, 960f, RowY);
        for (int i = 0; i < digitImages.Length; i++)
            digitImages[i] = CreateLayer("TimeDigit" + i, null, DigitFileX[0], DigitFileY, SlotX[i], RowY);

        Refresh(elapsed);
    }

    private void Update()
    {
        if (!initialized) return;
        // 使用游戏时间：随游戏速度变化，暂停时 timeScale 归零、计时自动冻结。
        elapsed += Time.deltaTime;

        int total = Mathf.FloorToInt(elapsed);
        int minutes = Mathf.Min(total / 60, 99);
        int seconds = total % 60;
        string text = minutes.ToString("D2") + ":" + seconds.ToString("D2");
        if (text == lastShown) return;

        lastShown = text;
        Refresh(elapsed);
    }

    private void Refresh(float time)
    {
        int total = Mathf.FloorToInt(time);
        int minutes = Mathf.Min(total / 60, 99);
        int seconds = total % 60;
        int[] values = { minutes / 10, minutes % 10, seconds / 10, seconds % 10 };

        for (int i = 0; i < digitImages.Length; i++)
            SetDigit(digitImages[i], values[i], SlotX[i], RowY);
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
        rect.sizeDelta = new Vector2(RefWidth, RefHeight);
        rect.anchoredPosition = ScreenToAnchored(sx, sy, fileX, fileY);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (sprite == null) image.enabled = false;
        return image;
    }

    private static Vector2 ScreenToAnchored(float sx, float sy, float fileX, float fileY)
    {
        return new Vector2(sx - fileX, sy - RefHeight + fileY);
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
