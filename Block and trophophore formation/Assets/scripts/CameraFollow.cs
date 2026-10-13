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

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        // 目标位置：相机完全跟随玩家XY，Z保持相机原值
        Vector3 targetCamPos = new Vector3(target.position.x, target.position.y, transform.position.z);
        float h = cam.orthographicSize;
        float w = h * cam.aspect;
        // 边界限制
        float limitX = Mathf.Clamp(targetCamPos.x, mapMinX + w, mapMaxX - w);
        float limitY = Mathf.Clamp(targetCamPos.y, mapMinY + h, mapMaxY - h);//x方向50位移到41,y方向100到95
        transform.position = new Vector3(limitX, limitY, transform.position.z);
    }
}