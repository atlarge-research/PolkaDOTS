using System;
using System.Linq;

namespace PolkaDOTS.Deployment
{
    /// <summary>
    /// Represents valid deployment configuration from Json file
    /// </summary>
    [Serializable]
    public struct JsonDeploymentConfig
    {
        public JsonDeploymentNode[] nodes;
        public ExperimentAction[] experimentActions;

        public override readonly string ToString()
        {
            var ns = string.Join(",", nodes.Select(x => x.ToString()).ToArray());
            var actions = string.Join(",", experimentActions.Select(x => x.ToString()).ToArray());
            return $"{{'nodes': [{ns}], 'experimentActions': [{actions}]}}";
        }
    }

    /// <summary>
    /// Represents a deployment node
    /// </summary>
    [Serializable]
    public struct JsonDeploymentNode
    {
        public int nodeID;
        public string nodeIP;
        public WorldConfig[] worldConfigs;

        public override readonly string ToString()
        {
            var configs = string.Join(",", worldConfigs.Select(x => x.ToString()).ToArray());
            return $"{{'nodeID': {nodeID}, 'nodeIP': {nodeIP}, 'worldConfigs': {configs}}}";
        }
    }

    /// <summary>
    /// Perform set of experiment actions at a certain time specified by delay
    /// </summary>
    [Serializable]
    public class ExperimentAction
    {
        public int delay;
        public NodeAction[] actions;

        public override string ToString()
        {
            var a = string.Join(",", actions.Select(x => x.ToString()).ToArray());
            return $"{{'delay': {delay}, 'actions': {actions}}}";
        }
    }

    /// <summary>
    /// Specified set of world actions to perform on node with matching id
    /// </summary>
    [Serializable]
    public class NodeAction
    {
        public int nodeID;
        public string[] worldNames;
        public WorldAction[] actions;

        public override string ToString()
        {
            var names = string.Join(",", worldNames.Select(x => x.ToString()).ToArray());
            var a = string.Join(",", actions.Select(x => x.ToString()).ToArray());
            return $"{{'nodeID': {nodeID}, 'worldNames': {names}, 'actions': {a}}}";
        }
    }

    /// <summary>
    /// Encode changes in deployment that can occur while the system is running.
    ///
    /// FIXME describe what Stop, Start, and Connect actually mean
    /// </summary>
    [Serializable]
    public enum WorldAction
    {
        Stop,
        Start,
        Connect,
    }

    [Flags]
    [Serializable]
    public enum WorldTypes
    {
        None = 0,
        Client = 1,
        SimulatedClient = 1 << 1,
        Server = 1 << 2

    }

    [Serializable]
    public enum MultiplayStreamingRoles
    {
        Disabled,
        Host,
        CloudHost,
        Guest
    }

    [Serializable]
    public enum ServiceFilterType
    {
        Includes,
        Excludes,
        Only,
    }

    [Serializable]
    public enum InitializationMode
    {
        Create,
        Start,
        Connect
    }

    [Flags]
    [Serializable]
    public enum EmulationType : int
    {
        None = 0,
        Idle = 0,
        Playback = 1,
        Simulation = 1 << 1,
        Record = 1 << 2,
    }

    /// <summary>
    /// Represents a world, each node can contain many worlds.
    /// </summary>
    [Serializable]
    public class WorldConfig
    {
        // Name of the world, used to uniquely identify it
        public string worldName;
        // The type of world, determines how connection is performed and what systems are loaded
        public WorldTypes worldType;
        // How this world should be initialized
        public InitializationMode initializationMode;
        // Multiplay role, determines if this world hosts thin clients, is a thin client, or is a normal client.
        public MultiplayStreamingRoles multiplayStreamingRoles;
        // The ID of the node that this world (if it is a non-streamed client) will connect to. Can be this node!
        public int serverNodeID;
        // The ID of the node that this world (if it is a streamed client) will connect to. Can be this node, but why would you do that?
        public int streamingNodeID;
        // The number of simulated clients to create and connect. Only valid if worldType is SimulatedClient
        public int numSimulatedClients;
        // Names of server service Types, handled according to serviceFilterType
        public string[] services;
        // How the service names are handled when instantiating this world
        public ServiceFilterType serviceFilterType;
        // The player emulation behaviour to use on a client world
        public EmulationType emulationType;

        public override string ToString() =>
            $"[worldType: {worldType}; multiplayStreamingRoles: {multiplayStreamingRoles}; serverNodeID: {serverNodeID}; streamingNodeID: {streamingNodeID};" +
            $"numSimulatedClients: {numSimulatedClients}; services: {services}; serviceFilterType: {serviceFilterType}; emulationType: {emulationType}; ]";
    }
}
