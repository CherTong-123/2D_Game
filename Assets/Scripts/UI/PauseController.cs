using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject PauseMenu;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!PauseMenu.activeSelf)
            {
                PauseGame();
            }
            else
            {
                ResumeGame();
            }
        }
    }
    private void PauseGame()
    {
        PauseMenu.SetActive(true);
        Time.timeScale = 0f;
    }
    private void ResumeGame()
    {
        PauseMenu.SetActive(false);
        Time.timeScale = 1f;
    }
}
