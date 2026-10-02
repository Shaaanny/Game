using UnityEngine;

/// <summary>
/// Third-person orbit camera. Mouse moves the camera around the player,
/// the camera follows with smooth damping, avoids clipping through walls,
/// and can shake when something gets hit.
/// Put this on the Main Camera (NOT as a child of the player).
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Header("Target")]
    [Tooltip("The player transform to follow.")]
    [SerializeField] private Transform target;
    [Tooltip("Point on the player the camera looks at (shoulder height).")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Orbit")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private bool invertY = false;

    [Header("Smoothing")]
    [Tooltip("How long the camera takes to catch up to the player. Smaller = snappier.")]
    [SerializeField] private float followSmoothTime = 0.08f;

    [Header("Wall Collision")]
    [Tooltip("Layers the camera should not go through (e.g. Default, Ground). Exclude Player and Enemy.")]
    [SerializeField] private LayerMask collisionLayers = 1; // "Default" layer
    [SerializeField] private float collisionRadius = 0.3f;

    private float yaw;
    private float pitch = 15f;
    private Vector3 smoothedPivot;
    private Vector3 pivotVelocity;

    // Screen shake state
    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeMagnitude;

    /// <summary>The horizontal angle of the camera. The player uses this to move relative to the view.</summary>
    public float Yaw => yaw;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        yaw = transform.eulerAngles.y;
        if (target != null) smoothedPivot = target.position + targetOffset;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Only read the mouse while the game is running (not in menus / pause).
        bool canLook = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        if (canLook)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch += invertY ? mouseY : -mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Smoothly follow the pivot point above the player.
        Vector3 desiredPivot = target.position + targetOffset;
        smoothedPivot = Vector3.SmoothDamp(smoothedPivot, desiredPivot, ref pivotVelocity, followSmoothTime);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = smoothedPivot - rotation * Vector3.forward * distance;

        // Pull the camera in if a wall is between the player and the camera.
        Vector3 dir = desiredPosition - smoothedPivot;
        float finalDistance = distance;
        if (Physics.SphereCast(smoothedPivot, collisionRadius, dir.normalized, out RaycastHit hit,
                               distance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            finalDistance = Mathf.Max(hit.distance, 0.5f);
        }
        Vector3 finalPosition = smoothedPivot + dir.normalized * finalDistance;

        // Apply screen shake as a small random offset.
        if (shakeTimeRemaining > 0f)
        {
            shakeTimeRemaining -= Time.unscaledDeltaTime;
            float strength = shakeMagnitude * (shakeTimeRemaining / shakeDuration); // fades out
            finalPosition += Random.insideUnitSphere * strength;
        }

        transform.position = finalPosition;
        transform.rotation = rotation;
    }

    /// <summary>
    /// Shakes the camera. Bigger magnitude = stronger shake.
    /// Example: CameraFollow.Instance.Shake(0.15f, 0.2f);
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        // Keep the stronger of the current and the new shake.
        if (magnitude >= shakeMagnitude || shakeTimeRemaining <= 0f)
        {
            shakeDuration = Mathf.Max(duration, 0.01f);
            shakeTimeRemaining = shakeDuration;
            shakeMagnitude = magnitude;
        }
    }

    /// <summary>Lets other scripts change the follow target at runtime.</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null) smoothedPivot = target.position + targetOffset;
    }
}
