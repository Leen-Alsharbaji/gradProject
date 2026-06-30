using UnityEngine;

// BotBrain.cs
// High-level state machine coordinating sensors and locomotion for the robot.
public class BotBrain : MonoBehaviour
{
    public enum BotState { Patrolling, InvestigatingAudio, SearchingArea, ExaminingVictim, ManualControl }
    
    [Header("Current State")]
    public BotState currentState = BotState.Patrolling;

    [Header("Bot Components")]
    public StableTwoWheelDrive driver;
    public VictimDetection eyes; 
    
    [Header("The Carrots (Targets)")]
    public Transform patrolCarrot;
    public Transform audioCarrot; 
    public Transform visualCarrot; 

    [Header("Investigation Settings")]
    public float searchTime = 8f; // Duration to scan an audio source
    private float searchTimer = 0f;
    private bool hasCalledForHelp = false;

    // Initialize references and subscribe to vision events
    void Start()
    {
        if (driver == null) driver = GetComponent<StableTwoWheelDrive>();
        if (eyes != null) eyes.OnVictimSpotted += HandleVictimSpotted;

        SetState(BotState.Patrolling);
    }

    void OnDestroy()
    {
        if (eyes != null) eyes.OnVictimSpotted -= HandleVictimSpotted;
    }

    // Main state update loop
    void Update()
    {
        if (currentState == BotState.InvestigatingAudio)
        {
            float distanceToSound = Vector3.Distance(transform.position, audioCarrot.position);

            if (distanceToSound <= driver.stoppingDistance + 1.0f)
            {
                Debug.Log("Bot arrived at audio source; beginning area scan.");
                SetState(BotState.SearchingArea);
            }
        }
        else if (currentState == BotState.SearchingArea)
        {
            // Rotate to scan the surrounding area
            transform.Rotate(0, 30f * Time.deltaTime, 0); 

            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0)
            {
                Debug.Log("Area scan complete; resuming patrol.");
                SetState(BotState.Patrolling);
            }
        }
        else if (currentState == BotState.ExaminingVictim)
        {
            float distanceToVictim = Vector3.Distance(transform.position, visualCarrot.position);
            
            if (distanceToVictim <= driver.stoppingDistance + 0.5f && !hasCalledForHelp)
            {
                CallForHelp();
            }

            // Operator: press 'T' to take manual control
            if (Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log("Operator requested manual control.");
                SetState(BotState.ManualControl);
            }
        }
        else if (currentState == BotState.ManualControl)
        {
            // Operator: press 'P' to return to autonomous patrol
            if (Input.GetKeyDown(KeyCode.P))
            {
                Debug.Log("Operator released manual control; resuming patrol.");
                SetState(BotState.Patrolling);
            }
        }
    }

    // Handle vision-detected victims; only act on horizontal/unconscious detections
    private void HandleVictimSpotted(Vector3 worldPosition, string victimStatus)
    {
        if (!victimStatus.Contains("Horizontal")) return;

        visualCarrot.position = worldPosition;

        if (currentState == BotState.InvestigatingAudio || currentState == BotState.SearchingArea)
        {
            Debug.Log("Vision override: victim located; examining.");
            SetState(BotState.ExaminingVictim);
        }
    }

    // Notify operator and stop movement
    private void CallForHelp()
    {
        hasCalledForHelp = true;
        Debug.LogWarning("Unconscious victim found: broadcasting coordinates to operator: " + visualCarrot.position);
        Debug.LogWarning("Manual teleoperation available. Press 'T' to engage.");
        driver.target = null; // Stop driving
    }

    // Update internal state and configure subsystems accordingly
    private void SetState(BotState newState)
    {
        currentState = newState;
        hasCalledForHelp = false;

        switch (newState)
        {
            case BotState.Patrolling:
                eyes.isVisionActive = false; // Sleep vision to save power
                driver.target = patrolCarrot;
                driver.isManualControl = false;
                break;

            case BotState.InvestigatingAudio:
                Debug.Log("Audio event received: activating vision and navigating to source.");
                eyes.isVisionActive = true; 
                driver.target = audioCarrot;
                driver.isManualControl = false;
                break;

            case BotState.SearchingArea:
                driver.target = null; // Stop moving forward, rotate in place
                searchTimer = searchTime;
                break;

            case BotState.ExaminingVictim:
                driver.target = visualCarrot; 
                break;

            case BotState.ManualControl:
                driver.target = null;
                driver.isManualControl = true; // Enable keyboard control
                Debug.LogWarning("Manual control active. Press 'P' to resume autonomous patrol.");
                break;
        }
    }

    // External audio sensor triggers this to request investigation
    public void OnHeardSound(Vector3 soundLocation)
    {
        if (currentState == BotState.ExaminingVictim || currentState == BotState.ManualControl) return;

        audioCarrot.position = soundLocation;
        SetState(BotState.InvestigatingAudio);
    }
}