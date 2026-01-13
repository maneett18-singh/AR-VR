using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class ImageCubeSpawner : MonoBehaviour
{
    [System.Serializable]
    public class WebSocketData
    {
        public string input_image_base64;
        public Dictionary<string, Dictionary<string, FeatureMap>> feature_maps;
    }

    [System.Serializable]
    public class FeatureMap
    {
        public int[] shape; // [H, W]
        public string base64;
    }

    public GameObject cubePrefab;
    public Material lineMaterial; // assign a simple unlit color material in Inspector

    private readonly List<GameObject> activeCubes = new();
    private readonly List<GameObject> receptiveLines = new();

    public void OnWebSocketMessage(string json)
    {
        try
        {
            Debug.Log($"📨 [ImageCubeSpawner] Received WebSocket message (length: {json.Length} chars)");
            Debug.Log($"📄 Message preview: {json.Substring(0, Mathf.Min(200, json.Length))}...");
            
            var data = JsonConvert.DeserializeObject<WebSocketData>(json);
            if (data == null)
            {
                Debug.LogWarning("⚠ [ImageCubeSpawner] WebSocketData was null, skipping frame.");
                return;
            }
            Debug.Log("✅ [ImageCubeSpawner] JSON deserialized successfully");
            
            // 🔍 Validate data structure
            if (data.feature_maps == null)
            {
                Debug.LogError("❌ [ImageCubeSpawner] feature_maps is NULL! Check your Python server JSON format.");
                Debug.LogError($"📄 Data structure: input_image_base64 = {(data.input_image_base64 != null ? "SET" : "NULL")}");
                return;
            }
            
            if (data.feature_maps.Count == 0)
            {
                Debug.LogWarning("⚠ [ImageCubeSpawner] feature_maps is empty (no layers to visualize)");
                return;
            }

            // 🧹 Clean previous visualization
            Debug.Log("🧹 [ImageCubeSpawner] Clearing previous visualization...");
            ClearAll();

            // ✅ Spawn input image cube as the first layer
            Debug.Log("🖼️ [ImageCubeSpawner] Creating input image cube...");
            GameObject inputCube = null;
            if (data.input_image_base64 != null)
            {
                inputCube = CreateCubeFromBase64(data.input_image_base64, Vector3.zero, new Vector2(28, 28));
                if (inputCube == null)
                    Debug.LogError("❌ [ImageCubeSpawner] Failed to create input image cube!");
                else
                    Debug.Log("✅ [ImageCubeSpawner] Input image cube created successfully");
            }
            else
            {
                Debug.LogWarning("⚠ [ImageCubeSpawner] No input_image_base64 provided, skipping input cube");
            }
            GameObject previousLayerCenter = inputCube;

            // ✅ Spawn feature maps as layers along Z-axis
            float layerSpacing = 3.0f;
            int layerIndex = 0;
            Debug.Log($"🔄 [ImageCubeSpawner] Processing {data.feature_maps.Count} layers...");

            foreach (var layer in data.feature_maps)
            {
                // Create a parent for this layer
                Debug.Log($"📦 [ImageCubeSpawner] Creating layer parent: Layer_{layer.Key} (Filter count: {layer.Value.Count})");
                GameObject layerParent = new GameObject($"Layer_{layer.Key}");
                layerParent.transform.SetParent(transform);
                Debug.Log($"✅ [ImageCubeSpawner] Layer parent created and set as child");

                // Collect all cubes in this layer to compute center
                List<GameObject> layerCubes = new();

                float offset = 1.5f;
                int index = 0;
                Debug.Log($"🔄 [ImageCubeSpawner] Processing {layer.Value.Count} feature maps in layer {layer.Key}...");

                foreach (var feature in layer.Value)
                {
                    FeatureMap fmap = feature.Value;
                    if (fmap?.base64 == null) 
                    {
                        Debug.LogWarning($"⚠ [ImageCubeSpawner] Feature map {feature.Key} has null base64 data, skipping...");
                        continue;
                    }

                    Vector2 shape = (fmap.shape != null && fmap.shape.Length >= 2)
                        ? new Vector2(fmap.shape[1], fmap.shape[0])
                        : new Vector2(28, 28);

                    Debug.Log($"🎨 [ImageCubeSpawner] Creating feature map {feature.Key} (shape: {shape.x}x{shape.y})");

                    // Arrange cubes in a grid per layer
                    Vector3 pos = new Vector3(
                        (index % 10) * offset,
                        (index / 10) * offset,
                        layerIndex * layerSpacing
                    );
                    Debug.Log($"📍 [ImageCubeSpawner] Cube position: {pos}");

                    GameObject cube = CreateCubeFromBase64(fmap.base64, pos, shape);
                    if (cube != null)
                    {
                        cube.transform.SetParent(layerParent.transform);
                        layerCubes.Add(cube);
                        Debug.Log($"✅ [ImageCubeSpawner] Feature map cube created: {cube.name}");
                    }
                    else
                    {
                        Debug.LogError($"❌ [ImageCubeSpawner] Failed to create cube for {feature.Key}");
                    }
                    index++;
                }

                // Compute layer center to draw connection lines
                Debug.Log($"🔢 [ImageCubeSpawner] Layer {layer.Key} has {layerCubes.Count} cubes");
                Vector3 layerCenter = ComputeLayerCenter(layerCubes);
                Debug.Log($"📍 [ImageCubeSpawner] Layer center computed: {layerCenter}");
                
                if (previousLayerCenter != null)
                {
                    Debug.Log($"🔗 [ImageCubeSpawner] Drawing connection line between layers...");
                    DrawReceptiveFieldLine(previousLayerCenter.transform.position, layerCenter);
                }

                previousLayerCenter = layerParent;
                layerIndex++;
            }

            Debug.Log($"✅ [ImageCubeSpawner] Successfully spawned {layerIndex} layers of feature maps!");
            Debug.Log($"📊 [ImageCubeSpawner] Total active cubes: {activeCubes.Count}, Lines: {receptiveLines.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ [ImageCubeSpawner] Failed to process WebSocket JSON: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // 🧩 Create a cube with texture and scaled size
    GameObject CreateCubeFromBase64(string base64, Vector3 position, Vector2 shape)
    {
        try
        {
            if (cubePrefab == null)
            {
                Debug.LogError("❌ [ImageCubeSpawner] cubePrefab is not assigned in Inspector!");
                return null;
            }

            Debug.Log($"🖼️ [ImageCubeSpawner] Converting base64 to texture (length: {base64.Length})...");
            Texture2D tex = ImageUtils.Base64ToTexture(base64);
            if (tex == null)
            {
                Debug.LogError("❌ [ImageCubeSpawner] Failed to convert base64 to texture!");
                return null;
            }
            Debug.Log($"✅ [ImageCubeSpawner] Texture created: {tex.width}x{tex.height}");

            Debug.Log($"🎮 [ImageCubeSpawner] Instantiating cube prefab at position {position}...");
            GameObject cube = Instantiate(cubePrefab, position, Quaternion.identity, transform);
            Debug.Log($"✅ [ImageCubeSpawner] Cube instantiated: {cube.name}");

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError("❌ [ImageCubeSpawner] Renderer component not found on cube prefab!");
            }
            else
            {
                renderer.material.mainTexture = tex;
                Debug.Log($"✅ [ImageCubeSpawner] Texture applied to renderer");
            }

            float scaleFactor = 0.05f;
            Vector3 newScale = new Vector3(shape.x * scaleFactor, shape.y * scaleFactor, 0.1f);
            cube.transform.localScale = newScale;
            Debug.Log($"📏 [ImageCubeSpawner] Cube scaled to: {newScale}");

            activeCubes.Add(cube);
            Debug.Log($"📦 [ImageCubeSpawner] Cube added to activeCubes list (total: {activeCubes.Count})");
            return cube;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ [ImageCubeSpawner] Error in CreateCubeFromBase64: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    // 🧮 Compute the center of all cubes in one layer
    Vector3 ComputeLayerCenter(List<GameObject> cubes)
    {
        if (cubes.Count == 0)
        {
            Debug.LogWarning("⚠ [ImageCubeSpawner] ComputeLayerCenter called with empty cube list!");
            return Vector3.zero;
        }

        Vector3 sum = Vector3.zero;
        foreach (var c in cubes)
            sum += c.transform.position;
        
        Vector3 center = sum / cubes.Count;
        Debug.Log($"📊 [ImageCubeSpawner] Layer center computed from {cubes.Count} cubes: {center}");
        return center;
    }

    // 🔗 Draw line between layers (receptive field)
    void DrawReceptiveFieldLine(Vector3 start, Vector3 end)
    {
        try
        {
            Debug.Log($"🔗 [ImageCubeSpawner] Creating line from {start} to {end}...");
            GameObject lineObj = new GameObject("ReceptiveFieldLine");
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPositions(new Vector3[] { start, end });
            
            if (lineMaterial == null)
            {
                Debug.LogWarning("⚠ [ImageCubeSpawner] lineMaterial not assigned, using default material!");
            }
            else
            {
                lr.material = lineMaterial;
            }
            
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.startColor = Color.cyan;
            lr.endColor = Color.magenta;
            receptiveLines.Add(lineObj);
            Debug.Log($"✅ [ImageCubeSpawner] Line created (total lines: {receptiveLines.Count})");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ [ImageCubeSpawner] Error drawing receptive field line: {ex.Message}");
        }
    }

    // 🧹 Clean up old visualization
    void ClearAll()
    {
        Debug.Log($"🧹 [ImageCubeSpawner] Clearing visualization: {activeCubes.Count} cubes, {receptiveLines.Count} lines...");
        
        foreach (var c in activeCubes) 
        {
            if (c != null) Destroy(c);
        }
        foreach (var l in receptiveLines) 
        {
            if (l != null) Destroy(l);
        }
        activeCubes.Clear();
        receptiveLines.Clear();
        
        Debug.Log("✅ [ImageCubeSpawner] Visualization cleared successfully");
    }
}
