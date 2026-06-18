using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleDeath : MonoBehaviour
{
    public static SimpleDeath Instance;
    
    public GameObject deathUI; // Перетащите сюда DeathBackground и кнопку
    
    void Awake()
    {
        Instance = this;
        deathUI.SetActive(false); // Прячем в начале
    }
    
    public void Die()
    {
        deathUI.SetActive(true); // Показываем экран смерти
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f; // Замораживаем игру
    }
    
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}