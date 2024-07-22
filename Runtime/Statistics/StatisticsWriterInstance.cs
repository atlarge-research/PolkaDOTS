using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;



namespace PolkaDOTS.Statistics
{
    /// <summary>
    /// Writes key statistics to a csv file
    /// </summary>

    // todo this is a (suboptimal) workaround for inability to properly convert profiler .raw files to csv.
    public class StatisticsWriterInstance : MonoBehaviour
    {
        public static StatisticsWriter instance;
        public static bool ready;

        void showAllMarkers()
        {
            var availableStatHandles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(availableStatHandles);

            StringBuilder sb = new StringBuilder(availableStatHandles.Count);
            foreach (ProfilerRecorderHandle item in availableStatHandles)
            {
                sb.Append(ProfilerRecorderHandle.GetDescription(item).Name + ",\n");
            }
            Debug.Log(sb.ToString());

            using (StreamWriter writer = new StreamWriter("C:/Users/joach/Desktop/markers.txt", true)) //// true to append data to the file
            {
                writer.Write(sb.ToString());
            }
        }

        private void Awake()
        {
            if (!ApplicationConfig.LogStats) {
                return;
            }

            //if (File.Exists(ApplicationConfig.StatsFilePath)){
            //    // Don't overwrite existing data
            //    Debug.Log($"Stats file {ApplicationConfig.StatsFilePath.Value} already exists. overwriting -logStats.");
            //    File.Delete(ApplicationConfig.StatsFilePath);
            //    return;
            //}

           
            Debug.Log("Creating statistics writer!");
            //showAllMarkers();
            instance = new StatisticsWriter();
            ready = true;
            
        }

        private void OnDisable()
        {
            if (ApplicationConfig.LogStats && ready)
            {
                // Write statistics before exit
                if(!instance.written)
                    instance.WriteStatisticsBuffer();
            }

            ready = false;
        }

        public void Update()
        {
            if (ApplicationConfig.LogStats && ready)
            {
                instance.Update();
            }
        }

        public static void WriteStatisticsBuffer()
        {
            if (instance is not null)
            {
                instance.WriteStatisticsBuffer();
            }
        }
        
    }

    public class StatisticsWriter
    {
        private Dictionary<string, ProfilerRecorder> recorders;
        
        private string metricsBuffer;
        
        public bool written;
        private FileStream statsFile;
        static ProfilerMarker stringBuildMarker = new ProfilerMarker("statisticWriterInstance.sb");
        static ProfilerMarker writeMarker = new ProfilerMarker("statisticWriterInstance.write");

        void setUpFile()
        {
            if (File.Exists(ApplicationConfig.StatsFilePath))
            {
                File.Delete(ApplicationConfig.StatsFilePath);
            }
            statsFile = File.Open(ApplicationConfig.StatsFilePath, FileMode.Create);
            Debug.Log($"[STATISTIC]{new String(Encoding.ASCII.GetChars(HeaderToBytes()))}");
            statsFile.Write(HeaderToBytes());
        }
        
        public StatisticsWriter()
        {

            recorders = new Dictionary<string, ProfilerRecorder>();
            written = false;


            // Add generic metrics
            AddStatisticRecorder("Main Thread", ProfilerCategory.Internal);
            AddStatisticRecorder("PlayerLoop", ProfilerCategory.Internal);
            AddStatisticRecorder("System Used Memory", ProfilerCategory.Memory);
            AddStatisticRecorder("GC Reserved Memory", ProfilerCategory.Memory);
            AddStatisticRecorder("Total Reserved Memory", ProfilerCategory.Memory);

            //AddStatisticRecorder("NFE Snapshot Tick", ProfilerCategory.Network);
            //AddStatisticRecorder("NFE Snapshot Size (bits)", ProfilerCategory.Network);
            //AddStatisticRecorder("NFE RTT", ProfilerCategory.Network);
            //AddStatisticRecorder("NFE Jitter", ProfilerCategory.Network);

            //AddStatisticRecorder("Multiplay FPS", ProfilerCategory.Scripts);
            //AddStatisticRecorder("Multiplay BitRate In", ProfilerCategory.Scripts);
            //AddStatisticRecorder("Multiplay BitRate Out", ProfilerCategory.Scripts);
            //AddStatisticRecorder("Multiplay RTT (ms)", ProfilerCategory.Scripts);
            //AddStatisticRecorder("Multiplay PacketLoss", ProfilerCategory.Scripts);

            AddStatisticRecorder("GameServer Opencraft.Terrain.TerrainGenerationSystem", ProfilerCategory.Scripts);
            AddStatisticRecorder("GameServer Opencraft.Terrain.TerrainToSpawn", ProfilerCategory.Scripts);
            AddStatisticRecorder("GameServer Opencraft.Terrain.TerrainStructuresSystem", ProfilerCategory.Scripts);
            AddStatisticRecorder("terrainMeshingSys", ProfilerCategory.Scripts);
            AddStatisticRecorder("GameClient Opencraft.Player.PlayerMovementSystem", ProfilerCategory.Scripts);
            AddStatisticRecorder("GameServer Opencraft.Player.PlayerMovementSystem", ProfilerCategory.Scripts);
            AddStatisticRecorder("ServerFixedUpdate", ProfilerCategory.Scripts);
            AddStatisticRecorder("SetAreaNeighborsJob (Burst)", ProfilerCategory.Scripts);
            AddStatisticRecorder("SetAreaNeighborsJob", ProfilerCategory.Scripts);
            AddStatisticRecorder("GhostUpdateSystem:UpdateJob (Burst)", ProfilerCategory.Scripts);
            AddStatisticRecorder("GhostUpdateJobCustom", ProfilerCategory.Scripts, true);
            AddStatisticRecorder("GhostUpdateSystem:UpdateJob", ProfilerCategory.Scripts);
            AddStatisticRecorder("Idle", ProfilerCategory.Scripts, true);
            AddStatisticRecorder("OculusRuntime.WaitToBeginFrame", ProfilerCategory.Scripts);
            AddStatisticRecorder("GameClient Unity.Entities.SimulationSystemGroup", ProfilerCategory.Scripts, true);
            AddStatisticRecorder("GameServer Unity.Entities.SimulationSystemGroup", ProfilerCategory.Scripts, true);
            AddStatisticRecorder("UnityEngine.CoreModule.dll!::UpdateFunction.Invoke() [Invoke]", ProfilerCategory.Scripts, true);
            AddStatisticRecorder("PostLateUpdate.FinishFrameRendering", ProfilerCategory.Scripts, true);


            AddStatisticRecorder("Number of Terrain Areas (Client)", ProfilerCategory.Scripts);
            AddStatisticRecorder("Number of Terrain Areas (Server)", ProfilerCategory.Scripts);

            foreach (var (name, recorder) in recorders)
            {
                if (!recorder.Valid)
                    Debug.LogWarning($"Recorder [{name}] is invalid!"); 
            }
            setUpFile();
        }

        public void AddStatisticRecorder(string name, ProfilerCategory category, bool mainThreadOnly = false)
        {
            if (!recorders.ContainsKey(name))
            {
                ProfilerRecorderOptions opts = ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame;
                opts = opts | (mainThreadOnly ? ProfilerRecorderOptions.CollectOnlyOnCurrentThread : ProfilerRecorderOptions.None);

                recorders.Add(name, ProfilerRecorder.StartNew(category, name, 1, opts));
            }
            else
            {
                Debug.LogWarning($"key already added {name}");
            }
        }

        private byte[] HeaderToBytes()
        {
            var sb = new StringBuilder("Frame Number;");
            foreach (var (name, _) in recorders)
                sb.Append($"{name};");
            sb.Append("dtime;\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
        
        private double getRecorderLastFrame(ProfilerRecorder recorder)
        {
            var samplesCount = recorder.Capacity;
            if (samplesCount == 0)
                return 0;

            double r = 0;
            unsafe
            {
                var samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);
                for (var i = 0; i < samplesCount; ++i)
                    r += samples[i].Value;
                r /= samplesCount;
            }

            return r;
        }

        public void Update()
        {
            stringBuildMarker.Begin();
            var sb = new StringBuilder($"{Time.frameCount};",300);
            
            foreach (var (name, rec) in recorders)
            {
                //Debug.Log($"{name}: {rec.Count}");
                //sb.Append($"{rec.LastValue.ToString()};");
                sb.Append($"{rec.LastValue};");
                //rec.Reset();
                //rec.Start();
            }
            sb.Append((Time.deltaTime).ToString() + ";");
            sb.Append("\n");

            stringBuildMarker.End();

            if (ApplicationConfig.LogStats)
            {
#if PLATFORM_ANDROID
                Debug.Log($"[STATISTIC]{sb.ToString()}");
#if UNITY_EDITOR
                statsFile.Write(Encoding.ASCII.GetBytes(sb.ToString()));
#endif
#else
                writeMarker.Begin();
                statsFile.Write(Encoding.ASCII.GetBytes(sb.ToString()));
                writeMarker.End();
            
#endif
            }

        }

        public void WriteStatisticsBuffer()
        {
            if (written)
            {
                Debug.LogWarning("Already wrote stats to file!");
                return;
            }

            Debug.Log($"Writing stats to file: {ApplicationConfig.StatsFilePath.Value}");

            //try
            //{
            //    // Write header
            //    if (!File.Exists(ApplicationConfig.StatsFilePath))
            //    {
            //        using (var file = File.Open(ApplicationConfig.StatsFilePath, FileMode.Create))
            //        {
            //            //file.Write(HeaderToBytes());
            //            Debug.Log(HeaderToBytes());
            //        }
            //    }
                
            //    // Write data
            //    using (var file = File.Open(ApplicationConfig.StatsFilePath, FileMode.Append))
            //    {
            //        //file.Write(Encoding.ASCII.GetBytes(metricsBuffer));
            //        //Debug.Log(Encoding.ASCII.GetBytes(metricsBuffer));
            //    }
            //}
            //catch (Exception e)
            //{
            //    Debug.LogError($"Failed to write statistics to {ApplicationConfig.StatsFilePath} with exception {e}");
            //}
            statsFile.Close();
            written = true;
        }
#if UNITY_EDITOR
        ~StatisticsWriter()
        {
            WriteStatisticsBuffer();
        }
#endif
    }
    

}