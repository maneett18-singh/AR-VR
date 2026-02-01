using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class CNNWebSocketClient : MonoBehaviour
{
    ClientWebSocket socket = new ClientWebSocket();
    [Header("WebSocket Server")]
    [SerializeField] private string serverUrl = "ws://localhost:8765";
    private Uri uri;
    
    public ImageCubeSpawner cubeSpawner;  // ✅ Assign in Inspector

    async void Start()
    {
        try
        {
            uri = new Uri(serverUrl);
            Debug.Log($"🔌 [CNNWebSocketClient] Attempting connection to {serverUrl}...");
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
    void CreateLayerPlane(ConvMessage msg)
{
    int width = msg.shape[1];
    int height = msg.shape[0];

    // 1️⃣ Create plane
    GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);

    // ✅ Fixed position at (0, 4, 0)
    plane.transform.position = new Vector3(0f, 4f, 8f);

    // Scale the plane according to feature map size
    plane.transform.localScale = new Vector3(width * 0.2f, height * 0.2f, 1f);

    // 2️⃣ Create texture
    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

    // 3️⃣ Fill texture with feature map data
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            float v = msg.data[y][x];
            float normalized = Mathf.InverseLerp(-5f, 5f, v); // normalize values
            tex.SetPixel(x, height - 1 - y, new Color(normalized, normalized, normalized));
        }
    }
    tex.Apply();

    // 4️⃣ Assign material
    Material mat = new Material(Shader.Find("Unlit/Texture"));
    mat.mainTexture = tex;
    plane.GetComponent<Renderer>().material = mat;

    // 5️⃣ Parent to this GameObject for hierarchy
    plane.transform.SetParent(transform);
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
