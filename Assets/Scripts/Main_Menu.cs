using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayLevelButton : MonoBehaviour
{
    public string levelName; // Name of the scene to load

    public void PlayLevel()
    {
        SceneManager.LoadScene(levelName);
    }
}
