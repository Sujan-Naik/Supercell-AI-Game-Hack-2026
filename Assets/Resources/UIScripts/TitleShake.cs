using UnityEngine;

public class TitleShake : MonoBehaviour
{
    [Header("可调参数")]
    public float rotateAmount = 8f;     // 旋转幅度
    public float rotateOffset = 0f;     // 中心偏移
    public float breathAmount = 0.05f;  // 呼吸强度
    public float speed = 2f;            // 动画速度

    [Header("方向控制")]
    public bool clockwise = true;       // ⭐ flag 控制正负方向

    RectTransform rect;
    Vector3 baseScale;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        baseScale = rect.localScale;
    }

    void Update()
    {
        float t = Time.unscaledTime * speed;

        int dir = clockwise ? 1 : -1;

        // 旋转
        float angle = rotateOffset + Mathf.Sin(t) * rotateAmount * dir;
        rect.localRotation = Quaternion.Euler(0, 0, angle);

        // 呼吸
        float scale = 1f + Mathf.Sin(t) * breathAmount;
        rect.localScale = baseScale * scale;
    }
}
