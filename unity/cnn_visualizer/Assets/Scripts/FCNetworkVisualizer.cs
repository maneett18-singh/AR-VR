using System;
using System.Collections.Generic;
using UnityEngine;

public class FCNetworkVisualizer : MonoBehaviour
{
    [Header("Layout")]
    public Vector3 origin = Vector3.zero;
    public float layerXSpacing = 3.5f;
    public float neuronSpacing = 0.22f;
    public Vector3 neuronScale = new Vector3(0.08f, 0.08f, 0.08f);
    public bool verticalStack = true;

    [Header("Rendering")]
    public Material lineMaterial; // assign Unlit/Color or a simple material
    public Gradient intensityGradient;
    [Tooltip("Connection thickness in world units (used when drawing thick connections).")]
    [Range(0.0001f, 1f)] public float lineWidth = 0.01f;
    public bool useThickConnections = true;
    public Camera referenceCamera;

    [Header("Connection Width Randomization")]
    [Tooltip("If enabled, each source neuron gets a random connection width.")]
    public bool randomizeWidthPerSourceNeuron = true;
    [Range(0.0001f, 0.1f)] public float minConnectionWidth = 0.002f;
    [Range(0.0001f, 0.1f)] public float maxConnectionWidth = 0.02f;
    [Range(0f, 1f)] public float thickNeuronFraction = 0.2f;
    public int randomSeed = 12345;

    [Header("Forward Propagation Pulse")]
    public bool animatePulse = true;
    [Tooltip("Cycles per second")]
    [Range(0.01f, 5f)] public float pulseSpeed = 0.5f;
    [Tooltip("Pulse width along the connection (0..1)")]
    [Range(0.01f, 0.5f)] public float pulseWidth = 0.12f;
    [Range(0f, 5f)] public float pulseIntensity = 1.25f;
    public Color pulseColor = Color.white;
    [Tooltip("If enabled, pulse travels from output->input (right->left). Disabled = input->output (left->right).")]
    public bool reversePulseDirection = false;

    [Header("Performance")]
    [Tooltip("0 = draw all connections. Otherwise limits outgoing connections per source neuron.")]
    public int maxConnectionsPerSourceNeuron = 0;

    [Header("Sizes")]
    [Tooltip("If enabled, the Inspector sizes (inputCount/hiddenCount/outputCount) override the sizes coming from the server payload.")]
    public bool overrideServerSizes = false;

    [Header("Default / Override Counts")]
    public int inputCount = 64;
    public int hiddenCount = 128;
    public int outputCount = 10;

    [Header("Bootstrap")]
    public bool buildOnStart = true;
    public bool drawPlaceholderConnectionsWhenNoWeights = true;

    private readonly List<Transform> _layer0 = new();
    private readonly List<Transform> _layer1 = new();
    private readonly List<Transform> _layer2 = new();

    private GameObject _connections01;
    private GameObject _connections12;

    private Mesh _mesh01;
    private Mesh _mesh12;

    private Material _mat01;
    private Material _mat12;

    private float[] _w01Flat; // [hidden,input] flattened row-major
    private float[] _w12Flat; // [output,hidden] flattened row-major

    private float[] _widths01BySource; // len = 64
    private float[] _widths12BySource; // len = 128

    private bool _built;

    private void Start()
    {
        if (buildOnStart && !_built)
            BuildNetwork(inputCount, hiddenCount, outputCount);

        if (referenceCamera == null)
            referenceCamera = Camera.main;
    }

    private void Update()
    {
        if (!animatePulse)
            return;

        float pulsePos = Mathf.Repeat(Time.time * pulseSpeed, 1f);
        if (reversePulseDirection)
            pulsePos = 1f - pulsePos;

        ApplyPulseToMaterial(_mat01, pulsePos);
        ApplyPulseToMaterial(_mat12, pulsePos);
    }

    private void ApplyPulseToMaterial(Material mat, float pulsePos)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_PulsePos"))
        {
            mat.SetFloat("_PulsePos", pulsePos);
            mat.SetFloat("_PulseWidth", pulseWidth);
            mat.SetFloat("_PulseIntensity", pulseIntensity);
            if (mat.HasProperty("_PulseColor"))
                mat.SetColor("_PulseColor", pulseColor);
        }
    }

    private void Reset()
    {
        // Reasonable default gradient
        intensityGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 0f),
                new GradientColorKey(Color.cyan, 0.5f),
                new GradientColorKey(Color.magenta, 1f),
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0.15f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        };
    }

    public void BuildNetwork(int input, int hidden, int output)
    {
        Clear();
        inputCount = input;
        hiddenCount = hidden;
        outputCount = output;

        BuildLayer(_layer0, inputCount, origin + Vector3.right * 0f);
        BuildLayer(_layer1, hiddenCount, origin + Vector3.right * layerXSpacing);
        BuildLayer(_layer2, outputCount, origin + Vector3.right * (2f * layerXSpacing));

        _connections01 = CreateConnectionObject("Connections_64_128", out _mesh01, out _mat01);
        _connections12 = CreateConnectionObject("Connections_128_10", out _mesh12, out _mat12);

        _built = true;

        // Precompute random widths per source neuron so the look is stable.
        if (randomizeWidthPerSourceNeuron)
        {
            _widths01BySource = GenerateSourceWidths(inputCount, randomSeed ^ 0x51A1);
            _widths12BySource = GenerateSourceWidths(hiddenCount, randomSeed ^ 0x9E37);
        }
        else
        {
            _widths01BySource = null;
            _widths12BySource = null;
        }

        // Helpful for debugging: show connectivity even before weights arrive.
        if (drawPlaceholderConnectionsWhenNoWeights)
        {
            _w01Flat = new float[hiddenCount * inputCount];
            _w12Flat = new float[outputCount * hiddenCount];
            for (int i = 0; i < _w01Flat.Length; i++) _w01Flat[i] = 1f;
            for (int i = 0; i < _w12Flat.Length; i++) _w12Flat[i] = 1f;
            RebuildConnections();
        }
    }

    private float[] GenerateSourceWidths(int sourceCount, int seed)
    {
        float minW = Mathf.Min(minConnectionWidth, maxConnectionWidth);
        float maxW = Mathf.Max(minConnectionWidth, maxConnectionWidth);
        float midW = Mathf.Lerp(minW, maxW, 0.35f);

        var rnd = new System.Random(seed);
        var widths = new float[sourceCount];
        for (int i = 0; i < sourceCount; i++)
        {
            bool thick = rnd.NextDouble() < thickNeuronFraction;
            float t = (float)rnd.NextDouble();
            widths[i] = thick ? Mathf.Lerp(midW, maxW, t) : Mathf.Lerp(minW, midW, t);
        }
        return widths;
    }

    private void BuildLayer(List<Transform> store, int count, Vector3 layerOrigin)
    {
        var parent = new GameObject($"Layer_{count}").transform;
        parent.SetParent(transform, false);
        parent.localPosition = layerOrigin;

        if (verticalStack)
        {
            float totalHeight = (count - 1) * neuronSpacing;
            float yTop = totalHeight * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float y = yTop - (i * neuronSpacing);

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Neuron_{i}";
                sphere.transform.SetParent(parent, false);
                sphere.transform.localPosition = new Vector3(0f, y, 0f);
                sphere.transform.localScale = neuronScale;

                var col = sphere.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                store.Add(sphere.transform);
            }
        }
        else
        {
            // Arrange in a near-square grid
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

                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Neuron_{i}";
                sphere.transform.SetParent(parent, false);
                sphere.transform.localPosition = new Vector3(x, y, 0f);
                sphere.transform.localScale = neuronScale;

                var col = sphere.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                store.Add(sphere.transform);
            }
        }
    }

    private GameObject CreateConnectionObject(string name, out Mesh mesh, out Material matInstance)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        mf.sharedMesh = mesh;

        Material baseMat;
        if (lineMaterial != null)
        {
            baseMat = lineMaterial;
        }
        else
        {
            // Prefer a shader that supports vertex colors + pulse animation.
            Shader shader = Shader.Find("Custom/UnlitVertexColorPulse");
            if (shader == null) shader = Shader.Find("Custom/UnlitVertexColor");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            baseMat = new Material(shader);
        }

        // Use a material instance so per-visualizer pulse params don't affect other objects.
        matInstance = new Material(baseMat);
        mr.sharedMaterial = matInstance;

        return go;
    }

    public void SetWeights(float[] w01FlatRowMajor, int w01Rows, int w01Cols, float[] w12FlatRowMajor, int w12Rows, int w12Cols)
    {
        // Expect w01 = [hidden,input], w12 = [output,hidden]
        if (w01Rows != hiddenCount || w01Cols != inputCount)
        {
            Debug.LogWarning($"[FCNetworkVisualizer] w01 shape mismatch. Expected {hiddenCount}x{inputCount} got {w01Rows}x{w01Cols}");
            return;
        }
        if (w12Rows != outputCount || w12Cols != hiddenCount)
        {
            Debug.LogWarning($"[FCNetworkVisualizer] w12 shape mismatch. Expected {outputCount}x{hiddenCount} got {w12Rows}x{w12Cols}");
            return;
        }

        _w01Flat = w01FlatRowMajor;
        _w12Flat = w12FlatRowMajor;

        if (!_built)
            BuildNetwork(inputCount, hiddenCount, outputCount);

        RebuildConnections();
    }

    public void RebuildConnections()
    {
        if (!_built || _w01Flat == null || _w12Flat == null)
            return;

        if (referenceCamera == null)
            referenceCamera = Camera.main;

        Vector3 camForward = referenceCamera != null ? referenceCamera.transform.forward : Vector3.forward;

        BuildConnectionMesh(_mesh01, _layer0, _layer1, _w01Flat, hiddenCount, inputCount, transpose: true, cameraForward: camForward, widthBySource: _widths01BySource);
        BuildConnectionMesh(_mesh12, _layer1, _layer2, _w12Flat, outputCount, hiddenCount, transpose: true, cameraForward: camForward, widthBySource: _widths12BySource);
    }

    /// <summary>
    /// Builds a MeshTopology.Lines mesh.
    /// weightsFlat is row-major [rows, cols].
    /// If transpose=true, then weight index is interpreted as weights[to, from] = weightsFlat[to*fromCount + from].
    /// </summary>
    private void BuildConnectionMesh(
        Mesh mesh,
        List<Transform> fromLayer,
        List<Transform> toLayer,
        float[] weightsFlat,
        int rows,
        int cols,
        bool transpose,
        Vector3 cameraForward,
        float[] widthBySource)
    {
        int fromCount = fromLayer.Count;
        int toCount = toLayer.Count;

        if (fromCount == 0 || toCount == 0) return;

        // Compute normalization
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        for (int i = 0; i < weightsFlat.Length; i++)
        {
            float v = weightsFlat[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }
        float range = max - min;
        bool constantWeights = range <= 1e-6f;
        if (constantWeights) range = 1f;

        // Preselect indices per source neuron if limiting.
        // We'll pick strongest by value for each "from".
        int perSource = maxConnectionsPerSourceNeuron;

        // Mesh data
        var vertices = new List<Vector3>(toCount * fromCount * (useThickConnections ? 4 : 2));
        var colors = new List<Color>(toCount * fromCount * (useThickConnections ? 4 : 2));
        var uvs = new List<Vector2>(toCount * fromCount * (useThickConnections ? 4 : 2));
        var indices = new List<int>(toCount * fromCount * (useThickConnections ? 6 : 2));

        int runningVertexIndex = 0;

        for (int from = 0; from < fromCount; from++)
        {
            // Collect weights for this source to all targets
            List<(int to, float w)> candidates = null;
            if (perSource > 0)
                candidates = new List<(int to, float w)>(toCount);

            for (int to = 0; to < toCount; to++)
            {
                float w = GetWeight(weightsFlat, rows, cols, from, to, transpose);
                if (perSource > 0)
                    candidates.Add((to, w));
                else
                    AddConnection(from, fromLayer[from].position, toLayer[to].position, w);
            }

            if (perSource > 0)
            {
                candidates.Sort((a, b) => b.w.CompareTo(a.w));
                int take = Mathf.Min(perSource, candidates.Count);
                for (int i = 0; i < take; i++)
                {
                    var (to, w) = candidates[i];
                    AddConnection(from, fromLayer[from].position, toLayer[to].position, w);
                }
            }
        }

        void AddConnection(int fromIndex, Vector3 startWorld, Vector3 endWorld, float w)
        {
            float t = constantWeights ? 1f : (w - min) / range;
            Color c = intensityGradient.Evaluate(t);

            float width = lineWidth;
            if (widthBySource != null && fromIndex >= 0 && fromIndex < widthBySource.Length)
                width = widthBySource[fromIndex];

            // Mesh vertices are in local space.
            Vector3 start = transform.InverseTransformPoint(startWorld);
            Vector3 end = transform.InverseTransformPoint(endWorld);

            if (!useThickConnections)
            {
                vertices.Add(start);
                vertices.Add(end);
                colors.Add(c);
                colors.Add(c);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                indices.Add(runningVertexIndex++);
                indices.Add(runningVertexIndex++);
                return;
            }

            Vector3 dir = (end - start);
            float len = dir.magnitude;
            if (len <= 1e-6f)
                return;
            dir /= len;

            // Build a camera-facing ribbon (two triangles).
            Vector3 camFwdLocal = transform.InverseTransformDirection(cameraForward);
            Vector3 side = Vector3.Cross(dir, camFwdLocal);
            if (side.sqrMagnitude < 1e-6f)
                side = Vector3.Cross(dir, Vector3.up);
            side.Normalize();
            Vector3 offset = side * (width * 0.5f);

            // 4 verts per connection
            vertices.Add(start - offset); // 0
            vertices.Add(start + offset); // 1
            vertices.Add(end - offset);   // 2
            vertices.Add(end + offset);   // 3

            colors.Add(c);
            colors.Add(c);
            colors.Add(c);
            colors.Add(c);

            // UV.x = 0..1 along the connection, used for pulse animation.
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));

            // 2 triangles (0,1,2) and (2,1,3)
            indices.Add(runningVertexIndex + 0);
            indices.Add(runningVertexIndex + 1);
            indices.Add(runningVertexIndex + 2);
            indices.Add(runningVertexIndex + 2);
            indices.Add(runningVertexIndex + 1);
            indices.Add(runningVertexIndex + 3);
            runningVertexIndex += 4;
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetIndices(indices, useThickConnections ? MeshTopology.Triangles : MeshTopology.Lines, 0, calculateBounds: true);
    }

    private static float GetWeight(float[] flat, int rows, int cols, int fromIndex, int toIndex, bool transpose)
    {
        // We store weights as [to, from]
        if (transpose)
        {
            // toIndex is row, fromIndex is col
            int idx = (toIndex * cols) + fromIndex;
            if (idx < 0 || idx >= flat.Length) return 0f;
            return flat[idx];
        }
        else
        {
            int idx = (fromIndex * cols) + toIndex;
            if (idx < 0 || idx >= flat.Length) return 0f;
            return flat[idx];
        }
    }

    public void Clear()
    {
        _built = false;
        _layer0.Clear();
        _layer1.Clear();
        _layer2.Clear();

        if (_connections01 != null) Destroy(_connections01);
        if (_connections12 != null) Destroy(_connections12);

        // Destroy layer parents and their children
        var children = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
            children.Add(transform.GetChild(i).gameObject);
        foreach (var go in children)
            Destroy(go);

        _mesh01 = null;
        _mesh12 = null;
        _w01Flat = null;
        _w12Flat = null;

        _widths01BySource = null;
        _widths12BySource = null;

        _mat01 = null;
        _mat12 = null;
    }
}
