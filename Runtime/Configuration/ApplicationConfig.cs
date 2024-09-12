using System.Text;
using PolkaDOTS.Bootstrap;
using PolkaDOTS.Configuration;
using PolkaDOTS.Deployment;
using UnityEngine;

namespace PolkaDOTS
{
    /// <summary>
    /// Static global class holding application configuration parameters. Handled by <see cref="CmdArgsReader"/>
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

        // ================== NETWORKING ==================
        public static readonly CommandLineParser.IntArgument NetworkTickRate = new CommandLineParser.IntArgument("-networkTickRate", 60);
        public static readonly CommandLineParser.IntArgument SimulationTickRate = new CommandLineParser.IntArgument("-simulationTickRate", 60);
        public static readonly CommandLineParser.IntArgument MaxSimulationStepsPerFrame = new CommandLineParser.IntArgument("-maxSimulationStepsPerFrame", 4);
        public static readonly CommandLineParser.IntArgument MaxSimulationStepBatchSize = new CommandLineParser.IntArgument("-maxSimulationStepBatchSize", 4);
        public static readonly CommandLineParser.IntArgument MaxPredictAheadTimeMS = new CommandLineParser.IntArgument("-maxPredictAheadTimeMS", 250);
        public static readonly CommandLineParser.FlagArgument DisablePrediction = new CommandLineParser.FlagArgument("-disablePrediction", false);

        // ================== APPLICATION ==================
        public static readonly CommandLineParser.FlagArgument NoGraphics = new CommandLineParser.FlagArgument("-nographics", false);
        public static readonly CommandLineParser.FlagArgument DebugEnabled = new CommandLineParser.FlagArgument("-debug", false);
        public static readonly CommandLineParser.StringArgument Seed = new CommandLineParser.StringArgument("-seed", "42");
        public static readonly CommandLineParser.EnumArgument<GameBootstrap.BootstrapPlayTypes> PlayType = new CommandLineParser.EnumArgument<GameBootstrap.BootstrapPlayTypes>("-playType", GameBootstrap.BootstrapPlayTypes.Client);
        public static readonly CommandLineParser.StringArgument ServerUrl = new CommandLineParser.StringArgument("-serverUrl", "127.0.0.1");
        public static readonly CommandLineParser.IntArgument ServerPort = new CommandLineParser.IntArgument("-serverPort", 7979);
        public static readonly CommandLineParser.FlagArgument TakeScreenshots = new CommandLineParser.FlagArgument("-takeScreenshots", false);
        public static readonly CommandLineParser.IntArgument TakeScreenshotsInterval = new CommandLineParser.IntArgument("-screenshotInterval", 5);
        public static readonly CommandLineParser.FilePathArgument ScreenshotFolder = new CommandLineParser.FilePathArgument("-screenshotFolder", Application.persistentDataPath + "/screenshots");
        public static readonly CommandLineParser.IntArgument Duration = new CommandLineParser.IntArgument("-duration", -1);
        public static readonly CommandLineParser.IntArgument Delay = new CommandLineParser.IntArgument("-startDelay", 0);
        public static readonly CommandLineParser.IntArgument UserID = new CommandLineParser.IntArgument("-userID", 0);


        // ================== MULTIPLAY ==================
        public static readonly CommandLineParser.EnumArgument<MultiplayStreamingRoles> MultiplayStreamingRole = new CommandLineParser.EnumArgument<MultiplayStreamingRoles>("-multiplayRole", MultiplayStreamingRoles.Disabled);
        public static readonly CommandLineParser.StringArgument SignalingUrl = new CommandLineParser.StringArgument("-signalingUrl", "ws://127.0.0.1:7981");
        // We only use WebSocket signaling TODO If necessary add support for ICE servers
        //internal static readonly CommandLineParser.StringArgument SignalingType = new CommandLineParser.StringArgument("-signalingType");
        //internal static readonly CommandLineParser.StringArrayArgument IceServerUrls = new CommandLineParser.StringArrayArgument("-iceServerUrl");
        //internal static readonly CommandLineParser.StringArgument IceServerUsername = new CommandLineParser.StringArgument("-iceServerUsername");
        //internal static readonly CommandLineParser.StringArgument IceServerCredential = new CommandLineParser.StringArgument("-iceServerCredential");

        // ================== REMOTE CONTROL =============

        public static readonly CommandLineParser.FlagArgument RemoteControl = new("-remoteControl", false);

        // ================== EMULATION ==================
        public static readonly CommandLineParser.EnumArgument<EmulationType> EmulationType = new CommandLineParser.EnumArgument<EmulationType>("-emulationType", Deployment.EmulationType.None);
        public static readonly CommandLineParser.FilePathArgument EmulationFile = new CommandLineParser.FilePathArgument("-emulationFile", Application.persistentDataPath + '\\' + "recordedInputs.inputtrace");
        public static readonly CommandLineParser.IntArgument NumSimulatedPlayers = new CommandLineParser.IntArgument("-numSimulatedPlayers", 1);
        public static readonly CommandLineParser.IntArgument SimulatedJoinInterval = new CommandLineParser.IntArgument("-simulatedJoinInterval", 0);

        // ================== STATISTICS ==================
        public static readonly CommandLineParser.FlagArgument LogStats = new CommandLineParser.FlagArgument("-logStats", false);
        public static readonly CommandLineParser.FilePathArgument StatsFilePath = new CommandLineParser.FilePathArgument("-statsFile", Application.persistentDataPath + '\\' + "stats.csv");

        public static readonly CommandLineParser.StringArgument StatsHttpUrl =
            new CommandLineParser.StringArgument("-statsHttpUrl", "");

        /// <summary>
        /// How often stats are send over HTTP.
        /// Expressed in seconds.
        /// Setting this to a higher value means sending more data less frequently.
        /// </summary>
        public static readonly CommandLineParser.IntArgument StatsHttpSendInterval =
            new CommandLineParser.IntArgument("-statsHttpSendInterval", 10);

        public new static string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Full Application Config:");
            // from: https://stackoverflow.com/questions/12480279/iterate-through-properties-of-static-class-to-populate-list
            var type = typeof(ApplicationConfig);
            foreach (var f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                var v = f.GetValue(null);
                sb.AppendLine($"{f.Name}={v}");
            }
            return sb.ToString();
        }
    }
}
