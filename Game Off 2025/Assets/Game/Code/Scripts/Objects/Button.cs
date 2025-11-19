using UnityEngine;
using Unity.Netcode;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    
    public class Button : NetworkBehaviour
    {
        [Header("Button Settings")]
        [Tooltip("Layer of objects that can press the button (e.g., 'Box').")]
        [SerializeField] private LayerMask boxLayer;

        
        public NetworkVariable<bool> IsPressed = new NetworkVariable<bool>(false);

        private int currentBoxCount = 0; 

        
        private bool IsBox(Collider other)
        {
            return ((1 << other.gameObject.layer) & boxLayer) != 0;
        }

       
        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer)
            {
                return;
            }

            if (IsBox(other))
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

            if (IsBox(other))
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