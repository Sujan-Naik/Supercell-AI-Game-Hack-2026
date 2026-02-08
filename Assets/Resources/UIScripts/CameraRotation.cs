using UnityEngine;

public class CameraRotation : MonoBehaviour
{
    public float centerY = 56.6f;  
    public float range = 20f;      
    public float speed = 0.05f;

    float time;
    float baseX;
    float baseZ;

    void Awake()
    {
        Vector3 e = transform.eulerAngles;
        baseX = e.x;
        baseZ = e.z;
    }

    void Update()
    {
        time += Time.deltaTime * speed;

        float yAngle = centerY + Mathf.Sin(time) * range;
        transform.rotation = Quaternion.Euler(baseX, yAngle, baseZ);
    }
}
