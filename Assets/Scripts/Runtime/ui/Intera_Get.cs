// 全局音量、速度和界面暂停状态。暂停期间调速只更新选择值。
using UnityEngine;
public class Intera_Get
{
    public static Intera_Get Instance { get; } = new Intera_Get();
    public float MainVolume = 1f;
    public float GameSpeed = 1f;
    public bool MenuPaused { get; private set; }
    public bool SplitPaused { get; private set; }
    public bool IsPaused => MenuPaused || SplitPaused;
    private Intera_Get() { }
    public void SetMainVolume(float value) { MainVolume = Mathf.Clamp01(value); AudioListener.volume = MainVolume; }
    public void SetGameSpeed(float value)
    {
        value = Mathf.Clamp01(value);
        GameSpeed = value <= 0.5f ? value + 0.5f : value * 2f;
        ApplyTime();
    }
    public void SetMenuPaused(bool value) { MenuPaused = value; ApplyTime(); }
    public void SetSplitPaused(bool value) { SplitPaused = value; ApplyTime(); }
    public void ResetPause() { MenuPaused = SplitPaused = false; ApplyTime(); }
    private void ApplyTime() { Time.timeScale = IsPaused ? 0f : GameSpeed; }
}
