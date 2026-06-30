using UnityEngine;

// StableTwoWheelDrive.cs
// Provides two-wheel locomotion and steering for an agent.
// Supports manual teleoperation and autonomous path following via PurePathfinder.
// Also handles anti-drift stabilization, visual wheel rotation, telemetry, and editor gizmos.

[RequireComponent(typeof(PurePathfinder))]
[RequireComponent(typeof(Rigidbody))]
public class StableTwoWheelDrive : MonoBehaviour
{
    [Header("Pathfinding Setup")]
    public Transform target; // Destination transform used by the pathfinder
    public float turnSpeed = 5f; // Angular speed used for steering
    public float stoppingDistance = 1.0f; // Distance considered "arrived"

    private Transform lastTargetTransform;
    private Vector3 lastTargetPosition;
    private PurePathfinder pathfinder;
    private float pathTimer = 0f; // Timer to throttle path recalculation (reserved)

    [Header("Manual Teleop Settings")]
    public float teleopSpeed = 3.0f;
    public float teleopTurnSpeed = 2.0f;
    public bool isManualControl = false; // When true, user input drives the agent
    private Rigidbody rb;

    [Header("Visual Wheels")]
    public GameObject leftWheelMesh; // Visual left wheel
    public GameObject rightWheelMesh; // Visual right wheel
    public float wheelRadius = 0.3f;

    [Header("Grip Settings")]
    [Tooltip("1 = high lateral grip, 0 = low lateral grip")]
    [Range(0f, 1f)]
    public float lateralGrip = 0.95f;

    [Header("Movement Settings")]
    public float driveForce = 1500f;
    public float maxSpeed = 5f;

    // Initialize components, apply constraints, and start telemetry
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        pathfinder = GetComponent<PurePathfinder>();

        // Prevent tipping by freezing X and Z rotations
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        StartCoroutine(TelemetryTicker());
    }

    // Per-frame updates: path recalculation trigger and wheel visuals
    void Update()
    {
        // Recalculate path when necessary
        if (!isManualControl && target != null)
        {
            bool targetChanged = target != lastTargetTransform;
            bool targetMoved = Vector3.Distance(target.position, lastTargetPosition) > 0.5f;
            if (targetChanged || targetMoved || !pathfinder.hasValidPath)
            {
                pathfinder.GeneratePath(target.position);
                lastTargetTransform = target;
                lastTargetPosition = target.position;
            }
        }

        // Rotate visual wheel meshes based on forward velocity
        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float distanceTraveled = forwardSpeed * Time.deltaTime;
        float circumference = 2f * Mathf.PI * wheelRadius;
        float rotationDegrees = (circumference > 0f) ? (distanceTraveled / circumference) * 360f : 0f;

        if (leftWheelMesh != null)
            leftWheelMesh.transform.Rotate(Vector3.right, rotationDegrees, Space.Self);

        if (rightWheelMesh != null)
            rightWheelMesh.transform.Rotate(Vector3.right, rotationDegrees, Space.Self);
    }

    // Fixed-timestep physics: handle manual control or autonomous navigation
    void FixedUpdate()
    {
        if (isManualControl)
        {
            // Read input and apply simple velocity/angular velocity
            float move = Input.GetAxis("Vertical");
            float turn = Input.GetAxis("Horizontal");

            Vector3 targetVelocity = transform.forward * move * teleopSpeed;
            rb.velocity = new Vector3(targetVelocity.x, rb.velocity.y, targetVelocity.z);
            rb.angularVelocity = new Vector3(0f, turn * teleopTurnSpeed, 0f);
            return;
        }

        // Autonomous navigation requires a valid target and path
        if (target == null || !pathfinder.hasValidPath)
        {
            rb.velocity = Vector3.Lerp(rb.velocity, new Vector3(0f, rb.velocity.y, 0f), Time.fixedDeltaTime * 5f);
            rb.angularVelocity = Vector3.zero;
            return;
        }

        // Apply lateral anti-drift force to improve stability
        float sidewaysSpeed = Vector3.Dot(rb.velocity, transform.right);
        Vector3 antiDrift = -transform.right * sidewaysSpeed * lateralGrip;
        rb.AddForce(antiDrift, ForceMode.VelocityChange);

        // Compute planar distance to the target
        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTgt = new Vector3(target.position.x, 0f, target.position.z);
        float distanceToTarget = Vector3.Distance(flatPos, flatTgt);

        if (distanceToTarget > stoppingDistance)
        {
            Vector3 directionToWaypoint = pathfinder.nextWaypoint - transform.position;
            directionToWaypoint.y = 0f;

            float alignment = 1f;
            if (directionToWaypoint.sqrMagnitude > 0.1f)
            {
                float angle = Vector3.SignedAngle(transform.forward, directionToWaypoint.normalized, Vector3.up);
                float steering = Mathf.Clamp(angle / 45f, -1f, 1f);
                rb.angularVelocity = new Vector3(0f, steering * turnSpeed, 0f);
                alignment = Vector3.Dot(transform.forward, directionToWaypoint.normalized);
            }

            // Drive forward only when reasonably aligned
            if (alignment > 0.5f)
            {
                if (rb.velocity.magnitude < maxSpeed)
                    rb.AddForce(transform.forward * driveForce, ForceMode.Force);
            }
            else
            {
                // Slow forward motion to allow pivoting
                rb.velocity = Vector3.Lerp(rb.velocity, new Vector3(0f, rb.velocity.y, 0f), Time.fixedDeltaTime * 3f);
            }
        }
        else
        {
            // Within stopping distance: brake and stop rotation
            rb.velocity = Vector3.Lerp(rb.velocity, new Vector3(0f, rb.velocity.y, 0f), Time.fixedDeltaTime * 10f);
            rb.angularVelocity = Vector3.zero;
        }
    }

    // TelemetryTicker: periodically log basic chase status for debugging
    private System.Collections.IEnumerator TelemetryTicker()
    {
        while (true)
        {
            if (!isManualControl && target != null && pathfinder != null)
            {
                float distance = Vector3.Distance(transform.position, target.position);
                if (distance > stoppingDistance)
                {
                    Debug.Log($"[Telemetry] Chasing {target.name} | Distance: {distance:F2}m | ValidPath: {pathfinder.hasValidPath} | Speed: {rb.velocity.magnitude:F2}");
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

#if UNITY_EDITOR
    // Draw wheel outlines in the editor for debugging and alignment
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(0.1f, 0.8f, 0.1f);

        if (leftWheelMesh != null)
        {
            UnityEditor.Handles.DrawWireDisc(leftWheelMesh.transform.position, leftWheelMesh.transform.right, wheelRadius);
        }

        if (rightWheelMesh != null)
        {
            UnityEditor.Handles.DrawWireDisc(rightWheelMesh.transform.position, rightWheelMesh.transform.right, wheelRadius);
        }
    }
#endif
}