using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [SerializeField] private GameObject PauseMenu;

    public bool IsPaused { get; private set; }

    [SerializeField] private bool pauseAudio = true;
    private bool audioPaused;

    /// <summary>暂停状态变化时广播，其他系统可订阅（例如停止/恢复 BGM、动画）。</summary>
    public event Action<bool> OnPauseChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (PauseMenu != null)
            PauseMenu.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused)
            {
                var menuUI = PauseMenu != null ? PauseMenu.GetComponent<PauseMenuUI>() : null;
                if (menuUI != null && menuUI.CloseSettingsIfOpen())
                    return;
            }
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        if (pauseAudio)
        {
            audioPaused = true;
            AudioListener.pause = true;
        }
        if (PauseMenu != null) PauseMenu.SetActive(true);
        OnPauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        IsPaused = false;
        // 恢复到玩家设置的游戏速度，而不是写死 1
        Time.timeScale = Intera_Get.Instance.GameSpeed;
        if (audioPaused)
        {
            audioPaused = false;
            AudioListener.pause = false;
        }
        if (PauseMenu != null) PauseMenu.SetActive(false);
        OnPauseChanged?.Invoke(false);
    }

    public void Restart()
    {
        Resume();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        // AudioListener.pause 是全局状态，离开场景时确保恢复，避免带到主菜单
        if (audioPaused)
        {
            audioPaused = false;
            AudioListener.pause = false;
        }
    }
}
