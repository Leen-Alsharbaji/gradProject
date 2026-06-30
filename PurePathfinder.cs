using UnityEngine;
using UnityEngine.AI;

// PurePathfinder.cs
// Wrapper around Unity NavMeshPath to generate and expose simple waypoint lists.
public class PurePathfinder : MonoBehaviour
{
    [Header("Exported Path Data (Read-Only)")]
    public Vector3[] pathWaypoints;
    public Vector3 nextWaypoint;
    public bool hasValidPath;

    [Header("Navigation Settings")]
    public float waypointTolerance = 1.0f; // Distance threshold to advance to next waypoint

    private NavMeshPath navMeshPath;
    private int currentWaypointIndex = 0;

    // Initialize the NavMeshPath instance
    void Start()
    {
        navMeshPath = new NavMeshPath();
    }

    // Update waypoint progression based on proximity
    void Update()
    {
        if (hasValidPath && pathWaypoints != null && currentWaypointIndex < pathWaypoints.Length)
        {
            Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatWaypoint = new Vector3(nextWaypoint.x, 0f, nextWaypoint.z);

            if (Vector3.Distance(flatPos, flatWaypoint) < waypointTolerance)
            {
                if (currentWaypointIndex < pathWaypoints.Length - 1)
                {
                    currentWaypointIndex++;
                    nextWaypoint = pathWaypoints[currentWaypointIndex];
                }
            }
        }
    }

    // Generate a NavMesh path and convert it to an array of waypoints
    public void GeneratePath(Vector3 targetPosition)
    {
        hasValidPath = NavMesh.CalculatePath(transform.position, targetPosition, NavMesh.AllAreas, navMeshPath);

        if (hasValidPath && navMeshPath.corners.Length > 0)
        {
            pathWaypoints = new Vector3[navMeshPath.corners.Length];

            for (int i = 0; i < navMeshPath.corners.Length; i++)
            {
                Vector3 originalCorner = navMeshPath.corners[i];
                originalCorner.y = transform.position.y; // Keep Y flat
                pathWaypoints[i] = originalCorner;
            }

            currentWaypointIndex = (pathWaypoints.Length > 1) ? 1 : 0;
            nextWaypoint = pathWaypoints[currentWaypointIndex];
        }
        else
        {
            pathWaypoints = new Vector3[0];
            nextWaypoint = transform.position;
        }
    }

    // Draw the computed path in the editor for debugging
    private void OnDrawGizmos()
    {
        if (pathWaypoints != null && pathWaypoints.Length > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < pathWaypoints.Length - 1; i++)
            {
                Gizmos.DrawLine(pathWaypoints[i], pathWaypoints[i + 1]);
                Gizmos.DrawSphere(pathWaypoints[i], 0.2f);
            }
            Gizmos.DrawSphere(pathWaypoints[pathWaypoints.Length - 1], 0.2f);
        }
    }
}