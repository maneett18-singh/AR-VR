using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

public class aWSManager : MonoBehaviour
{
    private ClientWebSocket socket = new ClientWebSocket();
    private Uri uri = new Uri("ws://localhost:8765"); // your Python WebSocket server URL

    [Header("Image Spawner Reference")]
    public ImageCubeSpawner cubeSpawner;  // ✅ drag this in Inspector

    async void Start()
    {
        try
        {
            await socket.ConnectAsync(uri, CancellationToken.None);
            Debug.Log("✅ Connected to PyTorch WebSocket server.");
            _ = ListenLoop(); // start receiving asynchronously
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ WebSocket connection failed: " + ex.Message);
        }
    }

    async Task ListenLoop()
    {
        var buffer = new byte[32768]; // increased for large JSONs
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
            OnMessage(msg);
        }
    }

   void OnMessage(string json)
{
    // Print the first few characters of any incoming message
    Debug.Log($"📩 Raw message: {json.Substring(0, Mathf.Min(200, json.Length))}");

    // ✅ Ignore non-JSON messages (e.g., heartbeat numbers or text)
    if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
    {
        Debug.LogWarning("⚠️ Skipping non-JSON message: " + json);
        return;
    }

    try
    {
        // Pass the valid JSON to ImageCubeSpawner
        if (cubeSpawner != null)
        {
            cubeSpawner.OnWebSocketMessage(json);
        }
        else
        {
            Debug.LogWarning("⚠️ cubeSpawner reference not set in Inspector.");
        }
    }
    catch (Exception ex)
    {
        Debug.LogError("⚠️ Failed to process JSON: " + ex.Message);
    }
}


    private void OnApplicationQuit()
    {
        if (socket != null)
        {
            _ = socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        }
    }
}
