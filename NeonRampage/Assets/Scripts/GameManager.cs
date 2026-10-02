using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameManager is the "brain" of the game. It tracks the score, the game state
/// (main menu, playing, paused, game over, victory), pausing with Esc, and
/// restarting / quitting. There must be exactly ONE GameManager in the scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    // Easy global access: GameManager.Instance.AddScore(10);
    public static GameManager Instance { get; private set; }

    // When we press Restart we reload the scene. This flag tells the new scene
    // to skip the main menu and jump straight into gameplay.
    private static bool skipMenuOnNextLoad = false;

    public enum GameState { MainMenu, Playing, Paused, GameOver, Victory }

    [Header("Win Condition")]
    [Tooltip("Reach this score to win (WaveManager can also trigger victory after the last wave).")]
    [SerializeField] private int targetScore = 10000;

    [Header("Audio Hooks (optional)")]
    [Tooltip("AudioSource used for one-shot game sounds and music. Assign later if you want sound.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip buttonClickSound;

    [Header("Hit Stop")]
    [Tooltip("Tiny freeze-frame when a heavy hit lands. 0 = disabled.")]
    [SerializeField] private float hitStopDuration = 0.04f;

    public GameState State { get; private set; } = GameState.MainMenu;
    public int Score { get; private set; }
    public int TargetScore => targetScore;
    public bool IsPlaying => State == GameState.Playing;

    private bool hitStopActive;

    private void Awake()
    {
        // Simple singleton: destroy duplicates.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (skipMenuOnNextLoad)
        {
            skipMenuOnNextLoad = false;
            StartGame();
        }
        else
        {
            ShowMainMenu();
        }
    }

    private void Update()
    {
        // Esc toggles the pause menu while playing.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (State == GameState.Playing) PauseGame();
            else if (State == GameState.Paused) ResumeGame();
        }
    }

    // ------------------------------------------------------------------
    // State changes
    // ------------------------------------------------------------------

    public void ShowMainMenu()
    {
        State = GameState.MainMenu;
        Time.timeScale = 0f;          // freeze the world behind the menu
        SetCursorLocked(false);
        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
    }

    public void StartGame()
    {
        State = GameState.Playing;
        Score = 0;
        Time.timeScale = 1f;
        SetCursorLocked(true);
        if (UIManager.Instance != null) UIManager.Instance.ShowHUD();
        if (WaveManager.Instance != null) WaveManager.Instance.BeginWaves();
    }

    public void PauseGame()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Time.timeScale = 0f;
        SetCursorLocked(false);
        if (UIManager.Instance != null) UIManager.Instance.ShowPauseMenu();
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        Time.timeScale = 1f;
        SetCursorLocked(true);
        if (UIManager.Instance != null) UIManager.Instance.ShowHUD();
    }

    public void GameOver()
    {
        if (State == GameState.GameOver || State == GameState.Victory) return;
        State = GameState.GameOver;
        Time.timeScale = 0f;
        SetCursorLocked(false);
        PlaySound(gameOverSound);
        if (UIManager.Instance != null) UIManager.Instance.ShowGameOver(Score);
    }

    public void Victory()
    {
        if (State == GameState.GameOver || State == GameState.Victory) return;
        State = GameState.Victory;
        Time.timeScale = 0f;
        SetCursorLocked(false);
        PlaySound(victorySound);
        if (UIManager.Instance != null) UIManager.Instance.ShowVictory(Score);
    }

    /// <summary>Reloads the current scene and starts playing immediately.</summary>
    public void RestartGame()
    {
        PlaySound(buttonClickSound);
        skipMenuOnNextLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Reloads the scene and shows the main menu.</summary>
    public void ReturnToMainMenu()
    {
        PlaySound(buttonClickSound);
        skipMenuOnNextLoad = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        PlaySound(buttonClickSound);
#if UNITY_EDITOR
        // Application.Quit does nothing in the editor, so stop Play Mode instead.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------
    // Score
    // ------------------------------------------------------------------

    public void AddScore(int amount)
    {
        if (State != GameState.Playing) return;
        Score += amount;
        if (Score >= targetScore) Victory();
    }

    // ------------------------------------------------------------------
    // Helpers used by other scripts
    // ------------------------------------------------------------------

    /// <summary>
    /// Spawns a particle effect prefab and destroys it after 'lifetime' seconds.
    /// Safe to call with a null prefab (does nothing).
    /// </summary>
    public static void SpawnEffect(GameObject effectPrefab, Vector3 position, float lifetime = 2f)
    {
        if (effectPrefab == null) return;
        GameObject fx = Instantiate(effectPrefab, position, Quaternion.identity);
        Destroy(fx, lifetime);
    }

    /// <summary>Plays a 2D one-shot sound through the GameManager's AudioSource.</summary>
    public void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    /// <summary>Freezes time for a few frames to make heavy hits feel punchy.</summary>
    public void HitStop()
    {
        if (hitStopDuration <= 0f || hitStopActive || State != GameState.Playing) return;
        StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        hitStopActive = true;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        // Only restore time if nothing else (pause, game over) changed it meanwhile.
        if (State == GameState.Playing) Time.timeScale = 1f;
        hitStopActive = false;
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
