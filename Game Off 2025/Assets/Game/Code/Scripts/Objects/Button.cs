using UnityEngine;
using Unity.Netcode;

namespace Unity.Multiplayer.Center.NetcodeForGameObjects
{

    public class Button : NetworkBehaviour
    {
        public bool pressed = false;
    }
}