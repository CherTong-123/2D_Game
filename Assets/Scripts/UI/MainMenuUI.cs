using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public GameObject settingsPanel;
    [SerializeField] private CanvasGroup fadeController;
    public void StartGame()
    {
        StartCoroutine(FadeToBlack());
    }
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
        float duration = 1f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            fadeController.alpha = timer / duration;
            yield return null;
        }
        fadeController.alpha = 1f;
    }
}
