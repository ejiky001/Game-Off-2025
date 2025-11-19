using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    // Controls the active state of a TargetObject based on the state of multiple buttons.
    public class ButtonActivator : NetworkBehaviour
    {
        //  ADDED: Public NetworkVariable to sync the button status to all clients
        [HideInInspector] // Hide from Inspector to avoid accidental change
        public NetworkVariable<bool> AllButtonsPressed = new NetworkVariable<bool>(false);

        [Header("Target Object to Control")]
        [Tooltip("Drag the GameObject that should be enabled/disabled here. If it has a NetworkObject, Despawn() will be used to disable it.")]
        public GameObject TargetObject;

        [Header("Required Buttons")]
        // ... (RequiredButtons list remains the same) ...
        public List<NetworkBehaviour> RequiredButtons = new List<NetworkBehaviour>();

        [Header("Settings")]
        // ... (startDisabled field remains the same) ...
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

        // ... (Start(), OnNetworkSpawn(), OnNetworkDespawn() remain the same) ...
        // Note: The logic for these methods is omitted for brevity but should be the same as your input.

        // The central logic that runs whenever ANY button's state changes.
        private void CheckAllButtonsStatus(bool oldValue, bool newValue)
        {
            if (!IsServer || TargetObject == null)
            {
                // On clients, we only want to update our local state based on the NetworkVariable,
                // but since the current setup only runs activation logic on the server, 
                // we'll keep the client return here, assuming clients only read the NetworkVariable.
                return;
            }

            // 1. Check if ALL buttons are currently pressed.
            bool allPressed = true;
            foreach (var buttonScript in RequiredButtons)
            {
                bool isButtonPressed = false;

                // ... (Safely cast and get the current value of IsPressed remains the same) ...
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

            //  MODIFIED: Update the NetworkVariable for all clients
            if (AllButtonsPressed.Value != allPressed)
            {
                AllButtonsPressed.Value = allPressed;
            }

            // 2. Determine the desired active state for the TargetObject.
            bool shouldBeActive = !allPressed;

            // 3. Apply the change using the appropriate method (Networked vs. Local).
            if (shouldBeActive)
            {
                // ... (Enable logic remains the same) ...
                if (!TargetObject.activeSelf)
                {
                    TargetObject.SetActive(true);
                    Debug.Log($"Activator: Button released. Target Object {TargetObject.name} is now Active: TRUE");
                }
            }
            else // shouldBeActive is FALSE (all buttons are pressed)
            {
                // ... (Disable/Despawn logic remains the same) ...
                if (m_TargetNetworkObject != null && m_TargetNetworkObject.IsSpawned)
                {
                    m_TargetNetworkObject.Despawn();
                    Debug.Log($"Activator: All buttons pressed. NetworkObject {TargetObject.name} **Despawned**.");
                }
                else if (TargetObject.activeSelf)
                {
                    TargetObject.SetActive(false);
                    Debug.Log($"Activator: All buttons pressed. Local Object {TargetObject.name} Disabled.");
                }
            }
        }
    }
}