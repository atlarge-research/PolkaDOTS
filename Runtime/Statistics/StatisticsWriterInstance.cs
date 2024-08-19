using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using WebSocketSharp;


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
            if (!ApplicationConfig.LogStats)
            {
                return;
            }

            if (File.Exists(ApplicationConfig.StatsFilePath))
            {
                // Don't overwrite existing data
                Debug.LogWarning($"Stats file {ApplicationConfig.StatsFilePath.Value} already exists. Appending to file.");
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
                if (!instance.written)
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

        /// <summary>
        /// An HTTP client used to push metrics to a running
        /// <see href="https://github.com/influxdata/telegraf">Telegraf</see> instance with
        /// an active <see href="https://github.com/influxdata/telegraf/blob/release-1.31/plugins/inputs/http_listener_v2/README.md">HTTP Listener plugin</see>.
        /// </summary>
        private HttpClient _httpClient = null;
        private StringBuilder _httpRecordBuffer = new StringBuilder();
        private DateTime _timeOfLastPostRequest = DateTime.UnixEpoch;
        private Task<HttpResponseMessage> _lastPostRequest;
        private string _telegrafHTTPServerURL;
        private static DateTime EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);


        public StatisticsWriter()
        {
            /*
             * The code below uses the Unity Profiler to obtain performance metrics.
             * The strings passed as 'name' are magic strings that match built-in markers in the Unity profiler.
             * To learn more about the Unity profiler and the availability of markers, see the following pages of the
             * Unity documentation:
             *
             * - https://docs.unity3d.com/ScriptReference/Unity.Profiling.ProfilerRecorder.html
             * - https://docs.unity3d.com/Manual/ProfilerMemory.html#markers.html
             */

            if (ApplicationConfig.StatsHttpUrl.Value.Length > 0)
            {
                _httpClient = new HttpClient();
                _telegrafHTTPServerURL = ApplicationConfig.StatsHttpUrl;
            }

            recorders = new Dictionary<string, ProfilerRecorder>();
            written = false;

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
            recorders.Add(name, ProfilerRecorder.StartNew(category, name));
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

            PushMetricsToTelegrafOverHTTP();
        }

        /// <summary>
        /// <para>
        /// This code writes metrics to a string buffer using the
        /// <see href="https://docs.influxdata.com/influxdb/v1/write_protocols/line_protocol_tutorial/">influx line protocol</see>
        /// </para>
        ///
        /// <para>
        /// Every 10 seconds, it sends the content of the buffer to a running Telegraf instance which should be
        /// configured to run a HTTP Listener v2 plugin.
        /// </para>
        ///
        /// <para>
        /// IMPORTANT: This method only does something when the -StatsHttpUrl argument is passed to the game!
        /// </para>
        /// </summary>
        private void PushMetricsToTelegrafOverHTTP()
        {
            if (_httpClient is not null)
            {
                _httpRecordBuffer.Append("opencraft_stats ");
                var first = true;
                foreach (var (name, rec) in recorders)
                {
                    if (!first)
                    {
                        _httpRecordBuffer.Append(",");
                    }
                    else
                    {
                        first = false;
                    }

                    _httpRecordBuffer.Append($"{name.Replace(" ", "_").ToLower()}={rec.LastValue}i");
                }

                var timeSinceEpoch = DateTime.UtcNow - EPOCH;
                _httpRecordBuffer.AppendLine($" {timeSinceEpoch.Ticks * 100L}");

                var timeSinceLastRequest = DateTime.UtcNow - _timeOfLastPostRequest;
                int sendIntervalInSeconds = ApplicationConfig.StatsHttpSendInterval;
                if (timeSinceLastRequest.Seconds > sendIntervalInSeconds &&
                    ( _lastPostRequest is null || _lastPostRequest.IsCompleted ))
                {
                    var contentString = _httpRecordBuffer.ToString();
                    var req = _httpClient.PostAsync(
                        _telegrafHTTPServerURL,
                        new StringContent(contentString));
                    _httpRecordBuffer.Clear();
                    // Print any errors to debug after request completes.
                    req.ContinueWith(t =>
                    {
                        if (t.Exception is not null)
                        {
                            Debug.LogWarning($"Failed to push metrics to Telegraf. Exception: {t.Exception}");
                            Debug.LogWarning($"Tried to POST: {contentString}");
                        }
                        else if (t.IsCompletedSuccessfully &&
                                 !t.Result.IsSuccessStatusCode)
                        {
                            var res = t.Result;
                            Debug.LogWarning(
                                $"Failed to push metrics to Telegraf. HTTP Status code: {res.StatusCode}.");
                            Debug.LogWarning($"Tried to POST: {contentString}");
                        }
                    });

                    _timeOfLastPostRequest = DateTime.UtcNow;
                    _lastPostRequest = req;
                }
            }
        }

        public void WriteStatisticsBuffer()
        {
            if (written)
            {
                Debug.LogWarning("Already wrote stats to file!");
                return;
            }

            Debug.Log("Writing stats to file");
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
    }
}
