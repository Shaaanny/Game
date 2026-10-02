using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy brain using a NavMeshAgent. One script for every enemy type;
/// pick the type in the Inspector and tweak the numbers.
///
///  Chaser  - fast and weak. Runs in, hits once, then retreats briefly (hit-and-run).
///  Brute   - slow and strong. Walks up and does a heavy, telegraphed smash.
///  Shooter - keeps its distance, shoots projectiles, retreats if you get too close.
///  Boss    - big. Slams the ground when close, fires a spread of projectiles when far.
///
/// States: Chasing -> Attacking -> (Retreating) -> Chasing ...
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAI : MonoBehaviour
{
    public enum EnemyType { Chaser, Brute, Shooter, Boss }
    public enum State { Chasing, Attacking, Retreating, Dead }

    [Header("Type")]
    [SerializeField] private EnemyType enemyType = EnemyType.Chaser;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float turnSpeed = 10f;

    [Header("Attack")]
    [Tooltip("Melee enemies: distance to start a hit. Shooter/Boss: distance to start shooting.")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.2f;
    [Tooltip("Pause before the hit lands so the player can react / dodge.")]
    [SerializeField] private float attackWindup = 0.35f;
    [Tooltip("Optional child object shown during the wind-up (e.g. a red sphere) as a warning.")]
    [SerializeField] private GameObject windupIndicator;

    [Header("Retreat")]
    [Tooltip("Chaser style: back off after each attack.")]
    [SerializeField] private bool retreatAfterAttack = false;
    [SerializeField] private float retreatDuration = 1.0f;
    [Tooltip("Shooter style: retreat when the player is closer than this. 0 = never.")]
    [SerializeField] private float tooCloseDistance = 0f;

    [Header("Ranged (Shooter & Boss)")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Where projectiles spawn. If empty, the enemy's chest is used.")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 18f;
    [Tooltip("Layers that block line of sight (walls, obstacles). Usually Default + Ground.")]
    [SerializeField] private LayerMask lineOfSightBlockers = 1;

    [Header("Boss Only")]
    [Tooltip("Boss uses a ground slam when the player is within this distance.")]
    [SerializeField] private float bossMeleeRange = 3.5f;
    [SerializeField] private float bossSlamRadius = 4.5f;
    [SerializeField] private int bossProjectileCount = 5;
    [SerializeField] private float bossSpreadAngle = 50f;

    [Header("Knockback")]
    [Tooltip("0 = full knockback, 1 = immune. Brutes and Bosses should be high.")]
    [Range(0f, 1f)]
    [SerializeField] private float knockbackResistance = 0f;

    [Header("Effects & Audio Hooks (optional)")]
    [SerializeField] private GameObject attackEffectPrefab;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private AudioClip shootSound;

    public State CurrentState => state;
    public EnemyType Type => enemyType;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerController player;
    private State state = State.Chasing;
    private float cooldownTimer;
    private float retreatTimer;
    private Vector3 knockbackVelocity;
    private Coroutine attackRoutine;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.updateRotation = false;   // we rotate manually to always face the player
        // How close the agent walks before stopping on its own.
        if (enemyType == EnemyType.Boss) agent.stoppingDistance = bossMeleeRange * 0.6f;
        else if (enemyType == EnemyType.Shooter) agent.stoppingDistance = 0f;
        else agent.stoppingDistance = attackRange * 0.8f;

        if (windupIndicator != null) windupIndicator.SetActive(false);
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            player = playerObj.GetComponent<PlayerController>();
        }

        // Make sure we are standing on the NavMesh (spawn points can be slightly off).
        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        // Small random delay so a whole wave doesn't attack on the exact same frame.
        cooldownTimer = Random.Range(0.2f, attackCooldown);
    }

    /// <summary>Called by the WaveManager so later waves hit harder and move faster.</summary>
    public void ApplyWaveScaling(float damageMultiplier, float speedMultiplier)
    {
        attackDamage *= damageMultiplier;
        moveSpeed *= speedMultiplier;
        if (agent != null) agent.speed = moveSpeed;
    }

    private void Update()
    {
        if (state == State.Dead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        if (!agent.isOnNavMesh) return;

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
        ApplyKnockbackMovement();

        // Player missing or dead: stand still.
        if (playerTransform == null || (player != null && player.IsDead))
        {
            agent.isStopped = true;
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        switch (state)
        {
            case State.Chasing:
                UpdateChasing(distance);
                break;

            case State.Attacking:
                // The attack coroutine does the work; just keep looking at the player.
                FaceTarget(playerTransform.position);
                break;

            case State.Retreating:
                UpdateRetreating();
                break;
        }
    }

    // ------------------------------------------------------------------
    // States
    // ------------------------------------------------------------------

    private void UpdateChasing(float distance)
    {
        // Shooters back away if the player rushes them.
        if (tooCloseDistance > 0f && distance < tooCloseDistance)
        {
            StartRetreat(retreatDuration);
            return;
        }

        bool inRange = distance <= attackRange;
        bool canSee = !IsRangedType() || HasLineOfSight();

        // The Boss keeps stomping toward you between attacks so it can use its slam.
        bool keepWalking = enemyType == EnemyType.Boss && cooldownTimer > 0f;

        if (inRange && canSee && !keepWalking)
        {
            // Close enough: stop and attack when the cooldown is ready.
            agent.isStopped = true;
            FaceTarget(playerTransform.position);
            if (cooldownTimer <= 0f)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }
        else
        {
            // Keep chasing.
            agent.isStopped = false;
            agent.SetDestination(playerTransform.position);
            FaceTarget(playerTransform.position);
        }
    }

    private void StartRetreat(float duration)
    {
        state = State.Retreating;
        retreatTimer = duration;
    }

    private void UpdateRetreating()
    {
        retreatTimer -= Time.deltaTime;

        // Pick a point a few metres directly away from the player and walk there.
        Vector3 away = transform.position - playerTransform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        Vector3 target = transform.position + away.normalized * 4f;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
        FaceTarget(playerTransform.position);

        if (retreatTimer <= 0f) state = State.Chasing;
    }

    // ------------------------------------------------------------------
    // Attacks
    // ------------------------------------------------------------------

    private IEnumerator AttackRoutine()
    {
        state = State.Attacking;
        agent.isStopped = true;
        cooldownTimer = attackCooldown;

        // Wind-up (telegraph)
        if (windupIndicator != null) windupIndicator.SetActive(true);
        yield return new WaitForSeconds(attackWindup);
        if (windupIndicator != null) windupIndicator.SetActive(false);

        if (state == State.Dead || playerTransform == null) yield break;

        switch (enemyType)
        {
            case EnemyType.Chaser:
            case EnemyType.Brute:
                DoMeleeHit(attackRange + 0.5f, attackDamage);
                break;

            case EnemyType.Shooter:
                FireAt(playerTransform.position + Vector3.up, 0f);
                break;

            case EnemyType.Boss:
                float distance = Vector3.Distance(transform.position, playerTransform.position);
                if (distance <= bossMeleeRange) DoBossSlam();
                else FireSpread();
                break;
        }

        // Short recovery so the player gets a window to punish.
        yield return new WaitForSeconds(0.25f);
        attackRoutine = null;

        if (state == State.Dead) yield break;
        if (retreatAfterAttack) StartRetreat(retreatDuration);
        else state = State.Chasing;
    }

    private void DoMeleeHit(float reach, float damage)
    {
        PlaySound(attackSound);
        GameManager.SpawnEffect(attackEffectPrefab, transform.position + transform.forward + Vector3.up, 1f);

        // The player may have dodged out of range during the wind-up.
        if (player != null && Vector3.Distance(transform.position, playerTransform.position) <= reach)
        {
            player.TakeDamage(damage);
        }
    }

    private void DoBossSlam()
    {
        PlaySound(attackSound);
        GameManager.SpawnEffect(attackEffectPrefab, transform.position, 2f);
        if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.4f, 0.5f);

        if (player != null && Vector3.Distance(transform.position, playerTransform.position) <= bossSlamRadius)
        {
            player.TakeDamage(attackDamage * 1.5f);
        }
    }

    private void FireSpread()
    {
        int count = Mathf.Max(1, bossProjectileCount);
        Vector3 target = playerTransform.position + Vector3.up;
        for (int i = 0; i < count; i++)
        {
            // Spread the shots evenly from -angle/2 to +angle/2.
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float angle = Mathf.Lerp(-bossSpreadAngle * 0.5f, bossSpreadAngle * 0.5f, t);
            FireAt(target, angle);
        }
    }

    private void FireAt(Vector3 targetPoint, float yawOffset)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning(name + ": No projectile prefab assigned.");
            return;
        }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 direction = (targetPoint - origin).normalized;
        direction = Quaternion.Euler(0f, yawOffset, 0f) * direction;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;

        Projectile proj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
        proj.Launch(direction, projectileSpeed, attackDamage, false);
        PlaySound(shootSound);
    }

    // ------------------------------------------------------------------
    // Knockback & death
    // ------------------------------------------------------------------

    /// <summary>Called by EnemyHealth when hit. 'force' = direction * strength.</summary>
    public void ApplyKnockback(Vector3 force)
    {
        knockbackVelocity += force * (1f - knockbackResistance) * 3f;
    }

    private void ApplyKnockbackMovement()
    {
        if (knockbackVelocity.sqrMagnitude < 0.01f) return;
        // agent.Move respects the NavMesh, so enemies can't be pushed through walls.
        agent.Move(knockbackVelocity * Time.deltaTime);
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 8f * Time.deltaTime);
    }

    /// <summary>Called by EnemyHealth right before the enemy is destroyed.</summary>
    public void OnDeath()
    {
        state = State.Dead;
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private bool IsRangedType()
    {
        return enemyType == EnemyType.Shooter || enemyType == EnemyType.Boss;
    }

    private bool HasLineOfSight()
    {
        Vector3 eye = transform.position + Vector3.up * 1.2f;
        Vector3 target = playerTransform.position + Vector3.up;
        // If a wall is in the way, Linecast returns true.
        return !Physics.Linecast(eye, target, lineOfSightBlockers, QueryTriggerInteraction.Ignore);
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        if (tooCloseDistance > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, tooCloseDistance);
        }
        if (enemyType == EnemyType.Boss)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, bossSlamRadius);
        }
    }
}
