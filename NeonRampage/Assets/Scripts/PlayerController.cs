using System.Collections;
using UnityEngine;

/// <summary>
/// Third-person player movement using a CharacterController.
/// Handles: WASD movement relative to the camera, sprint (Left Shift),
/// jump (Space), dash/dodge (Q or Right Click), health and stamina.
/// Uses the classic Input Manager (Input.GetAxis / Input.GetKey).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Main Camera. If empty, Camera.main is used.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [Tooltip("How fast the player turns to face the camera direction.")]
    [SerializeField] private float turnSpeed = 15f;
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravity = -25f;

    [Header("Dash / Dodge")]
    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 1.0f;
    [SerializeField] private float dashStaminaCost = 25f;
    [Tooltip("Player can't be damaged while dashing.")]
    [SerializeField] private bool invincibleWhileDashing = true;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [Tooltip("Short invincibility after taking a hit so you don't get shredded instantly.")]
    [SerializeField] private float invincibilityAfterHit = 0.4f;

    [Header("Stamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float sprintStaminaPerSecond = 20f;
    [SerializeField] private float staminaRegenPerSecond = 18f;
    [Tooltip("Seconds after using stamina before it starts refilling.")]
    [SerializeField] private float staminaRegenDelay = 0.8f;

    [Header("Hit Feedback")]
    [Tooltip("Renderer that flashes red when the player is hurt (the capsule's MeshRenderer).")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color hurtFlashColor = Color.red;
    [SerializeField] private float hurtShakeDuration = 0.25f;
    [SerializeField] private float hurtShakeMagnitude = 0.35f;

    [Header("Effects & Audio Hooks (optional)")]
    [SerializeField] private GameObject dashEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private AudioClip deathSound;

    // ---- Public read-only values (the UI reads these) ----
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;
    public bool IsDead { get; private set; }
    public bool IsDashing { get; private set; }
    /// <summary>0 = dash ready, 1 = just used. Handy for a cooldown icon.</summary>
    public float DashCooldownPercent => dashCooldown <= 0f ? 0f : Mathf.Clamp01(dashCooldownTimer / dashCooldown);

    private CharacterController controller;
    private Vector3 verticalVelocity;     // only the Y part is used (gravity / jump)
    private Vector3 dashDirection;
    private float dashTimer;
    private float dashCooldownTimer;
    private float staminaRegenTimer;
    private float invincibleTimer;
    private float speedMultiplier = 1f;
    private Coroutine speedBoostRoutine;
    private Color originalColor;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        CurrentHealth = maxHealth;
        CurrentStamina = maxStamina;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null) originalColor = bodyRenderer.material.color;
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (IsDead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        // Timers
        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
        if (invincibleTimer > 0f) invincibleTimer -= Time.deltaTime;

        // 1) Read input (classic Input Manager)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 input = new Vector3(h, 0f, v);
        input = Vector3.ClampMagnitude(input, 1f);

        // 2) Convert input to a world direction relative to the camera
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;
        if (cameraTransform != null)
        {
            camForward = cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();
        }
        Vector3 moveDir = camForward * input.z + camRight * input.x;

        // 3) Always face where the camera looks (good for aiming the ranged attack)
        if (camForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(camForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // 4) Dash (Q or Right Mouse)
        if ((Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1)) && CanDash())
        {
            StartDash(moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : camForward);
        }

        // 5) Horizontal movement
        Vector3 horizontalMove;
        if (IsDashing)
        {
            horizontalMove = dashDirection * dashSpeed;
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) IsDashing = false;
        }
        else
        {
            bool wantsSprint = Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > 0.01f;
            bool canSprint = wantsSprint && CurrentStamina > 0f;
            float speed = (canSprint ? sprintSpeed : walkSpeed) * speedMultiplier;
            horizontalMove = moveDir * speed;

            if (canSprint) UseStamina(sprintStaminaPerSecond * Time.deltaTime);
        }

        // 6) Gravity + jump
        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f; // small push keeps us glued to the ground / ramps
        }
        if (Input.GetButtonDown("Jump") && controller.isGrounded && !IsDashing)
        {
            // v = sqrt(h * -2 * g) gives exactly 'jumpHeight' metres
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            PlaySound(jumpSound);
        }
        verticalVelocity.y += gravity * Time.deltaTime;

        // 7) Move the CharacterController once per frame
        controller.Move((horizontalMove + verticalVelocity) * Time.deltaTime);

        // 8) Stamina regeneration
        RegenerateStamina();
    }

    // ------------------------------------------------------------------
    // Dash
    // ------------------------------------------------------------------

    private bool CanDash()
    {
        return !IsDashing && dashCooldownTimer <= 0f && CurrentStamina >= dashStaminaCost;
    }

    private void StartDash(Vector3 direction)
    {
        IsDashing = true;
        dashDirection = direction;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        UseStamina(dashStaminaCost);
        PlaySound(dashSound);
        GameManager.SpawnEffect(dashEffectPrefab, transform.position + Vector3.up, 1f);
    }

    // ------------------------------------------------------------------
    // Stamina
    // ------------------------------------------------------------------

    private void UseStamina(float amount)
    {
        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        staminaRegenTimer = staminaRegenDelay;
    }

    private void RegenerateStamina()
    {
        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
            return;
        }
        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenPerSecond * Time.deltaTime);
    }

    public void RestoreStamina(float amount)
    {
        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + amount);
    }

    // ------------------------------------------------------------------
    // Health
    // ------------------------------------------------------------------

    /// <summary>Called by enemies and enemy projectiles.</summary>
    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        if (invincibleTimer > 0f) return;
        if (IsDashing && invincibleWhileDashing) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        invincibleTimer = invincibilityAfterHit;

        PlaySound(hurtSound);
        if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(hurtShakeDuration, hurtShakeMagnitude);
        if (UIManager.Instance != null) UIManager.Instance.FlashDamage();
        if (bodyRenderer != null) StartCoroutine(HurtFlash());

        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    private void Die()
    {
        IsDead = true;
        PlaySound(deathSound);
        GameManager.SpawnEffect(deathEffectPrefab, transform.position + Vector3.up, 3f);
        if (bodyRenderer != null) bodyRenderer.enabled = false;
        if (GameManager.Instance != null) GameManager.Instance.GameOver();
    }

    private IEnumerator HurtFlash()
    {
        bodyRenderer.material.color = hurtFlashColor;
        yield return new WaitForSeconds(0.1f);
        if (bodyRenderer != null) bodyRenderer.material.color = originalColor;
    }

    // ------------------------------------------------------------------
    // Power-ups
    // ------------------------------------------------------------------

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        if (speedBoostRoutine != null) StopCoroutine(speedBoostRoutine);
        speedBoostRoutine = StartCoroutine(SpeedBoostRoutine(multiplier, duration));
    }

    private IEnumerator SpeedBoostRoutine(float multiplier, float duration)
    {
        speedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
        speedBoostRoutine = null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
