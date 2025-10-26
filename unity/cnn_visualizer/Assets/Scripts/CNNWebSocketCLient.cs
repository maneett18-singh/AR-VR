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

    async void Start()
    {
        try
        {
            await socket.ConnectAsync(uri, CancellationToken.None);
            Debug.Log("✅ Connected to PyTorch WebSocket server.");
            _ = ListenLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ WebSocket connection failed: " + ex.Message);
        }
    }

    async Task ListenLoop()
    {
        var buffer = new byte[8192];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
            OnMessage(msg);
        }
    }

    void OnMessage(string json)
    {
        Debug.Log($"📩 Message from Python: {json.Substring(0, Mathf.Min(120, json.Length))}...");
        var data = JsonUtility.FromJson<ConvMessage>(json);
        // later: visualize it!
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
        if (socket != null)
            socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
    }
}
