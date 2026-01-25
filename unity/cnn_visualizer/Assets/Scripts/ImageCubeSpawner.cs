using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class ImageCubeSpawner : MonoBehaviour
{
    [Serializable]
    public class WebSocketData
    {
        public string input_image_base64;
        public Dictionary<string, Dictionary<string, FeatureMap>> feature_maps;
    }

    [Serializable]
    public class FeatureMap
    {
        public int[] shape; // [H, W]
        public string base64;
    }

    public GameObject cubePrefab;
    public Material lineMaterial; // assign a simple unlit color material in Inspector

    [Header("Layout")]
    [Tooltip("All generated layer parents/lines will be created under this root. If null, one is created automatically.")]
    public Transform visualizationRoot;

    [Tooltip("Local offset applied to the whole visualization under visualizationRoot.")]
    public Vector3 baseOffset = Vector3.zero;

    [Min(0.1f)]
    public float layerSpacing = 5.0f;

    [Min(0.01f)]
    public float gridSpacing = 1.5f;

    [Tooltip("If true, automatically picks a compact grid (ceil(sqrt(N)) columns) for each layer.")]
    public bool autoGridColumns = true;

    [Min(1)]
    public int gridColumns = 10;

    [Tooltip("Centers each layer grid around its local origin.")]
    public bool centerLayerGrid = true;

    [Tooltip("If true, destroys the previous visualization on every new message.")]
    public bool clearOnMessage = true;

    [Header("Layer Order (Model Architecture)")]
    [Tooltip("Layers are rendered in this order if present in feature_maps. Unspecified layers (if any) are appended after.")]
    public string[] layerOrder = new[] { "conv1", "conv2", "pool" };

    [Tooltip("If true, layers not listed in layerOrder are still shown after the ordered ones.")]
    public bool includeUnspecifiedLayers = false;

    private readonly List<GameObject> activeCubes = new();
    private readonly List<GameObject> receptiveLines = new();

    private void Awake()
    {
        EnsureRoot();
    }

    private void EnsureRoot()
    {
        if (visualizationRoot != null) return;
        var go = new GameObject("ImageCubeVisualizationRoot");
        visualizationRoot = go.transform;
        visualizationRoot.SetParent(transform, false);
        visualizationRoot.localPosition = Vector3.zero;
        visualizationRoot.localRotation = Quaternion.identity;
        visualizationRoot.localScale = Vector3.one;
    }

    public void OnWebSocketMessage(string json)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<WebSocketData>(json);
            if (data?.feature_maps == null || data.feature_maps.Count == 0)
                return;

            EnsureRoot();
            if (clearOnMessage)
                ClearAll();

            // Input cube
            GameObject inputCube = null;
            if (!string.IsNullOrEmpty(data.input_image_base64))
            {
                inputCube = CreateCubeFromBase64(data.input_image_base64, visualizationRoot, baseOffset, new Vector2(28, 28));
            }
            Vector3? previousCenter = inputCube != null ? inputCube.transform.position : (Vector3?)null;

            // Start conv layers after the input cube so they don't overlap.
            int layerIndex = inputCube != null ? 1 : 0;

            void SpawnLayer(string layerName, Dictionary<string, FeatureMap> featureMaps)
            {
                if (featureMaps == null || featureMaps.Count == 0) return;

                var layerParent = new GameObject($"Layer_{layerName}");
                layerParent.transform.SetParent(visualizationRoot, false);
                layerParent.transform.localPosition = baseOffset + new Vector3(0f, 0f, layerIndex * layerSpacing);
                layerParent.transform.localRotation = Quaternion.identity;

                var layerCubes = new List<GameObject>();

                int countInLayer = featureMaps.Count;
                int cols = autoGridColumns
                    ? Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(countInLayer)))
                    : Mathf.Max(1, gridColumns);

                int rows = Mathf.CeilToInt(countInLayer / (float)cols);
                float width = Mathf.Min(cols, countInLayer) * gridSpacing;
                float height = rows * gridSpacing;
                Vector3 centerOffset = centerLayerGrid
                    ? new Vector3(-(width - gridSpacing) * 0.5f, -(height - gridSpacing) * 0.5f, 0f)
                    : Vector3.zero;

                int index = 0;
                foreach (var kv in featureMaps)
                {
                    var fmap = kv.Value;
                    if (string.IsNullOrEmpty(fmap?.base64))
                    {
                        index++;
                        continue;
                    }

                    Vector2 shape = (fmap.shape != null && fmap.shape.Length >= 2)
                        ? new Vector2(fmap.shape[1], fmap.shape[0])
                        : new Vector2(28, 28);

                    Vector3 localPos = centerOffset + new Vector3(
                        (index % cols) * gridSpacing,
                        (index / cols) * gridSpacing,
                        0f
                    );

                    var cube = CreateCubeFromBase64(fmap.base64, layerParent.transform, localPos, shape);
                    if (cube != null) layerCubes.Add(cube);
                    index++;
                }

                if (layerCubes.Count > 0)
                {
                    Vector3 center = ComputeLayerCenter(layerCubes);
                    if (previousCenter.HasValue)
                        DrawReceptiveFieldLine(previousCenter.Value, center);
                    previousCenter = center;
                }

                layerIndex++;
            }

            var used = new HashSet<string>();

            // Render known layers in model order first
            if (layerOrder != null)
            {
                foreach (var name in layerOrder)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (data.feature_maps.TryGetValue(name, out var maps))
                    {
                        SpawnLayer(name, maps);
                        used.Add(name);
                    }
                }
            }

            // Then render any remaining layers (if server sends extra)
            if (includeUnspecifiedLayers)
            {
                foreach (var kv in data.feature_maps)
                {
                    if (used.Contains(kv.Key)) continue;
                    SpawnLayer(kv.Key, kv.Value);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [ImageCubeSpawner] Failed to process WebSocket JSON: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private GameObject CreateCubeFromBase64(string base64, Transform parent, Vector3 localPosition, Vector2 shape)
    {
        try
        {
            if (cubePrefab == null)
            {
                Debug.LogError("❌ [ImageCubeSpawner] cubePrefab is not assigned in Inspector!");
                return null;
            }

            Texture2D tex = ImageUtils.Base64ToTexture(base64);
            if (tex == null)
                return null;

            GameObject cube = Instantiate(cubePrefab, parent);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.mainTexture = tex;

            float scaleFactor = 0.05f;
            cube.transform.localScale = new Vector3(shape.x * scaleFactor, shape.y * scaleFactor, 0.1f);

            activeCubes.Add(cube);
            return cube;
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [ImageCubeSpawner] Error in CreateCubeFromBase64: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private Vector3 ComputeLayerCenter(List<GameObject> cubes)
    {
        if (cubes.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var c in cubes)
            sum += c.transform.position;
        return sum / cubes.Count;
    }

    private void DrawReceptiveFieldLine(Vector3 start, Vector3 end)
    {
        try
        {
            EnsureRoot();
            GameObject lineObj = new GameObject("ReceptiveFieldLine");
            if (visualizationRoot != null)
                lineObj.transform.SetParent(visualizationRoot, false);

            var lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPositions(new[] { start, end });
            if (lineMaterial != null)
                lr.material = lineMaterial;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.startColor = Color.cyan;
            lr.endColor = Color.magenta;
            receptiveLines.Add(lineObj);
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ [ImageCubeSpawner] Error drawing receptive field line: {ex.Message}");
        }
    }

    private void ClearAll()
    {
        EnsureRoot();

        if (visualizationRoot != null)
        {
            for (int i = visualizationRoot.childCount - 1; i >= 0; i--)
            {
                var child = visualizationRoot.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }
        }

        foreach (var c in activeCubes)
            if (c != null) Destroy(c);
        foreach (var l in receptiveLines)
            if (l != null) Destroy(l);
        activeCubes.Clear();
        receptiveLines.Clear();
    }
}
