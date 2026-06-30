using UnityEngine;
using System.Net.Sockets;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

// VisualSensorySystem.cs
// Captures camera frames and sends them to an external vision service.
// Receives detection results and exposes them for debug drawing and brain processing.
public class VisualSensorySystem : MonoBehaviour
{
    [Header("Brain Link")]
    public BotBrain botBrain;

    [Header("Camera & Network")]
    public Camera cam;
    public float sendInterval = 0.2f;
    private int networkImageSize = 640; // YOLO native size for speed!

    private TcpClient client;
    private NetworkStream stream;
    private Texture2D screenTexture;
    
    // Store current detections for OnGUI
    private List<Hostile> currentHostiles = new List<Hostile>();
    private float screenScaleX, screenScaleY;

    void Start()
    {
        // Initialize TCP connection to the vision backend and prepare texture
        client = new TcpClient("127.0.0.1", 9999);
        stream = client.GetStream();
        screenTexture = new Texture2D(networkImageSize, networkImageSize, TextureFormat.RGB24, false);
        StartCoroutine(SendFramesCoroutine());
    }

    IEnumerator SendFramesCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(sendInterval);

            // 1. Capture & Downscale for SPEED
            RenderTexture rt = new RenderTexture(networkImageSize, networkImageSize, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            screenTexture.ReadPixels(new Rect(0, 0, networkImageSize, networkImageSize), 0, 0);
            screenTexture.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            rt.Release();

            byte[] imgBytes = screenTexture.EncodeToJPG(75); // 75 quality is plenty for AI, saves bandwidth

            // 2. Send to Python
            byte[] length = BitConverter.GetBytes(imgBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(length);
            
            try
            {
                stream.Write(length, 0, 4);
                stream.Write(imgBytes, 0, imgBytes.Length);

                // 3. Receive Data
                byte[] lenBytes = new byte[4];
                ReadFull(stream, lenBytes, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                int responseLength = BitConverter.ToInt32(lenBytes, 0);

                byte[] buffer = new byte[responseLength];
                ReadFull(stream, buffer, responseLength);
                string response = Encoding.UTF8.GetString(buffer);

                ParseAndProcessHostiles(response);
            }
            catch (Exception ex)
            {
                Debug.LogError("Vision connection lost: " + ex.Message);
                break;
            }
        }
    }

    private void ParseAndProcessHostiles(string jsonResponse)
    {
        try
        {
            var hostiles = JsonUtilityWrapper.FromJsonArray(jsonResponse);
            currentHostiles = new List<Hostile>(hostiles);

            // Calculate scaling factors to draw boxes back on the full-size Unity screen
            screenScaleX = (float)Screen.width / networkImageSize;
            screenScaleY = (float)Screen.height / networkImageSize;

            foreach (var hostile in currentHostiles)
            {
                if (hostile.is_down)
                {
                    // Calculate 3D position using a Raycast through the center of the bounding box
                    float centerX = (hostile.x1 + hostile.x2) / 2f * screenScaleX;
                    float centerY = (hostile.y1 + hostile.y2) / 2f * screenScaleY;
                    
                    // Note: Unity Screen coordinates have Y=0 at bottom. OpenCV has Y=0 at top. We must invert Y.
                    Ray ray = cam.ScreenPointToRay(new Vector3(centerX, Screen.height - centerY, 0));

                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                                                // Pass the 3D detection to the brain for handling
                                                // botBrain.OnSpottedVictim(hit.point);
                    }
                }
            }
        }
        catch { currentHostiles.Clear(); }
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

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        foreach (var h in currentHostiles)
        {
            // Red for downed victims, Yellow for standing people
            GUI.color = h.is_down ? Color.red : Color.yellow;
            
            // Scale the YOLO coordinates back up to your actual screen size
            float x1 = h.x1 * screenScaleX;
            float y1 = h.y1 * screenScaleY;
            float w = (h.x2 - h.x1) * screenScaleX;
            float hgt = (h.y2 - h.y1) * screenScaleY;

            int thickness = 3;
            GUI.DrawTexture(new Rect(x1, y1, w, thickness), Texture2D.whiteTexture); // Top
            GUI.DrawTexture(new Rect(x1, y1 + hgt, w, thickness), Texture2D.whiteTexture); // Bottom
            GUI.DrawTexture(new Rect(x1, y1, thickness, hgt), Texture2D.whiteTexture); // Left
            GUI.DrawTexture(new Rect(x1 + w, y1, thickness, hgt), Texture2D.whiteTexture); // Right
            
            if (h.is_down) GUI.Label(new Rect(x1, y1 - 20, 150, 20), "INJURED DETECTED");
        }
    }

    void OnApplicationQuit()
    {
        if (stream != null) stream.Close();
        if (client != null) client.Close();
    }
}

[Serializable]
public class Hostile
{
    public int x1, y1, x2, y2;
    public float conf;
    public bool is_down; // New flag!
}

public static class JsonUtilityWrapper
{
    public static Hostile[] FromJsonArray(string json)
    {
        string newJson = "{\"hostiles\":" + json + "}";
        HostileArrayWrapper wrapper = JsonUtility.FromJson<HostileArrayWrapper>(newJson);
        return wrapper.hostiles;
    }
    [Serializable]
    private class HostileArrayWrapper { public Hostile[] hostiles; }
}