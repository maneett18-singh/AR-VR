using System;
using System.Collections.Generic;
using UnityEngine;

public class SimpleFCNetworkVisualizer : MonoBehaviour
{
    [Header("Layout")]
    public Vector3 origin = Vector3.zero;
    public float layerXSpacing = 0.6f;
    public float neuronSpacing = 0.12f;
    public Vector3 neuronScale = new Vector3(0.06f, 0.06f, 0.06f);
    public bool verticalStack = true;
    [Range(0.2f, 5f)] public float neuronScaleMultiplier = 1f;

    [Header("Rendering")]
    public Material neuronMaterial;
    public Color neuronColor = Color.white;
    public Material lineMaterial;
    public Color lineColor = new Color(0.2f, 0.8f, 1f, 1f);
    public float lineWidth = 0.01f;

    [Header("Sizes")]
    public int inputCount = 64;
    public int hiddenCount = 128;
    public int outputCount = 10;
    public bool invertNetworkDirection = false;

    [Header("Bootstrap")]
    public bool buildOnStart = true;

    [Header("World Lock")]
    public bool detachFromParentOnStart = true;
    public bool placeInFrontOfCameraOnStart = true;
    [Range(0.2f, 10f)] public float cameraPlacementDistance = 2f;
    public Camera referenceCamera;

    [Header("Auto Configure")]
    [Tooltip("If enabled, applies VR-friendly defaults at runtime (fixed placement between CNN maps and numbers).")]
    public bool autoConfigureForVr = true;

    [Header("Anchor Placement")]
    public Transform cnnMapsAnchor;
    public Vector3 cnnMapsOffset = new Vector3(0f, 0f, 1.2f);
    public bool alignToCnnMaps = true;
    public bool placeInFrontOfCnnBounds = true;
    [Min(0f)] public float cnnBoundsPadding = 0.6f;

    public Transform outputNumbersAnchor;
    public Vector3 outputNumbersOffset = Vector3.zero;
    public bool alignOutputLayerToNumbers = false;
    public bool autoFindOutputNumbersAnchor = true;
    public string outputNumbersAnchorName = "PredictionNumbers";
    public string[] outputNumbersAnchorFallbackNames = new[] { "PredictionNumbers", "OutputNumbers", "Numbers", "PredictedNumbers", "Prediction Labels", "PredictionLabels" };

    [Header("Output Layer Override")]
    [Tooltip("If > 0, clamps the output layer to this count (use 10 for MNIST).")]
    public int forceOutputCount = 10;

    [Header("Output Labels")]
    public bool labelOutputNeurons = true;
    public Vector3 outputLabelOffset = new Vector3(0.12f, 0f, 0f);
    public int outputLabelFontSize = 80;
    public float outputLabelCharacterSize = 0.05f;
    public Color outputLabelColor = Color.white;

    [Header("Prediction Highlight")]
    public bool highlightPredictedOutput = true;
    public Color highlightColor = new Color(1f, 0.85f, 0.1f, 1f);
    public float highlightScaleMultiplier = 1.3f;

    private readonly List<Transform> _layer0 = new();
    private readonly List<Transform> _layer1 = new();
    private readonly List<Transform> _layer2 = new();
    private readonly List<LineRenderer> _lines = new();
    private readonly List<Transform> _outputLabels = new();
    private readonly List<Renderer> _outputRenderers = new();
    private readonly List<Vector3> _outputBaseScales = new();

    private Material _runtimeNeuronMat;
    private Material _runtimeLineMat;
    private int _highlightedOutput = -1;
    private float[] _w01;
    private float[] _w12;

    private void Start()
    {
        if (autoConfigureForVr)
        {
            placeInFrontOfCameraOnStart = false;
            alignToCnnMaps = true;
            alignOutputLayerToNumbers = true;
            autoFindOutputNumbersAnchor = true;
        }

        if (detachFromParentOnStart && transform.parent != null)
            transform.SetParent(null, true);

        if (referenceCamera == null)
            referenceCamera = Camera.main;

        if (placeInFrontOfCameraOnStart && referenceCamera != null)
        {
            transform.position = referenceCamera.transform.position + referenceCamera.transform.forward * cameraPlacementDistance;
            transform.rotation = Quaternion.LookRotation(referenceCamera.transform.forward, Vector3.up);
        }

        ApplyAnchorPlacement();

        if (buildOnStart)
            BuildNetwork(inputCount, hiddenCount, outputCount);
    }

    public void BuildNetwork(int input, int hidden, int output)
    {
        Clear();
        inputCount = input;
        hiddenCount = hidden;
        outputCount = output;

        if (!invertNetworkDirection)
        {
            BuildLayer(_layer0, inputCount, origin + Vector3.right * 0f);
            BuildLayer(_layer1, hiddenCount, origin + Vector3.right * layerXSpacing);
            BuildLayer(_layer2, outputCount, origin + Vector3.right * (2f * layerXSpacing));
        }
        else
        {
            // Invert: output on left, input on right
            BuildLayer(_layer0, outputCount, origin + Vector3.right * 0f);
            BuildLayer(_layer1, hiddenCount, origin + Vector3.right * layerXSpacing);
            BuildLayer(_layer2, inputCount, origin + Vector3.right * (2f * layerXSpacing));
        }

        CacheOutputRenderers();
        if (labelOutputNeurons)
            BuildOutputLabels();

        RebuildConnections();

        ApplyAnchorPlacement();
    }

    public void ApplyAnchorPlacement()
    {
        if (alignToCnnMaps && cnnMapsAnchor != null)
        {
            if (placeInFrontOfCnnBounds && TryGetCnnBounds(cnnMapsAnchor, out var bounds))
            {
                Vector3 localCenter = cnnMapsAnchor.InverseTransformPoint(bounds.center);
                float localFront = cnnMapsAnchor.InverseTransformPoint(bounds.max).z;
                Vector3 targetLocal = new Vector3(localCenter.x, localCenter.y, localFront + cnnBoundsPadding);
                transform.position = cnnMapsAnchor.TransformPoint(targetLocal) + cnnMapsOffset;
            }
            else
            {
                transform.position = cnnMapsAnchor.TransformPoint(cnnMapsOffset);
            }
        }

        if (alignOutputLayerToNumbers && outputNumbersAnchor == null && autoFindOutputNumbersAnchor)
        {
            if (!string.IsNullOrWhiteSpace(outputNumbersAnchorName))
            {
                var found = GameObject.Find(outputNumbersAnchorName);
                if (found != null)
                    outputNumbersAnchor = found.transform;
            }

            if (outputNumbersAnchor == null && outputNumbersAnchorFallbackNames != null)
            {
                foreach (var name in outputNumbersAnchorFallbackNames)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var found = GameObject.Find(name);
                    if (found != null)
                    {
                        outputNumbersAnchor = found.transform;
                        break;
                    }
                }
            }
        }

        if (alignOutputLayerToNumbers && outputNumbersAnchor != null)
        {
            Vector3 outputLocal = origin + Vector3.right * (2f * layerXSpacing);
            Vector3 outputWorld = transform.TransformPoint(outputLocal);
            Vector3 targetWorld = outputNumbersAnchor.TransformPoint(outputNumbersOffset);
            transform.position += (targetWorld - outputWorld);
        }
    }

    public void SetWeights(float[] w01Flat, int w01Rows, int w01Cols, float[] w12Flat, int w12Rows, int w12Cols)
    {
        if (!invertNetworkDirection)
        {
            if (w01Rows != hiddenCount || w01Cols != inputCount) return;
            if (w12Rows != outputCount || w12Cols != hiddenCount) return;
        }
        else
        {
            // When inverted, the visible order is swapped but weights are the same.
            if (w01Rows != hiddenCount || w01Cols != inputCount) return;
            if (w12Rows != outputCount || w12Cols != hiddenCount) return;
        }

        _w01 = w01Flat;
        _w12 = w12Flat;
        RebuildConnections();
    }

    public void SetPredictedOutput(int index)
    {
        _highlightedOutput = index;
        ApplyPredictionHighlight();
    }

    private void BuildLayer(List<Transform> store, int count, Vector3 layerOrigin)
    {
        var parent = new GameObject($"Layer_{store.Count}");
        parent.transform.SetParent(transform, false);
        parent.transform.localPosition = layerOrigin;

        if (!verticalStack)
        {
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)cols);
            float width = (cols - 1) * neuronSpacing;
            float height = (rows - 1) * neuronSpacing;

            for (int i = 0; i < count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                float x = (c * neuronSpacing) - width * 0.5f;
                float y = (r * -neuronSpacing) + height * 0.5f;
                CreateNeuron(parent.transform, new Vector3(x, y, 0f), store);
            }
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float y = (i * -neuronSpacing) + (count - 1) * neuronSpacing * 0.5f;
            CreateNeuron(parent.transform, new Vector3(0f, y, 0f), store);
        }
    }

    private void CreateNeuron(Transform parent, Vector3 localPos, List<Transform> store)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Neuron";
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = localPos;
    sphere.transform.localScale = neuronScale * neuronScaleMultiplier;

        var col = sphere.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        var renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = GetNeuronMaterial();

        store.Add(sphere.transform);
    }

    private void BuildOutputLabels()
    {
        for (int i = 0; i < _layer2.Count; i++)
        {
            var t = _layer2[i];
            if (t == null) continue;

            var go = new GameObject($"OutLabel_{i}");
            go.transform.SetParent(t, false);
            go.transform.localPosition = outputLabelOffset;
            go.transform.localRotation = Quaternion.identity;

            var text = go.AddComponent<TextMesh>();
            text.text = i.ToString();
            text.fontSize = outputLabelFontSize;
            text.characterSize = outputLabelCharacterSize;
            text.color = outputLabelColor;

            _outputLabels.Add(go.transform);
        }
    }

    private void CacheOutputRenderers()
    {
        _outputRenderers.Clear();
        _outputBaseScales.Clear();
        for (int i = 0; i < _layer2.Count; i++)
        {
            var r = _layer2[i].GetComponent<Renderer>();
            _outputRenderers.Add(r);
            _outputBaseScales.Add(_layer2[i].localScale);
        }
    }

    private void ApplyPredictionHighlight()
    {
        for (int i = 0; i < _layer2.Count; i++)
        {
            var t = _layer2[i];
            var r = _outputRenderers[i];
            if (t == null || r == null) continue;

            if (highlightPredictedOutput && i == _highlightedOutput)
            {
                r.material.color = highlightColor;
                t.localScale = _outputBaseScales[i] * highlightScaleMultiplier;
            }
            else
            {
                r.material.color = neuronColor;
                t.localScale = _outputBaseScales[i];
            }
        }
    }

    private void RebuildConnections()
    {
        foreach (var line in _lines)
        {
            if (line != null) Destroy(line.gameObject);
        }
        _lines.Clear();

        if (!invertNetworkDirection)
        {
            BuildConnections(_layer0, _layer1, _w01, hiddenCount, inputCount);
            BuildConnections(_layer1, _layer2, _w12, outputCount, hiddenCount);
        }
        else
        {
            // Same weights, but visually reversed order
            BuildConnections(_layer0, _layer1, _w12, outputCount, hiddenCount);
            BuildConnections(_layer1, _layer2, _w01, hiddenCount, inputCount);
        }
    }

    private void BuildConnections(List<Transform> from, List<Transform> to, float[] weights, int rows, int cols)
    {
        if (from.Count == 0 || to.Count == 0) return;

        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        if (weights != null && weights.Length > 0)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                float w = weights[i];
                min = Mathf.Min(min, w);
                max = Mathf.Max(max, w);
            }
        }
        float range = Mathf.Max(1e-6f, max - min);

        for (int i = 0; i < from.Count; i++)
        {
            for (int j = 0; j < to.Count; j++)
            {
                float t = 1f;
                if (weights != null && weights.Length == rows * cols)
                {
                    int idx = j * cols + i;
                    float w = weights[idx];
                    t = (w - min) / range;
                }

                var go = new GameObject("Line");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, from[i].position);
                lr.SetPosition(1, to[j].position);
                lr.startWidth = lineWidth;
                lr.endWidth = lineWidth;
                lr.useWorldSpace = true;
                lr.material = GetLineMaterial();

                Color c = Color.Lerp(new Color(lineColor.r, lineColor.g, lineColor.b, 0.2f), lineColor, t);
                lr.startColor = c;
                lr.endColor = c;

                _lines.Add(lr);
            }
        }
    }

    private Material GetNeuronMaterial()
    {
        if (neuronMaterial != null) return neuronMaterial;
        if (_runtimeNeuronMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _runtimeNeuronMat = new Material(shader);
            _runtimeNeuronMat.color = neuronColor;
        }
        return _runtimeNeuronMat;
    }

    private Material GetLineMaterial()
    {
        if (lineMaterial != null) return lineMaterial;
        if (_runtimeLineMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _runtimeLineMat = new Material(shader);
            _runtimeLineMat.color = lineColor;
        }
        return _runtimeLineMat;
    }

    private void Clear()
    {
        foreach (var t in _layer0) if (t != null) Destroy(t.gameObject);
        foreach (var t in _layer1) if (t != null) Destroy(t.gameObject);
        foreach (var t in _layer2) if (t != null) Destroy(t.gameObject);
        foreach (var l in _lines) if (l != null) Destroy(l.gameObject);
        foreach (var l in _outputLabels) if (l != null) Destroy(l.gameObject);

        _layer0.Clear();
        _layer1.Clear();
        _layer2.Clear();
        _lines.Clear();
        _outputLabels.Clear();
    }

    private bool TryGetCnnBounds(Transform anchor, out Bounds bounds)
    {
        var renderers = anchor.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }
}
