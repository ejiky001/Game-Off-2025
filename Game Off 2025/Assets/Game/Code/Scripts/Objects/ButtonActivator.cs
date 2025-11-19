using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    // Controls the active state of a TargetObject based on the state of multiple buttons.
    public class ButtonActivator : NetworkBehaviour
    {
        [Header("Target Object to Control")]
        [Tooltip("Drag the GameObject that should be enabled/disabled here. If it has a NetworkObject, Despawn() will be used to disable it.")]
        public GameObject TargetObject;

        [Header("Required Buttons")]
        [Tooltip("Drag all required Button scripts here.")]
        public List<NetworkBehaviour> RequiredButtons = new List<NetworkBehaviour>();

        [Header("Settings")]
        [Tooltip("If checked, the target object will start disabled.")]
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

        private void Start()
        {
            if (TargetObject == null)
            {
                Debug.LogError($"TargetObject is not assigned on {gameObject.name}. Cannot manage active state.");
                return;
            }

            // Optional: Start the target GameObject disabled.
            if (startDisabled)
            {
                TargetObject.SetActive(false);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (TargetObject == null) return;

            if (!IsServer)
            {
                // Only the server monitors button states and controls the object's active state.
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
                    Debug.LogError($"Activator on {gameObject.name} contains an unhandled script type: {buttonScript.GetType().Name}.");
                }
            }

            // 2. Perform an initial check in case buttons are already pressed on spawn
            // This is essential for new clients joining late.
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

                // Safely unsubscribe based on type (cleanup)
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
            if (!IsServer || TargetObject == null)
            {
                return;
            }

            // 1. Check if ALL buttons are currently pressed.
            bool allPressed = true;
            foreach (var buttonScript in RequiredButtons)
            {
                bool isButtonPressed = false;

                // Safely cast and get the current value of IsPressed.
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

            // 2. Determine the desired active state for the TargetObject.
            // Target should be **disabled** if all buttons are pressed (allPressed = true)
            // Target should be **enabled** if any button is not pressed (allPressed = false)
            bool shouldBeActive = !allPressed;

            // 3. Apply the change using the appropriate method (Networked vs. Local).
            if (shouldBeActive)
            {
                // The object should be ENABLED (at least one button is unpressed).

                // If the NetworkObject was Despawned, you cannot simply SetActive(true) to bring it back.
                // Assuming this is a one-way puzzle (e.g., clear a path) or that the object
                // was only locally disabled, we use SetActive(true).
                if (!TargetObject.activeSelf)
                {
                    TargetObject.SetActive(true);
                    Debug.Log($"Activator: Button released. Target Object {TargetObject.name} is now Active: TRUE");
                }
            }
            else // shouldBeActive is FALSE (all buttons are pressed)
            {
                // The object should be DISABLED (all buttons are pressed).

                if (m_TargetNetworkObject != null && m_TargetNetworkObject.IsSpawned)
                {
                    // Correctly disable a networked object for all clients.
                    m_TargetNetworkObject.Despawn();
                    Debug.Log($"Activator: All buttons pressed. NetworkObject {TargetObject.name} **Despawned**.");
                }
                else if (TargetObject.activeSelf)
                {
                    // Standard disabling for non-networked objects.
                    TargetObject.SetActive(false);
                    Debug.Log($"Activator: All buttons pressed. Local Object {TargetObject.name} Disabled.");
                }
            }
        }
    }
}