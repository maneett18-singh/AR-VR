using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class CNNWebSocketClient : MonoBehaviour
{
    ClientWebSocket socket = new ClientWebSocket();
    Uri uri = new Uri("ws://localhost:8765");
    
    public ImageCubeSpawner cubeSpawner;  // ✅ Assign in Inspector

    async void Start()
    {
        try
        {
            Debug.Log("🔌 [CNNWebSocketClient] Attempting connection to ws://localhost:8765...");
            await socket.ConnectAsync(uri, CancellationToken.None);
            Debug.Log("✅ [CNNWebSocketClient] Connected to PyTorch WebSocket server.");
            _ = ListenLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ [CNNWebSocketClient] WebSocket connection failed: " + ex.Message);
        }
    }

    async Task ListenLoop()
    {
        var buffer = new byte[65536];
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
                Debug.LogError($"❌ [CNNWebSocketClient] Receive error: {ex.Message}");
            }
        }
    }

    void OnMessage(string json)
    {
        Debug.Log($"📩 [CNNWebSocketClient] Message from Python: {json.Substring(0, Mathf.Min(120, json.Length))}...");
        
        // ✅ Now pass to ImageCubeSpawner
        if (cubeSpawner != null)
        {
            cubeSpawner.OnWebSocketMessage(json);
        }
        else
        {
            Debug.LogWarning("⚠️ [CNNWebSocketClient] cubeSpawner not assigned!");
        }
    }

    [Serializable]
    public class ConvMessage
    {
        public string type;
        public int[] shape;
        public float[][] data;
    }

    private void OnApplicationQuit()
    {
        if (socket != null && socket.State == WebSocketState.Open)
            socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
    }
}
