using UnityEngine;
using NativeWebSocket;

public class WSManager : MonoBehaviour
{
    private WebSocket ws;
    public ImageCubeSpawner cubeSpawner;

    async void Start()
    {
        ws = new WebSocket("ws://localhost:8765"); // replace with your actual WebSocket URL

        ws.OnMessage += (bytes) =>
        {
            string message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("📨 Received message: " + message);
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                cubeSpawner.OnWebSocketMessage(message);
            });
        };

        await ws.Connect();
        Debug.Log("WebSocket connected!");
    }

    async void OnApplicationQuit()
    {
        await ws.Close();
    }
}
