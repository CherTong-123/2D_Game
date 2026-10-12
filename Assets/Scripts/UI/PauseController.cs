using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject PauseMenu;
    private bool hasPaused = false;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!hasPaused)
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
        hasPaused = true;
        PauseMenu.SetActive(true);
        Time.timeScale = 0f;
    }
    private void ResumeGame()
    {
        hasPaused = false;
        PauseMenu.SetActive(false);
        Time.timeScale = 1f;
    }
}
