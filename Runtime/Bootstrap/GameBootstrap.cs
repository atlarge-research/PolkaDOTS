using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using PolkaDOTS.Configuration;
using PolkaDOTS.Debugging;
using PolkaDOTS.Deployment;
using PolkaDOTS.Emulation;
using PolkaDOTS.Multiplay;
using PolkaDOTS.Networking;
using PolkaDOTS.Statistics;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace PolkaDOTS.Bootstrap
{
    public static class BootstrapInstance
    {
        public static GameBootstrap instance; // Reference to the ICustomBootstrap
    }

    /// <summary>
    /// Reads configuration locally or from remote and sets ups deployment or game worlds accordingly
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class GameBootstrap : ICustomBootstrap
    {
        // TODO use dict<str worldName, World world>
        public List<World> worlds;
        private World deploymentWorld;
        private List<Type> _uniqueSystemTypes;
        public bool Initialize(string defaultWorldName)
        {
            // Get the global command line reader class
            if (!CmdArgsReader.ParseCmdArgs())
            {
                Application.Quit();
                return false;
            }

            Debug.Log(ApplicationConfig.ToString());

            // If there is a start delay, wait to perform bootstrap initialization
            if (ApplicationConfig.Delay.Value > 0)
            {
                int milliseconds = ApplicationConfig.Delay.Value * 1000;
                Thread.Sleep(milliseconds);
            }

            worlds = new List<World>();
            _uniqueSystemTypes = new List<Type>();

            // Pre world creation initialization
            BootstrapInstance.instance = this;
            NetworkStreamReceiveSystem.DriverConstructor = new NetCodeDriverConstructor();
            // Deployment world handles both requesting and answering configuration requests
            if (ApplicationConfig.GetRemoteConfig || ApplicationConfig.ImportDeploymentConfig.Value is not null)
            {
                deploymentWorld = SetupDeploymentServiceWorld();
                // Create connection listen/connect request in deployment world
                if (ApplicationConfig.GetRemoteConfig)
                {
                    // Deployment client
                    // Parse deployment network endpoint
                    if (!NetworkEndpoint.TryParse(ApplicationConfig.DeploymentURL, (ushort)ApplicationConfig.DeploymentPort,
                            out NetworkEndpoint deploymentEndpoint,
                            NetworkFamily.Ipv4))
                    {
                        Debug.Log($"Couldn't parse deployment URL of {ApplicationConfig.DeploymentURL}:{ApplicationConfig.DeploymentPort}, falling back to 127.0.0.1!");
                        deploymentEndpoint = NetworkEndpoint.LoopbackIpv4.WithPort((ushort)ApplicationConfig.DeploymentPort);
                    }

                    Entity connReq = deploymentWorld.EntityManager.CreateEntity();
                    deploymentWorld.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestConnect { Endpoint = deploymentEndpoint });
                }
                else
                {
                    // Deployment server
                    Entity listenReq = deploymentWorld.EntityManager.CreateEntity();
                    deploymentWorld.EntityManager.AddComponentData(listenReq,
                        new NetworkStreamRequestListen { Endpoint = NetworkEndpoint.AnyIpv4.WithPort((ushort)ApplicationConfig.DeploymentPort) });
                }
            }
            else
            {
                // Use only local configuration
                SetupWorldsFromLocalConfig();
            }

            // Add a new world with a remote control system if specified in command-line arguments
            if (ApplicationConfig.RemoteControl)
            {
                var rcWorld = new World("remoteControlWorld", WorldFlags.None);
                var systems = new NativeList<SystemTypeIndex>(64, Allocator.Temp)
                {
                    TypeManager.GetSystemTypeIndex(typeof(RemoteControlledDeploymentSystem))
                };
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(rcWorld, systems);
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(rcWorld);
            }

            return true;
        }

        /// <summary>
        /// Creates a world with a minimal set of systems necessary for Netcode for Entities to connect, and the
        /// Deployment systems <see cref="DeploymentReceiveSystem"/> and <see cref="DeploymentServiceSystem"/>.
        /// </summary>
        /// <returns></returns>
        private World SetupDeploymentServiceWorld()
        {
            Debug.Log("Creating deployment world");
            //BootstrappingConfig.DeploymentClientConnectAddress = deploymentEndpoint;
            //BootstrappingConfig.DeploymentPort = ApplicationConfig.DeploymentPort;
            //BootstrappingConfig.DeploymentServerListenAddress = NetworkEndpoint.AnyIpv4.WithPort(BootstrappingConfig.DeploymentPort);

            // Create the world
            var flags = ApplicationConfig.GetRemoteConfig ? WorldFlagsExtension.DeploymentClient : WorldFlagsExtension.DeploymentServer;
            var world = new World("DeploymentWorld", (WorldFlags)flags);
            // Fetch all editor and package (but not user-added) systems
            var filterFlags = ApplicationConfig.GetRemoteConfig ? WorldSystemFilterFlags.ClientSimulation : WorldSystemFilterFlags.ServerSimulation;
            var systems = TypeManager.GetUnitySystemsTypeIndices(filterFlags);

            // Remove built-in NetCode world initialization
            var filteredSystems = new NativeList<SystemTypeIndex>(64, Allocator.Temp);
            foreach (var system in systems)
            {
                var systemName = TypeManager.GetSystemName(system);
                if (systemName.Contains((FixedString64Bytes)"ConfigureThinClientWorldSystem")
                    || systemName.Contains((FixedString64Bytes)"ConfigureClientWorldSystem")
                    || systemName.Contains((FixedString64Bytes)"ConfigureServerWorldSystem"))
                {
                    continue;
                }

                filteredSystems.Add(system);
            }

            // Add deployment service systems
            var deploymentClassType = ApplicationConfig.GetRemoteConfig ? typeof(DeploymentReceiveSystem) : typeof(DeploymentServiceSystem);
            filteredSystems.Add(TypeManager.GetSystemTypeIndex(deploymentClassType));

            // Add Unity Scene System for managing GUIDs
            filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(SceneSystem)));
            // Add NetCode monitor
            filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(ConnectionMonitorSystem)));

            // Add AuthoringSceneLoader
            filteredSystems.Add(TypeManager.GetSystemTypeIndex(typeof(AuthoringSceneLoaderSystem)));

            // Re-sort the systems
            TypeManager.SortSystemTypesInCreationOrder(filteredSystems);


            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, filteredSystems);

            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }


        public void SetupWorldsFromLocalConfig()
        {
            NativeList<WorldUnmanaged> newWorlds = new NativeList<WorldUnmanaged>(Allocator.Temp);
            SetupWorlds(ApplicationConfig.MultiplayStreamingRole, ApplicationConfig.PlayType, ref newWorlds,
                ApplicationConfig.NumSimulatedPlayers, autoStart: true, autoConnect: true, ApplicationConfig.ServerUrl, (ushort)ApplicationConfig.ServerPort, ApplicationConfig.SignalingUrl);
        }


        /// <summary>
        /// Sets up bootstrapping details and creates local worlds
        /// </summary>
        public void SetupWorlds(MultiplayStreamingRoles mRole, BootstrapPlayTypes playTypes, ref NativeList<WorldUnmanaged> worldReferences,
            int numSimulatedClients, bool autoStart, bool autoConnect, string serverUrl, ushort serverPort, string signalingUrl, string worldName = "")
        {

            Debug.Log($"Setting up worlds with playType {playTypes} and streaming role {mRole}");

            List<World> newWorlds = new List<World>();

            // ================== SETUP WORLDS ==================

            ApplicationConfig.MultiplayStreamingRole.SetValue(mRole);


            if (playTypes == BootstrapPlayTypes.StreamedClient)
            {
                mRole = MultiplayStreamingRoles.Guest;
            }

            //Client
            if (playTypes is BootstrapPlayTypes.Client or BootstrapPlayTypes.ClientAndServer)
            {
                // Streamed client
                if (mRole == MultiplayStreamingRoles.Guest)
                {
                    var world = CreateStreamedClientWorld(worldName);
                    newWorlds.Add(world);
                }

                if (mRole != MultiplayStreamingRoles.Guest)
                {
                    var world = CreateDefaultClientWorld(worldName, mRole == MultiplayStreamingRoles.Host, mRole == MultiplayStreamingRoles.CloudHost);
                    newWorlds.Add(world);
                }
            }


            // Simulated client
            if (playTypes == BootstrapPlayTypes.SimulatedClient && numSimulatedClients > 0)
            {
                var worldList = CreateSimulatedClientWorlds(numSimulatedClients, worldName);
                newWorlds.AddRange(worldList);

            }

            // Server
            if (playTypes is BootstrapPlayTypes.Server or BootstrapPlayTypes.ClientAndServer)
            {
                var world = CreateDefaultServerWorld(worldName);
                newWorlds.Add(world);
            }

            foreach (var world in newWorlds)
            {
                worldReferences.Add(world.Unmanaged);
                worlds.Add(world);

                if (autoStart)
                    SetWorldToUpdating(world);
            }


            if (autoConnect)
                ConnectWorlds(mRole, playTypes, serverUrl, serverPort, signalingUrl);
        }


        public void SetWorldToUpdating(World world)
        {
#if UNITY_DOTSRUNTIME
            CustomDOTSWorlds.AppendWorldToClientTickWorld(world);
#else
            if (!ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(world))
            {
                Debug.Log($"Adding world {world.Name} to update list");
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
            }
#endif
        }

        /// <summary>
        /// Adds worlds to update list
        /// </summary>
        public void StartWorlds(bool autoConnect, MultiplayStreamingRoles mRole, BootstrapPlayTypes playTypes, string serverUrl,
            ushort serverPort, string signalingUrl)
        {

            Debug.Log($"Starting worlds with playType {playTypes} and streaming role {mRole}");
            // ================== SETUP WORLDS ==================
            foreach (var world in worlds)
            {
                // Client worlds
                if (playTypes is BootstrapPlayTypes.Client or BootstrapPlayTypes.ClientAndServer
                    && world.IsClient() && !world.IsSimulatedClient() && !world.IsStreamedClient())
                {
                    SetWorldToUpdating(world);
                }
                // Streamed guest client worlds
                if (playTypes is BootstrapPlayTypes.Client or BootstrapPlayTypes.ClientAndServer
                    && mRole == MultiplayStreamingRoles.Guest
                    && world.IsStreamedClient())
                {
                    SetWorldToUpdating(world);
                }
                // Simulated client worlds
                if (playTypes == BootstrapPlayTypes.SimulatedClient && world.IsSimulatedClient())
                {
                    SetWorldToUpdating(world);
                }
                // Server worlds
                if (playTypes is BootstrapPlayTypes.Server or BootstrapPlayTypes.ClientAndServer
                    && world.IsServer())
                {
                    SetWorldToUpdating(world);
                }
            }

            if (autoConnect)
                ConnectWorlds(mRole, playTypes, serverUrl, serverPort, signalingUrl);
        }


        /// <summary>
        /// Connects worlds with types specified through playTypes and streaming roles
        /// </summary>
        public void ConnectWorlds(MultiplayStreamingRoles mRole, BootstrapPlayTypes playTypes, string serverUrl,
            ushort serverPort, string signalingUrl)
        {

            Debug.Log($"Connecting worlds with playType {playTypes} and streaming role {mRole}");
            // ================== SETUP WORLDS ==================
            foreach (var world in worlds)
            {
                // Client worlds
                if (playTypes is BootstrapPlayTypes.Client or BootstrapPlayTypes.ClientAndServer
                    && world.IsClient() && !world.IsSimulatedClient() && !world.IsStreamedClient()
                    && mRole != MultiplayStreamingRoles.Guest)
                {
                    Entity connReq = world.EntityManager.CreateEntity();
                    var ips = Dns.GetHostAddresses(serverUrl);
                    Assert.IsTrue(ips.Length > 0);
                    NetworkEndpoint.TryParse(ips[0].ToString(), serverPort,
                        out NetworkEndpoint gameEndpoint, NetworkFamily.Ipv4);
                    Debug.Log($"Created connection request for {gameEndpoint}");
                    world.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestConnect { Endpoint = gameEndpoint });
                }
                // Streamed guest client worlds
                if ((playTypes == BootstrapPlayTypes.Client || playTypes == BootstrapPlayTypes.ClientAndServer)
                    && mRole == MultiplayStreamingRoles.Guest
                    && world.IsStreamedClient())
                {
                    Entity connReq = world.EntityManager.CreateEntity();

                    Debug.Log($"Creating multiplay guest connect with endpoint {signalingUrl}");
                    world.EntityManager.AddComponentData(connReq,
                        new StreamedClientRequestConnect { url = new FixedString512Bytes(signalingUrl) });
                }
                // Simulated client worlds
                if (playTypes == BootstrapPlayTypes.SimulatedClient && world.IsSimulatedClient())
                {
                    Entity connReq = world.EntityManager.CreateEntity();
                    var ips = Dns.GetHostAddresses(serverUrl);
                    Assert.IsTrue(ips.Length > 0);
                    NetworkEndpoint.TryParse(ips[0].ToString(), serverPort,
                        out NetworkEndpoint gameEndpoint, NetworkFamily.Ipv4);
                    Debug.Log($"Created connection request for {gameEndpoint}");
                    world.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestConnect { Endpoint = gameEndpoint });
                }
                // Server worlds
                if ((playTypes == BootstrapPlayTypes.Server || playTypes == BootstrapPlayTypes.ClientAndServer)
                    && world.IsServer())
                {
                    Entity connReq = world.EntityManager.CreateEntity();
                    var listenNetworkEndpoint = NetworkEndpoint.AnyIpv4.WithPort(serverPort);
                    Debug.Log($"Created listen request for {listenNetworkEndpoint}");
                    world.EntityManager.AddComponentData(connReq,
                        new NetworkStreamRequestListen { Endpoint = listenNetworkEndpoint });
                }

            }
        }

        public World CreateDefaultClientWorld(string worldName, bool isHost = false, bool isCloudHost = false)
        {
            WorldSystemFilterFlags flags = WorldSystemFilterFlags.ClientSimulation |
                                           WorldSystemFilterFlags.Presentation;
            // If we aren't running graphics on this client (e.g. a simulated player) then don't run presentation systems.
            if (ApplicationConfig.NoGraphics.Value)
            {
                flags = WorldSystemFilterFlags.ClientSimulation;
            }
            var clientSystems = DefaultWorldInitialization.GetAllSystems(flags);
            // Disable the default NetCode world configuration
            var filteredClientSystems = new List<Type>();
            foreach (var system in clientSystems)
            {
                if (system.Name == "ConfigureThinClientWorldSystem" || system.Name == "ConfigureClientWorldSystem")
                    continue;
                if (system.IsDefined(typeof(UniqueSystemAttribute), false))
                {
                    Debug.Log($"Adding type with UniqueSystemAttribute: {system.Name}");
                    if (_uniqueSystemTypes.Contains(system))
                        continue;
                    _uniqueSystemTypes.Add(system);
                }
                filteredClientSystems.Add(system);
            }

            if (isHost)
            {
                if (worldName == "")
                    worldName = "HostClientWorld";
                return CreateClientWorld(worldName, (WorldFlags)WorldFlagsExtension.HostClient,
                    filteredClientSystems);
            }
            else if (isCloudHost)
            {
                if (worldName == "")
                    worldName = "CloudHostClientWorld";
                return CreateClientWorld(worldName, (WorldFlags)WorldFlagsExtension.CloudHostClient,
                    filteredClientSystems);
            }
            else
            {
                if (worldName == "")
                    worldName = "ClientWorld";
                return CreateClientWorld(worldName, WorldFlags.GameClient, filteredClientSystems);
            }

        }

        public World CreateStreamedClientWorld(string worldName)
        {
            var systems = new List<Type> { typeof(MultiplayInitSystem), typeof(EmulationInitSystem), typeof(TakeScreenshotSystem), typeof(UpdateWorldTimeSystem), typeof(StopWorldSystem) };
            var filteredClientSystems = new List<Type>();
            foreach (var system in systems)
            {
                if (system.IsDefined(typeof(UniqueSystemAttribute), false))
                {
                    if (_uniqueSystemTypes.Contains(system))
                        continue;
                    _uniqueSystemTypes.Add(system);
                }
                filteredClientSystems.Add(system);
            }
            if (worldName == "")
                worldName = "StreamingGuestWorld";
            return CreateClientWorld(worldName, (WorldFlags)WorldFlagsExtension.StreamedClient, filteredClientSystems);
        }


        public List<World> CreateSimulatedClientWorlds(int numSimulatedClients, string worldName)
        {
            List<World> newWorlds = new List<World>();

            // Re-use Netcode for EntitiesSim ThinClient systems
            //var thinClientSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ThinClientSimulation);
            var thinClientSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation);

            // Disable the default NetCode world configuration
            var filteredThinClientSystems = new List<Type>();
            var filteredThinClientSystemsWithUnique = new List<Type>();
            foreach (var system in thinClientSystems)
            {
                if (system.Name is "ConfigureThinClientWorldSystem" or "ConfigureClientWorldSystem")
                    continue;
                if (system.IsDefined(typeof(UniqueSystemAttribute), false))
                {
                    if (_uniqueSystemTypes.Contains(system))
                        continue;
                    _uniqueSystemTypes.Add(system);
                    filteredThinClientSystemsWithUnique.Add(system);
                    continue;
                }
                filteredThinClientSystems.Add(system);
                filteredThinClientSystemsWithUnique.Add(system);
            }


            if (worldName == "")
                worldName = "SimulatedClientWorld_";
            for (var i = 0; i < numSimulatedClients; i++)
            {
                List<Type> systems;
                if (i == 0)
                {
                    // Only add unique systems to the first world
                    systems = filteredThinClientSystemsWithUnique;
                }
                else
                {
                    systems = filteredThinClientSystems;
                }
                var w = CreateClientWorld(worldName + $"{ApplicationConfig.UserID.Value + i}",
                    (WorldFlags)WorldFlagsExtension.SimulatedClient, systems);
                w.ID = ApplicationConfig.UserID.Value + i;
                // Add delay
                var e = w.EntityManager.CreateEntity();
                w.EntityManager.AddComponentData(e, new StartGameStreamDelay { delay = ApplicationConfig.SimulatedJoinInterval.Value * i });

                newWorlds.Add(w);
            }

            return newWorlds;
        }

        public World CreateDefaultServerWorld(string worldName)
        {
            // todo: specify what systems in the server world
            var serverSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);

            var filteredServerSystems = new List<Type>();
            foreach (var system in serverSystems)
            {
                if (system.Name == "ConfigureServerWorldSystem")
                    continue;
                if (system.IsDefined(typeof(UniqueSystemAttribute), false))
                {
                    if (_uniqueSystemTypes.Contains(system))
                        continue;
                    _uniqueSystemTypes.Add(system);
                }
                filteredServerSystems.Add(system);
            }

            if (worldName == "")
                worldName = "ServerWorld";
            return CreateServerWorld(worldName, WorldFlags.GameServer, filteredServerSystems);
        }

        /// <summary>
        /// Utility method for creating new client worlds.
        /// </summary>
        /// <param name="name">The client world name</param>
        /// <param name="flags">WorldFlags for the created world</param>
        /// <param name="systems">List of systems the world will include</param>
        /// <returns></returns>
        public World CreateClientWorld(string name, WorldFlags flags, IReadOnlyList<Type> systems)
        {
            var world = new World(name, flags);

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);


            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }

        /// <summary>
        /// Utility method for creating a new server world.
        /// Can be used in custom implementations of `Initialize` as well as in your game logic (in particular client/server build)
        /// when you need to create server programmatically (ex: frontend that allow selecting the role or other logic).
        /// </summary>
        /// <param name="name">The server world name</param>
        /// <returns></returns>
        public World CreateServerWorld(string name, WorldFlags flags, IReadOnlyList<Type> systems)
        {
            var world = new World(name, flags);

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);


#if UNITY_DOTSRUNTIME
            CustomDOTSWorlds.AppendWorldToServerTickWorld(world);
#else
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
#endif

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }


        public void ExitGame()
        {
            if (ApplicationConfig.LogStats)
                StatisticsWriterInstance.WriteStatisticsBuffer();

#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
                Application.Quit();
#endif
        }

        [Serializable]
        public enum BootstrapPlayTypes
        {
            /// <summary>
            /// The application can run as client, server or both. By default, both client and server world are created
            /// and the application can host and play as client at the same time.
            /// <para>
            /// This is the default modality when playing in the editor, unless changed by using the play mode tool.
            /// </para>
            /// </summary>
            ClientAndServer = 0,
            ServerAndClient = 0, // Aliases
            ClientServer = 0,
            ServerClient = 0,
            /// <summary>
            /// The application run as a client. Only clients worlds are created and the application should connect to
            /// a server.
            /// </summary>
            Client = 1,
            /// <summary>
            /// The application run as a server. Usually only the server world is created and the application can only
            /// listen for incoming connection.
            /// </summary>
            Server = 2,
            /// <summary>
            /// The application run as a thin client. Only connections to a server and input emulation are performed.
            /// No frontend systems are run.
            /// </summary>
            StreamedClient = 3,
            StreamClient = 3,
            GuestClient = 3,
            /// <summary>
            /// Minimal client for running player emulation/simulated with no frontend, useful for experiments and debugging
            /// </summary>
            SimulatedClient = 4,
            SimulateClient = 4,
            SimulationClient = 4,
            SimulatedClients = 4,
            SimulateClients = 4,
            SimulationClients = 4
        }
    }


}


