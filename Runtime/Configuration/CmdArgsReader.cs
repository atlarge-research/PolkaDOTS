using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using PolkaDOTS.Bootstrap;
using PolkaDOTS.Deployment;
using PolkaDOTS.Emulation;
using Unity.NetCode;
using UnityEngine;
using WebSocketSharp;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using ParrelSync;
using UnityEditor;
#endif

namespace PolkaDOTS.Configuration
{
    public static class CmdArgsReader
    {
#if UNITY_EDITOR
        private static EditorCmdArgs editorArgs;
#endif
        // Returns the string array of cmd line arguments from environment, ParrelSync, or an editor GameObject
        private static string[] GetCommandlineArgs()
        {
            var args = new[] { "" };
#if UNITY_EDITOR
            // ParrelSync clones can have arguments passed to them in the Clones Manager window
            editorArgs = (EditorCmdArgs)GameObject.FindFirstObjectByType(typeof(EditorCmdArgs));
            if (ClonesManager.IsClone())
            {
                // Get the custom arguments for this clone project.
                args = ClonesManager.GetArgument().Trim().Split();
            }
            else
            {
                // Otherwise, use arguments in editor MonoBehaviour
                args = editorArgs.editorArgs.Trim().Split();
            }
#else
            // Read from normal command line application arguments
            args = Environment.GetCommandLineArgs();
#endif
            return args;
        }

        public static bool ParseCmdArgs()
        {
#if UNITY_EDITOR
            Debug.Log("Overriding config with editor vars.");
            // Override PlayType, NumThinClients, ServerAddress, and ServerPort from editor settings
            var s_PrefsKeyPrefix = $"MultiplayerPlayMode_{Application.productName}_";
            var s_PlayModeTypeKey = s_PrefsKeyPrefix + "PlayMode_Type";
            var s_RequestedNumThinClientsKey = s_PrefsKeyPrefix + "NumThinClients";
            var s_AutoConnectionAddressKey = s_PrefsKeyPrefix + "AutoConnection_Address";
            var s_AutoConnectionPortKey = s_PrefsKeyPrefix + "AutoConnection_Port";
            // Editor PlayType
            var editorPlayType =
                (ClientServerBootstrap.PlayType)EditorPrefs.GetInt(s_PlayModeTypeKey,
                    (int)ClientServerBootstrap.PlayType.ClientAndServer);
            if (ApplicationConfig.PlayType != GameBootstrap.BootstrapPlayTypes.StreamedClient &&
                ApplicationConfig.PlayType != GameBootstrap.BootstrapPlayTypes.SimulatedClient)
            {
                ApplicationConfig.PlayType.Value = (GameBootstrap.BootstrapPlayTypes)editorPlayType;
            }
            // Server address
            var editorServerAddress = EditorPrefs.GetString(s_AutoConnectionAddressKey, "127.0.0.1");
            ApplicationConfig.ServerUrl.Value = editorServerAddress;
            //Server port
            var editorServerPort = EditorPrefs.GetInt(s_AutoConnectionPortKey, 7979);
            if (editorServerPort != 0)
            {
                ApplicationConfig.ServerPort.Value = editorServerPort;
            }
#endif

            var arguments = GetCommandlineArgs();
            Debug.Log($"Parsing args: {string.Join(", ", arguments)}");
            if (!CommandLineParser.TryParse(arguments))
            {
                Debug.LogError("Parsing command line arguments failed!");
                return false;
            }

#if UNITY_EDITOR
            // Override Deployment ApplicationConfig using this MonoBehaviour's attributes
            if (editorArgs.useDeploymentConfig && !ClonesManager.IsClone())
            {
                if (editorArgs.deploymentConfig.IsNullOrEmpty())
                {
                    Debug.Log($"UseDeploymentConfig flag set but deploymentConfig is empty");
                }
                else
                {
                    //Use Newtonsoft JSON parsing to support enum serialization to/from string
                    ApplicationConfig.ImportDeploymentConfig.Value = JsonConvert.DeserializeObject<JsonDeploymentConfig>(editorArgs.deploymentConfig);
                }
            }
#endif

            // Sanity checks
            if (ApplicationConfig.GetRemoteConfig && ApplicationConfig.DeploymentID == -1)
            {
                Debug.Log($"Remote config flag set with no deployment ID provided, using 0!");
                ApplicationConfig.DeploymentID.Value = 0;
            }

            if (ApplicationConfig.PlayType == GameBootstrap.BootstrapPlayTypes.Server &&
                ApplicationConfig.MultiplayStreamingRole != MultiplayStreamingRoles.Disabled)
            {
                Debug.Log("Cannot run Multiplay streaming on Server, disabling Multiplay!");
                ApplicationConfig.MultiplayStreamingRole.Value = MultiplayStreamingRoles.Disabled;
            }

            return true;
        }

    }
}
