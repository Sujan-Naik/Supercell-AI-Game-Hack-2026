using UnityEngine;

public class BulletScript : MonoBehaviour
{

    public float force = 20f;
    public float Destroyafter = 10f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Safety check
        if (rb != null)
        {
            rb.AddForce(transform.forward * force, ForceMode.Impulse);

            Destroy(gameObject, Destroyafter);
        }
    }
}