using UnityEngine;
using Unity.Barracuda;
using TMPro;

#region Scaler Data Structures
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
public class DigitalTwinVisualizer : MonoBehaviour
{
    // ============================
    // AI
    // ============================
    [Header("AI Brain")]
    public NNModel modelAsset;
    public TextAsset scalerJson;

    // ============================
    // Live Inputs
    // ============================
    [Header("Live Inputs")]
    [Range(0, 180000)] public float loadVal = 0f;   // N
    public float deflectionVal = 5.5f;              // mm
    public float fc = 25f;
    public float fy = 314f;                          // MPa

    // ============================
    // Beam Geometry
    // ============================
    [Header("Beam Physics")]
    public float beamLengthMM = 1050f;
    public float beamHeightMM = 300f;

    // ============================
    // Mapping
    // ============================
    [Header("Coordinate Mapping")]
    public bool centerIsZero = true;

    // ============================
    // Visualization
    // ============================
    [Header("Visualization")]
    public Gradient colorGradient;
    [Range(0.1f, 5f)] public float sensitivity = 1f;

    [Header("Validation Display")]
    public TextMeshPro validationText;

    // ============================
    // Internals
    // ============================
    private IWorker worker;
    private ScalerData scalers;
    private Mesh mesh;

    private Vector3[] originalVertices;
    private Vector3[] vertices;
    private Color[] colors;

    // ============================
    // Init
    // ============================
    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;

        originalVertices = mesh.vertices;
        vertices = new Vector3[originalVertices.Length];
        colors = new Color[originalVertices.Length];

        if (scalerJson != null)
            scalers = JsonUtility.FromJson<ScalerData>(scalerJson.text);

        if (modelAsset != null)
        {
            var model = ModelLoader.Load(modelAsset);
            worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
        }
    }

    // ============================
    // Update
    // ============================
    void Update()
    {
        if (worker == null || scalers == null || mesh == null)
            return;

        RunBatchInference();
    }

    // ============================
    // Core Logic
    // ============================
    void RunBatchInference()
    {
        int vCount = originalVertices.Length;
        float[] inputData = new float[vCount * 6];

        // ---- Normalize global inputs
        float nLoad = (loadVal - scalers.load_mag.mean) / scalers.load_mag.scale;
        float nDefl = (deflectionVal - scalers.global_deflection.mean) / scalers.global_deflection.scale;
        float nFc = (fc - scalers.fc.mean) / scalers.fc.scale;
        float nFy = (fy - scalers.fy.mean) / scalers.fy.scale;

        // ---- Build NN input
        for (int i = 0; i < vCount; i++)
        {
            Vector3 v0 = originalVertices[i];

            float physX = centerIsZero
                ? v0.x * beamLengthMM
                : (v0.x + 0.5f) * beamLengthMM;

            float physY = v0.y * beamHeightMM;

            float nX = (physX - scalers.x.mean) / scalers.x.scale;
            float nY = (physY - scalers.y.mean) / scalers.y.scale;

            int idx = i * 6;
            inputData[idx + 0] = nX;
            inputData[idx + 1] = nY;
            inputData[idx + 2] = nLoad;
            inputData[idx + 3] = nDefl;
            inputData[idx + 4] = nFc;
            inputData[idx + 5] = nFy;
        }

        using (Tensor inputTensor = new Tensor(vCount, 6, inputData))
        {
            worker.Execute(inputTensor);
            Tensor output = worker.PeekOutput();

            float maxStress = 0f;
            float halfLengthMM = beamLengthMM * 0.5f;
            float maxDeflectionM = deflectionVal * 0.001f;

            // ---- PHYSICS: linear load scaling
            float loadRatio = Mathf.Clamp01(loadVal / 180000f); // max design load

            for (int i = 0; i < vCount; i++)
            {
                Vector3 v = originalVertices[i];

                // ============================
                // STRESS (SHAPE × LOAD × fy)
                // ============================
                float stressMPa = 0f;

                if (loadVal > 0f)
                {
                    float shape = Mathf.Abs(output[i, 1]); // NN gives shape only
                    stressMPa = shape * fy * loadRatio;

                    maxStress = Mathf.Max(maxStress, stressMPa);
                }

                float t = (loadVal > 0f)
                    ? Mathf.Clamp01(stressMPa / fy)
                    : 0f;

                colors[i] = colorGradient.Evaluate(t);

                // ============================
                // DEFORMATION (visual)
                // ============================
                if (loadVal > 0f)
                {
                    float physX = centerIsZero
                        ? v.x * beamLengthMM
                        : (v.x + 0.5f) * beamLengthMM;

                    float xi = 1f - Mathf.Abs(physX) / halfLengthMM;
                    xi = Mathf.Clamp01(xi);

                    float deflection = -xi * xi * maxDeflectionM * sensitivity;
                    v.y += deflection;
                }

                vertices[i] = v;
            }

            output.Dispose();

            // ---- Apply mesh updates
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateNormals();

            // ---- UI
            if (validationText != null)
            {
                validationText.text =
                    $"Load = {loadVal:F0} N\n" +
                    $"σmax = {maxStress:F3} MPa";
            }
        }
    }

    // ============================
    // Cleanup
    // ============================
    void OnDestroy()
    {
        worker?.Dispose();
    }
}
