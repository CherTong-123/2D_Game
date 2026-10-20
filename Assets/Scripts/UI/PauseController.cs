// 暂停与分裂共用按键入口，保证一次 Esc 只关闭当前界面。
using UnityEngine;
public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject PauseMenu;
    private SplitScreenUI split;
    private MicrobeController player;
    public void Configure(GameObject menu, SplitScreenUI splitScreen, MicrobeController microbe)
    { PauseMenu = menu; split = splitScreen; player = microbe; }
    private PauseMenuUI MenuUI => PauseMenu != null ? PauseMenu.GetComponent<PauseMenuUI>() : null;
    private void Update()
    {
        if (player.IsDead) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (split.IsOpen) split.SetOpen(false);
            else if (PauseMenu.activeSelf)
            {
                var ui = MenuUI;
                if (ui != null && ui.IsSettingOpen) ui.closeSetting();
                else ResumeGame();
            }
            else PauseGame();
        }
        else if (Input.GetKeyDown(KeyCode.Tab) && !PauseMenu.activeSelf) split.SetOpen(!split.IsOpen);
    }
    public void PauseGame(bool playSound = true)
    {
        PauseMenu.SetActive(true);
        var ui = MenuUI;
        if (ui != null) ui.HideSetting();
        Intera_Get.Instance.SetMenuPaused(true);
        if (playSound) AudioManager.Instance.PlayPauseOpen();
    }
    public void ResumeGame()
    {
        var ui = MenuUI;
        if (ui != null) ui.HideSetting();
        PauseMenu.SetActive(false);
        Intera_Get.Instance.SetMenuPaused(false);
        AudioManager.Instance.PlayPauseClose();
    }
}
