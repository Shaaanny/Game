using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns enemies in waves at the spawn points.
///  - Each wave has more (and tougher) enemies than the last.
///  - Brutes join from wave 2, Shooters from wave 3.
///  - Every 5th wave is a Boss wave.
///  - Short break between waves with a countdown.
///  - Clearing the final wave = Victory.
/// There must be exactly ONE WaveManager in the scene.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawn Points")]
    [Tooltip("Empty GameObjects placed around the arena edges.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject chaserPrefab;
    [SerializeField] private GameObject brutePrefab;
    [SerializeField] private GameObject shooterPrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("Wave Settings")]
    [Tooltip("Clear this many waves to win. 0 = endless (win by score only).")]
    [SerializeField] private int totalWaves = 10;
    [SerializeField] private int enemiesInFirstWave = 4;
    [SerializeField] private int extraEnemiesPerWave = 2;
    [Tooltip("A boss appears every N waves.")]
    [SerializeField] private int bossEveryNWaves = 5;
    [Tooltip("Normal enemies that come along with the boss.")]
    [SerializeField] private int bossWaveEscorts = 3;
    [Tooltip("Seconds between each enemy spawn within a wave.")]
    [SerializeField] private float spawnInterval = 0.6f;
    [Tooltip("Break (seconds) before the first wave and between waves.")]
    [SerializeField] private float timeBetweenWaves = 5f;
    [Tooltip("Never have more than this many enemies alive at once.")]
    [SerializeField] private int maxAliveAtOnce = 15;

    [Header("Unlocks")]
    [SerializeField] private int bruteStartWave = 2;
    [SerializeField] private int shooterStartWave = 3;

    [Header("Difficulty Scaling (per wave after the first)")]
    [SerializeField] private float healthIncreasePerWave = 0.12f;  // +12% health per wave
    [SerializeField] private float damageIncreasePerWave = 0.06f;  // +6% damage per wave
    [SerializeField] private float speedIncreasePerWave = 0.03f;   // +3% speed per wave

    [Header("Audio Hooks (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip waveStartSound;
    [SerializeField] private AudioClip bossWaveSound;
    [SerializeField] private AudioClip waveClearedSound;

    // ---- Public read-only values (UI reads these) ----
    public int CurrentWave { get; private set; }
    public int EnemiesRemaining => enemiesLeftToSpawn + aliveEnemies.Count;
    public bool IsOnBreak { get; private set; }
    public float BreakTimeRemaining { get; private set; }
    public bool IsBossWave => bossEveryNWaves > 0 && CurrentWave > 0 && CurrentWave % bossEveryNWaves == 0;
    public int TotalWaves => totalWaves;

    private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();
    private int enemiesLeftToSpawn;
    private bool started;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Called by GameManager.StartGame().</summary>
    public void BeginWaves()
    {
        if (started) return;
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("WaveManager: No spawn points assigned!");
            return;
        }
        started = true;
        StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        while (true)
        {
            // ---- Break / countdown ----
            IsOnBreak = true;
            BreakTimeRemaining = timeBetweenWaves;
            while (BreakTimeRemaining > 0f)
            {
                BreakTimeRemaining -= Time.deltaTime; // scaled time, so pausing pauses the countdown
                yield return null;
            }
            IsOnBreak = false;

            // ---- Start next wave ----
            CurrentWave++;
            if (IsBossWave)
            {
                PlaySound(bossWaveSound);
                if (UIManager.Instance != null) UIManager.Instance.ShowBanner("BOSS WAVE " + CurrentWave + "!");
            }
            else
            {
                PlaySound(waveStartSound);
                if (UIManager.Instance != null) UIManager.Instance.ShowBanner("WAVE " + CurrentWave);
            }

            yield return StartCoroutine(SpawnWave());

            // ---- Wait until every enemy is dead ----
            while (EnemiesRemaining > 0)
            {
                aliveEnemies.RemoveAll(e => e == null); // safety: clean destroyed references
                yield return null;
            }

            PlaySound(waveClearedSound);
            if (UIManager.Instance != null) UIManager.Instance.ShowBanner("WAVE " + CurrentWave + " CLEARED");

            // ---- Victory after the last wave ----
            if (totalWaves > 0 && CurrentWave >= totalWaves)
            {
                if (GameManager.Instance != null) GameManager.Instance.Victory();
                yield break;
            }
        }
    }

    private IEnumerator SpawnWave()
    {
        List<GameObject> toSpawn = BuildWaveList(CurrentWave);
        enemiesLeftToSpawn = toSpawn.Count;

        foreach (GameObject prefab in toSpawn)
        {
            // Don't flood the arena: wait while too many are alive.
            while (aliveEnemies.Count >= maxAliveAtOnce)
            {
                aliveEnemies.RemoveAll(e => e == null);
                yield return null;
            }

            SpawnEnemy(prefab);
            enemiesLeftToSpawn--;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>Decides which enemies appear in a given wave.</summary>
    private List<GameObject> BuildWaveList(int wave)
    {
        List<GameObject> list = new List<GameObject>();
        bool bossWave = bossEveryNWaves > 0 && wave % bossEveryNWaves == 0;

        if (bossWave)
        {
            if (bossPrefab != null) list.Add(bossPrefab);
            for (int i = 0; i < bossWaveEscorts; i++)
            {
                GameObject escort = PickRandomEnemy(wave);
                if (escort != null) list.Add(escort);
            }
            return list;
        }

        int count = enemiesInFirstWave + (wave - 1) * extraEnemiesPerWave;
        for (int i = 0; i < count; i++)
        {
            GameObject enemy = PickRandomEnemy(wave);
            if (enemy != null) list.Add(enemy);
        }
        return list;
    }

    private GameObject PickRandomEnemy(int wave)
    {
        // Build a weighted pool. Chasers are common, brutes and shooters rarer.
        List<GameObject> pool = new List<GameObject>();
        if (chaserPrefab != null) { pool.Add(chaserPrefab); pool.Add(chaserPrefab); pool.Add(chaserPrefab); }
        if (brutePrefab != null && wave >= bruteStartWave) { pool.Add(brutePrefab); }
        if (shooterPrefab != null && wave >= shooterStartWave) { pool.Add(shooterPrefab); pool.Add(shooterPrefab); }

        if (pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null) return;

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Vector3 position = point.position + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));

        // Snap to the NavMesh so the agent spawns correctly.
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            position = hit.position;
        }
        else
        {
            Debug.LogWarning("WaveManager: Spawn point '" + point.name + "' is not near the NavMesh. Did you bake it?");
        }

        GameObject enemyObj = Instantiate(prefab, position, point.rotation);

        // Make later waves tougher.
        int wavesAfterFirst = Mathf.Max(0, CurrentWave - 1);
        float healthMult = 1f + healthIncreasePerWave * wavesAfterFirst;
        float damageMult = 1f + damageIncreasePerWave * wavesAfterFirst;
        float speedMult = 1f + speedIncreasePerWave * wavesAfterFirst;

        EnemyHealth health = enemyObj.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.ApplyHealthMultiplier(healthMult);
            aliveEnemies.Add(health);
        }
        else
        {
            Debug.LogWarning("WaveManager: Enemy prefab '" + prefab.name + "' has no EnemyHealth component.");
        }

        EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
        if (ai != null) ai.ApplyWaveScaling(damageMult, speedMult);
    }

    /// <summary>Called by EnemyHealth when an enemy dies.</summary>
    public void OnEnemyDied(EnemyHealth enemy)
    {
        aliveEnemies.Remove(enemy);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // Shows spawn points in the Scene view.
    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.magenta;
        foreach (Transform p in spawnPoints)
        {
            if (p != null) Gizmos.DrawWireSphere(p.position, 1f);
        }
    }
}
