// PlayerLook.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode; // We need this to check IsOwner
using Unity.Multiplayer.Center.NetcodeForGameObjects;

public class PlayerLook : MonoBehaviour
{
    // References
    private Player m_Player; // Reference to the main networked Player script
    private Transform m_CameraTransform;
    private InputAction m_LookAction;

    // Look Parameters
    [SerializeField] private float mouseSensitivity = 0.25f;
    private float verticalRotation = 0f; // Stores the current pitch (up/down)

    private void Awake()
    {
        m_Player = GetComponent<Player>();

        // Find the camera transform based on the Player script's reference
        if (m_Player.playerCamera != null)
        {
            m_CameraTransform = m_Player.playerCamera.transform;
        }
        else
        {
            Debug.LogError("Player Camera reference is missing on the Player script!");
        }
    }

    public void Initialize(InputAction lookAction)
    {
        // This is called from Player.cs to pass the input action
        m_LookAction = lookAction;
    }

    private void Update()
    {
        // ONLY the local owner should handle look input
        if (m_Player == null || !m_Player.IsOwner || m_LookAction == null)
        {
            return;
        }

        Vector2 lookInput = m_LookAction.ReadValue<Vector2>();

        // 1. Vertical (Pitch) Rotation - Camera Only
        // This gives instant, client-only feedback.
        HandleVerticalRotation(lookInput.y);

        // 2. Horizontal (Yaw) Rotation - Player Body & Prediction
        // We apply the rotation instantly on the client for prediction, 
        // and then send the result (the current full rotation state) to the server.
        HandleHorizontalRotation(lookInput.x);
    }

    private void HandleVerticalRotation(float yInput)
    {
        verticalRotation -= yInput * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

        if (m_CameraTransform != null)
        {
            m_CameraTransform.localEulerAngles = new Vector3(verticalRotation, 0f, 0f);
        }
    }

    // --- PlayerLook.cs (UPDATED) ---

    private void HandleHorizontalRotation(float xInput)
    {
        float horizontalDelta = xInput * mouseSensitivity;

        if (Mathf.Abs(horizontalDelta) > 0.0001f)
        {
            // 1. Accumulate the delta locally for client prediction (in Player.FixedUpdate)
            m_Player.AccumulateRotationDelta(horizontalDelta);

            // 2. Send the original delta to the server for authoritative rotation
            m_Player.RequestRotationDeltaServerRpc(horizontalDelta);
        }
    }
}