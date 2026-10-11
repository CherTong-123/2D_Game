using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    public GameObject settingsPanel;
    [SerializeField] private CanvasGroup menuBtn;
    
    [SerializeField]private CanvasGroup fadeController;
    [SerializeField]private UnityEngine.UI.Image fadeImage;
    public void StartGame()
    {
        fadeImage.raycastTarget = true;
        StartCoroutine(FadeToBlack());
    }
    //public void ContinueGame(){}
    public void OpenSettings()
    {
        menuBtn.interactable = false;
        settingsPanel.SetActive(true);
        
    }
    public void CloseSettings()
    {
        menuBtn.interactable = true;
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
            timer += Time.deltaTime;
            fadeController.alpha = timer / duration;
            yield return null;
        }
        fadeController.alpha = 1f;
        yield return new WaitForSecondsRealtime(0.5f);
        SceneManager.LoadScene("Game View");
    }
}
