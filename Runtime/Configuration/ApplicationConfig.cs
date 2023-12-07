using PolkaDOTS.Bootstrap;
using PolkaDOTS.Configuration;
using PolkaDOTS.Deployment;
using UnityEngine;

namespace PolkaDOTS
{
    /// <summary>
    /// Static global class holding application configuration parameters. Filled by <see cref="CmdArgsReader"/>
    /// </summary>
    [ArgumentClass]
    public static class ApplicationConfig
    {
        // ================== DEPLOYMENT ==================
        public static readonly CommandLineParser.JsonFileArgument<JsonDeploymentConfig> ImportDeploymentConfig = new CommandLineParser.JsonFileArgument<JsonDeploymentConfig>("-deploymentJson", null);
        public static CommandLineParser.IntArgument DeploymentID = new CommandLineParser.IntArgument("-deploymentID", -1);
        public static readonly CommandLineParser.FlagArgument GetRemoteConfig = new CommandLineParser.FlagArgument("-remoteConfig", false);
        public static readonly CommandLineParser.StringArgument DeploymentURL = new CommandLineParser.StringArgument("-deploymentURL", "127.0.0.1");
        public static readonly CommandLineParser.IntArgument DeploymentPort = new CommandLineParser.IntArgument("-deploymentPort", 7980);
        
        // ================== APPLICATION ==================
        public static readonly CommandLineParser.FlagArgument DebugEnabled = new CommandLineParser.FlagArgument("-debug", false);
        public static readonly CommandLineParser.StringArgument Seed = new CommandLineParser.StringArgument("-seed", "42");
        public static readonly CommandLineParser.EnumArgument<GameBootstrap.BootstrapPlayTypes> PlayType = new CommandLineParser.EnumArgument<GameBootstrap.BootstrapPlayTypes>("-playType", GameBootstrap.BootstrapPlayTypes.Client);
        public static readonly CommandLineParser.StringArgument ServerUrl = new CommandLineParser.StringArgument("-serverUrl", "127.0.0.1");
        public static readonly CommandLineParser.IntArgument ServerPort = new CommandLineParser.IntArgument("-serverPort", 7979);
        //public static readonly JsonFileArgumentClass<JsonCmdArgs> LocalConfigJson = new JsonFileArgumentClass<JsonCmdArgs>("-localConfigJson");
        public static readonly CommandLineParser.IntArgument NetworkTickRate = new CommandLineParser.IntArgument("-networkTickRate", 60);
        public  static readonly CommandLineParser.IntArgument SimulationTickRate = new CommandLineParser.IntArgument("-simulationTickRate", 60);
        public static readonly CommandLineParser.FlagArgument TakeScreenshots = new CommandLineParser.FlagArgument("-takeScreenshots", false);
        public static readonly CommandLineParser.IntArgument TakeScreenshotsInterval = new CommandLineParser.IntArgument("-screenshotInterval", 5);
        public static readonly CommandLineParser.FilePathArgument ScreenshotFolder = new CommandLineParser.FilePathArgument("-screenshotFolder", Application.persistentDataPath + "/screenshots");
        public static readonly CommandLineParser.IntArgument Duration = new CommandLineParser.IntArgument("-duration", -1);
        public static readonly CommandLineParser.IntArgument UserID = new CommandLineParser.IntArgument("-userID", -1);
        
        // ================== SIGNALING ==================
        public static readonly CommandLineParser.StringArgument SignalingUrl = new CommandLineParser.StringArgument("-signalingUrl", "ws://127.0.0.1:7981");

        // We only use WebSocket
        //internal static readonly StringArgument SignalingType = new StringArgument("-signalingType");
        
        // If necessary add support for ICE servers
        //internal static readonly StringArrayArgument IceServerUrls = new StringArrayArgument("-iceServerUrl");
        //internal static readonly StringArgument IceServerUsername = new StringArgument("-iceServerUsername");
        //internal static readonly StringArgument IceServerCredential = new StringArgument("-iceServerCredential");
        
        // ================== MULTIPLAY ==================
        public static readonly CommandLineParser.EnumArgument<MultiplayStreamingRoles> MultiplayStreamingRole = new CommandLineParser.EnumArgument<MultiplayStreamingRoles>("-multiplayRole", MultiplayStreamingRoles.Disabled);
        //public static readonly CommandLineParser.IntArgument SwitchToStreamDuration = new CommandLineParser.IntArgument("-switchToStream", 0);
        
        // ================== EMULATION ==================
        public static readonly CommandLineParser.EnumArgument<EmulationType> EmulationType = new CommandLineParser.EnumArgument<EmulationType>("-emulationType", Deployment.EmulationType.None);
        public static readonly CommandLineParser.FilePathArgument EmulationFile = new CommandLineParser.FilePathArgument("-emulationFile", Application.persistentDataPath + '\\' + "recordedInputs.inputtrace");
        public static readonly CommandLineParser.IntArgument NumThinClientPlayers = new CommandLineParser.IntArgument("-numThinClientPlayers", 0);
        
        // ================== STATISTICS ==================
        public static readonly CommandLineParser.FlagArgument LogStats = new CommandLineParser.FlagArgument("-logStats", false);
        public static readonly CommandLineParser.FilePathArgument StatsFilePath = new CommandLineParser.FilePathArgument("-statsFile", Application.persistentDataPath + '\\' + "stats.csv");
        
        /*static readonly List<IArgument> options = new List<IArgument>()
        {
            ImportDeploymentConfig, DeploymentID, GetRemoteConfig, DeploymentURL, DeploymentPort,
            DebugEnabled, Seed, PlayType, ServerUrl, ServerPort, NetworkTickRate, SimulationTickRate, TakeScreenshots, TakeScreenshotsInterval, ScreenshotFolder, Duration, UserID,
            SignalingUrl,
            MultiplayStreamingRole,SwitchToStreamDuration,
            EmulationType, EmulationFile, NumThinClientPlayers,
            LogStats, StatsFilePath
        };*/
        
    }

}