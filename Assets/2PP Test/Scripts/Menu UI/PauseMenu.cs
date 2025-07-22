using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{

    [SerializeField] GameObject pauseMenu;
    [SerializeField] GameObject healthBars;
    [SerializeField] GameObject deathScreen;
    [SerializeField] GameObject victoryScreen;



    public void Start()
    {
        pauseMenu.SetActive(true);
        healthBars.SetActive(false);
        deathScreen.SetActive(false);
        victoryScreen.SetActive(false);
        Time.timeScale = 0f; // Pause the game by setting time scale to 0
        //show mouse cursor
        Cursor.lockState = CursorLockMode.None;
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        { 
            pauseMenu.SetActive(true);
            healthBars.SetActive(false);
            deathScreen.SetActive(false);
            victoryScreen.SetActive(false);
            Time.timeScale = 0f; // Pause the game by setting time scale to 0
            //show mouse cursor
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void Pause()
    {
        pauseMenu.SetActive(true);
        healthBars.SetActive(false);
        deathScreen.SetActive(false);
        victoryScreen.SetActive(false);
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
        healthBars.SetActive(true);
        deathScreen.SetActive(false);
        victoryScreen.SetActive(false);
        Time.timeScale = 1f;
        //hide mouse cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Restart() 
    { 
        pauseMenu.SetActive(false);
        healthBars.SetActive(true);
        deathScreen.SetActive(false);
        victoryScreen.SetActive(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Restart the current scene
        Time.timeScale = 1f; // Reset time scale to normal
    }

    public void ShowDeathScreen()
    {
        pauseMenu.SetActive(false);
        healthBars.SetActive(false);
        deathScreen.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ShowVictoryScreen()
    {
        pauseMenu.SetActive(false);
        healthBars.SetActive(false);
        deathScreen.SetActive(false);
        victoryScreen.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
    }
}
