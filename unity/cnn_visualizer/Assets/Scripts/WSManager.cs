using System;
using System.Collections;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

public class WSManager : MonoBehaviour
{
    [Header("HTTP Server Settings")]
    [Tooltip("FastAPI base URL (no trailing slash). Example: http://localhost:8765")]
    public string serverBaseUrl = "http://localhost:8765";

    [Tooltip("If true, fetch exactly one inference on Start().")]
    public bool fetchOnStart = true;

    [Tooltip("Optional: if set, server will fetch image from this URL.")]
    public string imageUrl = "https://machinelearningmastery.com/wp-content/uploads/2019/02/sample_image-300x298.png";

    [Tooltip("Optional: key to trigger a single fetch at runtime.")]
    public KeyCode fetchKey = KeyCode.Space;

    [Tooltip("Request timeout (seconds).")]
    public int timeoutSeconds = 120;

    [Header("Image Spawner Reference")]
    public ImageCubeSpawner cubeSpawner;  // ✅ drag ImageCubeSpawner in Inspector

    void Start()
    {
        if (fetchOnStart)
            StartCoroutine(FetchOnce());
    }

    void Update()
    {
        if (Input.GetKeyDown(fetchKey))
        {
            StartCoroutine(FetchOnce());
        }
    }

    [Serializable]
    private class InferRequest
    {
        public string image_url;
        public string image_base64;
    }

    IEnumerator FetchOnce()
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
        {
            Debug.LogError("❌ [WSManager] serverBaseUrl is empty.");
            yield break;
        }

        var endpoint = serverBaseUrl.TrimEnd('/') + "/infer";

        var reqObj = new InferRequest
        {
            image_url = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
            image_base64 = null,
        };

        var body = JsonConvert.SerializeObject(reqObj);
        var bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);

        Debug.Log($"🌐 [WSManager] Sending single HTTP request to {endpoint}");

        using var request = new UnityWebRequest(endpoint, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = timeoutSeconds;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"❌ [WSManager] HTTP request failed: {request.error}\nResponse: {request.downloadHandler?.text}");
            yield break;
        }

        var json = request.downloadHandler.text;
        OnMessage(json);
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
}
