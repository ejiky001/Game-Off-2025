using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode; 



[RequireComponent(typeof(NavMeshAgent))]
public class EnemyFollow : NetworkBehaviour
{
    // The NavMeshAgent component controls the enemy's pathfinding and movement.
    private NavMeshAgent agent;

    [Tooltip("The tag of the player GameObject to follow (usually 'Player').")]
    public string playerTag = "Player";

    [Tooltip("The speed at which the enemy moves.")]
    public float movementSpeed = 3.5f;

    // This reference holds the detected player's Transform.
    private Transform playerTarget;

    // NetworkVariable synchronizes the following state from Server to all Clients.
    // Use the default value (false) and read permissions for everyone.
    private readonly NetworkVariable<bool> isFollowing = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // --- Initialization ---
    // Note: Use Awake/Start/OnNetworkSpawn, not just Start, for networked components
    public override void OnNetworkSpawn()
    {
        // Only the Server should manage the NavMeshAgent components for AI movement
        if (IsServer)
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = movementSpeed;
            agent.isStopped = true;
        }

        // Subscribe to the network variable change event. 
        // This is mainly for debugging or if you wanted a client-side visual change.
        isFollowing.OnValueChanged += OnFollowingStateChanged;

        // Find the player initially if possible (though trigger is better for detection)
        // Note: For a real NGO game, players spawn late. Finding them dynamically is safer.
        // We'll rely on the trigger for finding the target.
    }

    // Unsubscribe when the object is destroyed
    public override void OnNetworkDespawn()
    {
        isFollowing.OnValueChanged -= OnFollowingStateChanged;
    }

    // Called on all clients and the server when the state changes.
    private void OnFollowingStateChanged(bool previousValue, bool newValue)
    {
        Debug.Log($"Enemy state changed. Following: {newValue}");
        // Note: We don't need to manually stop/start the agent here for movement 
        // because the NetworkTransform component is typically handling client-side sync.
    }

    // --- Main Update Loop ---
    void Update()
    {
        // CRITICAL: AI logic MUST only run on the server.
        if (!IsServer)
        {
            // Clients do not calculate paths or set destinations.
            // Their movement is synchronized by the NetworkTransform component.
            return;
        }

        // Server-side movement logic
        if (isFollowing.Value && playerTarget != null)
        {
            // Server sets the player's current position as the destination.
            agent.SetDestination(playerTarget.position);

            if (agent.isStopped)
            {
                agent.isStopped = false;
            }
        }
        else if (agent.isStopped == false)
        {
            // If we lose the target and the agent is still moving, stop it.
            agent.isStopped = true;
        }
    }

    // --- Trigger Detection (Player Enters Range) ---
    private void OnTriggerEnter(Collider other)
    {
        // CRITICAL: Only the Server should handle AI state changes and pursuit logic.
        if (!IsServer) return;

        // Check if the collider belongs to the player object.
        if (other.CompareTag(playerTag))
        {
            Debug.Log("Server: Player entered detection range. Starting pursuit.");
            playerTarget = other.transform;

            // Update the NetworkVariable, which synchronizes to all clients.
            isFollowing.Value = true;

            // Start the NavMeshAgent's movement on the server.
            if (agent != null)
            {
                agent.isStopped = false;
                agent.SetDestination(playerTarget.position);
            }
        }
    }

    // --- Trigger Exit (Player Leaves Range) ---
    private void OnTriggerExit(Collider other)
    {
        // CRITICAL: Only the Server should handle AI state changes and pursuit logic.
        if (!IsServer) return;

        // Check if the collider belongs to the player object.
        if (other.CompareTag(playerTag))
        {
            Debug.Log("Server: Player left detection range. Stopping pursuit.");

            // Update the NetworkVariable, which synchronizes to all clients.
            isFollowing.Value = false;
            playerTarget = null;

            // Stop the NavMeshAgent's movement on the server.
            if (agent != null)
            {
                agent.isStopped = true;
            }
        }
    }
}