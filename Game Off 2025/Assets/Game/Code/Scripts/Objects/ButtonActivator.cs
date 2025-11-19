using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    // This script should be placed on the GameObject you want to disable 
    // when all required buttons are pressed.
    public class ButtonActivator : NetworkBehaviour
    {
        [Header("Required Buttons")]
        [Tooltip("Drag all required Button scripts (including color-specific ones) here.")]
        // We use NetworkBehaviour as the base type to hold all different derived button types.
        public List<NetworkBehaviour> RequiredButtons = new List<NetworkBehaviour>();

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
                // Only the server monitors the button states and controls the object's active state.
                return;
            }

            // 1. Register for the value change event on all required buttons.
            foreach (var buttonScript in RequiredButtons)
            {
                // Safely handle each button type and subscribe to its IsPressed NetworkVariable.
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

            // Unsubscribe to prevent memory leaks.
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
            if (!IsServer)
            {
                return;
            }

            // Check if ALL buttons in the list are currently pressed.
            bool allPressed = true;
            foreach (var buttonScript in RequiredButtons)
            {
                bool isButtonPressed = false;

                // Safely cast and get the current value of IsPressed for the specific type.
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

            // We disable the object if all buttons are pressed.
            bool targetActiveState = !allPressed;

            // Apply the change only if necessary.
            if (gameObject.activeSelf != targetActiveState)
            {
                gameObject.SetActive(targetActiveState);
                Debug.Log($"Activator: All buttons pressed? {allPressed}. Object is now Active: {targetActiveState}");
            }
        }
    }
}