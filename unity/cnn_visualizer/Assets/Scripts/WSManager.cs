using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

public class WSManager : MonoBehaviour
{
    private ClientWebSocket socket = new ClientWebSocket();
    private Uri uri = new Uri("ws://localhost:8765"); // your Python WebSocket server URL

    [Header("Image Spawner Reference")]
    public ImageCubeSpawner cubeSpawner;  // ✅ drag ImageCubeSpawner in Inspector

    async void Start()
    {
        try
        {
            Debug.Log("🔌 [WSManager] Attempting to connect to ws://localhost:8765...");
            await socket.ConnectAsync(uri, CancellationToken.None);
            Debug.Log("✅ [WSManager] Connected to PyTorch WebSocket server.");
            _ = ListenLoop(); // start receiving asynchronously
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [WSManager] WebSocket connection failed: {ex.Message}");
        }
    }

    async Task ListenLoop()
    {
        var buffer = new byte[65536]; // increased for large JSONs with base64 images
        while (socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                OnMessage(msg);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [WSManager] Receive error: {ex.Message}");
            }
        }
    }

    void OnMessage(string json)
    {
        // Print the first few characters of any incoming message
        Debug.Log($"📩 [WSManager] Raw message received (length: {json.Length}): {json.Substring(0, Mathf.Min(200, json.Length))}");

        // ✅ Ignore non-JSON messages (e.g., heartbeat numbers or text)
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
        {
            Debug.LogWarning($"⚠️ [WSManager] Skipping non-JSON message: {json}");
            return;
        }

        try
        {
            // Pass the valid JSON to ImageCubeSpawner
            if (cubeSpawner != null)
            {
                Debug.Log("📤 [WSManager] Passing message to ImageCubeSpawner...");
                cubeSpawner.OnWebSocketMessage(json);
            }
            else
            {
                Debug.LogError("❌ [WSManager] cubeSpawner reference not set in Inspector! Please assign it.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [WSManager] Failed to process JSON: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OnApplicationQuit()
    {
        if (socket != null && socket.State == WebSocketState.Open)
        {
            Debug.Log("🔌 [WSManager] Closing WebSocket...");
            _ = socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        }
    }
}
