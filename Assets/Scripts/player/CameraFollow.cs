using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标（拖 player 进来）")]
    public Transform target;

    [Header("地图边界（世界坐标）")]
    public float mapMinX = -100f;
    public float mapMaxX = 100f;
    public float mapMinY = -100f;
    public float mapMaxY = 100f;

    [Header("开场：从全地图拉近")]
    public bool playIntro = true;      // 不想看开场就取消勾选
    public float introDuration = 2.5f;      // 持续几秒
    public float playZoom = 15f;        // 开场结束后的正常视角（越大看得越远）
    public AnimationCurve introEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("滚轮缩放")]
    public float zoomSpeed = 2f;
    public float minZoom = 15f;
    public float maxZoom = 25f;

    private Camera cam;
    private bool introPlaying;
    private float introTimer;
    private float introStartSize;
    private float introEndSize;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (!playIntro)
        {
            cam.orthographicSize = playZoom;
            return;
        }

        // 算出「装下整张地图」需要的 orthographicSize
        float halfH = (mapMaxY - mapMinY) * 0.5f;   // 高度方向
        float halfW = (mapMaxX - mapMinX) * 0.5f;   // 宽度方向
        introStartSize = Mathf.Max(halfH, halfW / cam.aspect);  // 取能同时装下的那个

        introEndSize = playZoom;

        cam.orthographicSize = introStartSize;
        transform.position = new Vector3(
            (mapMinX + mapMaxX) * 0.5f,
            (mapMinY + mapMaxY) * 0.5f,
            transform.position.z);

        introTimer = 0f;
        introPlaying = true;
    }

    void LateUpdate()
    {
        if (introPlaying)
        {
            introTimer += Time.deltaTime;
            float t = Mathf.Clamp01(introTimer / introDuration);
            float e = introEase.Evaluate(t);

            cam.orthographicSize = Mathf.Lerp(introStartSize, introEndSize, e);

            if (t >= 1f)
            {
                introPlaying = false;
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
            }
        }
        else
        {
            HandleZoomInput();
        }

        if (target != null)
            transform.position = ClampToMap(TargetPos());
    }

    void HandleZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize - scroll * zoomSpeed,
            minZoom, maxZoom);
    }

    Vector3 TargetPos()
    {
        return new Vector3(target.position.x, target.position.y, transform.position.z);
    }

    // 边界限制（同时修掉「视野比地图大时 min > max」的坑）
    Vector3 ClampToMap(Vector3 p)
    {
        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        float minX = mapMinX + w, maxX = mapMaxX - w;
        float minY = mapMinY + h, maxY = mapMaxY - h;

        p.x = (minX <= maxX) ? Mathf.Clamp(p.x, minX, maxX) : (mapMinX + mapMaxX) * 0.5f;
        p.y = (minY <= maxY) ? Mathf.Clamp(p.y, minY, maxY) : (mapMinY + mapMaxY) * 0.5f;

        return p;
    }
}
