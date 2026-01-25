using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

public class CNNWebSocketClient : MonoBehaviour
{
    ClientWebSocket socket = new ClientWebSocket();
    Uri uri = new Uri("ws://localhost:8765");
    
    public ImageCubeSpawner cubeSpawner;  // ✅ Assign in Inspector

    [Header("CNN Pose Options (Configurable)")]
    [Tooltip("List of poses/classes your CNN supports. Edit in Inspector; NOT hardcoded.")]
    [SerializeField] private List<string> availablePoses = new List<string> { "pose_0" };

    [Tooltip("Index into Available Poses to use when requesting/controlling the server.")]
    [SerializeField] private int selectedPoseIndex = 0;

    [Tooltip("If enabled, automatically send the selected pose once after connecting.")]
    [SerializeField] private bool sendPoseOnConnect = true;

    [Tooltip("Optional key to cycle to next pose at runtime.")]
    [SerializeField] private KeyCode nextPoseKey = KeyCode.P;

    [Tooltip("Optional key to send the currently selected pose.")]
    [SerializeField] private KeyCode sendPoseKey = KeyCode.O;

    async void Start()
    {
        try
        {
            Debug.Log("🔌 [CNNWebSocketClient] Attempting connection to ws://localhost:8765...");
            await socket.ConnectAsync(uri, CancellationToken.None);
            Debug.Log("✅ [CNNWebSocketClient] Connected to PyTorch WebSocket server.");

            if (sendPoseOnConnect)
            {
                _ = SendSelectedPoseAsync();
            }

            _ = ListenLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ [CNNWebSocketClient] WebSocket connection failed: " + ex.Message);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(nextPoseKey))
        {
            CyclePose();
        }

        if (Input.GetKeyDown(sendPoseKey))
        {
            _ = SendSelectedPoseAsync();
        }
    }

    public IReadOnlyList<string> AvailablePoses => availablePoses;

    public int SelectedPoseIndex
    {
        get => selectedPoseIndex;
        set => selectedPoseIndex = Mathf.Clamp(value, 0, Mathf.Max(0, availablePoses.Count - 1));
    }

    public string SelectedPose
    {
        get
        {
            if (availablePoses == null || availablePoses.Count == 0)
                return string.Empty;
            var idx = Mathf.Clamp(selectedPoseIndex, 0, availablePoses.Count - 1);
            return availablePoses[idx] ?? string.Empty;
        }
    }

    public void CyclePose()
    {
        if (availablePoses == null || availablePoses.Count == 0)
        {
            Debug.LogWarning("⚠️ [CNNWebSocketClient] No poses configured (Available Poses is empty)." );
            return;
        }

        selectedPoseIndex = (selectedPoseIndex + 1) % availablePoses.Count;
        Debug.Log($"🧭 [CNNWebSocketClient] Selected pose: '{SelectedPose}' (index {selectedPoseIndex})");
    }

    async Task SendSelectedPoseAsync()
    {
        if (socket == null || socket.State != WebSocketState.Open)
        {
            Debug.LogWarning("⚠️ [CNNWebSocketClient] Can't send pose; WebSocket isn't connected.");
            return;
        }

        var pose = SelectedPose;
        if (string.IsNullOrWhiteSpace(pose))
        {
            Debug.LogWarning("⚠️ [CNNWebSocketClient] Can't send pose; SelectedPose is empty. Configure Available Poses in Inspector.");
            return;
        }

        // Minimal JSON command. Server can ignore if it doesn't support commands.
        // Example: {"type":"set_pose","pose":"open_palm"}
        var payload = $"{{\"type\":\"set_pose\",\"pose\":\"{EscapeForJson(pose)}\"}}";
        var bytes = Encoding.UTF8.GetBytes(payload);

        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.Log($"📤 [CNNWebSocketClient] Sent pose command: {payload}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [CNNWebSocketClient] Failed to send pose command: {ex.Message}");
        }
    }

    static string EscapeForJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
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
