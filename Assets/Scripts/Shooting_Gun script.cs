using UnityEngine;
using UnityEngine.UIElements;

public class Shooting_Gunscript : MonoBehaviour
{
   
    public GameObject[] bullets;
    public Transform firePoint;

    public float shootInterval = 0.5f;
    private float shootTimer = 0f;

    public GameObject sparkle;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {

            shootTimer -= Time.deltaTime;

            if (shootTimer <= 0f)
            {
                Shoot();
                shootTimer = shootInterval;
            }
        }
    }

    public void Shoot()
    {

            int randomIndex = Random.Range(0, bullets.Length);
            GameObject selectedBullet = bullets[randomIndex];

            // Spawn bullet
            GameObject bullet = Instantiate(selectedBullet, firePoint.position, firePoint.rotation);

            // Spawn sparkle
            GameObject spark = Instantiate(sparkle, firePoint.position, firePoint.rotation);

            // Destroy spawned objects
            Destroy(bullet, 5f);
            Destroy(spark, 2f);
        
    }
}
