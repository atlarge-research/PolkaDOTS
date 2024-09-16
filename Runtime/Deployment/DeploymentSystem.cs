using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using PolkaDOTS.Bootstrap;
using PolkaDOTS.Multiplay;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Rendering;


namespace PolkaDOTS.Deployment
{
    /// <summary>
    /// This struct is sent as RPC payload to request the configuration for the given node.
    /// </summary>
    public struct RequestConfigRPC : IRpcCommand
    {
        public int nodeID;
    }

    /// <summary>
    /// This struct is used in <see cref="DeploymentConfigRPC"/> to specify which action to take.
    /// </summary>
    [Flags]
    [Serializable]
    public enum ConfigRPCActions
    {
        Create = 1, // Creates the structures for a world but does not start running it
        Start = 1 << 1, // Adds a created world to the player running world list
        Connect = 1 << 2, // Creates a connection request in a running world
    }

    /// <summary>
    /// This struct is sent as the payload of an RPC used to trigger an action present in the deploymentgraph.
    /// </summary>
    public struct DeploymentConfigRPC : IRpcCommand
    {
        public int nodeID;

        //What action to apply to these world types
        public ConfigRPCActions action;

        public FixedString64Bytes worldName;

        public WorldTypes worldType;

        public MultiplayStreamingRoles multiplayStreamingRoles;

        // Game server connection
        public FixedString64Bytes serverIP;
        public ushort serverPort;

        //  Multiplay streaming host/guest
        public FixedString64Bytes signallingIP;

        public int numSimulatedClients;

        // todo
        // Names of server service Types, handled according to serviceFilterType
        // public string[] services;
        // How the service names are handled when instantiating this world
        // public ServiceFilterType serviceFilterType;

        // The player emulation behaviour to use on a client world
        public EmulationType emulationType;

        /*public override string ToString() =>
            $"[nodeID: { nodeID};  worldTypes: {(WorldTypes)worldTypes}; numThinClients: {numThinClients};" +
            $"emulationType: {emulationType}; ]";*/
    }

    /// <summary>
    /// This struct is sent as the payload of an RPC used to trigger an action present in the deploymentgraph.
    /// </summary>
    public struct WorldActionRPC : IRpcCommand
    {
        public int nodeID;
        public FixedString64Bytes worldName;
        public WorldAction action;
        public FixedString64Bytes connectionIP;
        public ushort connectionPort;
    }

    /// <summary>
    /// RPC error response
    /// </summary>
    [Serializable]
    public enum ConfigErrorType
    {
        UnknownID,
        DuplicateID,
        UnknownWorld,
    }

    /// <summary>
    /// RPC error response
    /// </summary>
    public struct ConfigErrorRPC : IRpcCommand
    {
        public int nodeID;
        public ConfigErrorType errorType;
    }

    /// <summary>
    /// A component used to signal that a connection has asked for deployment configuration
    /// </summary>
    public struct ConfigurationSent : IComponentData
    {
    }

    /// <summary>
    /// Listens for <see cref="RequestConfigRPC"/> and responds with one or more <see cref="DeploymentConfigRPC"/> containing
    /// configuration set in the <see cref="DeploymentGraph"/>
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Disabled)] // Don't automatically add to worlds
    public partial class DeploymentServiceSystem : SystemBase
    {
        private DeploymentGraph _deploymentGraph;
        private bool _allNodesConnected;
        private double _startTime;
        protected override void OnCreate()
        {
            _deploymentGraph = new DeploymentGraph();
            // Check if deployment graph contains configuration for this local node
            DeploymentNode? node = _deploymentGraph.GetNodeByID(ApplicationConfig.DeploymentID);
            _allNodesConnected = false;
            _startTime = double.NaN;
            if (node.HasValue)
            {
                Debug.Log("Overriding local config from deployment graph");
                _deploymentGraph.SetConnected(ApplicationConfig.DeploymentID);
                List<DeploymentConfigRPC> cRPCs = _deploymentGraph.NodeToConfigRPCs(ApplicationConfig.DeploymentID);
                foreach (var cRPC in cRPCs)
                {
                    DeploymentConfigHelpers.HandleDeploymentConfigRPC(cRPC, NetworkEndpoint.LoopbackIpv4, out NativeList<WorldUnmanaged> newWorlds);
                    // Should not need to use the authoring scene loader as all worlds will be created in the first tick
                }
            }
            else
            {
                // Setup worlds from local configuration
                BootstrapInstance.instance.SetupWorldsFromLocalConfig();
            }
        }


        protected override void OnUpdate()
        {
            // Answer received configuration request RPCs
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var connectionLookup = GetComponentLookup<NetworkStreamConnection>();
            var netDriver = SystemAPI.GetSingleton<NetworkStreamDriver>();

            foreach (var (reqSrc, req, reqEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<RequestConfigRPC>>()
                         .WithEntityAccess())
            {
                var sourceConn = reqSrc.ValueRO.SourceConnection;
                commandBuffer.AddComponent<ConfigurationSent>(sourceConn); // Mark this connection as request received
                                                                           //commandBuffer.AddComponent<NetworkStreamInGame>(sourceConn);

                var res = commandBuffer.CreateEntity();
                var nodeID = req.ValueRO.nodeID;
                var node = _deploymentGraph.GetNodeByID(nodeID);
                Debug.Log($"Got configuration request for node with ID {nodeID}");
                // Check request validity
                if (node == null)
                {
                    Debug.Log($"Received configuration request from node with unknown ID: {req.ValueRO.nodeID}");
                    commandBuffer.AddComponent(res, new ConfigErrorRPC { nodeID = nodeID, errorType = ConfigErrorType.UnknownID });
                    commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                }
                else if (node.Value.connected)
                {
                    Debug.Log($"Received configuration request from node with already connected ID: {req.ValueRO.nodeID}");
                    commandBuffer.AddComponent(res, new ConfigErrorRPC { nodeID = nodeID, errorType = ConfigErrorType.DuplicateID });
                    commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                }
                else
                {
                    // Mark we have received a request from this node
                    _deploymentGraph.SetConnected(nodeID);
                    // Get the source network endpoint of the node
                    var connection = connectionLookup[sourceConn];
                    var remoteEndpoint = netDriver.GetRemoteEndPoint(connection);
                    if (!_deploymentGraph.CompareEndpoint(nodeID, remoteEndpoint))
                    {
                        Debug.Log($"Received config request for node {nodeID} from endpoint {remoteEndpoint}," +
                                         $"even though this node is configured to be at endpoint {_deploymentGraph.GetEndpoint(nodeID)}");
                        // should we exit here?
                    }
                    _deploymentGraph.SetEndpoint(nodeID, remoteEndpoint, sourceConn);
                    // Build response with configuration details
                    var cRPCs = _deploymentGraph.NodeToConfigRPCs(nodeID);
                    // Create a set of configuration RPCs
                    foreach (var cRPC in cRPCs)
                    {
                        commandBuffer.AddComponent(res, cRPC);
                        commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = sourceConn });
                        res = commandBuffer.CreateEntity();
                    }
                }
                // Destroy the request
                commandBuffer.DestroyEntity(reqEntity);
            }

            // Check if all nodes connected
            _allNodesConnected = _deploymentGraph.CheckAllNodesConnected();
            if (_allNodesConnected && double.IsNaN(_startTime))
            {
                _startTime = World.Time.ElapsedTime;
            }

            // Handle received configuration error RPC
            foreach (var (_, errorRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ConfigErrorRPC>>()
                         .WithEntityAccess())
            {
                Debug.Log($"Received configuration error response of type: {errorRPC.ValueRO.errorType} from node with ID {errorRPC.ValueRO.nodeID}");
                commandBuffer.DestroyEntity(reqEntity);
            }

            // Handle timing events
            var elapsed = World.Time.ElapsedTime - _startTime;
            if (ApplicationConfig.Duration > 0 && elapsed > ApplicationConfig.Duration)
            {
                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Experiment duration of {ApplicationConfig.Duration.Value} seconds elapsed! Exiting.");
                BootstrapInstance.instance.ExitGame();
            }

            // Handle experiment control events
            //
            // Example of experiment event JSON:
            // "experimentActions":[
            // 	{
            // 		"delay": 120,
            // 		"actions": [
            // 			{
            // 				"nodeID": 1,
            // 				"worldNames": ["GameClient", "StreamedClient"],
            // 				"actions": ["Stop", "Connect"]
            // 			},
            // 			{
            // 				"nodeID": 2,
            // 				"worldNames": ["CloudHostClient"],
            // 				"actions": ["Connect"]
            // 			}
            // 		]
            // 	}
            // ]
            for (var experimentID = 0; experimentID < _deploymentGraph.ExperimentActionList.Count; experimentID++)
            {
                // Wait for all nodes to connect to begin experiment
                if (!_allNodesConnected)
                {
                    break;
                }

                var experimentAction = _deploymentGraph.ExperimentActionList[experimentID];
                if (elapsed > experimentAction.delay && !experimentAction.done)
                {
                    var nodeActions = experimentAction.deploymentNodeActions;

                    foreach (var nodeAction in nodeActions)
                    {
                        // Do the action
                        var node = _deploymentGraph.GetNodeByID(nodeAction.nodeID).Value;
                        if (!node.connected)
                        {
                            Debug.LogWarning($"NodeAction failed, node {node.id} has not connected!");
                            continue;
                        }
                        // Iterate over the actions, perform them one by one
                        for (var i = 0; i < nodeAction.worldActions.Length; i++)
                        {
                            // Look up the given node's world config as listed in the deployment graph file
                            var worldConfig = node.worldConfigs[nodeAction.worldConfigID[i]];
                            // Get the name for that world config
                            var worldName = worldConfig.worldName;
                            // Get the corresponding world action
                            var action = nodeAction.worldActions[i];

                            // Address to point to deployment component. Assume it's on localhost for now...
                            var connectionURL = new FixedString64Bytes("127.0.0.1");
                            // Assume the port is 7979 for now...
                            ushort connectionPort = 7979;

                            // To see what a WorldAction actually does, check the DeploymentConfigHelpers.HandleWorldAction method
                            if (action == WorldAction.Connect)
                            {
                                // Streamed client world
                                if (worldConfig.worldType == WorldTypes.Client && worldConfig.multiplayStreamingRoles ==
                                    MultiplayStreamingRoles.Guest)
                                {
                                    var targetNode = _deploymentGraph.GetNodeByID(worldConfig.streamingNodeID);
                                    if (!targetNode.HasValue)
                                    {
                                        Debug.LogWarning($"Target node for streaming {worldConfig.streamingNodeID} does not exist!");
                                    }
                                    else
                                    {
                                        // Update the IP address to that the renderer component
                                        connectionURL = targetNode.Value.endpoint;
                                    }

                                    // Update port number to the one we hardcoded for the renderer
                                    connectionPort = 7981;
                                }
                                // Client world
                                else if (worldConfig.worldType == WorldTypes.Client && worldConfig.multiplayStreamingRoles !=
                                           MultiplayStreamingRoles.Guest)
                                {
                                    // TODO This code does almost the same as the if-part of this clause,
                                    // except it does not change the port number..
                                    var targetNode =
                                        _deploymentGraph.GetNodeByID(worldConfig.serverNodeID);
                                    if (!targetNode.HasValue)
                                    {
                                        Debug.LogWarning($"Target node for game {worldConfig.serverNodeID} does not exist!");
                                    }
                                    else
                                    {
                                        connectionURL = targetNode.Value.endpoint;
                                    }
                                }
                                // Server world
                                else if (worldConfig.worldType == WorldTypes.Server)
                                {
                                    // defaults work for now
                                }
                            }

                            // Create a WorldActionRPC based on the WorldAction and other fields above
                            var wa = new WorldActionRPC
                            {
                                nodeID = node.id,
                                action = action,
                                worldName = worldName,
                                connectionIP = connectionURL,
                                connectionPort = connectionPort
                            };

                            // Turns out that we have the deployment system, an actual RPC is not needed!
                            // We can call the method directly.
                            // But we do need to pass the loopback address for some reason :))
                            if (node.id == ApplicationConfig.DeploymentID)
                            {
                                // If the action is for the local node, handle it
                                if (!DeploymentConfigHelpers.HandleWorldAction(wa, NetworkEndpoint.LoopbackIpv4))
                                {
                                    Debug.LogWarning($"World {wa.worldName} not found!");
                                }
                            }
                            // Else we send it as an RPC to the node who needs to change its deployment
                            else
                            {
                                var res = commandBuffer.CreateEntity();
                                commandBuffer.AddComponent(res, wa);
                                commandBuffer.AddComponent(res,
                                    new SendRpcCommandRequest { TargetConnection = node.sourceConnection });
                            }
                        }
                    }
                    experimentAction.done = true;
                    _deploymentGraph.ExperimentActionList[experimentID] = experimentAction;
                }
            }
            commandBuffer.Playback(EntityManager);
        }

    }

    /// <summary>
    /// Sends <see cref="RequestConfigRPC"/> and uses the configuration in the response <see cref="DeploymentConfigRPC"/>
    /// to create local worlds
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Disabled)] // Don't automatically add to worlds
    [BurstCompile]
    public partial class DeploymentReceiveSystem : SystemBase
    {
        private double _startTime;
        private bool _configReceived;
        [BurstCompile]
        protected override void OnCreate()
        {
            //var builder = new EntityQueryBuilder(Allocator.Temp)
            //    .WithAll<NetworkId>();
            //RequireForUpdate(GetEntityQuery(builder));
            _startTime = double.NaN;
            _configReceived = false;
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            // Look up components related to networking
            var connectionLookup = GetComponentLookup<NetworkStreamConnection>();
            var netDriver = SystemAPI.GetSingleton<NetworkStreamDriver>();

            // Create a new command buffer, which holds ECS actions such as creating, destroying, and adding entities and components,
            // and will be played back later
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            // Get all entities with a NetworkID and without a ConfigurationSent component
            foreach (var (netID, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess().WithNone<ConfigurationSent>())
            {
                // Schedule adding ConfigurationSent components to the newly retrieved entities
                commandBuffer.AddComponent<ConfigurationSent>(entity);
                // Create a new entity that represents a DeploymentConfiguration request
                // (this node will ask another node how it needs to be configured)
                var req = commandBuffer.CreateEntity();
                // Add to the new entity a component representing the request to receive deployment configuration
                // Set the nodeID to the current node so that the remote machine knows which configuration to look up
                commandBuffer.AddComponent(req, new RequestConfigRPC { nodeID = ApplicationConfig.DeploymentID });
                // Add to the new entity a Unity built-in component that signals to Unity that this entity should be sent as an RPC(?)
                commandBuffer.AddComponent(req, new SendRpcCommandRequest { TargetConnection = entity });
                Debug.Log($"Sending configuration request.");
            }

            // Query Unity for RPC errors and handle them
            foreach (var (reqSrc, errorRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<ConfigErrorRPC>>()
                         .WithEntityAccess())
            {
                Debug.Log($"Received configuration error response of type: {errorRPC.ValueRO.errorType}");
                commandBuffer.DestroyEntity(reqEntity);
            }

            // Handle all correctly received configuration RPCs
            foreach (var (reqSrc, configRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<DeploymentConfigRPC>>()
                         .WithEntityAccess())
            {
                var connection = connectionLookup[reqSrc.ValueRO.SourceConnection];
                var remoteEndpoint = netDriver.GetRemoteEndPoint(connection);

                var cRPC = configRPC.ValueRO;

                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Received configuration {cRPC.action} RPC on world {cRPC.worldName} with type {cRPC.worldType}:{cRPC.multiplayStreamingRoles} from {remoteEndpoint}");
                // Mark when we receive the config requests
                _startTime = World.Time.ElapsedTime;
                _configReceived = true;

                DeploymentConfigHelpers.HandleDeploymentConfigRPC(cRPC, remoteEndpoint, out var newWorlds);

                if (!newWorlds.IsEmpty)
                {
                    GenerateAuthoringSceneLoadRequests(commandBuffer, ref newWorlds);
                }

                commandBuffer.DestroyEntity(reqEntity);
            }

            // Handle all received experiment WorldActionRPCs
            foreach (var (reqSrc, worldActionRPC, reqEntity) in SystemAPI
                         .Query<RefRO<ReceiveRpcCommandRequest>, RefRO<WorldActionRPC>>()
                         .WithEntityAccess())
            {
                var connection = connectionLookup[reqSrc.ValueRO.SourceConnection];
                var remoteEndpoint = netDriver.GetRemoteEndPoint(connection);
                var wRPC = worldActionRPC.ValueRO;

                if (!DeploymentConfigHelpers.HandleWorldAction(wRPC, remoteEndpoint))
                {
                    Debug.Log($"World with name {wRPC.worldName} not found!");
                    var res = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent(res, new ConfigErrorRPC { nodeID = ApplicationConfig.DeploymentID, errorType = ConfigErrorType.UnknownWorld });
                    commandBuffer.AddComponent(res, new SendRpcCommandRequest { TargetConnection = reqSrc.ValueRO.SourceConnection });
                }

                commandBuffer.DestroyEntity(reqEntity);
            }

            commandBuffer.Playback(EntityManager);

            if (ApplicationConfig.Duration > 0 && _configReceived && (World.Time.ElapsedTime - _startTime) > ApplicationConfig.Duration)
            {
                Debug.Log($"[{DateTime.Now.TimeOfDay}]: Experiment duration of {ApplicationConfig.Duration} seconds elapsed! Exiting.");
                BootstrapInstance.instance.ExitGame();
            }
        }

        public static void GenerateAuthoringSceneLoadRequests(EntityCommandBuffer ecb, ref NativeList<WorldUnmanaged> newWorlds)
        {
            foreach (var world in newWorlds)
            {
                if ((world.IsClient() || world.IsServer() || world.IsSimulatedClient()) && !world.IsStreamedClient())
                {
                    var e = ecb.CreateEntity();
                    ecb.AddComponent(e, new LoadAuthoringSceneRequest { world = world });
                }
            }
        }
    }

    [BurstCompile]
    internal static class DeploymentConfigHelpers
    {
        public static void HandleDeploymentConfigRPC(DeploymentConfigRPC cRPC, NetworkEndpoint sourceConn, out NativeList<WorldUnmanaged> newWorlds)
        {
            newWorlds = new NativeList<WorldUnmanaged>(16, Allocator.Temp);
            if (cRPC.worldType == WorldTypes.None)
            {
                Debug.Log($"Received deployment config RPC with no worldtype!");
                return;
            }
            var playTypes = GameBootstrap.BootstrapPlayTypes.ServerAndClient;
            if (cRPC.worldType == WorldTypes.Client)
            {
                playTypes = GameBootstrap.BootstrapPlayTypes.Client;
            }

            if (cRPC.worldType == WorldTypes.Server)
            {
                playTypes = GameBootstrap.BootstrapPlayTypes.Server;
            }

            if (cRPC.worldType == WorldTypes.SimulatedClient)
            {
                playTypes = GameBootstrap.BootstrapPlayTypes.SimulatedClient;
            }

            var create = (ConfigRPCActions.Create & cRPC.action) != 0;
            var start = (ConfigRPCActions.Start & cRPC.action) != 0;
            var connect = (ConfigRPCActions.Connect & cRPC.action) != 0;

            // If the node we are connecting to is the deployment node, use its external IP rather than internal
            if (cRPC.serverIP == "source")
            {
                var addr = sourceConn.WithPort(0).ToString();
                cRPC.serverIP = addr[..^2]; // strip ":0" suffix from addr string
                if (cRPC.serverIP == "127.0.0.1")
                {
                    cRPC.serverIP = new FixedString64Bytes(ApplicationConfig.ServerUrl.Value);
                }
            }
            if (cRPC.signallingIP == "source")
            {
                var addr = sourceConn.WithPort(0).ToString();
                cRPC.signallingIP = addr[..^2]; // strip ":0" suffix from addr string
                if (cRPC.signallingIP == "127.0.0.1")
                {
                    cRPC.signallingIP = new FixedString64Bytes(ApplicationConfig.SignalingUrl.Value);
                }
            }

            if (create)
            {
                BootstrapInstance.instance.SetupWorlds(cRPC.multiplayStreamingRoles, playTypes, ref newWorlds, cRPC.numSimulatedClients,
                    autoStart: start, autoConnect: connect, cRPC.serverIP.ToString(), cRPC.serverPort, cRPC.signallingIP.ToString(), cRPC.worldName.ToString());
            }
            else if (start)
            {
                BootstrapInstance.instance.StartWorlds(autoConnect: connect, cRPC.multiplayStreamingRoles, playTypes,
                    cRPC.serverIP.ToString(), cRPC.serverPort, cRPC.signallingIP.ToString());
            }
            else if (connect)
            {
                BootstrapInstance.instance.ConnectWorlds(cRPC.multiplayStreamingRoles, playTypes,
                    cRPC.serverIP.ToString(), cRPC.serverPort, cRPC.signallingIP.ToString());
            }
        }

        /// <summary>
        /// This method performs a <see cref="WorldActionRPC"/>, which changes the deployment of the game at runtime.
        /// </summary>
        /// <param name="wRPC">the action to perform</param>
        /// <param name="sourceConn"></param>
        /// <returns></returns>
        public static bool HandleWorldAction(WorldActionRPC wRPC, NetworkEndpoint sourceConn)
        {
            Debug.Log($"[{DateTime.Now.TimeOfDay}]: Received worldAction {wRPC.action} RPC for world {wRPC.worldName}");

            // Find the world whose name matches
            var i = BootstrapInstance.instance.worlds.FindLastIndex(w => w.Name == wRPC.worldName.ToString());
            if (i < 0)
            {
                return false;
            }
            var world = BootstrapInstance.instance.worlds[i];

            var connURL = wRPC.connectionIP.ToString();
            var connPort = wRPC.connectionPort;

            // If the node we are connecting to is the deployment node, use its external IP rather than internal
            // Jesse: I have no clue if this is still relevant. Maybe can be deleted?
            if (connURL == "source")
            {
                var addr = sourceConn.WithPort(0).ToString();
                connURL = addr[..^2]; // strip the ":0" suffix from the address string
                Debug.Log($"'source' url converted to {connURL}");
            }

            var action = wRPC.action;
            HandleWorldAction(world, connURL, connPort, action);

            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="world"></param>
        /// <param name="connURL"></param>
        /// <param name="connPort"></param>
        /// <param name="action"></param>
        public static void HandleWorldAction(World world, string connURL, ushort connPort, WorldAction action)
        {
            switch (action)
            {
                case WorldAction.Stop:
                    // Create an entity with an ExitWorld component in the selected world
                    // This triggers the StopWorldSystem which is waiting for an entity with this component
                    var exitReq = world.EntityManager.CreateEntity();
                    world.EntityManager.AddComponentData(exitReq, new ExitWorld());
                    break;
                case WorldAction.Start:
                    // Start is similar to connect but does not create the entities needed to ... start the new world?
                    BootstrapInstance.instance.SetWorldToUpdating(world);
                    break;
                case WorldAction.Connect:
                    BootstrapInstance.instance.SetWorldToUpdating(world);
                    var connReq = world.EntityManager.CreateEntity();

                    if (world.IsStreamedClient())
                    {
                        var signalingConnUrl = $"ws://{connURL}:{connPort}";
                        // todo remove this
                        // Jesse: but can it be safely removed? :P
                        if (connURL == "127.0.0.1")
                        {
                            signalingConnUrl = ApplicationConfig.SignalingUrl;
                        }
                        // Create a new entity with a StreamedClientRequestConnect component
                        // This entity/component will be picked up by the MultiplayInitSystem
                        world.EntityManager.AddComponentData(connReq,
                            new StreamedClientRequestConnect { url = new FixedString512Bytes(signalingConnUrl) });
                    }
                    else if (world.IsClient() && !world.IsStreamedClient())
                    {
                        // todo remove this
                        // Jesse: but can it be safely removed? :P
                        if (connURL == "127.0.0.1")
                        {
                            connURL = ApplicationConfig.ServerUrl;
                        }
                        NetworkEndpoint.TryParse(connURL, connPort,
                            out var gameEndpoint, NetworkFamily.Ipv4);
                        Debug.Log($"Connecting client world {world.Name} to {connURL} : {connPort} = {gameEndpoint}");
                        // Create a new entity with a NetworkStreamRequestConnect component
                        // This entity will be picked up by Unity's netcode libary and
                        //  initiate a connection to the server
                        // The naming is confusing to us, in the context of video streaming, but this is a regular
                        //  server-client connection
                        world.EntityManager.AddComponentData(connReq,
                            new NetworkStreamRequestConnect { Endpoint = gameEndpoint });
                    }
                    else if (world.IsServer())
                    {
                        var listenNetworkEndpoint = NetworkEndpoint.AnyIpv4.WithPort(connPort);
                        world.EntityManager.AddComponentData(connReq,
                            new NetworkStreamRequestListen { Endpoint = listenNetworkEndpoint });
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Sends <see cref="RequestConfigRPC"/> and uses the configuration in the response <see cref="DeploymentConfigRPC"/>
    /// to create local worlds
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Disabled)] // Don't automatically add to worlds
    public partial class RemoteControlledDeploymentSystem : SystemBase
    {
        private HttpListener _httpListener;
        private Task<HttpListenerContext> _request;

        [Serializable]
        public class RemoteDeploymentRequest
        {
            public string role;
            public string ipv4;
            public ushort port;
            public ushort numberOfClients = 1;
            public string signalingUrl = "ws://127.0.0.1:7981";

            public override string ToString()
            {
                return $"role={role},addr={ipv4}:{port},numberOfClients={numberOfClients},signalingUrl={signalingUrl}";
            }
        }

        protected override void OnCreate()
        {
            _httpListener = new HttpListener();
            // TODO pass uri prefix as config option
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        protected override void OnStartRunning()
        {
            _httpListener.Prefixes.Add("http://*:7982/");
            _httpListener.Start();
        }

        protected override void OnStopRunning()
        {
            StopHttpListener();
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            StopHttpListener();
        }

        private void StopHttpListener()
        {
            if (_httpListener != null && _httpListener.IsListening)
            {
                _httpListener.Stop();
                _httpListener.Close();
            }
        }

        protected override void OnUpdate()
        {
            if (_request is not null && _request.IsCompleted)
            {
                // Is it smart to do parse HTTP requests and send a reply on the same thread
                // as we're rendering a frame?
                // Unlikely!
                // However, we (1) expect these events to be vary rare compared to the frame rate and
                // (2) handling this event will any way trigger a delay because we will be switching
                // between local and remote rendering.
                if (_request.IsCompletedSuccessfully)
                {
                    var result = _request.Result;
                    var request = result.Request;
                    var response = result.Response;

                    RemoteDeploymentRequest req;
                    using (var body = request.InputStream)
                    using (var reader = new System.IO.StreamReader(body, request.ContentEncoding))
                    {
                        var requestBody = reader.ReadToEnd();
                        req = JsonUtility.FromJson<RemoteDeploymentRequest>(requestBody);
                    }

                    // Respond to request
                    response.StatusCode = 204;
                    response.ContentLength64 = 0;
                    response.Close();

                    HandleRequest(req);
                }
                else
                {
                    Debug.Log("Got a buggy request!");
                }
            }

            // Listen for a new request
            if (_request is null || _request.IsCompleted)
            {
                _request = _httpListener.GetContextAsync();
            }
        }

        private void HandleRequest(RemoteDeploymentRequest request)
        {
            Debug.Log(request);

            var role = request.role;
            // Translate Hostname to IP address immediately because most of the code cannot handle hostnames
            // TODO error handling
            var serverIP = request.ipv4;
            var serverPort = request.port;
            var numSimulatedClients = request.numberOfClients;
            var signalingUrl = request.signalingUrl;

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            var worlds = new NativeList<WorldUnmanaged>(16, Allocator.Temp);
            var worldName = role;

            // TODO check our current role and server we're connected to
            if (role == "thinClient")
            {
            }
            else if (role == "client")
            {
                // Stop the running thin client world
                var thinClientWorld = BootstrapInstance.instance.worlds.Find(w => w.Name == "StreamingGuestWorld");
                DeploymentConfigHelpers.HandleWorldAction(thinClientWorld, null, 0, WorldAction.Stop);

                // Create, start, and connect a regular client world
                var bootstrap = BootstrapInstance.instance;
                bootstrap.SetupWorlds(MultiplayStreamingRoles.Disabled, GameBootstrap.BootstrapPlayTypes.Client,
                    ref worlds, numSimulatedClients, autoStart: true, autoConnect: true, serverIP, serverPort, signalingUrl, worldName);
                DeploymentReceiveSystem.GenerateAuthoringSceneLoadRequests(commandBuffer, ref worlds);
                commandBuffer.Playback(EntityManager);
            }
            else
            {
                Debug.LogWarning($"Unknown remote deployment request role: {request.role}");
            }
        }
    }
}
