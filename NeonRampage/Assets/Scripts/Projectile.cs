using UnityEngine;

/// <summary>
/// A simple bullet used by BOTH the player and enemies.
/// It moves itself every frame and uses a SphereCast to detect hits, so it never
/// passes through thin walls and does not need a Rigidbody.
/// Call Launch() right after Instantiate().
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Defaults (overridden by Launch)")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float damage = 10f;

    [Header("Behaviour")]
    [Tooltip("Seconds before the projectile destroys itself.")]
    [SerializeField] private float lifetime = 3f;
    [Tooltip("Size of the hit check. Match roughly the visual sphere's radius.")]
    [SerializeField] private float radius = 0.15f;
    [Tooltip("Which layers the projectile can hit. 'Everything' works fine.")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [Tooltip("Knockback applied to enemies hit by the player's projectile.")]
    [SerializeField] private float knockback = 2f;

    [Header("Colors (optional)")]
    [Tooltip("Tints the projectile so you can tell player shots from enemy shots.")]
    [SerializeField] private Color playerColor = new Color(0f, 1f, 1f);   // cyan
    [SerializeField] private Color enemyColor = new Color(1f, 0.2f, 0.8f); // pink

    [Header("Effects & Audio Hooks (optional)")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioClip hitSound;

    private Vector3 direction = Vector3.forward;
    private bool firedByPlayer = true;
    private bool launched;
    private bool finished;

    /// <summary>
    /// Starts the projectile moving.
    /// fromPlayer = true  -> it damages enemies and ignores the player.
    /// fromPlayer = false -> it damages the player and ignores enemies.
    /// </summary>
    public void Launch(Vector3 newDirection, float newSpeed, float newDamage, bool fromPlayer)
    {
        direction = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : transform.forward;
        speed = newSpeed;
        damage = newDamage;
        firedByPlayer = fromPlayer;
        launched = true;

        transform.rotation = Quaternion.LookRotation(direction);

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) rend.material.color = firedByPlayer ? playerColor : enemyColor;

        // Our own collider (if any) is only visual; we detect hits with SphereCast instead.
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null) ownCollider.enabled = false;

        Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        // If someone forgot to call Launch, still fly forward and clean up.
        if (!launched)
        {
            direction = transform.forward;
            launched = true;
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        if (finished) return;

        float step = speed * Time.deltaTime;

        // Look along the path we will travel this frame.
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, direction, step,
                                                  hitLayers, QueryTriggerInteraction.Ignore);

        // SphereCastAll is not sorted, so find the closest valid hit ourselves.
        RaycastHit? closestHit = null;
        float closestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (ShouldIgnore(hit.collider)) continue;

            // distance == 0 means we started inside the collider; still counts as a hit.
            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
            }
        }

        if (closestHit.HasValue)
        {
            RaycastHit hit = closestHit.Value;
            Vector3 impactPoint = hit.point == Vector3.zero ? transform.position : hit.point;
            OnHit(hit.collider, impactPoint);
            return;
        }

        transform.position += direction * step;
    }

    /// <summary>Projectiles pass through whoever fired them (and their teammates).</summary>
    private bool ShouldIgnore(Collider col)
    {
        if (firedByPlayer)
        {
            if (col.CompareTag("Player")) return true;
        }
        else
        {
            if (col.GetComponentInParent<EnemyHealth>() != null) return true;
        }
        // Don't collide with other projectiles.
        if (col.GetComponentInParent<Projectile>() != null) return true;
        return false;
    }

    private void OnHit(Collider col, Vector3 point)
    {
        finished = true;

        if (firedByPlayer)
        {
            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(damage, point, direction * knockback);
                if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.08f, 0.08f);
            }
        }
        else
        {
            PlayerController player = col.GetComponentInParent<PlayerController>();
            if (player != null) player.TakeDamage(damage);
        }

        GameManager.SpawnEffect(hitEffectPrefab, point, 1.5f);
        if (hitSound != null) AudioSource.PlayClipAtPoint(hitSound, point);

        Destroy(gameObject);
    }
}
