using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls every screen: Main Menu, HUD, Pause, Game Over and Victory.
/// Buttons are wired up in code (Start), so you only need to drag the
/// Button objects into the fields below - no OnClick setup required.
/// Uses the built-in UI (Slider, Text, Image, Button). In Unity 6 / 2022 create
/// texts with: GameObject > UI > Legacy > Text.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Player Reference")]
    [Tooltip("If empty, found automatically via the 'Player' tag.")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("HUD")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text waveText;
    [SerializeField] private Text enemiesText;
    [Tooltip("Shows the countdown between waves.")]
    [SerializeField] private Text breakText;
    [Tooltip("Big centre text for 'WAVE 3', 'BOSS WAVE!', pickup messages.")]
    [SerializeField] private Text bannerText;
    [SerializeField] private float bannerDuration = 2f;
    [Tooltip("Optional: Image with Image Type = Filled, shows the dash cooldown.")]
    [SerializeField] private Image dashCooldownImage;
    [Tooltip("Optional: Image with Image Type = Filled, shows the ranged attack cooldown.")]
    [SerializeField] private Image fireCooldownImage;
    [Tooltip("Optional: full-screen red Image that flashes when the player is hurt.")]
    [SerializeField] private Image damageOverlay;
    [SerializeField] private float damageOverlayMaxAlpha = 0.35f;

    [Header("End Screens")]
    [SerializeField] private Text gameOverScoreText;
    [SerializeField] private Text victoryScoreText;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseMainMenuButton;
    [SerializeField] private Button gameOverRestartButton;
    [SerializeField] private Button gameOverMainMenuButton;
    [SerializeField] private Button victoryRestartButton;
    [SerializeField] private Button victoryMainMenuButton;

    private float bannerTimer;
    private float damageOverlayAlpha;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) player = playerObj.GetComponent<PlayerController>();
        }
        if (playerCombat == null && player != null) playerCombat = player.GetComponent<PlayerCombat>();

        // Wire every button to the GameManager. Each one is optional.
        AddListener(playButton, () => GameManager.Instance.StartGame());
        AddListener(quitButton, () => GameManager.Instance.QuitGame());
        AddListener(resumeButton, () => GameManager.Instance.ResumeGame());
        AddListener(pauseRestartButton, () => GameManager.Instance.RestartGame());
        AddListener(pauseMainMenuButton, () => GameManager.Instance.ReturnToMainMenu());
        AddListener(gameOverRestartButton, () => GameManager.Instance.RestartGame());
        AddListener(gameOverMainMenuButton, () => GameManager.Instance.ReturnToMainMenu());
        AddListener(victoryRestartButton, () => GameManager.Instance.RestartGame());
        AddListener(victoryMainMenuButton, () => GameManager.Instance.ReturnToMainMenu());

        if (bannerText != null) bannerText.gameObject.SetActive(false);
        SetOverlayAlpha(0f);
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private void Update()
    {
        UpdateHUD();
        UpdateBanner();
        UpdateDamageOverlay();
    }

    // ------------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------------

    private void UpdateHUD()
    {
        if (hudPanel == null || !hudPanel.activeInHierarchy) return;

        if (player != null)
        {
            if (healthBar != null)
            {
                healthBar.maxValue = player.MaxHealth;
                healthBar.value = player.CurrentHealth;
            }
            if (staminaBar != null)
            {
                staminaBar.maxValue = player.MaxStamina;
                staminaBar.value = player.CurrentStamina;
            }
            if (dashCooldownImage != null) dashCooldownImage.fillAmount = 1f - player.DashCooldownPercent;
        }

        if (playerCombat != null && fireCooldownImage != null)
            fireCooldownImage.fillAmount = 1f - playerCombat.FireCooldownPercent;

        if (GameManager.Instance != null && scoreText != null)
            scoreText.text = "SCORE: " + GameManager.Instance.Score + " / " + GameManager.Instance.TargetScore;

        WaveManager waves = WaveManager.Instance;
        if (waves != null)
        {
            if (waveText != null)
            {
                string total = waves.TotalWaves > 0 ? " / " + waves.TotalWaves : "";
                waveText.text = "WAVE: " + waves.CurrentWave + total;
            }
            if (enemiesText != null) enemiesText.text = "ENEMIES: " + waves.EnemiesRemaining;
            if (breakText != null)
            {
                breakText.gameObject.SetActive(waves.IsOnBreak);
                if (waves.IsOnBreak)
                    breakText.text = "NEXT WAVE IN " + Mathf.CeilToInt(waves.BreakTimeRemaining);
            }
        }
    }

    /// <summary>Shows a big message in the middle of the screen for a moment.</summary>
    public void ShowBanner(string message)
    {
        if (bannerText == null || string.IsNullOrEmpty(message)) return;
        bannerText.text = message;
        bannerText.gameObject.SetActive(true);
        bannerTimer = bannerDuration;
    }

    private void UpdateBanner()
    {
        if (bannerText == null || bannerTimer <= 0f) return;
        bannerTimer -= Time.unscaledDeltaTime;

        // Fade out during the last half second.
        Color c = bannerText.color;
        c.a = Mathf.Clamp01(bannerTimer / 0.5f);
        bannerText.color = c;

        if (bannerTimer <= 0f) bannerText.gameObject.SetActive(false);
    }

    /// <summary>Called by PlayerController when the player is hurt.</summary>
    public void FlashDamage()
    {
        damageOverlayAlpha = damageOverlayMaxAlpha;
    }

    private void UpdateDamageOverlay()
    {
        if (damageOverlay == null) return;
        damageOverlayAlpha = Mathf.MoveTowards(damageOverlayAlpha, 0f, Time.unscaledDeltaTime * 1.5f);
        SetOverlayAlpha(damageOverlayAlpha);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (damageOverlay == null) return;
        Color c = damageOverlay.color;
        c.a = alpha;
        damageOverlay.color = c;
    }

    // ------------------------------------------------------------------
    // Screens (called by GameManager)
    // ------------------------------------------------------------------

    public void ShowMainMenu()
    {
        SetPanels(mainMenu: true, hud: false, pause: false, gameOver: false, victory: false);
    }

    public void ShowHUD()
    {
        SetPanels(mainMenu: false, hud: true, pause: false, gameOver: false, victory: false);
    }

    public void ShowPauseMenu()
    {
        // Keep the HUD visible behind the pause menu.
        SetPanels(mainMenu: false, hud: true, pause: true, gameOver: false, victory: false);
    }

    public void ShowGameOver(int finalScore)
    {
        SetPanels(mainMenu: false, hud: false, pause: false, gameOver: true, victory: false);
        if (gameOverScoreText != null) gameOverScoreText.text = "FINAL SCORE: " + finalScore;
    }

    public void ShowVictory(int finalScore)
    {
        SetPanels(mainMenu: false, hud: false, pause: false, gameOver: false, victory: true);
        if (victoryScoreText != null) victoryScoreText.text = "FINAL SCORE: " + finalScore;
    }

    private void SetPanels(bool mainMenu, bool hud, bool pause, bool gameOver, bool victory)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenu);
        if (hudPanel != null) hudPanel.SetActive(hud);
        if (pausePanel != null) pausePanel.SetActive(pause);
        if (gameOverPanel != null) gameOverPanel.SetActive(gameOver);
        if (victoryPanel != null) victoryPanel.SetActive(victory);
    }
}
