# PolkaDOTS
PolkaDOTS is a framework for dynamic, differentiated deployment in online games. It is built on and extends the Unity Data Oriented
Technology Stack (DOTS) as well as the Unity Netcode For Entities (NFE) package. At the moment, PolkaDOTS only supports
Unity 2022 LTS.

## Features
* **Render Streaming** enables client builds to act as cloud gaming hosts to stream the game.

* **Multiplay** lets a single client act act as a render streaming host to more than one guest client, while also running a local player.  

* **Dynamic Deployment** allows remote configuration and reconfiguration of game instances at runtime, orchestrated
by the [DeploymentSystem](./Runtime/Deployment/DeploymentSystem.cs)

* **Player Emulation** through input playback for automated testing and benchmarking. More advanced emulation can
be implemented through adding a player simulation system.

* **Performance monitoring** leverages the Unity Profiler features to monitor performance of game instances in realtime,
and write them to analyzable logs. 

## Installation
Clone this repository into your Unity 2022 project as `<ProjectRoot>/Packages/PolkaDOTS` and open your project.
The Unity Package Manager should automatically install dependency packages. 

## Usage
PolkaDOTS overrides the default functionality of DOTS and NFE systems through the custom bootstrap
in [GameBootstrap.cs](./Runtime/Bootstrap/GameBootstrap.cs). To enable this, under 
`Project Settings -> Player -> Other Settings -> Scriping Define Symbols` add `UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP` and
`NETCODE_DEBUG`, then add the `/Prefabs/Bootstrap/GameBootstrap` prefab to your base scene.

### Player Frontend
The Render Streaming, Multiplay and Player Emulation functionalities require the use of a specific camera rendering and input collection
setup. This setup can be added to a scene drag-and-drop by adding the `Prefabs/Player/PolkaDOTS_Frontend` prefab. 

#### Player Object and Entity Linkage
The PolkaDOTS framework provides two player GameObject prefabs, `Prefabs/Player/MultiplayPlayer` and `Prefabs/Player/MultiplayGuest`,
which are instantiated by [Multiplay.cs](./Runtime/Player/Multiplay/Multiplay.cs) on clients for local and remote (guest) players.
However, the PolkaDOTS framework does _not_ automatically link these instantiated GameObjects to any Player entities.
Therefore, it is necessary to create a system to link the player GameObjects to your projects Player entities, to correctly 
gather input and assign it to the correct entity, and to move the GameObject according to the Player entity movement.

### Configuration
PolkaDOTS adds a set of command line arguments in [ApplicationConfig.cs](./Runtime/Configuration/ApplicationConfig.cs). These
arguments are read by the [ConfigParser.cs](./Runtime/Configuration/ConfigParser.cs). You can add additional command line
arguments by creating a new static class with the `ArgumentClass` attribute added, and adding to this class argument fields 
of the types in [ConfigParser.cs](./Runtime/Configuration/ConfigParser.cs). 

To use command line arguments in-editor, modify the `Editor Args` field on the `Editor Cmd Args` script, attached to the `GameBootstrap` prefab.

### Performance Monitoring
To enable PolkaDOTS performance monitoring, add the `Prefabs/Statistics/Statistics` prefab to the base scene of your project.
This prefab contains the `Runtime Profiler Manager` and the `Statistics Writer`.

The [Runtime Profiler Manager](./Runtime/Statistics/RuntimeProfilerManager.cs) manages the Unity Profiler's built-in
capacity to write performance metrics to `.raw` files by setting a new file in intervals of frames, as the Profiler can
only only up to 2000 frames in memory at a time. It is recommended to use the `Statistics Writer` instead, as relying on
the `.raw` files entails having to either view them individually in the Unity Profiler Window, or attempt to convert them
to `.csv` for other forms of visualization. It also does not support user-added custom profiler counters.

The [Statistics Writer Instance](./Runtime/Statistics/StatisticsWriterInstance.cs) collects performance metrics when running
the game, and writes them directly to a single persistent `.csv` file. This is useful for conducting benchmarking of multiple
game instances. To collect additional metrics, create a new profiler counter and add it to the writer instance using `AddStatisticRecorder()`.
It is then necessary to create a system to record data into the new counter.

<details>
  <summary>Adding a new profiler counter and module example</summary>

```csharp
 public class GameStatistics
{
    public static readonly ProfilerCategory GameStatisticsCategory = ProfilerCategory.Scripts;
    
    public const string NumPlayersName = "Number of Players";
    
    public static readonly ProfilerCounterValue<int> NumPlayers =
        new ProfilerCounterValue<int>(GameStatisticsCategory, NumPlayersName, ProfilerMarkerDataUnit.Count);
}

[ProfilerModuleMetadata("Game Statistics")] 
public class GameProfilerModule : ProfilerModule
{
    static readonly ProfilerCounterDescriptor[] k_Counters = new ProfilerCounterDescriptor[]
    {
        new ProfilerCounterDescriptor(GameStatistics.NumPlayersName, GameStatistics.GameStatisticsCategory),
    };
    public GameProfilerModule() : base(k_Counters) { }
}
```
</details>

### Entity Subscenes
PolkaDOTS modifies ECS entity world management, and removes the assumption that entity worlds will be run immediately
when created. It is thus necessary to modify ECS subscene loading so they are loaded when needed. Therefore,
to load a subscene, create a new subscene that contains only the [SceneLoader.cs](./Runtime/Deployment/SceneLoader.cs)
script, and in its `Scene` field set your original subscene. The [SceneLoader.cs](./Runtime/Deployment/SceneLoader.cs) will
either automatically load your subscene when the world is loaded, or be loaded by the `DeploymentWorld` when using
`RemoteConfig`. 

If your game has logic that depends on these subscenes being loaded before running, an easy way to check is to
add the [WorldReadyAuthoring](./Extensions/NetcodeForEntities/WorldReadyAuthoring.cs) script to your subscene. This will
add an entity with the `WorldReady` component to the world when the subscene is loaded.
