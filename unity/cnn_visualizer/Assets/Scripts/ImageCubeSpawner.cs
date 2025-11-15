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
            var data = JsonConvert.DeserializeObject<WebSocketData>(json);
            if (data == null)
            {
                Debug.LogWarning("⚠ WebSocketData was null, skipping frame.");
                return;
            }

            // 🧹 Clean previous visualization
            ClearAll();

            // ✅ Spawn input image cube as the first layer
            GameObject inputCube = CreateCubeFromBase64(data.input_image_base64, Vector3.zero, new Vector2(28, 28));
            GameObject previousLayerCenter = inputCube;

            // ✅ Spawn feature maps as layers along Z-axis
            float layerSpacing = 3.0f;
            int layerIndex = 0;

            foreach (var layer in data.feature_maps)
            {
                // Create a parent for this layer
                GameObject layerParent = new GameObject($"Layer_{layer.Key}");
                layerParent.transform.SetParent(transform);

                // Collect all cubes in this layer to compute center
                List<GameObject> layerCubes = new();

                float offset = 1.5f;
                int index = 0;

                foreach (var feature in layer.Value)
                {
                    FeatureMap fmap = feature.Value;
                    if (fmap?.base64 == null) continue;

                    Vector2 shape = (fmap.shape != null && fmap.shape.Length >= 2)
                        ? new Vector2(fmap.shape[1], fmap.shape[0])
                        : new Vector2(28, 28);

                    // Arrange cubes in a grid per layer
                    Vector3 pos = new Vector3(
                        (index % 10) * offset,
                        (index / 10) * offset,
                        layerIndex * layerSpacing
                    );

                    GameObject cube = CreateCubeFromBase64(fmap.base64, pos, shape);
                    cube.transform.SetParent(layerParent.transform);
                    layerCubes.Add(cube);
                    index++;
                }

                // Compute layer center to draw connection lines
                Vector3 layerCenter = ComputeLayerCenter(layerCubes);
                DrawReceptiveFieldLine(previousLayerCenter.transform.position, layerCenter);

                previousLayerCenter = layerParent;
                layerIndex++;
            }

            Debug.Log($"✅ Spawned {layerIndex} layers of feature maps!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"⚠ Failed to process WebSocket JSON: {ex.Message}");
        }
    }

    // 🧩 Create a cube with texture and scaled size
    GameObject CreateCubeFromBase64(string base64, Vector3 position, Vector2 shape)
    {
        Texture2D tex = ImageUtils.Base64ToTexture(base64);
        GameObject cube = Instantiate(cubePrefab, position, Quaternion.identity, transform);
        cube.GetComponent<Renderer>().material.mainTexture = tex;

        float scaleFactor = 0.05f;
        cube.transform.localScale = new Vector3(shape.x * scaleFactor, shape.y * scaleFactor, 0.1f);

        activeCubes.Add(cube);
        return cube;
    }

    // 🧮 Compute the center of all cubes in one layer
    Vector3 ComputeLayerCenter(List<GameObject> cubes)
    {
        if (cubes.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var c in cubes)
            sum += c.transform.position;
        return sum / cubes.Count;
    }

    // 🔗 Draw line between layers (receptive field)
    void DrawReceptiveFieldLine(Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("ReceptiveFieldLine");
        var lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { start, end });
        lr.material = lineMaterial;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.startColor = Color.cyan;
        lr.endColor = Color.magenta;
        receptiveLines.Add(lineObj);
    }

    // 🧹 Clean up old visualization
    void ClearAll()
    {
        foreach (var c in activeCubes) Destroy(c);
        foreach (var l in receptiveLines) Destroy(l);
        activeCubes.Clear();
        receptiveLines.Clear();
    }
}
