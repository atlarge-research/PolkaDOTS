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
using System.IO;
#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

#if UNITY_EDITOR
using ParrelSync;
using UnityEditor;
#endif

namespace PolkaDOTS.Configuration
{
    public static class CmdArgsReader
    {
        [Serializable]
        private class CmdArgsJson
        {
            public string[] args = new string[0];
        }
#if UNITY_EDITOR
        private static EditorCmdArgs editorArgs;

#endif

        [SerializeField]
        private static string argjsonFileName = "cmdArgs";

        // Returns the string array of cmd line arguments from environment, ParrelSync, or an editor GameObject
        private static string[] GetCommandlineArgs()
        {
            string[] args = new[] { "" };
#if UNITY_EDITOR
            // ParrelSync clones can have arguments passed to them in the Clones Manager window
            editorArgs = (EditorCmdArgs)GameObject.FindFirstObjectByType(typeof(EditorCmdArgs));
            if (ClonesManager.IsClone())
            {
                // Get the custom arguments for this clone project.  
                args = ClonesManager.GetArgument().Split(' ');
            }
            else
            {
                // Otherwise, use arguments in editor MonoBehaviour 
                //args = editorArgs.editorArgs.Split(' ');
                args = getArgsFromJson();
            }
#else
            // Read from normal command line application arguments
            args = Environment.GetCommandLineArgs();
            if(args.Length == 1){
                args = getArgsFromJson();
            }
#endif
            Debug.Log($"running with arguments: {args.ToString()}");
            return args;
        }

        private static string[] getArgsFromJson()
        {
            TextAsset jsonTxt = Resources.Load<TextAsset>(argjsonFileName);
            Debug.Log($"[CONFIG:] Found arg json: {jsonTxt}");
            CmdArgsJson argsObj = JsonConvert.DeserializeObject<CmdArgsJson>(jsonTxt.text);
            Debug.Log($"[CONFIG:] Found args: {argsObj.args}");
            return argsObj.args;
        }

        public static bool ParseCmdArgs()
        {
            var arguments = GetCommandlineArgs();
            Debug.Log($"Parsing args: {String.Join(", ", arguments)}");
            if (!CommandLineParser.TryParse(arguments))
            {
                Debug.LogError("Parsing command line arguments failed!");
                return false;
            }


#if UNITY_EDITOR

            Debug.Log("Overriding config with editor vars.");
            // Override PlayType, NumThinClients, ServerAddress, and ServerPort from editor settings 
            string s_PrefsKeyPrefix = $"MultiplayerPlayMode_{Application.productName}_";
            string s_PlayModeTypeKey = s_PrefsKeyPrefix + "PlayMode_Type";
            string s_RequestedNumThinClientsKey = s_PrefsKeyPrefix + "NumThinClients";
            string s_AutoConnectionAddressKey = s_PrefsKeyPrefix + "AutoConnection_Address";
            string s_AutoConnectionPortKey = s_PrefsKeyPrefix + "AutoConnection_Port";
            // Editor PlayType
            ClientServerBootstrap.PlayType editorPlayType =
                (ClientServerBootstrap.PlayType)EditorPrefs.GetInt(s_PlayModeTypeKey,
                    (int)ClientServerBootstrap.PlayType.ClientAndServer);
            if (ApplicationConfig.PlayType != GameBootstrap.BootstrapPlayTypes.StreamedClient &&
                ApplicationConfig.PlayType != GameBootstrap.BootstrapPlayTypes.SimulatedClient )
                ApplicationConfig.PlayType.SetValue((GameBootstrap.BootstrapPlayTypes)editorPlayType);
            // Server address
            string editorServerAddress = EditorPrefs.GetString(s_AutoConnectionAddressKey, "127.0.0.1");
            ApplicationConfig.ServerUrl.SetValue(editorServerAddress);
            //Server port
            int editorServerPort = EditorPrefs.GetInt(s_AutoConnectionPortKey, 7979);
            if (editorServerPort != 0)
                ApplicationConfig.ServerPort.SetValue(editorServerPort); 

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
                    ApplicationConfig.ImportDeploymentConfig.SetValue(JsonConvert.DeserializeObject<JsonDeploymentConfig>(editorArgs.deploymentConfig)); 
                }
            }

#endif

            // Sanity checks
            if (ApplicationConfig.GetRemoteConfig && ApplicationConfig.DeploymentID == -1)
            {
                Debug.Log($"Remote config flag set with no deployment ID provided, using 0!");
                ApplicationConfig.DeploymentID.SetValue(0);
            }

            if (ApplicationConfig.PlayType == GameBootstrap.BootstrapPlayTypes.Server &&
                ApplicationConfig.MultiplayStreamingRole != MultiplayStreamingRoles.Disabled)
            {
                Debug.Log("Cannot run Multiplay streaming on Server, disabling Multiplay!");
                ApplicationConfig.MultiplayStreamingRole.SetValue(MultiplayStreamingRoles.Disabled);
            }
            
            return true;
        }

    }
}