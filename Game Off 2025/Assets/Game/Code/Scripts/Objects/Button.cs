using UnityEngine;
using Unity.Netcode;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{
    public class Button : NetworkBehaviour
    {

        public NetworkVariable<bool> IsPressed = new NetworkVariable<bool>(false);


        
        public void PressButtonByProjectile()
        {
            if (!IsServer)
            {
                return;
            }

            if (!IsPressed.Value)
            {
                IsPressed.Value = true;
                Debug.Log("[Button] Activated by projectile.");
            }

            
        }

    }
}