using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标（拖player进来）")]
    public Transform target;

    [Header("地图边界（世界坐标）")]
    public float mapMinX;
    public float mapMaxX;
    public float mapMinY;
    public float mapMaxY;
    public float zoomSpeed = 2f;  // 缩放速度
    public float minZoom = 3f;    // 最小视野大小，数值越小越近
    public float maxZoom = 10f;   // 最大视野大小，数值越大越远

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 鼠标滚轮缩放视野
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0f)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(
                cam.orthographicSize,
                minZoom,
                maxZoom
            );
        }

        // 摄像机跟随目标，保持原有逻辑
        Vector3 targetCamPos = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );

        float h = cam.orthographicSize;
        float w = h * cam.aspect;

        // 地图边界限制
        float limitX = Mathf.Clamp(
            targetCamPos.x,
            mapMinX + w,
            mapMaxX - w
        );

        float limitY = Mathf.Clamp(
            targetCamPos.y,
            mapMinY + h,
            mapMaxY - h
        );

        transform.position = new Vector3(
            limitX,
            limitY,
            transform.position.z
        );
    }
}