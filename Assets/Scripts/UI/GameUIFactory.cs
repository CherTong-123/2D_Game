// ============================================================================
// GameUIFactory.cs：运行时界面的矩形、文字和按钮创建。
// ============================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class GameUIFactory
{
    public static void Rect(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = lower; rect.offsetMax = upper; }

    public static GameObject Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        Rect(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return go;
    }

    public static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, string value, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(Transform parent, string name, TMP_FontAsset font, string label)
    {
        var go = Panel(parent, name, new Color(0.2f, 0.4f, 0.55f));
        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        var text = Text(go.transform, "Label", font, label, 24);
        text.alignment = TextAlignmentOptions.Center;
        Rect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }
}
