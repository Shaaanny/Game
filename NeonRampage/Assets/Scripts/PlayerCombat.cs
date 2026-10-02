using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player attacks:
///  - Melee (Left Click): a 3-hit combo. Each swing checks a sphere in front of the player.
///  - Ranged (E or F): fires a Projectile toward the centre of the screen, with a cooldown.
/// Also handles the damage power-up.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Melee Combo")]
    [Tooltip("Damage of hit 1, hit 2 and hit 3 of the combo.")]
    [SerializeField] private float[] comboDamage = { 12f, 15f, 28f };
    [Tooltip("Knockback strength of each combo hit.")]
    [SerializeField] private float[] comboKnockback = { 3f, 4f, 9f };
    [Tooltip("Time between swings. You can't attack faster than this.")]
    [SerializeField] private float swingCooldown = 0.3f;
    [Tooltip("If you don't click again within this time, the combo resets to hit 1.")]
    [SerializeField] private float comboResetTime = 0.9f;
    [Tooltip("Empty child object in front of the player (about 1.2 m forward, 1 m up).")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1.3f;
    [Tooltip("Layers that melee can hit. Set to the Enemy layer (Everything also works, just slower).")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Melee Visual (optional)")]
    [Tooltip("A child object (e.g. a flat cube 'blade') shown briefly during each swing.")]
    [SerializeField] private GameObject swingVisual;
    [SerializeField] private float swingVisualTime = 0.12f;

    [Header("Ranged Attack")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Empty child object where projectiles spawn (in front of the chest).")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private float projectileDamage = 15f;
    [SerializeField] private float fireCooldown = 0.45f;
    [Tooltip("How far the aim ray checks from the camera.")]
    [SerializeField] private float aimDistance = 100f;

    [Header("Game Feel")]
    [SerializeField] private float hitShakeDuration = 0.1f;
    [SerializeField] private float hitShakeMagnitude = 0.12f;
    [Tooltip("The 3rd combo hit is stronger: bigger shake + hit stop.")]
    [SerializeField] private float finisherShakeMagnitude = 0.25f;

    [Header("Effects & Audio Hooks (optional)")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip shootSound;

    // ---- Public read-only values (UI can read these) ----
    public int ComboStep => comboStep;
    public float FireCooldownPercent => fireCooldown <= 0f ? 0f : Mathf.Clamp01(fireTimer / fireCooldown);
    public float DamageMultiplier => damageMultiplier;

    private PlayerController player;
    private Camera mainCamera;
    private int comboStep;            // 0, 1 or 2
    private float swingTimer;
    private float comboTimer;
    private float fireTimer;
    private float damageMultiplier = 1f;
    private Coroutine damageBoostRoutine;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (swingVisual != null) swingVisual.SetActive(false);
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (attackPoint == null) attackPoint = transform;
        if (firePoint == null) firePoint = transform;
    }

    private void Update()
    {
        if (player.IsDead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        if (swingTimer > 0f) swingTimer -= Time.deltaTime;
        if (fireTimer > 0f) fireTimer -= Time.deltaTime;

        // Reset the combo if the player waited too long.
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) comboStep = 0;
        }

        // Left Click = melee
        if (Input.GetMouseButtonDown(0) && swingTimer <= 0f)
        {
            MeleeAttack();
        }

        // E or F = ranged
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)) && fireTimer <= 0f)
        {
            RangedAttack();
        }
    }

    // ------------------------------------------------------------------
    // Melee
    // ------------------------------------------------------------------

    private void MeleeAttack()
    {
        int step = comboStep;
        bool isFinisher = step == comboDamage.Length - 1;

        float damage = GetArrayValue(comboDamage, step, 10f) * damageMultiplier;
        float knockback = GetArrayValue(comboKnockback, step, 3f);

        swingTimer = swingCooldown;
        comboTimer = comboResetTime;
        comboStep = (comboStep + 1) % Mathf.Max(1, comboDamage.Length);

        PlaySound(swingSound);
        if (swingVisual != null) StartCoroutine(ShowSwingVisual());

        // Find every collider inside the attack sphere on the Enemy layer.
        Collider[] hits = Physics.OverlapSphere(attackPoint.position, attackRadius, enemyLayers, QueryTriggerInteraction.Ignore);

        // An enemy can have several colliders. Make sure we only damage it once per swing.
        HashSet<EnemyHealth> alreadyHit = new HashSet<EnemyHealth>();
        bool hitSomething = false;

        foreach (Collider col in hits)
        {
            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || alreadyHit.Contains(enemy)) continue;
            alreadyHit.Add(enemy);

            Vector3 knockDir = enemy.transform.position - transform.position;
            knockDir.y = 0f;
            Vector3 hitPoint = col.ClosestPoint(attackPoint.position);
            enemy.TakeDamage(damage, hitPoint, knockDir.normalized * knockback);
            hitSomething = true;
        }

        if (hitSomething)
        {
            PlaySound(hitSound);
            if (CameraFollow.Instance != null)
                CameraFollow.Instance.Shake(hitShakeDuration, isFinisher ? finisherShakeMagnitude : hitShakeMagnitude);
            if (isFinisher && GameManager.Instance != null) GameManager.Instance.HitStop();
        }
    }

    private IEnumerator ShowSwingVisual()
    {
        swingVisual.SetActive(true);
        yield return new WaitForSeconds(swingVisualTime);
        if (swingVisual != null) swingVisual.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Ranged
    // ------------------------------------------------------------------

    private void RangedAttack()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("PlayerCombat: No projectile prefab assigned.");
            return;
        }
        fireTimer = fireCooldown;

        // Aim at whatever is in the centre of the screen.
        Vector3 aimPoint;
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            aimPoint = ray.origin + ray.direction * aimDistance;

            // The ray passes through the player's own body, so look at ALL hits
            // and use the closest one that isn't the player.
            RaycastHit[] rayHits = Physics.RaycastAll(ray, aimDistance, ~0, QueryTriggerInteraction.Ignore);
            // Ignore anything between the camera and the player (it would make us shoot backwards).
            float minDistance = Vector3.Dot(firePoint.position - ray.origin, ray.direction);
            float closest = float.MaxValue;
            foreach (RaycastHit hit in rayHits)
            {
                if (hit.collider.CompareTag("Player") || hit.distance < minDistance) continue;
                if (hit.distance < closest)
                {
                    closest = hit.distance;
                    aimPoint = hit.point;
                }
            }
        }
        else
        {
            aimPoint = firePoint.position + transform.forward * aimDistance;
        }

        Vector3 direction = (aimPoint - firePoint.position).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
        Projectile proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(direction));
        proj.Launch(direction, projectileSpeed, projectileDamage * damageMultiplier, true);

        PlaySound(shootSound);
        GameManager.SpawnEffect(muzzleFlashPrefab, firePoint.position, 0.5f);
    }

    // ------------------------------------------------------------------
    // Power-ups
    // ------------------------------------------------------------------

    public void ApplyDamageBoost(float multiplier, float duration)
    {
        if (damageBoostRoutine != null) StopCoroutine(damageBoostRoutine);
        damageBoostRoutine = StartCoroutine(DamageBoostRoutine(multiplier, duration));
    }

    private IEnumerator DamageBoostRoutine(float multiplier, float duration)
    {
        damageMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        damageMultiplier = 1f;
        damageBoostRoutine = null;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static float GetArrayValue(float[] array, int index, float fallback)
    {
        if (array == null || array.Length == 0) return fallback;
        return array[Mathf.Clamp(index, 0, array.Length - 1)];
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // Draws the melee range in the Scene view so you can tune it.
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}
