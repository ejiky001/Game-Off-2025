using TMPro;
using UnityEngine;
using UnityEngine.UI; // Required for Button component

// This script will manage the lobby name display based on button interaction.
public class LobbyNameMenu : MonoBehaviour
{
    // --- Configuration in Inspector ---
    [Tooltip("Drag the TMP_InputField component used for the lobby name here.")]
    [SerializeField] private TMP_InputField m_LobbyInputField;

    [Tooltip("Drag the TMP_Text component that will display the lobby name here.")]
    [SerializeField] private TMP_Text m_LobbyNameDisplay;

    // --- ADDED: Reference for the button that triggers the update ---
    [Tooltip("Drag the Button component (e.g., the 'Create Session' button) here.")]
    [SerializeField] private Button m_UpdateButton;

    // --- Constants ---
    private const string NamePrefix = "Lobby name: ";

    void Awake()
    {
        // 1. Initial validation
        if (m_LobbyInputField == null || m_LobbyNameDisplay == null || m_UpdateButton == null)
        {
            Debug.LogError("Required components (Input, Display, or Button) are not assigned in the Inspector for LobbyMenuDisplayManager.");
        }

        // 2. Set initial text
        m_LobbyNameDisplay.text = NamePrefix + "";

        // 3. --- MODIFIED: Attach a listener to the button's onClick event ---
        m_UpdateButton.onClick.AddListener(OnUpdateButtonClicked);
    }

    private void OnUpdateButtonClicked()
    {
        // Get the final text from the input field
        string finalLobbyName = m_LobbyInputField.text;

        // Update the display TMP_Text with the prefix + the final lobby name
        m_LobbyNameDisplay.text = NamePrefix + finalLobbyName;

        // Optionally, you can reset the input field here if needed:
        // m_LobbyInputField.text = ""; 
    }

    // You can optionally clean up the listener when the object is destroyed
    private void OnDestroy()
    {
        if (m_UpdateButton != null)
        {
            m_UpdateButton.onClick.RemoveListener(OnUpdateButtonClicked);
        }
    }

    // NOTE: The previous OnInputFieldValueChanged and its listener are removed.
}