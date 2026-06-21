using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartCurrentScene : MonoBehaviour
{
    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}