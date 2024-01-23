using System;
using PolkaDOTS.Deployment;
using Unity.Serialization;
using UnityEngine;

namespace PolkaDOTS.Emulation
{
    /// <summary>
    /// Runs player emulation
    /// </summary>
    public class Emulation : MonoBehaviour
    {
        public InputRecorder inputRecorder;
        [DontSerialize]public EmulationType emulationType = EmulationType.None;

        public void Pause()
        {
            if ((emulationType & EmulationType.Playback) == EmulationType.Playback)
            {
                inputRecorder.PauseReplay();
            }
        }

        public void Play()
        {
            if ((emulationType & EmulationType.Playback) == EmulationType.Playback)
            {
                inputRecorder.StartReplay();
            }
        }
        

        public void initializeSimulation()
        {
            // TODO
        }
        

        public void initializePlayback()
        {
            if (inputRecorder.replayIsRunning)
            {
                Debug.Log($"Resuming input playback from {ApplicationConfig.EmulationFile.Value}");
                Play();
                return;
            }
            try
            {
                inputRecorder.LoadCaptureFromFile(ApplicationConfig.EmulationFile.Value);
            }
            catch (Exception ex)
            {
                Debug.Log("Failed to load input playback file with error:");
                Debug.LogError(ex.ToString());
                return;
            } 
            Debug.Log($"Starting input playback from {ApplicationConfig.EmulationFile.Value}");
            inputRecorder.StartReplay();
        }
        
        public void initializeRecording()
        {
            Debug.Log("Starting input capturing!");
            //inputRecorder.gameObject.SetActive(true);
            inputRecorder.StartCapture();
        }
        
        private void OnApplicationQuit()
        {
            if ((emulationType & EmulationType.Record) == EmulationType.Record)
            {
                Debug.Log($"Saving capture file to {ApplicationConfig.EmulationFile.Value}");
                inputRecorder.StopCapture();
                inputRecorder.SaveCaptureToFile(ApplicationConfig.EmulationFile.Value);
            }

            if ((emulationType & EmulationType.Playback) == EmulationType.Playback)
            {
                inputRecorder.StopReplay();
            }
        }
    }
    
}