// ============================================================================
// AudioManager.cs：全局音频管理器。
// 素材统一放在 Assets/Resources/Music 下，运行时用 Resources.Load 加载，
// 因此不需要在场景里手动挂 AudioSource。
//
// 音量分三级：
//   主音量（AudioListener.volume）        -> Intera_Get 控制
//   BGM 音量（musicSource.volume）        -> SetMusicVolume
//   音效音量（PlayOneShot 的缩放值）      -> SetSFXVolume
// ============================================================================
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[AudioManager]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioManager>();
            }
            return instance;
        }
    }

    private enum MusicMode { None, Menu, Game }

    private const string MusicDir = "Music/";

    [Header("音量")]
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private AudioSource musicSource;
    private AudioSource sfxSource;

    private AudioClip menuMusic;
    private AudioClip[] gameLayers; // low / mid / high
    private AudioClip uiClick;
    private AudioClip pauseOpen;
    private AudioClip pauseClose;
    private AudioClip cellSplit;
    private AudioClip gameOver;

    private MusicMode mode;
    private int gameLayerIndex;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void Init()
    {
        if (musicSource != null) return;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = sfxVolume;

        menuMusic = Load("bgm_menu_loop");
        gameLayers = new[]
        {
            Load("bgm_game_layer1_low"),
            Load("bgm_game_layer2_mid"),
            Load("bgm_game_layer3_high")
        };
        uiClick = Load("ui_click_01");
        pauseOpen = Load("ui_pause_open");
        pauseClose = Load("ui_pause_close");
        cellSplit = Load("cell_split");
        gameOver = Load("sys_gameover");
    }

    private static AudioClip Load(string name)
    {
        var clip = Resources.Load<AudioClip>(MusicDir + name);
        if (clip == null) Debug.LogWarning($"[Audio] 未找到音频资源 Resources/{MusicDir}{name}");
        return clip;
    }

    private void Update()
    {
        if (musicSource == null) return;
        if (mode == MusicMode.Menu && !musicSource.isPlaying) PlayMenuLoop();
        else if (mode == MusicMode.Game && !musicSource.isPlaying) PlayNextGameLayer();
    }

    // ================================================================
    // 音乐
    // ================================================================
    public void PlayMenuMusic()
    {
        Init();
        mode = MusicMode.Menu;
        PlayMenuLoop();
    }

    private void PlayMenuLoop()
    {
        if (menuMusic == null) return;
        musicSource.loop = false;
        musicSource.clip = menuMusic;
        musicSource.Play();
    }

    /// <summary>游戏场景 BGM：低 -> 中 -> 高 三段循环播放。</summary>
    public void PlayGameMusic()
    {
        Init();
        mode = MusicMode.Game;
        gameLayerIndex = 0;
        PlayNextGameLayer();
    }

    private void PlayNextGameLayer()
    {
        if (gameLayers == null || gameLayers.Length == 0) return;

        // 跳过缺失资源，避免空引用。
        for (int i = 0; i < gameLayers.Length && gameLayers[gameLayerIndex] == null; i++)
            gameLayerIndex = (gameLayerIndex + 1) % gameLayers.Length;
        if (gameLayers[gameLayerIndex] == null) return;

        musicSource.loop = false;
        musicSource.clip = gameLayers[gameLayerIndex];
        musicSource.Play();
        gameLayerIndex = (gameLayerIndex + 1) % gameLayers.Length;
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
        mode = MusicMode.None;
    }

    // ================================================================
    // 音效
    // ================================================================
    public void PlayUI() => PlayOneShot(uiClick);
    public void PlayPauseOpen() => PlayOneShot(pauseOpen);
    public void PlayPauseClose() => PlayOneShot(pauseClose);
    public void PlayCellSplit() => PlayOneShot(cellSplit);
    public void PlayGameOver() => PlayOneShot(gameOver);

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ================================================================
    // 音量
    // ================================================================
    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }
}
