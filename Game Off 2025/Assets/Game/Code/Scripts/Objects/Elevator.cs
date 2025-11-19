using UnityEngine;
using Unity.Netcode;

/// <summary>
/// A server-authoritative networked platform that constantly moves 
/// back and forth (ping-pongs) between two defined points.
/// </summary>
public class ElevatorPlatform : NetworkBehaviour
{
    [Header("Movement Setup")]
    [Tooltip("The starting point (empty GameObject transform)")]
    [SerializeField] private Transform pointA;
    [Tooltip("The ending point (empty GameObject transform)")]
    [SerializeField] private Transform pointB;
    [Tooltip("Speed of the elevator movement.")]
    [SerializeField] private float speed = 1.0f;

    // Used to track the time for the smooth PingPong movement
    private float startTime;
    private float journeyLength;
    private Vector3 startPosition;

    /// <summary>
    /// Called when the NetworkObject is spawned on the network.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // Only the server should control the movement logic and set up the starting state.
        if (!IsServer)
        {
            enabled = false; // Disable the script on clients.
            return;
        }

        // Check if points are assigned.
        if (pointA == null || pointB == null)
        {
            Debug.LogError("ElevatorPlatform requires both Point A and Point B Transforms to be assigned.");
            return;
        }

        // Initialize the journey tracking variables on the server.
        startPosition = transform.position;
        startTime = Time.time;
        journeyLength = Vector3.Distance(pointA.position, pointB.position);
    }

    /// <summary>
    /// FixedUpdate is used for physics and consistent movement updates. 
    /// This only runs on the server.
    /// </summary>
    private void FixedUpdate()
    {
        // This check is the primary safety measure to ensure only the server drives the movement.
        if (!IsServer)
        {
            return;
        }

        // Calculate the time elapsed since starting
        float timeElapsed = Time.time - startTime;

        // Calculate a value 't' that smoothly oscillates between 0 and 1.
        // Mathf.PingPong(time, length) repeats the time value back and forth between 0 and length.
        // We normalize it by the journey length to control speed relative to distance.
        float journeyTimeScale = journeyLength / speed;
        float t = Mathf.PingPong(timeElapsed / journeyTimeScale, 1.0f);

        // Perform the smooth movement (Linear Interpolation)
        Vector3 newPosition = Vector3.Lerp(pointA.position, pointB.position, t);

        // Assign the new position. The NetworkTransform component (if attached) 
        // will automatically synchronize this position to all clients.
        transform.position = newPosition;
    }

    /*
     * QUICK SETUP GUIDE:
     * * 1. CREATE SCENE OBJECTS:
     * - Create the visual platform GameObject (e.g., a Cube).
     * - Add a Collider component to the platform.
     * - Add a NetworkObject component to the platform.
     * - Add a NetworkTransform component to the platform (this is vital for syncing position).
     * - Add this 'ElevatorPlatform' script to the platform.
     * - Create two empty GameObjects, name them "ElevatorPointA" and "ElevatorPointB", and place them 
     * at the start and end of the desired path.
     * * 2. ASSIGN REFERENCES:
     * - Drag "ElevatorPointA" and "ElevatorPointB" into the corresponding slots 
     * in the Inspector of the 'ElevatorPlatform' script.
     * * 3. PLAYER PARENTING (IMPORTANT):
     * - For players to stick to the moving platform, you typically need logic in the 
     * Player Controller script to parent the player's transform to the platform's 
     * transform upon collision, and un-parent upon leaving. This is essential for moving platforms.
     */
    //delete this comment
}