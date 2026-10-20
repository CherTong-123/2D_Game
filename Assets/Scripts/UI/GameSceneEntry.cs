// ============================================================================
// GameSceneEntry.cs：游戏场景入口，接入已有分裂场景和暂停预制体。
// 挂在 GameView 的独立物体上。所有界面随游戏场景一起释放。
// ============================================================================
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSceneEntry : MonoBehaviour
{
    [Header("界面资源")]
    public GameObject cellSlotPrefab;
    public GameObject pausePrefab;
    public TMP_FontAsset font;
    private MicrobeController player;
    private TMP_Text hud;
    private SplitScreenUI split;
    private PauseController pause;
    private TimeHUD timeHud;
    private TimeHUD countdownHud;
    private ScoreHUD scoreHud;

    private IEnumerator Start()
    {
        Intera_Get.Instance.ResetPause();
        AudioManager.Instance.PlayGameMusic();
        player = FindObjectOfType<MicrobeController>();
        var camera = Camera.main;
        yield return SceneManager.LoadSceneAsync("Divid_Scene", LoadSceneMode.Additive);
        var uiScene = SceneManager.GetSceneByName("Divid_Scene");
        Canvas canvas = null;
        foreach (var root in uiScene.GetRootGameObjects())
        {
            foreach (var uiCamera in root.GetComponentsInChildren<Camera>(true)) uiCamera.gameObject.SetActive(false);
            if (root.TryGetComponent<Canvas>(out var found)) canvas = found;
        }
        var panel = canvas.transform.Find("DividUI");
        foreach (Transform child in canvas.transform) child.gameObject.SetActive(child == panel);
        var gridArea = (RectTransform)panel.Find("Top_Left");
        foreach (Transform child in gridArea) child.gameObject.SetActive(false);
        var bg = panel.Find("BG").GetComponent<Image>();
        bg.color = new Color(0.05f, 0.1f, 0.15f, 0.25f);
        bg.raycastTarget = false;

        // 保留原场景的列表外观，替换旧演示条目。
        var scroll = panel.Find("Top_Right/Cell_List").GetComponent<ScrollRect>();
        scroll.content.gameObject.SetActive(false);
        var contentObject = new GameObject("RuntimeCells", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var content = contentObject.GetComponent<RectTransform>();
        content.SetParent(scroll.viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30;

        split = canvas.gameObject.AddComponent<SplitScreenUI>();
        split.panelRoot = panel.gameObject;
        split.gridArea = gridArea;
        split.listContent = content;
        split.slotTemplate = cellSlotPrefab;
        split.exitButton = panel.Find("Top_Right/Exit").GetComponent<Button>();
        split.mainCamera = camera;
        split.microbe = player;
        split.font = font;
        split.Initialize();

        var menu = Instantiate(pausePrefab, canvas.transform);
        var menuUI = menu.GetComponent<PauseMenuUI>();
        menuUI.player = player;
        menu.SetActive(false);
        pause = gameObject.AddComponent<PauseController>();
        pause.Configure(menu, split, player);
        hud = GameUIFactory.Text(canvas.transform, "PlayerStatus", font, "", 22);
        GameUIFactory.Rect(hud.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(20, -84), new Vector2(-20, -16));

        var timeHudObject = new GameObject("TimeHUD", typeof(RectTransform));
        timeHudObject.transform.SetParent(canvas.transform, false);
        timeHud = timeHudObject.AddComponent<TimeHUD>();
        timeHud.Configure(new Vector2(960f, 1010f), 1f, true, false, 0f);
        timeHud.Initialize();

        var countdownObject = new GameObject("CountdownHUD", typeof(RectTransform));
        countdownObject.transform.SetParent(canvas.transform, false);
        countdownHud = countdownObject.AddComponent<TimeHUD>();
        countdownHud.Configure(new Vector2(960f, 920f), 0.5f, false, true, 180f);
        countdownHud.Initialize();

        var scoreHudObject = new GameObject("ScoreHUD", typeof(RectTransform));
        scoreHudObject.transform.SetParent(canvas.transform, false);
        scoreHud = scoreHudObject.AddComponent<ScoreHUD>();
        scoreHud.Initialize();

        player.Died += OnDeath;
    }

    private void Update()
    {
        if (hud == null) return;
        hud.gameObject.SetActive(!split.IsOpen);
        hud.text = player.IsDead ? "主细胞已死亡，请选择重新开始或返回主菜单" :
            $"生命 {player.GetComponent<maincell>().hp} / {player.GetComponent<maincell>().maxHp}    能量 {player.energy:F0} / {player.energyLimit:F0}\nTab 分裂    E 释放蛋白    F 喷射    Esc 暂停";
    }
    private void OnDeath()
    {
        AudioManager.Instance.StopMusic();
        AudioManager.Instance.PlayGameOver();
        split.SetOpen(false);
        pause.PauseGame(false);
    }
    private void OnDestroy() { if (player != null) player.Died -= OnDeath; }
}
