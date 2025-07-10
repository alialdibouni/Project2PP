using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{

    [SerializeField] GameObject pauseMenu;

    public void Start()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f; // Pause the game by setting time scale to 0
        //show mouse cursor
        Cursor.lockState = CursorLockMode.None;
    }

    public void Pause()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f; // Pause the game by setting time scale to 0
    }

    public void Home()
    {
        SceneManager.LoadScene("MainMenu"); // Load the main menu scene
        Time.timeScale = 1f; // Reset time scale to normal
    }

    public void Resume()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
        //hide mouse cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Restart() 
    { 
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Restart the current scene
        Time.timeScale = 1f; // Reset time scale to normal
    }
}
