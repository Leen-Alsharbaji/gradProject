using UnityEngine;
using System.Net.Sockets;
using System;
using System.Text;
using System.Collections;

// VictimDetection.cs
// Sends camera frames to a vision service and converts detections to world positions.
// Exposes events when victims are localized for higher-level behavior.
public class VictimDetection : MonoBehaviour
{
    [Header("Vision Settings")]
    public Camera cam;
    public float sendInterval = 0.5f; 
    
    [Header("Network")]
    public string host = "127.0.0.1";
    public int port = 9999;

    public event Action<Vector3, string> OnVictimSpotted;

    private TcpClient client;
    private NetworkStream stream;
    private Texture2D screenTexture;
    
    private bool hasNewDetection = false;
    public bool isVisionActive = false;
    private VictimData targetVictim;
    private VictimData victimToDraw; // Kept separate so OnGUI can draw continuously

    void Start()
    {
        if (cam == null) cam = GetComponent<Camera>();
        
        try {
            client = new TcpClient(host, port);
            stream = client.GetStream();
            screenTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            StartCoroutine(SendFramesCoroutine());
        } catch (Exception e) {
            Debug.LogError($"Vision connection failed: {e.Message}");
        }
    }

    void Update()
    {
            if (hasNewDetection && targetVictim != null)
        {
            hasNewDetection = false;

            float unityScreenY = Screen.height - targetVictim.screenY;
            Vector3 screenPos = new Vector3(targetVictim.screenX, unityScreenY, 0);

            Ray ray = cam.ScreenPointToRay(screenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    Debug.Log($"Vision detection: 1 person, {targetVictim.inferenceTime:F1}ms inference. Status: {targetVictim.status}");
                    OnVictimSpotted?.Invoke(hit.point, targetVictim.status);
                }
        }
    }

    IEnumerator SendFramesCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(sendInterval);

               if (!isVisionActive) 
            {
                victimToDraw = null; // Clear the screen box if we go to sleep
                continue; 
            }

            if (stream == null || !stream.CanWrite) continue;
         
            RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            screenTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenTexture.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();


            byte[] imgBytes = screenTexture.EncodeToJPG();
            byte[] length = BitConverter.GetBytes(imgBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(length);

            try
            {
                stream.Write(length, 0, 4);
                stream.Write(imgBytes, 0, imgBytes.Length);

                byte[] lenBytes = new byte[4];
                ReadFull(stream, lenBytes, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                int responseLength = BitConverter.ToInt32(lenBytes, 0);

                byte[] buffer = new byte[responseLength];
                ReadFull(stream, buffer, responseLength);
                string response = Encoding.UTF8.GetString(buffer);

                VictimData[] victims = JsonHelper.FromJson<VictimData>("{\"Items\":" + response + "}");
                
                if (victims != null && victims.Length > 0)
                {
                    targetVictim = victims[0];
                    foreach(var v in victims) {
                        if (v.status.Contains("Horizontal")) targetVictim = v;
                    }
                    victimToDraw = targetVictim; // Save for OnGUI
                    hasNewDetection = true; 
                }
                else
                {
                    victimToDraw = null; // Clear box if no one is seen
                }
            }
                catch (Exception ex)
            {
                Debug.LogWarning("Vision communication error: " + ex.Message);
                break;
            }
        }
    }

    // THE BOX DRAWING
    void OnGUI()
    {
        if (victimToDraw != null && victimToDraw.box != null && victimToDraw.box.Length == 4)
        {
            // If they are horizontal/unconscious, draw a red box. If standing, yellow.
            GUI.color = victimToDraw.status.Contains("Horizontal") ? Color.red : Color.yellow;
            
            int x1 = victimToDraw.box[0];
            int y1 = victimToDraw.box[1];
            int x2 = victimToDraw.box[2];
            int y2 = victimToDraw.box[3];

            GUI.DrawTexture(new Rect(x1, y1, x2 - x1, 2), Texture2D.whiteTexture); // Top
            GUI.DrawTexture(new Rect(x1, y2, x2 - x1, 2), Texture2D.whiteTexture); // Bottom
            GUI.DrawTexture(new Rect(x1, y1, 2, y2 - y1), Texture2D.whiteTexture); // Left
            GUI.DrawTexture(new Rect(x2, y1, 2, y2 - y1), Texture2D.whiteTexture); // Right
            
            // Label the box with the victim's status
            GUI.Label(new Rect(x1, y1 - 20, 300, 20), $"Status: {victimToDraw.status}");
        }
    }

    private void ReadFull(NetworkStream stream, byte[] buffer, int size)
    {
        int totalRead = 0;
        while (totalRead < size)
        {
            int read = stream.Read(buffer, totalRead, size - totalRead);
            if (read == 0) throw new Exception("Disconnected");
            totalRead += read;
        }
    }

    void OnApplicationQuit()
    {
        if (stream != null) stream.Close();
        if (client != null) client.Close();
    }
}

[Serializable]
public class VictimData
{
    public string status;
    public float screenX;
    public float screenY;
    public int[] box;           // <--- Keeps your OnGUI drawing working!
    public float boxCenterX;    // <--- Keeps your navigation working!
    public float boxCenterY;
    public float boxWidth;
    public float boxHeight;
    public float inferenceTime; 
    public float conf;
}
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
        return wrapper.Items;
    }
    [Serializable]
    private class Wrapper<T> { public T[] Items; }
}