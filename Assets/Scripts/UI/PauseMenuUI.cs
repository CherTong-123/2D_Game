using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject settingPanel;
    [SerializeField] CanvasGroup pauseMenu;


    [SerializeField] private CanvasGroup fadeController;
    [SerializeField] private UnityEngine.UI.Image fadeImage;
    [SerializeField] private GameObject PauseMenu;

    public void ContinueGame()
    {
        if (PauseController.Instance != null)
        {
            PauseController.Instance.Resume();
            return;
        }

        PauseMenu.SetActive(false);
        Time.timeScale = 1f;
    }
    public void RestartGame()
    {
        if (PauseController.Instance != null)
        {
            PauseController.Instance.Restart();
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void openSetting()
    {
        settingPanel.SetActive(true);
    }
    public void closeSetting()
    {
        settingPanel.SetActive(false);
    }
    public bool CloseSettingsIfOpen()
    {
        if (settingPanel != null && settingPanel.activeSelf)
        {
            settingPanel.SetActive(false);
            return true;
        }
        return false;
    }
    public void Mainmenu()
    {
        fadeImage.raycastTarget = true;
        StartCoroutine(FadeToBlack());
    }

    private IEnumerator FadeToBlack()
    {
        float duration = 0.5f;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            fadeController.alpha = timer / duration;
            yield return null;
        }
        fadeController.alpha = 1f;
        yield return new WaitForSecondsRealtime(0.5f);
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
