using System.Collections;
using System.Collections.Generic;

using UnityEngine;


using UnityEngine.SceneManagement;

using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private bool starting;
    private void Awake() { Intera_Get.Instance.ResetPause(); }
    public GameObject settingsPanel;

    [SerializeField]private CanvasGroup fadeController;
    [SerializeField]private UnityEngine.UI.Image fadeImage;
    public void StartGame()
    {
        if (starting) return;
        starting = true;
        fadeImage.raycastTarget = true;
        StartCoroutine(FadeToBlack());
    }
    //public void ContinueGame(){}
    public void OpenSettings()
    {
        settingsPanel.SetActive(true);

    }
    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("游戏结束");
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
        SceneManager.LoadScene("GameView");
    }
}
