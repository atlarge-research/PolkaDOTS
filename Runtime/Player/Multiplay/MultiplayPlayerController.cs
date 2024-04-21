using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using Unity.Entities;
using Unity.RenderStreaming;
using Unity.Serialization;
using UnityEditor;
using Unity.Mathematics;

/*
 * Gathers input from Player GameObject
 */
namespace PolkaDOTS.Multiplay
{
    // Collects input either from local devices or a remote input stream using InputActions
    public class MultiplayPlayerController : MonoBehaviour
    {

        [SerializeField] InputReceiver playerInput;
        [DontSerialize] public string username;
        [DontSerialize] public Vector2 inputMovement;
        [DontSerialize] public Vector2 inputLook;
        [DontSerialize] public bool inputJump;
        [DontSerialize] public bool inputPrimaryAction = false;
        [DontSerialize] public bool inputSecondaryAction = false;
        [DontSerialize] public int selectedItem = 1;
        [DontSerialize] public bool playerEntityExists;
        [DontSerialize] public bool playerEntityRequestSent;
        [DontSerialize] public Entity playerEntity;

        protected void Awake()
        {
            playerInput.onDeviceChange += OnDeviceChange;
            username = $"{ApplicationConfig.UserID.Value}";
        }

        private void OnEnable()
        {
        }

        private void OnDestroy()
        {
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    {
                        playerInput.PerformPairingWithDevice(device);
                        CheckPairedDevices();
                        return;
                    }
                case InputDeviceChange.Removed:
                    {
                        playerInput.UnpairDevices(device);
                        CheckPairedDevices();
                        return;
                    }
            }
        }

        public void CheckPairedDevices()
        {
            if (!playerInput.user.valid)
                return;

            // todo: touchscreen support
            bool hasTouchscreenDevice =
                playerInput.user.pairedDevices.Count(_ => _.path.Contains("Touchscreen")) > 0;
        }

        private void Update()
        {
        }


        public void OnControlsChanged()
        {
        }

        public void OnDeviceLost()
        {
        }

        public void OnDeviceRegained()
        {
        }

        public void OnMovement(InputAction.CallbackContext value)
        {
            inputMovement = value.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext value)
        {
            inputLook = value.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                inputJump = true;
            }
        }

        public void OnPrimaryAction(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                inputPrimaryAction = true;
            }
        }

        public void OnSecondaryAction(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                inputSecondaryAction = true;
            }
        }

        public void OnEscapeAction(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                Debug.Log("Exiting game!");
                //World.DisposeAllWorlds();
#if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif
            }
        }

        public void OnLeftItem(InputAction.CallbackContext value)
        {
            if (value.performed)
            {
                selectedItem = math.max(selectedItem - 1, 1);
                Debug.Log($"Selected item: {selectedItem}");
            }
        }

        public void OnRightItem(InputAction.CallbackContext value)
        {

            int maxItems = 7;
            if (value.performed)
            {
                selectedItem = math.min(selectedItem + 1, maxItems);
                Debug.Log($"Selected item: {selectedItem}");
            }
        }
    }
}

