// 暂停菜单：继续、重开、设置和返回主菜单。
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private CanvasGroup fadeController;
    [SerializeField] private Image fadeImage;
    [SerializeField] private GameObject PauseMenu;
    public MicrobeController player;
    private bool changingScene;
    private void Awake()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == "Continue") button.onClick.AddListener(ContinueGame);
            if (button.name == "Restart") button.onClick.AddListener(RestartGame);
        }
        fadeController.alpha = 0;
        fadeImage.raycastTarget = false;
    }
    private void OnEnable()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
            if (button.name == "Continue") button.interactable = player == null || !player.IsDead;
    }
    public void ContinueGame()
    {
        if (player != null && player.IsDead) return;
        settingPanel.SetActive(false);
        PauseMenu.SetActive(false);
        Intera_Get.Instance.SetMenuPaused(false);
    }
    public void openSetting() { settingPanel.SetActive(true); }
    public void closeSetting() { settingPanel.SetActive(false); }
    public void Mainmenu() { ChangeScene("MainMenu"); }
    public void RestartGame() { ChangeScene("GameView"); }
    private void ChangeScene(string scene)
    {
        if (changingScene) return;
        changingScene = true;
        fadeImage.raycastTarget = true;
        StartCoroutine(FadeToScene(scene));
    }
    private IEnumerator FadeToScene(string scene)
    {
        for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime)
        { fadeController.alpha = t / 0.5f; yield return null; }
        Intera_Get.Instance.ResetPause();
        SceneManager.LoadScene(scene);
    }
}
