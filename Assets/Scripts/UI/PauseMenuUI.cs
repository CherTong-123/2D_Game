using System.Collections;
using System.Collections.Generic;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject settingPanel;
    [SerializeField] CanvasGroup pauseMenu;


    [SerializeField] private CanvasGroup fadeController;
    [SerializeField] private UnityEngine.UI.Image fadeImage;
    //public void ContinueGame(){}
    public void openSetting()
    {
        pauseMenu.interactable = false;
        settingPanel.SetActive(true);
    }
    public void closeSetting()
    {
        pauseMenu.interactable = true;
        settingPanel.SetActive(false);
    }
    public void Mainmenu()
    {
        fadeImage.raycastTarget = true;
        StartCoroutine(FadeToBlack());
    }
    public void QuitGame()
    {
        Application.Quit();
    }

    private IEnumerator FadeToBlack()
    {
        float duration = 0.5f;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            fadeController.alpha = timer / duration;
            yield return null;
        }
        fadeController.alpha = 1f;
        yield return new WaitForSecondsRealtime(0.5f);
        SceneManager.LoadScene("Mainmenu");
    }
}
