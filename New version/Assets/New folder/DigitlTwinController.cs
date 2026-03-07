using UnityEngine;

public class DigitalTwinController : MonoBehaviour
{
    [Header("Visualizer & Materials")]
    public DigitalTwinVisualizer visualizer;
    private Material beamMaterial;

    [Header("AI Models (Barracuda)")]
    public PINNRunner pinnRunner;
    public ScalerManager scaler;

    void Start()
    {
        if (visualizer != null)
        {
            beamMaterial = visualizer.GetComponent<MeshRenderer>().material;
        }
    }

    public void ProcessMask(Texture2D mask)
    {
        if (mask == null) return;

        if (beamMaterial != null)
        {
            beamMaterial.SetTexture("_CurrentCrackTex", mask);
        }

        float crackHeight = ExtractCrackHeight(mask);
        float currentDamage = Mathf.Clamp01(crackHeight);
        float estimatedDeflectionMM = currentDamage * 6.7f;

        try
        {
            if (scaler != null && scaler.parameters != null)
            {
                // 🛠️ THE FIX: Build the array of 6 inputs your PINN expects!
                // *IMPORTANT*: This array order must match the exact order your Python code used during training.
                float[] pinnInputs = new float[]
                {
                    scaler.Transform(525f, scaler.parameters.x),                           // x
                    scaler.Transform(0f, scaler.parameters.y),                             // y
                    scaler.Transform(visualizer.loadVal, scaler.parameters.load_mag),      // load_mag
                    scaler.Transform(estimatedDeflectionMM, scaler.parameters.global_deflection), // global_deflection
                    scaler.Transform(-24.89f, scaler.parameters.fc),                       // fc (concrete strength)
                    scaler.Transform(314.6f, scaler.parameters.fy)                         // fy (steel strength)
                };

                // Run PINN with all 6 inputs
                float predictedOutputScaled = pinnRunner.Run(pinnInputs);

                // Assuming your PINN outputs predicted load (adjust if it predicts stress!)
                float realLoadN = scaler.InverseTransform(predictedOutputScaled, scaler.parameters.load_mag);

                Debug.Log($"✅ [PINN SUCCESS] Deflection: {estimatedDeflectionMM:F2}mm | AI Output: {realLoadN:F0}");

                if (visualizer != null)
                {
                    visualizer.loadVal = realLoadN;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ PINN Math Error: {e.Message}");
        }
    }

    float ExtractCrackHeight(Texture2D mask)
    {
        int maxY = 0;
        for (int y = 0; y < mask.height; y++)
        {
            for (int x = 0; x < mask.width; x++)
            {
                if (mask.GetPixel(x, y).r < 0.5f)
                    maxY = Mathf.Max(maxY, y);
            }
        }
        return (float)maxY / mask.height;
    }
}