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

    [Header("Output Neuron Labels")]
    [Tooltip("If enabled, each neuron in the last (output) layer is labeled 0-9 (or 0..N-1 for other output sizes).")]
    public bool labelOutputNeurons = true;

    [Tooltip("Local offset for each label relative to its neuron sphere.")]
    public Vector3 outputLabelOffset = new Vector3(0.18f, 0f, 0f);

    [Tooltip("If enabled, label offset is applied toward the camera each frame so the label stays on the visible side of the neuron.")]
    public bool outputLabelsStickToCameraSide = true;

    [Tooltip("Font size used by TextMesh (bigger is sharper).")]
    public int outputLabelFontSize = 120;

    [Tooltip("World scale of the TextMesh characters.")]
    public float outputLabelCharacterSize = 0.08f;

    public Color outputLabelColor = Color.white;

    [Tooltip("If enabled, labels rotate each frame to face the camera.")]
    public bool outputLabelsFaceCamera = true;

    [Tooltip("If enabled, labels are only readable from the camera-facing side (adds a small backing plate behind text).")]
    public bool outputLabelsSingleSided = true;

    [Tooltip("How far behind the text to place the backing plate.")]
    public float outputLabelBackPlateOffset = 0.0025f;

    [Tooltip("Color of the backing plate (use an opaque color to hide mirrored text from the back).")]
    public Color outputLabelBackPlateColor = new Color(0f, 0f, 0f, 1f);

    [Header("Output Neuron Size")]
    [Tooltip("Scales only the output layer neuron spheres (helps readability at a distance).")]
    [Range(0.5f, 3f)]
    public float outputNeuronScaleMultiplier = 1.35f;

    [Header("Output Prediction Highlight")]
    [Tooltip("If enabled, the predicted output neuron will glow.")]
    public bool highlightPredictedOutput = true;

    [Tooltip("Emission color used for the highlighted output neuron.")]
    public Color highlightEmissionColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Tooltip("How strong the emission glow is.")]
    [Range(0f, 10f)]
    public float highlightEmissionIntensity = 3.5f;

    [Tooltip("Optional scale multiplier applied to the highlighted output neuron.")]
    [Range(1f, 3f)]
    public float highlightScaleMultiplier = 1.35f;

    [Header("Output Connection Focus")]
    [Tooltip("If enabled, only the wires connected to the predicted output neuron keep their normal color; all other output wires become grey.")]
    public bool focusOutputConnectionsOnPrediction = true;

    [Tooltip("Color used for dimmed (non-selected) output wires.")]
    public Color dimmedOutputWireColor = new Color(0.12f, 0.12f, 0.12f, 0.04f);

    [Tooltip("How much to brighten the focused output wires (multiplies RGB; alpha stays at 1).")]
    [Range(1f, 10f)]
    public float focusedOutputWireBrightness = 6f;

    [Tooltip("Extra thickness multiplier applied only to output wires that connect to the predicted output neuron.")]
    [Range(1f, 8f)]
    public float focusedOutputWireThicknessMultiplier = 3f;

    [Tooltip("If enabled, the moving pulse on the last layer also focuses only on the predicted output neuron.")]
    public bool focusOutputPulseOnPrediction = true;

    private readonly List<Transform> _layer0 = new();
    private readonly List<Transform> _layer1 = new();
    private readonly List<Transform> _layer2 = new();

    private readonly List<Transform> _outputLabelTransforms = new();

    private readonly List<Renderer> _outputNeuronRenderers = new();
    private readonly List<Material> _outputNeuronMaterials = new();
    private readonly List<Vector3> _outputNeuronBaseScales = new();
    private int _highlightedOutputIndex = -1;

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
        if (outputLabelsFaceCamera && _outputLabelTransforms.Count > 0)
        {
            if (referenceCamera == null)
                referenceCamera = Camera.main;

            if (referenceCamera != null)
            {
                // Billboard labels to the camera *position* so they move correctly as you walk around.
                for (int i = 0; i < _outputLabelTransforms.Count; i++)
                {
                    var t = _outputLabelTransforms[i];
                    if (t == null)
                        continue;

                    if (outputLabelsStickToCameraSide)
                    {
                        // Parent is the neuron. Keep the label on the camera-facing side.
                        Transform neuron = t.parent;
                        if (neuron != null)
                        {
                            Vector3 toCam = (referenceCamera.transform.position - neuron.position);
                            if (toCam.sqrMagnitude > 1e-6f)
                            {
                                toCam.Normalize();

                                // Preserve the intended distance (magnitude from the inspector offset).
                                float dist = outputLabelOffset.magnitude;
                                if (dist <= 1e-6f) dist = 0.18f;

                                // Convert world direction into the neuron's local direction.
                                Vector3 localDir = neuron.InverseTransformDirection(toCam);
                                t.localPosition = localDir * dist;
                            }
                        }
                    }

                    // Face the camera.
                    t.rotation = Quaternion.LookRotation(t.position - referenceCamera.transform.position, referenceCamera.transform.up);
                }
            }
        }

        if (!animatePulse)
            return;

        float pulsePos = Mathf.Repeat(Time.time * pulseSpeed, 1f);
        if (reversePulseDirection)
            pulsePos = 1f - pulsePos;

        ApplyPulseToMaterial(_mat01, pulsePos);
        // If focusing the output pulse, hide the pulse when not focused to a valid output.
        if (focusOutputPulseOnPrediction && focusOutputConnectionsOnPrediction)
        {
            bool hasFocus = _highlightedOutputIndex >= 0 && _highlightedOutputIndex < outputCount;
            ApplyPulseToMaterial(_mat12, pulsePos, enabled: hasFocus);
        }
        else
        {
            ApplyPulseToMaterial(_mat12, pulsePos);
        }
    }

    private void ApplyPulseToMaterial(Material mat, float pulsePos, bool enabled = true)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_PulsePos"))
        {
            mat.SetFloat("_PulsePos", pulsePos);
            mat.SetFloat("_PulseWidth", pulseWidth);
            mat.SetFloat("_PulseIntensity", enabled ? pulseIntensity : 0f);
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

        ApplyOutputNeuronScale();

        // Create digit labels (0..outputCount-1) for the output layer.
        if (labelOutputNeurons)
            BuildOutputNeuronLabels();

        CacheOutputNeuronRenderers();

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

    private void ApplyOutputNeuronScale()
    {
        float mul = Mathf.Max(0.0001f, outputNeuronScaleMultiplier);
        for (int i = 0; i < _layer2.Count; i++)
        {
            var t = _layer2[i];
            if (t == null) continue;
            // Start from the base neuronScale used for all layers.
            t.localScale = Vector3.Scale(neuronScale, Vector3.one * mul);
        }
    }

    private void CacheOutputNeuronRenderers()
    {
        _outputNeuronRenderers.Clear();
        _outputNeuronMaterials.Clear();
        _outputNeuronBaseScales.Clear();

        for (int i = 0; i < _layer2.Count; i++)
        {
            var t = _layer2[i];
            if (t == null)
            {
                _outputNeuronRenderers.Add(null);
                _outputNeuronMaterials.Add(null);
                _outputNeuronBaseScales.Add(Vector3.one);
                continue;
            }

            _outputNeuronBaseScales.Add(t.localScale);
            var r = t.GetComponent<Renderer>();
            _outputNeuronRenderers.Add(r);
            // Force an instance so we don't accidentally modify shared materials.
            _outputNeuronMaterials.Add(r != null ? r.material : null);
        }

        // Re-apply highlight if one was already set.
        ApplyOutputHighlight(_highlightedOutputIndex);
    }

    /// <summary>
    /// Call this with server prediction (0-9) to glow the corresponding output neuron.
    /// Pass -1 to clear highlight.
    /// </summary>
    public void SetPredictedOutput(int predictedClass)
    {
        _highlightedOutputIndex = predictedClass;
        ApplyOutputHighlight(predictedClass);

        // Refresh only the output connection mesh so focus-mode updates immediately.
        if (_built && _mesh12 != null && _w12Flat != null)
            RebuildOutputConnectionsOnly();
    }

    private void RebuildOutputConnectionsOnly()
    {
        if (!_built || _w12Flat == null)
            return;

        if (referenceCamera == null)
            referenceCamera = Camera.main;

        Vector3 camForward = referenceCamera != null ? referenceCamera.transform.forward : Vector3.forward;
        BuildConnectionMesh(
            _mesh12,
            _layer1,
            _layer2,
            _w12Flat,
            outputCount,
            hiddenCount,
            transpose: true,
            cameraForward: camForward,
            widthBySource: _widths12BySource,
            focusToIndex: focusOutputConnectionsOnPrediction ? _highlightedOutputIndex : -1,
            dimmedColor: dimmedOutputWireColor);
    }

    private void ApplyOutputHighlight(int predictedClass)
    {
        if (!highlightPredictedOutput)
            predictedClass = -1;

        for (int i = 0; i < _outputNeuronMaterials.Count; i++)
        {
            var mat = _outputNeuronMaterials[i];
            if (mat == null) continue;

            // Reset emission.
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            // Reset scale.
            if (i < _layer2.Count && _layer2[i] != null && i < _outputNeuronBaseScales.Count)
                _layer2[i].localScale = _outputNeuronBaseScales[i];
        }

        if (predictedClass < 0 || predictedClass >= _outputNeuronMaterials.Count)
            return;

        var highlightMat = _outputNeuronMaterials[predictedClass];
        if (highlightMat != null && highlightMat.HasProperty("_EmissionColor"))
        {
            highlightMat.EnableKeyword("_EMISSION");
            // Some shaders expect HDR emission; multiply by intensity.
            highlightMat.SetColor("_EmissionColor", highlightEmissionColor * Mathf.Max(0f, highlightEmissionIntensity));
        }

        if (predictedClass < _layer2.Count && _layer2[predictedClass] != null && predictedClass < _outputNeuronBaseScales.Count)
        {
            _layer2[predictedClass].localScale = _outputNeuronBaseScales[predictedClass] * Mathf.Max(1f, highlightScaleMultiplier);
        }
    }

    private void BuildOutputNeuronLabels()
    {
        _outputLabelTransforms.Clear();

        for (int i = 0; i < _layer2.Count; i++)
        {
            var neuron = _layer2[i];
            if (neuron == null) continue;

            // Create a child text label.
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(neuron, false);
            labelGo.transform.localPosition = outputLabelOffset;

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = i.ToString();
            tm.color = outputLabelColor;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = Mathf.Max(1, outputLabelFontSize);
            tm.characterSize = Mathf.Max(0.0001f, outputLabelCharacterSize);

            // Make sure the text actually renders and stays readable.
            tm.richText = false;

            // Force a simple built-in font if none is assigned.
            if (tm.font == null)
                tm.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var r = tm.GetComponent<Renderer>();
            if (r != null)
            {
                // Render after most geometry so it's less likely to be hidden by the sphere/lines.
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = 50;
            }

            // Prevent the label from being readable from the back by placing an opaque plate behind it.
            if (outputLabelsSingleSided)
            {
                var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
                back.name = "BackPlate";
                back.transform.SetParent(labelGo.transform, false);
                // Put the plate slightly behind the text (text is at z=0).
                back.transform.localPosition = new Vector3(0f, 0f, outputLabelBackPlateOffset);
                back.transform.localRotation = Quaternion.identity;

                // Size the plate based on label size.
                float s = Mathf.Max(0.01f, outputLabelCharacterSize) * 1.4f;
                back.transform.localScale = new Vector3(s, s, 1f);

                var backCol = back.GetComponent<Collider>();
                if (backCol != null) backCol.enabled = false;

                var backRenderer = back.GetComponent<MeshRenderer>();
                if (backRenderer != null)
                {
                    var mat = new Material(Shader.Find("Unlit/Color"));
                    mat.color = outputLabelBackPlateColor;
                    backRenderer.sharedMaterial = mat;
                    backRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    backRenderer.receiveShadows = false;
                    backRenderer.sortingOrder = 49; // just behind text
                }
            }

            _outputLabelTransforms.Add(labelGo.transform);
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
        BuildConnectionMesh(
            _mesh12,
            _layer1,
            _layer2,
            _w12Flat,
            outputCount,
            hiddenCount,
            transpose: true,
            cameraForward: camForward,
            widthBySource: _widths12BySource,
            focusToIndex: focusOutputConnectionsOnPrediction ? _highlightedOutputIndex : -1,
            dimmedColor: dimmedOutputWireColor);
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
        float[] widthBySource,
        int focusToIndex = -1,
        Color? dimmedColor = null)
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
                    AddConnection(from, fromLayer[from].position, toLayer[to].position, to, w);
            }

            if (perSource > 0)
            {
                candidates.Sort((a, b) => b.w.CompareTo(a.w));
                int take = Mathf.Min(perSource, candidates.Count);
                for (int i = 0; i < take; i++)
                {
                    var (to, w) = candidates[i];
                    AddConnection(from, fromLayer[from].position, toLayer[to].position, to, w);
                }
            }
        }

        void AddConnection(int fromIndex, Vector3 startWorld, Vector3 endWorld, int toIndex, float w)
        {
            float t = constantWeights ? 1f : (w - min) / range;
            Color c = intensityGradient.Evaluate(t);

            // Pulse mask: when focus mode is active, ONLY the focused connections should pulse.
            // We'll encode this as vertex color alpha (1 = allow pulse, 0 = no pulse).
            // The pulse shader will multiply pulse by this mask.
            float pulseMask = 1f;

            bool focusActive = focusToIndex >= 0;

            bool isFocusedConnection = (!focusActive) || (toIndex == focusToIndex);

            // Focus mode: dim all connections except those that land on focusToIndex.
            if (focusActive && !isFocusedConnection)
            {
                c = dimmedColor ?? new Color(0.35f, 0.35f, 0.35f, 0.35f);
                pulseMask = 0f;
            }
            else if (focusActive)
            {
                pulseMask = 1f;

                // Make the selected wires very bright/visible.
                float b = Mathf.Max(1f, focusedOutputWireBrightness);
                c.r = Mathf.Clamp01(c.r * b);
                c.g = Mathf.Clamp01(c.g * b);
                c.b = Mathf.Clamp01(c.b * b);
            }

            // Store mask in alpha. We keep base alpha at 1 so the mesh is still visible;
            // transparency should be controlled via RGB/dimmedColor if desired.
            c.a = pulseMask;

            float width = lineWidth;
            if (widthBySource != null && fromIndex >= 0 && fromIndex < widthBySource.Length)
                width = widthBySource[fromIndex];

            // Make ONLY the focused output connections thicker.
            if (focusActive && isFocusedConnection)
                width *= Mathf.Max(1f, focusedOutputWireThicknessMultiplier);

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
        _outputLabelTransforms.Clear();

    _outputNeuronRenderers.Clear();
    _outputNeuronMaterials.Clear();
    _outputNeuronBaseScales.Clear();
    _highlightedOutputIndex = -1;

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
