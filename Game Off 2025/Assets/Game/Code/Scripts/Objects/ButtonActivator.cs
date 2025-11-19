using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    // This script should be placed on a control GameObject (e.g., an 'Activator') 
    // to manage the enabled/disabled state of a TARGET object.
    public class ButtonActivator : NetworkBehaviour
    {
        [Header("Target Object")]
        [Tooltip("Drag the GameObject that should be enabled/disabled here.")]
        // *** ADDED THIS LINE ***
        public GameObject TargetObject;

        [Header("Required Buttons")]
        [Tooltip("Drag all required Button scripts (including color-specific ones) here.")]
        // We use NetworkBehaviour as the base type to hold all different derived button types.
        public List<NetworkBehaviour> RequiredButtons = new List<NetworkBehaviour>();

        [Header("Settings")]
        [Tooltip("If checked, the target object will start disabled.")]
        [SerializeField] private bool startDisabled = false;

        private void Start()
        {
            // Optional: Start the target GameObject disabled.
            if (startDisabled && TargetObject != null) // *** MODIFIED: Check TargetObject
            {
                TargetObject.SetActive(false); // *** MODIFIED: Use TargetObject
            }
        }

        public override void OnNetworkSpawn()
        {
            if (TargetObject == null) // *** ADDED: Safety check for the target
            {
                Debug.LogError($"TargetObject is not assigned on {gameObject.name}. Cannot manage active state.");
                return;
            }

            if (!IsServer)
            {
                // Only the server monitors the button states and controls the object's active state.
                return;
            }

            // 1. Register for the value change event on all required buttons.
            foreach (var buttonScript in RequiredButtons)
            {
                // ... (Subscription logic remains the same) ...
                if (buttonScript is Button originalButton)
                {
                    originalButton.IsPressed.OnValueChanged += CheckAllButtonsStatus;
                }
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
                else
                {
                    Debug.LogError($"Activator on {gameObject.name} contains an unhandled script type: {buttonScript.GetType().Name}. Please ensure all button scripts expose 'public NetworkVariable<bool> IsPressed'.");
                }
            }

            // 2. Perform an initial check in case buttons are already pressed on spawn
            CheckAllButtonsStatus(false, false);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                return;
            }

            // ... (Unsubscription logic remains the same) ...
            foreach (var buttonScript in RequiredButtons)
            {
                if (buttonScript == null) continue;

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
        }

        // The central logic that runs whenever ANY button's state changes.
        private void CheckAllButtonsStatus(bool oldValue, bool newValue)
        {
            if (!IsServer || TargetObject == null) // *** MODIFIED: Check TargetObject again
            {
                return;
            }

            // Check if ALL buttons in the list are currently pressed.
            bool allPressed = true;
            foreach (var buttonScript in RequiredButtons)
            {
                bool isButtonPressed = false;

                // Safely cast and get the current value of IsPressed for the specific type.
                // ... (Button state check logic remains the same) ...
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
                    break; // Found one unpressed button, no need to check the rest.
                }
            }

            // We disable the object if all buttons are pressed (targetActiveState = false).
            // We enable the object if at least one button is unpressed (targetActiveState = true).
            bool targetActiveState = !allPressed;

            // Apply the change only if necessary.
            // *** MODIFIED: Use TargetObject
            if (TargetObject.activeSelf != targetActiveState)
            {
                // *** MODIFIED: Use TargetObject
                TargetObject.SetActive(targetActiveState);
                Debug.Log($"Activator: All buttons pressed? {allPressed}. Target Object is now Active: {targetActiveState}");
            }
        }
    }
}