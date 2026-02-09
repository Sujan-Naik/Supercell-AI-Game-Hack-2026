using UnityEngine;

public class Level1 : MonoBehaviour
{
    void Start()
    {
        Cursor.visible = false;        // Hide cursor
        Cursor.lockState = CursorLockMode.Locked; // Lock to center
    }
}
