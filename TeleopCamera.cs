using UnityEngine;

// TeleopCamera.cs
// Smoothly follows and orients the camera relative to a robot target.
public class TeleopCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform robot; // Drag your robot here in the inspector

    [Header("Camera Feel")]
    public Vector3 offset = new Vector3(0, 2.5f, -4f); // Height and distance behind robot
    public float followSpeed = 5f;
    public float rotationSpeed = 5f;

    void LateUpdate()
    {
        if (robot == null) return;
        // Calculate where the camera should be (behind and above the robot)
        Vector3 targetPosition = robot.position + robot.TransformDirection(offset);

        // Smoothly glide to that position
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);

        // Keep looking at the robot smoothly
        Vector3 lookTarget = robot.position + (Vector3.up * 1.0f); // Look slightly above the robot's base
        Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }
}