using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq; // Required for using the .All() extension method

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    // This script should be placed on the GameObject you want to disable 
    // when all required buttons are pressed.
    public class RequiredButtonsActivator : NetworkBehaviour
    {
        [Header("Required Buttons")]
        [Tooltip("Drag all required Button scripts here.")]
        // The list of buttons that must all be pressed to disable this object.
        public List<Button> RequiredButtons = new List<Button>();

        [Header("Settings")]
        [Tooltip("If checked, the object will start disabled.")]
        [SerializeField] private bool startDisabled = false;

        private void Start()
        {
            // Optional: Start the GameObject disabled.
            if (startDisabled)
            {
                gameObject.SetActive(false);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                // Only the server needs to monitor the button states and disable the object.
                // Clients will simply observe the disabled state via network synchronization.
                return;
            }

            // Register for the value change event on all required buttons.
            // This is more efficient than checking in Update().
            foreach (var button in RequiredButtons)
            {
                // Subscribe to the NetworkVariable's ValueChanged event.
                // We'll use the same function to handle any button's state change.
                button.IsPressed.OnValueChanged += CheckAllButtonsStatus;
            }

            // Perform an initial check in case buttons are already pressed on spawn
            CheckAllButtonsStatus(false, false);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                return;
            }

            // Unsubscribe to prevent memory leaks when the object is destroyed.
            foreach (var button in RequiredButtons)
            {
                // Safely unsubscribe only if the button is not null (hasn't been destroyed first).
                if (button != null)
                {
                    button.IsPressed.OnValueChanged -= CheckAllButtonsStatus;
                }
            }
        }

        // The callback function for when any button's IsPressed state changes.
        // The arguments (oldValue, newValue) are required for the OnValueChanged delegate 
        // but we don't need to use them here.
        private void CheckAllButtonsStatus(bool oldValue, bool newValue)
        {
            // This logic MUST only run on the server.
            if (!IsServer)
            {
                return;
            }

            // Check if ALL buttons in the list are currently pressed (IsPressed.Value == true).
            // The .All() extension method from System.Linq is a very clean way to do this.
            bool allPressed = RequiredButtons.All(button => button.IsPressed.Value);

            // The target state is disabled (false) if all buttons are pressed.
            bool targetActiveState = !allPressed;

            // Only change the state if it's different from the current state.
            if (gameObject.activeSelf != targetActiveState)
            {
                Debug.Log($"All required buttons are {(allPressed ? "PRESSED" : "NOT PRESSED")}. Disabling object: {!allPressed}");

                // Set the active state. This change is automatically synchronized by Netcode 
                // for the root GameObject of a NetworkObject.
                gameObject.SetActive(targetActiveState);
            }
        }
    }
}