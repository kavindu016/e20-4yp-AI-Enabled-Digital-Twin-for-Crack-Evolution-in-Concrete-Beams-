using UnityEngine;
using TMPro;
using Unity.Barracuda;

#region Scaler Classes
[System.Serializable]
public class ScalerItem
{
    public float mean;
    public float scale;
}

[System.Serializable]
public class ScalerData
{
    public ScalerItem x;
    public ScalerItem y;
    public ScalerItem load_mag;
    public ScalerItem global_deflection;
    public ScalerItem fc;
    public ScalerItem fy;
}
#endregion

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class DigitalTwinVisualizer : MonoBehaviour
{
    [Header("PINN Model")]
    public NNModel modelAsset;
    public TextAsset scalerJson;
    public string inputName = "input";

    [Header("Model Material Inputs")]
    public float fc = 30f;
    public float fy = 500f;

    [Header("Real Data Config")]
    public float maxRealDeflectionMM = 6.7f;
    public float maxLoadN = 185490f;

    [Range(0f, 185490f)] public float loadVal = 0f;

    [Header("Visualization")]
    [Range(1f, 300f)] public float visualExaggeration = 1f;

    public enum LengthAxis { X, Z }
    public LengthAxis lengthAxis = LengthAxis.X;

    [Header("No-Load Color")]
    public Color noLoadColor = Color.green;

    [Header("Real-world Crack Coloring")]
    [Tooltip("How strongly damage prefers the bottom (tension zone).")]
    [Range(0.5f, 5f)] public float bottomBias = 2.0f;
    [Range(0f, 3f)] public float upwardGrowth = 1.2f;

    public bool useLoadPoint = false;
    public Transform loadPointWorld;
    [Range(0.01f, 2f)] public float loadRadius = 0.25f;

    // ==========================================
    // 🌟 NEW: AI PHYSICS CRACK PREDICTION (CDP)
    // ==========================================
    [Header("AI Physics Cracks (CDP)")]
    [Tooltip("Enable to use the PINN's actual physical damage output to draw cracks.")]
    public bool useAICracks = true;
    public Color aiCrackColor = Color.black;
    [Tooltip("Higher value makes the AI cracks look sharper and less blurry.")]
    [Range(0.1f, 5.0f)] public float aiCrackSharpness = 2.0f;

    [Header("Manual Crack Location (Type Here)")]
    public bool useManualCrack = true;
    [Range(0f, 1f)] public float manualCrackU = 0.5f;
    [Range(0f, 1f)] public float manualCrackV = 0.1f;
    [Range(0.001f, 0.5f)] public float manualCrackRadius = 0.05f;
    [Range(0f, 1f)] public float manualCrackSeverity = 1.0f;
    [Range(0.1f, 10f)] public float manualCrackSharpness = 3.0f;

    [Header("Beam Text (Drag BeamText Here)")]
    public TMP_Text beamText;
    public bool showDebugText = true;

    [Header("Station Points (for Table)")]
    [Range(2, 50)] public int segments = 10;
    [Range(0f, 1f)] public float stationYNormalized = 0.5f;

    [Header("Fallback Beam Properties (used only if model unavailable)")]
    public float beamLength_m = 1.0f;
    public float beamWidth_m = 0.15f;
    public float beamDepth_m = 0.25f;
    public float E_Pa = 25e9f;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] vertices;
    private Color[] colors;

    // NEW: Array to hold AI damage predictions for each vertex
    private float[] aiDamageField;

    private float meshMinA, meshMaxA;
    private float meshMinY, meshMaxY;

    // station arrays
    private float[] stationX;
    private float[] stationStress;
    private float[] stationStrain;
    private float[] stationDisp;

    public float[] UI_stationX => stationX;
    public float[] UI_stationStress => stationStress;
    public float[] UI_stationStrain => stationStrain;
    public float[] UI_stationDisp => stationDisp;

    // model
    private Model runtimeModel;
    private IWorker worker;
    private ScalerData scaler;
    private bool modelReady = false;

    void Start()
    {
        mesh = Instantiate(GetComponent<MeshFilter>().mesh);
        GetComponent<MeshFilter>().mesh = mesh;

        baseVertices = mesh.vertices;
        vertices = new Vector3[baseVertices.Length];
        colors = new Color[baseVertices.Length];

        // Initialize AI damage array
        aiDamageField = new float[baseVertices.Length];

        meshMinA = float.MaxValue;
        meshMaxA = float.MinValue;
        meshMinY = float.MaxValue;
        meshMaxY = float.MinValue;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            float a = (lengthAxis == LengthAxis.X) ? baseVertices[i].x : baseVertices[i].z;
            meshMinA = Mathf.Min(meshMinA, a);
            meshMaxA = Mathf.Max(meshMaxA, a);

            float y = baseVertices[i].y;
            meshMinY = Mathf.Min(meshMinY, y);
            meshMaxY = Mathf.Max(meshMaxY, y);
        }

        InitModel();
        EnsureStations();
        ComputeStations();
    }

    void Update()
    {
        if (mesh == null) return;

        // 🌟 NEW: Compute full-beam AI Cracks if enabled and load is applied
        if (useAICracks && modelReady && loadVal > 0.001f)
        {
            ComputeAICracksBatch();
        }

        ApplyDeformationAndColor();
        UpdateBeamText();
        ComputeStations();
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }

    void InitModel()
    {
        modelReady = false;
        if (modelAsset == null || scalerJson == null) return;

        scaler = JsonUtility.FromJson<ScalerData>(scalerJson.text);
        if (scaler == null) return;

        try
        {
            runtimeModel = ModelLoader.Load(modelAsset);
            worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, runtimeModel);
            modelReady = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Model init failed: " + ex.Message);
            modelReady = false;
        }
    }

    void EnsureStations()
    {
        if (stationX != null && stationX.Length == segments) return;
        stationX = new float[segments];
        stationStress = new float[segments];
        stationStrain = new float[segments];
        stationDisp = new float[segments];
    }

    public void ForceComputeStationsForUI() => ComputeStations();

    void ComputeStations()
    {
        EnsureStations();
        if (modelReady) ComputeStationsFromModel();
        else ComputeStationsFallback();
    }

    // ==========================================
    // 🌟 NEW: FAST BATCH INFERENCE FOR CRACKS
    // ==========================================
    void ComputeAICracksBatch()
    {
        int vCount = baseVertices.Length;
        float[] batchInput = new float[vCount * 6];

        float globalDeflectionMM = GetGlobalDeflectionMM(loadVal);
        float sLoad = Standardize(loadVal, scaler.load_mag);
        float sDefl = Standardize(globalDeflectionMM, scaler.global_deflection);
        float sFc = Standardize(fc, scaler.fc);
        float sFy = Standardize(fy, scaler.fy);

        for (int i = 0; i < vCount; i++)
        {
            // Get position from 0.0 (Left) to 1.0 (Right)
            float axisVal = (lengthAxis == LengthAxis.X) ? baseVertices[i].x : baseVertices[i].z;
            float x01 = Mathf.InverseLerp(meshMinA, meshMaxA, axisVal);

            float y01 = Mathf.InverseLerp(meshMinY, meshMaxY, baseVertices[i].y);

            // ==========================================
            // 🛠️ THE FIX: 0 to 1050 Mapping
            // ==========================================
            // Removed the (x01 - 0.5f) offset!
            // Now Unity Left = 0mm, Unity Right = 1050mm.
            float physX_mm = x01 * (beamLength_m * 1000f);
            float physY_mm = y01 * (beamDepth_m * 1000f);

            float sx = Standardize(physX_mm, scaler.x);
            float sy = Standardize(physY_mm, scaler.y);

            int idx = i * 6;
            batchInput[idx + 0] = sx;
            batchInput[idx + 1] = sy;
            batchInput[idx + 2] = sLoad;
            batchInput[idx + 3] = sDefl;
            batchInput[idx + 4] = sFc;
            batchInput[idx + 5] = sFy;
        }

        using (Tensor inputTensor = new Tensor(vCount, 6, batchInput))
        {
            worker.Execute(new System.Collections.Generic.Dictionary<string, Tensor> { { inputName, inputTensor } });
            using Tensor output = worker.PeekOutput();

            for (int i = 0; i < vCount; i++)
            {
                // Index 2 is the Concrete Damaged Plasticity (CDP) variable
                aiDamageField[i] = output.length > (i * 3 + 2) ? output[i, 2] : 0f;
                Debug.Log($"Load:{loadVal}  O0:{output[i, 0]}  O1:{output[i, 1]}  O2:{output[i, 2]}");

                // 🕵️ DEBUG LOG: Print the exact damage value at the dead-center of the beam
                if (i == vCount / 2 && loadVal > 150000f)
                {
                    Debug.Log($"[Crack Check] Load: {loadVal} | Raw AI Damage: {aiDamageField[i]} | Stress: {output[i, 1]}");
                }
            }
        }
    }

    // [KEEPING ALL YOUR EXISTING STATION & FALLBACK LOGIC UNTOUCHED...]
    void ComputeStationsFromModel()
    {
        float beamLength = Mathf.Max(0.0001f, beamLength_m);
        if (loadVal <= 0.001f)
        {
            for (int i = 0; i < segments; i++)
            {
                float xNorm = (segments == 1) ? 0f : (float)i / (segments - 1);
                stationX[i] = xNorm * beamLength;
                stationDisp[i] = stationStress[i] = stationStrain[i] = 0f;
            }
            return;
        }

        float globalDeflectionMM = GetGlobalDeflectionMM(loadVal);
        Vector3 pred = RunModel(0.5f * beamLength, stationYNormalized * beamDepth_m, loadVal, globalDeflectionMM, fc, fy);

        float modelDisp = Mathf.Abs(pred.x);
        float modelStress = Mathf.Abs(pred.y);
        float modelStrain = Mathf.Abs(pred.z);

        for (int i = 0; i < segments; i++)
        {
            float xNorm = (segments == 1) ? 0f : (float)i / (segments - 1);
            stationX[i] = xNorm * beamLength;

            float xi = (xNorm - 0.5f) * 2f;
            float bend = Mathf.Max(0f, 1f - xi * xi);
            float momentShape = Mathf.Clamp01(xNorm <= 0.5f ? xNorm / 0.5f : (1f - xNorm) / 0.5f);

            stationDisp[i] = modelDisp * bend;
            stationStress[i] = modelStress * momentShape;
            stationStrain[i] = modelStrain * momentShape;
        }
    }

    void ComputeStationsFallback()
    {
        float L = Mathf.Max(0.0001f, beamLength_m);
        float P = Mathf.Max(0f, loadVal);
        float b = Mathf.Max(0.0001f, beamWidth_m);
        float h = Mathf.Max(0.0001f, beamDepth_m);

        float I = (b * h * h * h) / 12f;
        float y = h / 2f;
        float loadRatio = Mathf.Clamp01(loadVal / maxLoadN);

        float realDeflectionMM = loadRatio * maxRealDeflectionMM;
        float deflectionMeters = (realDeflectionMM / 1000f) * visualExaggeration;
        if (Mathf.Abs(transform.localScale.y) > 1e-6f) deflectionMeters /= transform.localScale.y;

        for (int i = 0; i < segments; i++)
        {
            float x = (i + 0.5f) / segments * L;
            stationX[i] = x;
            float M = (x <= L * 0.5f) ? (P * x * 0.5f) : (P * (L - x) * 0.5f);
            float sigma = (I > 1e-12f) ? (M * y / I) : 0f;

            stationStress[i] = sigma;
            stationStrain[i] = (E_Pa > 1e-6f) ? (sigma / E_Pa) : 0f;

            float x01 = x / L;
            float xi = (x01 - 0.5f) * 2f;
            stationDisp[i] = Mathf.Max(0f, 1f - xi * xi) * deflectionMeters;
        }
    }

    Vector3 RunModel(float x, float y, float load, float globalDeflectionMM, float fcVal, float fyVal)
    {
        float sx = Standardize(x, scaler.x);
        float sy = Standardize(y, scaler.y);
        float sLoad = Standardize(load, scaler.load_mag);
        float sDefl = Standardize(globalDeflectionMM, scaler.global_deflection);
        float sFc = Standardize(fcVal, scaler.fc);
        float sFy = Standardize(fyVal, scaler.fy);

        using Tensor input = new Tensor(1, 6);
        input[0, 0] = sx; input[0, 1] = sy; input[0, 2] = sLoad;
        input[0, 3] = sDefl; input[0, 4] = sFc; input[0, 5] = sFy;

        worker.Execute(new System.Collections.Generic.Dictionary<string, Tensor> { { inputName, input } });
        using Tensor output = worker.PeekOutput();

        return new Vector3(
            output.length > 0 ? output[0] : 0f,
            output.length > 1 ? output[1] : 0f,
            output.length > 2 ? output[2] : 0f
        );
    }

    float Standardize(float value, ScalerItem s)
    {
        if (s == null) return value;
        if (Mathf.Abs(s.scale) < 1e-8f) return value - s.mean;
        return (value - s.mean) / s.scale;
    }

    float GetGlobalDeflectionMM(float currentLoad) => Mathf.Clamp01(currentLoad / maxLoadN) * maxRealDeflectionMM;

    void ApplyDeformationAndColor()
    {
        System.Array.Copy(baseVertices, vertices, baseVertices.Length);

        if (loadVal <= 0.001f)
        {
            for (int i = 0; i < colors.Length; i++) colors[i] = noLoadColor;
            mesh.vertices = vertices; mesh.colors = colors;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return;
        }

        float loadRatio = Mathf.Clamp01(loadVal / maxLoadN);
        float realDeflectionMM = loadRatio * maxRealDeflectionMM;
        float deflectionMeters = (realDeflectionMM / 1000f) * visualExaggeration;

        float scaleY = transform.localScale.y;
        if (Mathf.Abs(scaleY) > 1e-6f) deflectionMeters /= scaleY;

        Vector3 loadLocal = Vector3.zero;
        if (useLoadPoint && loadPointWorld != null)
            loadLocal = transform.InverseTransformPoint(loadPointWorld.position);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            float axisVal = (lengthAxis == LengthAxis.X) ? v.x : v.z;
            float x01 = Mathf.InverseLerp(meshMinA, meshMaxA, axisVal);
            float xi = (x01 - 0.5f) * 2f;
            float bend = Mathf.Max(0f, 1f - xi * xi);

            v.y -= bend * deflectionMeters;
            vertices[i] = v;

            float y01 = Mathf.InverseLerp(meshMinY, meshMaxY, baseVertices[i].y);
            float bottomWeight = Mathf.Pow(Mathf.Clamp01(1f - y01), bottomBias);
            float grow = Mathf.Lerp(0f, upwardGrowth, loadRatio);
            float grownBottom = Mathf.Clamp01(bottomWeight + grow * (1f - bottomWeight) * bottomWeight);

            float loadWeight = 1f;
            if (useLoadPoint && loadPointWorld != null)
            {
                Vector2 p = new Vector2(baseVertices[i].x, baseVertices[i].z);
                Vector2 lp = new Vector2(loadLocal.x, loadLocal.z);
                float near = 1f - Mathf.SmoothStep(0f, loadRadius, Vector2.Distance(p, lp));
                loadWeight = Mathf.Clamp01(near);
            }

            float intensity = bend * grownBottom * loadWeight * loadRatio;

            // Manual crack logic overlay
            if (useManualCrack)
            {
                float du = x01 - manualCrackU;
                float dv = y01 - manualCrackV;
                float dist = Mathf.Sqrt(du * du + dv * dv);
                float r = Mathf.Max(0.0001f, manualCrackRadius);
                float t = Mathf.Pow(Mathf.Clamp01(1f - (dist / r)), manualCrackSharpness) * manualCrackSeverity;
                intensity = Mathf.Max(intensity, t);
            }

            // Calculate base heatmap color
            Color finalColor = Color.Lerp(noLoadColor, Color.red, Mathf.Clamp01(intensity));

            // ==========================================
            // 🌟 NEW: OVERLAY AI CDP CRACK DAMAGE
            // ==========================================
            if (useAICracks && modelReady)
            {
                // Grab the physical damage parameter (0.0 to 1.0)
                float aiDamage = aiDamageField[i];

                // Optional: apply sharpness so cracks look more defined
                aiDamage = Mathf.Clamp01(aiDamage * 1.5f);
                aiDamage = Mathf.Pow(aiDamage, aiCrackSharpness);

                // Blend towards the crack color (black) based on AI damage
                finalColor = Color.Lerp(finalColor, aiCrackColor, aiDamage);
            }

            colors[i] = finalColor;
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void UpdateBeamText()
    {
        if (!showDebugText || beamText == null) return;
        float loadRatio = Mathf.Clamp01(loadVal / maxLoadN);

        beamText.text =
            $"Load: {loadVal:F0} N\n" +
            $"Load Ratio: {loadRatio:F2}\n" +
            $"Model: {(modelReady ? "ON" : "OFF")}\n" +
            $"AI Cracks (CDP): {(useAICracks ? "ON" : "OFF")}\n" +
            $"Manual Crack: {(useManualCrack ? "ON" : "OFF")}";
    }
}

    
   