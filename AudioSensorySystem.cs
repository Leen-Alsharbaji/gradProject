using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(AudioListener))]
// AudioSensorySystem.cs
// Buffers in-game audio, sends it to a server for analysis, and notifies the brain on triggers.
public class AudioSensorySystem : MonoBehaviour
{
    [Header("Brain Link")]
    [Tooltip("Drag the BotBrain script here")]
    public BotBrain botBrain;

    [Header("Server Setup")]
    public string serverUrl = "http://127.0.0.1:8000/analyze-audio";

    [Header("Listening Settings")]
    [Tooltip("How many seconds of game audio to buffer before sending to Python")]
    public int recordTime = 2; 

    // Audio buffering variables
    private List<float> audioBuffer = new List<float>();
    private int sampleRate;
    private bool isRecording = false;
    private bool readyToSend = false;
    private float[] lastRecordedData;

    [System.Serializable]
    private class ServerResponse
    {
        public string status;
        public bool trigger_detected;
        public string text;
    }

    // Initialize recording and sample rate
    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        isRecording = true; 
        Debug.Log($"Audio system initialized. Sample rate: {sampleRate}Hz. Buffering {recordTime} seconds.");
    }

    // Capture audio on the audio thread; convert to mono and buffer
    void OnAudioFilterRead(float[] data, int numChannels)
    {
        if (!isRecording || botBrain.currentState != BotBrain.BotState.Patrolling) return;

        for (int i = 0; i < data.Length; i += numChannels)
        {
            float combinedSample = 0f;
            for (int c = 0; c < numChannels; c++) combinedSample += data[i + c];
            audioBuffer.Add(combinedSample / numChannels);
        }

        if (audioBuffer.Count >= sampleRate * recordTime)
        {
            lastRecordedData = audioBuffer.ToArray(); 
            audioBuffer.Clear(); 
            isRecording = false; // Pause recording while sending
            readyToSend = true;
        }
    }

    // Main thread: send buffered audio when ready
    void Update()
    {
        if (readyToSend)
        {
            readyToSend = false;
            Debug.Log($"Audio buffer full ({recordTime}s). Sending for analysis.");
            StartCoroutine(ProcessAndSendAudio(lastRecordedData, 1, sampleRate));
        }
    }

    // Send WAV to server and handle response
    IEnumerator ProcessAndSendAudio(float[] samples, int channels, int frequency)
    {
        byte[] wavData = ConvertToWAV(samples, channels, frequency);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "game_audio.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Server error: {www.error}");
            }
            else
            {
                string jsonResult = www.downloadHandler.text;
                ServerResponse response = JsonUtility.FromJson<ServerResponse>(jsonResult);

                Debug.Log($"Audio analysis result: '{response.text}'");

                if (response.trigger_detected)
                {
                    Debug.Log("Trigger detected: localizing source.");
                    Vector3 soundLocation = GetClosestSoundSourcePosition();
                    botBrain.OnHeardSound(soundLocation);
                }
            }
        }

        // Resume listening unless state changed
        audioBuffer.Clear();
        isRecording = true;
    }

    // Convert float samples to WAV bytes
    private byte[] ConvertToWAV(float[] samples, int channels, int frequency)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            short[] intData = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
            }

            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + intData.Length * 2);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(intData.Length * 2);

            foreach (var dataPoint in intData) writer.Write(dataPoint);

            return stream.ToArray();
        }
    }

    // Estimate the most likely sound source from active AudioSources
    private Vector3 GetClosestSoundSourcePosition()
    {
        AudioSource[] allSources = FindObjectsOfType<AudioSource>();
        AudioSource activeSource = null;
        float closestDistance = Mathf.Infinity;

        foreach (AudioSource source in allSources)
        {
            if (source.gameObject == this.gameObject) continue;
            if (source.isPlaying)
            {
                float dist = Vector3.Distance(transform.position, source.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    activeSource = source;
                }
            }
        }

        if (activeSource != null)
        {
            Debug.Log($"Localized audio source: {activeSource.gameObject.name} at {activeSource.transform.position}");
            return activeSource.transform.position;
        }

        Debug.LogWarning("Trigger detected but no active AudioSource found; defaulting forward.");
        return transform.position + transform.forward * 5f; 
    }
}