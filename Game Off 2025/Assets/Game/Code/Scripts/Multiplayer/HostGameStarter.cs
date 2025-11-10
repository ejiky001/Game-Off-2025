using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
// FIX: Required to access the Player class definition (from Player.cs)
using Unity.Multiplayer.Center.NetcodeForGameObjects;

/// <summary>
/// Manages the "Start Game" button, making it visible only to the Host,
/// and triggering a network-synchronized scene load when clicked.
/// </summary>
public class HostGameStarter : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag the Button component the host will press to start the game. Start the button disabled in the Inspector.")]
    [SerializeField] private UnityEngine.UI.Button m_StartButton;

    [Header("Scene Configuration")]
    [Tooltip("The exact name of the scene to load (must be in your build settings).")]
    [SerializeField] private string m_GameSceneName = "GameScene";

    void Start()
    {
        // 1. Validate Netcode Singleton
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("HostGameStarter requires an active NetworkManager in the scene.");
            if (m_StartButton != null) m_StartButton.gameObject.SetActive(false);
            return;
        }

        // 2. Register for the OnServerStarted event. This ensures the button setup
        // only runs AFTER the NetworkManager has successfully started as a Host/Server.
        NetworkManager.Singleton.OnServerStarted += OnServerRoleConfirmed;

        // 3. Handle case where this script is added to a running Host
        if (NetworkManager.Singleton.IsHost)
        {
            OnServerRoleConfirmed();
        }

        // 4. Final checks
        if (string.IsNullOrEmpty(m_GameSceneName))
        {
            Debug.LogError("Game Scene Name is not set on HostGameStarter.");
        }
        if (m_StartButton == null)
        {
            Debug.LogError("Start Button reference is missing on HostGameStarter.");
        }
    }

    /// <summary>
    /// Event handler for NetworkManager.OnServerStarted. 
    /// Only the Host will execute this logic.
    /// </summary>
    private void OnServerRoleConfirmed()
    {
        if (m_StartButton != null && NetworkManager.Singleton.IsHost)
        {
            // Now we know we are the host, activate the button
            m_StartButton.gameObject.SetActive(true);

            // Attach Click Listener
            m_StartButton.onClick.RemoveAllListeners(); // Safety first
            m_StartButton.onClick.AddListener(StartGame);

            Debug.Log("Host button enabled and ready.");
        }
    }

    /// <summary>
    /// Executes when the Host clicks the button.
    /// Triggers a synchronized scene load across all connected clients using NGO's SceneManager.
    /// </summary>
    private void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("Only the host can initiate the scene change.");
            return;
        }

        Debug.Log($"Host starting game and loading scene: {m_GameSceneName}");

        // NGO automatically synchronizes the scene load to all connected clients (Clients and Server).
        // This relies on the scene name being correct and the scene being in Build Settings.
        NetworkManager.Singleton.SceneManager.LoadScene(
            m_GameSceneName,
            LoadSceneMode.Single
        );
    }

    private void OnDestroy()
    {
        // Clean up the event listener
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerRoleConfirmed;
        }

        // Remove the button click listener
        if (m_StartButton != null)
        {
            m_StartButton.onClick.RemoveListener(StartGame);
        }
    }
}