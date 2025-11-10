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

        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = currentIndex + 1;

        // Validate index
        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"No next scene exists! Current index: {currentIndex}");
            return;
        }

        // Convert index to scene name (required by NGO)
        string nextScenePath = SceneUtility.GetScenePathByBuildIndex(nextIndex);
        string nextSceneName = System.IO.Path.GetFileNameWithoutExtension(nextScenePath);

        Debug.Log($"Host loading next scene: {nextSceneName}");

        NetworkManager.Singleton.SceneManager.LoadScene(
            nextSceneName,
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