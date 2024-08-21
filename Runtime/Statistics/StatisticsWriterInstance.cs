﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
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
        private void Awake()
        {
            if (!ApplicationConfig.LogStats) {
                return;
            }

            if (File.Exists(ApplicationConfig.StatsFilePath)){
                // Don't overwrite existing data
                Debug.Log($"Stats file {ApplicationConfig.StatsFilePath.Value} already exists. overwriting -logStats.");
                File.Delete(ApplicationConfig.StatsFilePath);
                return;
            }

           
            Debug.Log("Creating statistics writer!");
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
        
        public StatisticsWriter()
        {
            recorders = new Dictionary<string, ProfilerRecorder>();
            written = false;
            // Add generic metrics
            AddStatisticRecorder("Main Thread", ProfilerCategory.Internal);
            AddStatisticRecorder("System Used Memory", ProfilerCategory.Memory);
            AddStatisticRecorder("GC Reserved Memory", ProfilerCategory.Memory);
            AddStatisticRecorder("Total Reserved Memory", ProfilerCategory.Memory);
            
            AddStatisticRecorder("NFE Snapshot Tick", ProfilerCategory.Network);
            AddStatisticRecorder("NFE Snapshot Size (bits)", ProfilerCategory.Network);
            AddStatisticRecorder("NFE RTT", ProfilerCategory.Network);
            AddStatisticRecorder("NFE Jitter", ProfilerCategory.Network);
            
            AddStatisticRecorder("Multiplay FPS", ProfilerCategory.Scripts);
            AddStatisticRecorder("Multiplay BitRate In", ProfilerCategory.Scripts);
            AddStatisticRecorder("Multiplay BitRate Out", ProfilerCategory.Scripts);
            AddStatisticRecorder("Multiplay RTT (ms)", ProfilerCategory.Scripts);
            AddStatisticRecorder("Multiplay PacketLoss", ProfilerCategory.Scripts);
            //AddStatisticRecorder("Number of Terrain Areas (Client)", ProfilerCategory.Scripts);
            //AddStatisticRecorder("Number of Terrain Areas (Server)", ProfilerCategory.Scripts);
            
            /*foreach (var (name, recorder) in recorders)
            {
                if (!recorder.Valid)
                    Debug.LogWarning($"Recorder [{name}] is invalid!"); 
            }*/
        }

        public void AddStatisticRecorder(string name, ProfilerCategory category)
        {
            if (!recorders.ContainsKey(name))
            {
                recorders.Add(name, ProfilerRecorder.StartNew(category, name));
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
            sb.Append("\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
        
        public void Update()
        {
            var sb = new StringBuilder($"{Time.frameCount};");
            foreach (var (_, rec) in recorders)
                sb.Append($"{rec.LastValue.ToString()};");
            sb.Append("\n");
            metricsBuffer += sb.ToString();

        }

        public void WriteStatisticsBuffer()
        {
            if (written)
            {
                Debug.LogWarning("Already wrote stats to file!");
                return;
            }

            Debug.Log($"Writing stats to file: {ApplicationConfig.StatsFilePath.Value}");
            try
            {
                // Write header
                if (!File.Exists(ApplicationConfig.StatsFilePath))
                {
                    using (var file = File.Open(ApplicationConfig.StatsFilePath, FileMode.Create))
                    {
                        file.Write(HeaderToBytes());
                    }
                }
                
                // Write data
                using (var file = File.Open(ApplicationConfig.StatsFilePath, FileMode.Append))
                {
                    file.Write(Encoding.ASCII.GetBytes(metricsBuffer));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write statistics to {ApplicationConfig.StatsFilePath} with exception {e}");
            }
            
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