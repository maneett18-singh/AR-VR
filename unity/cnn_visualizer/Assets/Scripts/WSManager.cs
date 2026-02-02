using System;
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

public class WSManager : MonoBehaviour
{
    private ClientWebSocket socket = new ClientWebSocket();
    [Header("WebSocket Server")]
    [SerializeField] private string serverUrl = "ws://172.20.10.6:8765"; // your Python WebSocket server URL
    private Uri uri;

    [Header("Image Spawner Reference")]
    public ImageCubeSpawner cubeSpawner;  // ✅ drag ImageCubeSpawner in Inspector

    [Header("FC Graph Visualizer (Optional)")]
    public FCNetworkVisualizer fcVisualizer; // ✅ drag FCNetworkVisualizer in Inspector

    private UnityMainThreadDispatcher dispatcher;

    // WebSocket client state
    private ClientWebSocket socket;
    private Uri uri;

    [Serializable]
    private class FcGraphPayload
    {
        public int[] sizes;
        public FcWeights w1;
        public FcWeights w2;
    }

    [Serializable]
    private class FcWeights
    {
        public int[] shape;
        public string dtype;
        public string aggregation;
        public string base64;
        public float min;
        public float max;
    }

    [Serializable]
    private class RootPayload
    {
        public FcGraphPayload fc_graph;
        public int predicted_class;
    }

    private async void Start()
    {
        // Ensure dispatcher exists on the Unity main thread.
        dispatcher = UnityMainThreadDispatcher.Instance();

        // Initialize websocket
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
        {
            uri = new Uri(serverUrl);
            Debug.Log($"🔌 [WSManager] Attempting to connect to {serverUrl}...");
            await socket.ConnectAsync(uri, CancellationToken.None);
            Debug.Log("✅ [WSManager] Connected to PyTorch WebSocket server.");
            _ = ListenLoop(); // start receiving asynchronously
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [WSManager] WebSocket connection failed: {ex.Message}");
        }
    }

    private async Task ListenLoop()
    {
        var buffer = new byte[65536]; // chunk buffer; messages may be larger than this
        while (socket.State == WebSocketState.Open)
        {
            try
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                        return;
                    }

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                string msg = sb.ToString();
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

        // IMPORTANT: WebSocket receive callbacks may run off the Unity main thread.
        // All Instantiate/UnityEngine object access must happen on the main thread.
        if (dispatcher == null)
            dispatcher = UnityMainThreadDispatcher.Instance();

        dispatcher.Enqueue(() =>
        {
            try
            {
                // Optional: update FC weight visualization
                if (fcVisualizer != null)
                {
                    TryUpdateFcVisualizer(json);
                }

                // Highlight predicted digit in the output layer (0-9)
                TryHighlightPrediction(json);

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
        });
    }

    private void TryHighlightPrediction(string json)
    {
        if (fcVisualizer == null)
            return;

        try
        {
            var root = JsonConvert.DeserializeObject<RootPayload>(json);
            // If the server doesn't send it, default will be 0; guard by checking common range.
            int p = root.predicted_class;
            if (p < 0 || p > 9)
                return;

            fcVisualizer.SetPredictedOutput(p);
        }
        catch
        {
            // Ignore: not all messages include predictions.
        }
    }

    private void TryUpdateFcVisualizer(string json)
    {
        try
        {
            var root = JsonConvert.DeserializeObject<RootPayload>(json);
            if (root?.fc_graph?.sizes == null || root.fc_graph.sizes.Length != 3)
                return;

            var fc = root.fc_graph;
            if (fc.w1?.shape == null || fc.w2?.shape == null) return;
            if (string.IsNullOrEmpty(fc.w1.base64) || string.IsNullOrEmpty(fc.w2.base64)) return;

            // sizes are [64,128,10] but w1 is [128,64] and w2 is [10,128]
            int serverInput = fc.sizes[0];
            int serverHidden = fc.sizes[1];
            int serverOutput = fc.sizes[2];

            float[] w1 = DecodeFloat32Base64(fc.w1.base64);
            float[] w2 = DecodeFloat32Base64(fc.w2.base64);
            if (w1 == null || w2 == null) return;

            // Optionally override server sizes with Inspector sizes (clamped to server dimensions)
            int targetInput = serverInput;
            int targetHidden = serverHidden;
            int targetOutput = serverOutput;

            if (fcVisualizer.overrideServerSizes)
            {
                targetInput = Mathf.Clamp(fcVisualizer.inputCount, 1, serverInput);
                targetHidden = Mathf.Clamp(fcVisualizer.hiddenCount, 1, serverHidden);
                targetOutput = Mathf.Clamp(fcVisualizer.outputCount, 1, serverOutput);
            }

            // Slice weights to match target sizes (top-left block)
            float[] w1Sliced = SliceMatrixRowMajor(w1, serverHidden, serverInput, targetHidden, targetInput);
            float[] w2Sliced = SliceMatrixRowMajor(w2, serverOutput, serverHidden, targetOutput, targetHidden);

            fcVisualizer.BuildNetwork(targetInput, targetHidden, targetOutput);
            fcVisualizer.SetWeights(w1Sliced, targetHidden, targetInput, w2Sliced, targetOutput, targetHidden);
            Debug.Log("✅ [WSManager] FC graph visualized");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"⚠️ [WSManager] FC visualizer update skipped: {ex.Message}");
        }
    }

    private static float[] SliceMatrixRowMajor(float[] src, int srcRows, int srcCols, int dstRows, int dstCols)
    {
        dstRows = Mathf.Clamp(dstRows, 0, srcRows);
        dstCols = Mathf.Clamp(dstCols, 0, srcCols);
        var dst = new float[dstRows * dstCols];
        for (int r = 0; r < dstRows; r++)
        {
            int srcRowOffset = r * srcCols;
            int dstRowOffset = r * dstCols;
            Array.Copy(src, srcRowOffset, dst, dstRowOffset, dstCols);
        }
        return dst;
    }

    private static float[] DecodeFloat32Base64(string b64)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(b64);
            int floatCount = bytes.Length / 4;
            var floats = new float[floatCount];
            Buffer.BlockCopy(bytes, 0, floats, 0, floatCount * 4);
            return floats;
        }
        catch
        {
            return null;
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
