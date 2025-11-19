using UnityEngine;
using Unity.Netcode;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class RedButton : NetworkBehaviour
    {
        [Header("Button Settings")]
        [Tooltip("Layer of objects that must press the button (e.g., 'Box').")]
        [SerializeField] private LayerMask boxLayer;

        // The specific tag this button accepts.
        private const string RequiredTag = "RedBox";

        public NetworkVariable<bool> IsPressed = new NetworkVariable<bool>(false);

        private int currentBoxCount = 0;

        /// <summary>
        /// Checks if the colliding object satisfies both the required Box Layer and the RedBox Tag.
        /// </summary>
        private bool IsRequiredBox(Collider other)
        {
            // 1. Check Layer: Must be on the specified boxLayer
            bool hasBoxLayer = ((1 << other.gameObject.layer) & boxLayer) != 0;

            // 2. Check Tag: Must have the specific color tag ("RedBox")
            bool hasColorTag = other.CompareTag(RequiredTag);

            return hasBoxLayer && hasColorTag;
        }


        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer)
            {
                return;
            }

            if (IsRequiredBox(other))
            {
                currentBoxCount++;

                if (currentBoxCount > 0 && !IsPressed.Value)
                {
                    IsPressed.Value = true;
                }
            }
        }


        private void OnTriggerExit(Collider other)
        {
            if (!IsServer)
            {
                return;
            }

            if (IsRequiredBox(other))
            {
                currentBoxCount = Mathf.Max(0, currentBoxCount - 1);

                if (currentBoxCount == 0 && IsPressed.Value)
                {
                    IsPressed.Value = false;
                }
            }
        }
    }
}