using UnityEngine;
using UnityEngine.AI;

// TriggerSpiralMover.cs
// Moves the attached object in an outward spiral/circle pattern used as a navigation waypoint.
[RequireComponent(typeof(SphereCollider))]
public class SimpleCircleSweep : MonoBehaviour
{
    [Header("References")]
    public Transform bot;

    [Header("Sweep Settings")]
    public float startRadius = 2.0f;
    public float radiusStep = 2.0f; // How much wider the circle gets after a full loop
    public float angleStep = 45f;   // How far the carrot jumps around the circle
    public float maxRadius = 30f;

    private Vector3 centerPoint;
    private float currentAngle = 0f;
    private float currentRadius;

    void Start()
    {
        // Ensure this object acts as a trigger and initialize center
        GetComponent<SphereCollider>().isTrigger = true;
        if (bot != null) centerPoint = bot.position;
        currentRadius = startRadius;
        MoveCarrot();
    }

    void OnTriggerEnter(Collider other)
    {
        // 2. If the bot reaches the carrot, move it to the next spot
        if (other.transform == bot || other.transform.IsChildOf(bot))
        {
            MoveCarrot();
        }
    }

    void MoveCarrot()
    {
        if (currentRadius > maxRadius)
        {
            Debug.Log("Search complete: maximum radius exceeded.");
            return;
        }

        currentAngle += angleStep;

        // 3. If we complete a full circle, reset the angle and widen the radius
        if (currentAngle >= 360f)
        {
            currentAngle -= 360f; 
            currentRadius += radiusStep; 
            
            if (currentRadius > maxRadius) return; // Stop if the new radius is too big
        }

        // Calculate the perfect math circle coordinates
        float rad = currentAngle * Mathf.Deg2Rad;
        float targetX = centerPoint.x + (currentRadius * Mathf.Cos(rad));
        float targetZ = centerPoint.z + (currentRadius * Mathf.Sin(rad));

        Vector3 rawPos = new Vector3(targetX, centerPoint.y, targetZ);

        // Snap the carrot to the ground so the bot can actually reach it 
        // (Prevents the carrot from floating in the air or burying underground)
        if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
        else
        {
            // Fallback just in case there is no NavMesh at that exact spot
            transform.position = rawPos; 
        }
    }

    // Draw editor gizmos for visualization of the sweep
    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            // Draw the fixed center point
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(centerPoint, 0.5f);

            // Draw the current ring the carrot is moving along
            Gizmos.color = Color.yellow;
            int segments = 36;
            float step = 360f / segments;
            Vector3 lastPoint = centerPoint + new Vector3(currentRadius, 0, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float rad = (i * step) * Mathf.Deg2Rad;
                Vector3 nextPoint = centerPoint + new Vector3(Mathf.Cos(rad) * currentRadius, 0, Mathf.Sin(rad) * currentRadius);
                Gizmos.DrawLine(lastPoint, nextPoint);
                lastPoint = nextPoint;
            }
        }
    }
}