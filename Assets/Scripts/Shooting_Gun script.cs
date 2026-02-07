using UnityEngine;
using UnityEngine.UIElements;

public class Shooting_Gunscript : MonoBehaviour
{
    public GameObject[] bullets;
    public Transform firePoint;

    public float shootInterval = 0.5f;
    private float shootTimer;

    void Update()
    {
        if (Input.GetKey(KeyCode.E))
        {
            shootTimer -= Time.deltaTime;

            if (shootTimer <= 0f)
            {
                Shoot();
                shootTimer = shootInterval;
            }
        }
        else
        {
            shootTimer = 0f;
        }
    }

    void Shoot()
    {
        int randomIndex = Random.Range(0, bullets.Length);
        GameObject selectedBullet = bullets[randomIndex];

        Instantiate(selectedBullet, firePoint.position, firePoint.rotation);
    }
}
