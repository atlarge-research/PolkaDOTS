using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PolkaDOTS.Multiplay
{
    public class MultiplayPlayerControllerVR : MultiplayPlayerController
    {
        public Quaternion lookVR;
        public override bool isVRplayer()
        {
            return true;
        }

        public override void OnLook(InputAction.CallbackContext value)
        {
            Debug.LogError("for VR players, please use OnLookVR instead of OnLook") ;
        }

        public void OnLookVR(InputAction.CallbackContext value)
        {
            lookVR = value.ReadValue<Quaternion>();
            Debug.Log("HMD moved");
        }
    }
}