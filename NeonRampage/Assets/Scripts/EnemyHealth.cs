using System.Collections;
using UnityEngine;

/// <summary>
/// Health for any enemy (Chaser, Brute, Shooter, Boss).
/// Handles: taking damage, hit flash, floating damage numbers, hit/death particles,
/// score on death, random pickup drops, and telling the WaveManager it died.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 30f;
    [Tooltip("Points added to the score when this enemy dies.")]
    [SerializeField] private int scoreValue = 100;
    [Tooltip("Tick this on the Boss prefab.")]
    [SerializeField] private bool isBoss = false;

    [Header("Hit Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    [Header("Damage Numbers")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private Color damageNumberColor = Color.yellow;
    [Tooltip("Optional font. If empty, Unity's built-in font is used.")]
    [SerializeField] private Font damageNumberFont;

    [Header("Drops")]
    [Range(0f, 1f)]
    [SerializeField] private float dropChance = 0.3f;
    [Tooltip("Pickup prefabs that can drop. One is chosen at random.")]
    [SerializeField] private GameObject[] dropPrefabs;

    [Header("Effects & Audio Hooks (optional)")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsBoss => isBoss;
    public int ScoreValue => scoreValue;

    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashRoutine;
    private EnemyAI ai;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        ai = GetComponent<EnemyAI>();

        // Remember every renderer's colour so we can flash and restore it.
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].material.color;
        }
    }

    /// <summary>Called by the WaveManager to make later waves tougher.</summary>
    public void ApplyHealthMultiplier(float multiplier)
    {
        maxHealth *= multiplier;
        CurrentHealth = maxHealth;
    }

    /// <summary>
    /// Deal damage to this enemy.
    /// hitPoint  = where the hit landed (for particles / numbers).
    /// knockback = direction * strength to push the enemy (Vector3.zero for none).
    /// </summary>
    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 knockback)
    {
        if (IsDead) return;

        CurrentHealth -= amount;

        GameManager.SpawnEffect(hitEffectPrefab, hitPoint, 1.5f);
        if (hitSound != null) AudioSource.PlayClipAtPoint(hitSound, hitPoint);
        if (showDamageNumbers) SpawnDamageNumber(amount, hitPoint);

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(HitFlash());

        if (ai != null && knockback.sqrMagnitude > 0f) ai.ApplyKnockback(knockback);

        if (CurrentHealth <= 0f) Die();
    }

    private void Die()
    {
        IsDead = true;
        CurrentHealth = 0f;

        Vector3 center = transform.position + Vector3.up;
        GameManager.SpawnEffect(deathEffectPrefab, center, 3f);
        if (deathSound != null) AudioSource.PlayClipAtPoint(deathSound, center);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreValue);
            if (isBoss) GameManager.Instance.HitStop();
        }
        if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(isBoss ? 0.6f : 0.15f, isBoss ? 0.6f : 0.15f);

        TryDropPickup();

        if (WaveManager.Instance != null) WaveManager.Instance.OnEnemyDied(this);
        if (ai != null) ai.OnDeath();

        Destroy(gameObject);
    }

    private void TryDropPickup()
    {
        if (dropPrefabs == null || dropPrefabs.Length == 0) return;
        // Bosses always drop something.
        if (!isBoss && Random.value > dropChance) return;

        GameObject prefab = dropPrefabs[Random.Range(0, dropPrefabs.Length)];
        if (prefab == null) return;
        Instantiate(prefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
    }

    private IEnumerator HitFlash()
    {
        SetColor(flashColor, false);
        yield return new WaitForSeconds(flashDuration);
        SetColor(Color.white, true);
        flashRoutine = null;
    }

    private void SetColor(Color color, bool restoreOriginal)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].material.color = restoreOriginal ? originalColors[i] : color;
        }
    }

    // ------------------------------------------------------------------
    // Floating damage numbers (built at runtime, no prefab needed)
    // ------------------------------------------------------------------

    private void SpawnDamageNumber(float amount, Vector3 position)
    {
        Font font = damageNumberFont;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) return;

        GameObject numberObj = new GameObject("DamageNumber");
        numberObj.transform.position = position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f;

        TextMesh text = numberObj.AddComponent<TextMesh>();
        text.text = Mathf.RoundToInt(amount).ToString();
        text.font = font;
        text.fontSize = 64;
        text.characterSize = 0.05f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = damageNumberColor;

        MeshRenderer meshRenderer = numberObj.GetComponent<MeshRenderer>();
        meshRenderer.material = font.material;

        // Run the animation on the GameManager so it keeps going after this enemy is destroyed.
        if (GameManager.Instance != null)
            GameManager.Instance.StartCoroutine(AnimateDamageNumber(numberObj, text));
        else
            Destroy(numberObj, 0.8f);
    }

    private static IEnumerator AnimateDamageNumber(GameObject numberObj, TextMesh text)
    {
        const float duration = 0.8f;
        float t = 0f;
        Color startColor = text.color;

        while (t < duration && numberObj != null)
        {
            t += Time.deltaTime;
            numberObj.transform.position += Vector3.up * 1.5f * Time.deltaTime;

            // Always face the camera so it is readable.
            if (Camera.main != null) numberObj.transform.rotation = Camera.main.transform.rotation;

            Color c = startColor;
            c.a = 1f - (t / duration);
            text.color = c;
            yield return null;
        }

        if (numberObj != null) Destroy(numberObj);
    }
}
