using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    // Controls the active state of a TargetObject based on the state of multiple buttons.
    public class ButtonActivator : NetworkBehaviour
    {
        // Public NetworkVariable to sync the overall button status to all clients
        public NetworkVariable<bool> AllButtonsPressed = new NetworkVariable<bool>(false);

        [Header("Target Object to Control")]
        [Tooltip("Drag the GameObject that should be enabled/disabled here. If it has a NetworkObject, Despawn() will be used to disable it.")]
        public GameObject TargetObject;

        [Header("Required Buttons")]
        [Tooltip("List of all required button scripts (of type Button, BlueButton, etc.).")]
        public List<NetworkBehaviour> RequiredButtons = new List<NetworkBehaviour>();

        [Header("Settings")]
        [Tooltip("If true, the Target Object will be disabled on Start if no buttons are pressed.")]
        [SerializeField] private bool startDisabled = false;

        private NetworkObject m_TargetNetworkObject;

        private void Awake()
        {
            if (TargetObject != null)
            {
                // Cache the NetworkObject reference if it exists.
                m_TargetNetworkObject = TargetObject.GetComponent<NetworkObject>();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 1. Subscribe to the change event for the TargetObject's overall status.
            //    This is how clients and the server react to the final synchronized state.
            AllButtonsPressed.OnValueChanged += OnAllButtonsPressedChanged;

            if (IsServer)
            {
                // 2. On the SERVER, subscribe to the change event for *EACH* required button.
                //    When a single button changes, it triggers the check for ALL buttons.
                foreach (var buttonScript in RequiredButtons)
                {
                    // Use a helper method to safely cast and subscribe
                    SubscribeToButton(buttonScript);
                }

                // 3. Perform an initial check on the server to set the starting state
                //    (This ensures the first value of AllButtonsPressed is correct).
                CheckAllButtonsStatus(false, false);
            }

            // Apply the initial state based on the NetworkVariable value and the 'startDisabled' setting.
            // On clients, this will use the value synchronized during spawn.
            if (IsClient)
            {
                // Call the reaction logic immediately with the current state.
                OnAllButtonsPressedChanged(AllButtonsPressed.Value, AllButtonsPressed.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // 1. Unsubscribe from the overall status
            AllButtonsPressed.OnValueChanged -= OnAllButtonsPressedChanged;

            // 2. Unsubscribe from individual buttons (only necessary if subscribed, which is on the server)
            if (IsServer)
            {
                foreach (var buttonScript in RequiredButtons)
                {
                    UnsubscribeFromButton(buttonScript);
                }
            }
        }

        // --- Subscription Helpers ---

        private void SubscribeToButton(NetworkBehaviour buttonScript)
        {
            // We subscribe to the individual button's IsPressed NetworkVariable.
            // The handler (CheckAllButtonsStatus) will run on the server when the button's state changes.
            if (buttonScript is Button originalButton)
            {
                originalButton.IsPressed.OnValueChanged += CheckAllButtonsStatus;
            }
            // Add logic for other button types here (assuming they exist in your project)
            else if (buttonScript is BlueButton blueButton)
            {
                blueButton.IsPressed.OnValueChanged += CheckAllButtonsStatus;
            }
            else if (buttonScript is RedButton redButton)
            {
                redButton.IsPressed.OnValueChanged += CheckAllButtonsStatus;
            }
            else if (buttonScript is GreenButton greenButton)
            {
                greenButton.IsPressed.OnValueChanged += CheckAllButtonsStatus;
            }
        }

        private void UnsubscribeFromButton(NetworkBehaviour buttonScript)
        {
            if (buttonScript is Button originalButton)
            {
                originalButton.IsPressed.OnValueChanged -= CheckAllButtonsStatus;
            }
            else if (buttonScript is BlueButton blueButton)
            {
                blueButton.IsPressed.OnValueChanged -= CheckAllButtonsStatus;
            }
            else if (buttonScript is RedButton redButton)
            {
                redButton.IsPressed.OnValueChanged -= CheckAllButtonsStatus;
            }
            else if (buttonScript is GreenButton greenButton)
            {
                greenButton.IsPressed.OnValueChanged -= CheckAllButtonsStatus;
            }
        }

        // --- Core Logic (Server-Only) ---

        // The central logic that runs whenever ANY button's state changes.
        // This only runs on the SERVER and updates the shared NetworkVariable.
        private void CheckAllButtonsStatus(bool oldValue, bool newValue)
        {
            if (!IsServer || TargetObject == null)
            {
                return;
            }

            // 1. Check if ALL buttons are currently pressed.
            bool allPressed = true;
            foreach (var buttonScript in RequiredButtons)
            {
                bool isButtonPressed = false;

                // Safely cast and get the current value of IsPressed from the button's NetworkVariable.
                if (buttonScript is Button originalButton)
                {
                    isButtonPressed = originalButton.IsPressed.Value;
                }
                else if (buttonScript is BlueButton blueButton)
                {
                    isButtonPressed = blueButton.IsPressed.Value;
                }
                else if (buttonScript is RedButton redButton)
                {
                    isButtonPressed = redButton.IsPressed.Value;
                }
                else if (buttonScript is GreenButton greenButton)
                {
                    isButtonPressed = greenButton.IsPressed.Value;
                }

                if (!isButtonPressed)
                {
                    allPressed = false;
                    break;
                }
            }

            // MODIFIED: Update the NetworkVariable for all clients.
            // This will trigger OnAllButtonsPressedChanged() on the Server and all Clients.
            if (AllButtonsPressed.Value != allPressed)
            {
                AllButtonsPressed.Value = allPressed;
                Debug.Log($"Activator (Server): Updating AllButtonsPressed to: {allPressed}");
            }
        }

        // --- Target Object Reaction (Server & Clients) ---

        // This method is called when 'AllButtonsPressed' changes (on Server AND Clients).
        private void OnAllButtonsPressedChanged(bool oldValue, bool newValue)
        {
            if (TargetObject == null) return;

            // The object should be ACTIVE when NOT all buttons are pressed.
            bool shouldBeActive = !newValue;

            if (shouldBeActive)
            {
                // Enable logic
                if (!TargetObject.activeSelf)
                {
                    TargetObject.SetActive(true);
                    Debug.Log($"Activator: Button released. Target Object {TargetObject.name} is now Active: TRUE");

                    // Note: If the TargetObject was Despawned, you'd typically need to Spawn() it back 
                    // on the server here, but for simple activation, SetActive(true) is used.
                }
            }
            else // shouldBeActive is FALSE (AllButtonsPressed is TRUE)
            {
                // Disable/Despawn logic (Despawn is Server-only)
                if (m_TargetNetworkObject != null && m_TargetNetworkObject.IsSpawned)
                {
                    if (IsServer)
                    {
                        m_TargetNetworkObject.Despawn();
                        Debug.Log($"Activator (Server): All buttons pressed. NetworkObject {TargetObject.name} **Despawned**.");
                    }
                }
                else if (TargetObject.activeSelf)
                {
                    // Local deactivation for clients and non-NetworkObjects
                    TargetObject.SetActive(false);
                    Debug.Log($"Activator: All buttons pressed. Local Object {TargetObject.name} Disabled.");
                }
            }
        }
    }
}