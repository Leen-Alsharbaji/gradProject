using UnityEngine;

// SweepingTarget.cs
// Moves a trigger object across a floor mesh in a grid pattern, used as navigation waypoints.
[RequireComponent(typeof(SphereCollider))]
public class TriggerGridMover : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Bot that will trigger the cube to move.")]
    public Transform bot;
    [Tooltip("The MeshCollider attached to your floor plane.")]
    public MeshCollider floorCollider;

    [Header("Grid Settings")]
    [Tooltip("How far the cube steps down when reaching the end of a row.")]
    public float stepSize = 1.0f;
    [Tooltip("Space between the physical edge of the mesh and the target.")]
    public float padding = 1.0f;

    // Boundary variables
    private float minX, maxX, minZ, maxZ;
    
    // Waypoint State Tracking
    private enum MoveState { SweepingRow, SteppingDown }
    private MoveState nextMove = MoveState.SweepingRow;
    private int currentDirectionX = 1; // 1 = moving right to maxX, -1 = moving left to minX
    private bool sweepingComplete = false;

    void Start()
    {
        if (floorCollider == null)
        {
            Debug.LogError("TriggerGridMover: No Floor MeshCollider assigned!");
            return;
        }

        // 1. Get exact bounds directly from the Mesh Collider once.
        Bounds bounds = floorCollider.bounds;
        
        minX = bounds.min.x + padding;
        maxX = bounds.max.x - padding;
        minZ = bounds.min.z + padding;
        maxZ = bounds.max.z - padding;

        // 2. Snap to the starting top-left corner
        transform.position = new Vector3(minX, floorCollider.transform.position.y, maxZ);

        // Ensure this cube acts as a trigger
        GetComponent<SphereCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (sweepingComplete) return;
        // If the bot triggered this waypoint, advance the sweep
        if (other.transform == bot || other.transform.IsChildOf(bot)) MoveToNextWaypoint();
    }

    void MoveToNextWaypoint()
    {
        Vector3 nextPosition = transform.position;

        if (nextMove == MoveState.SweepingRow)
        {
            // Send the cube all the way to the far edge of the current row
            nextPosition.x = (currentDirectionX == 1) ? maxX : minX;
            
            // The next time the bot hits us, we need to step down
            nextMove = MoveState.SteppingDown; 
        }
        else if (nextMove == MoveState.SteppingDown)
        {
            // Move the cube down one row
            nextPosition.z -= stepSize;
            
            // Flip the direction so the next sweep goes the opposite way
            currentDirectionX *= -1; 
            
            // The next time the bot hits us, we need to sweep the row
            nextMove = MoveState.SweepingRow;
        }

        // Final check to see if we reached the bottom edge of the floor
        if (nextPosition.z < minZ)
        {
            nextPosition.z = minZ; // Clamp to boundary
            sweepingComplete = true;
            Debug.Log("Sweeping complete: area covered.");
        }

        // Instantly teleport the cube to the new waypoint
        transform.position = nextPosition;
    }

    // Optimized Gizmo: Only draws the boundary box.
    void OnDrawGizmosSelected()
    {
        if (floorCollider == null) return;

        Gizmos.color = Color.green;
        Bounds b = floorCollider.bounds;
        
        Vector3 center = new Vector3(b.center.x, floorCollider.transform.position.y + 0.1f, b.center.z);
        Vector3 size = new Vector3(b.size.x - (padding * 2), 0.05f, b.size.z - (padding * 2));
        
        Gizmos.DrawWireCube(center, size);
    }
}