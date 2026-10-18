
using UnityEngine;
using UnityEngine.UI;

public class ToggleSwitch : MonoBehaviour
{
    [Header("开关状态图片")]
    [SerializeField] private Sprite onSprite;
    [SerializeField] private Sprite offSprite;

    private bool isOn = true;

    private Image targetImage;
    private Button button;

    public bool IsOn => isOn;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        button = GetComponent<Button>();

        button.onClick.AddListener(Toggle);
        UpdateVisual();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(Toggle);
        }
    }

    public void Toggle()
    {
        isOn = !isOn;
        UpdateVisual();
    }

    public void SetState(bool value)
    {
        isOn = value;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        targetImage.sprite = isOn ? onSprite : offSprite;
    }
}
