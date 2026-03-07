using Unity.Barracuda;
using UnityEngine;

public class PINNRunner : MonoBehaviour
{
    public NNModel modelAsset;
    IWorker worker;

    void Start()
    {
        var model = ModelLoader.Load(modelAsset);
        // Automatically selects GPU or CPU based on the system
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
    }

    public float Run(float[] inputs)
    {
        // 1. Create a tensor for our 6 inputs
        Tensor inputTensor = new Tensor(1, inputs.Length);
        for (int i = 0; i < inputs.Length; i++)
        {
            inputTensor[i] = inputs[i];
        }

        // 2. Execute the neural network
        worker.Execute(inputTensor);

        // 3. PeekOutput returns a reference to the worker's internal memory (Do NOT dispose this)
        Tensor output = worker.PeekOutput();
        float result = output[0];

        // 4. Dispose ONLY the tensor we manually created
        inputTensor.Dispose();

        return result;
    }

    // 🧹 THE FIX: Tell the GPU to free the memory when we hit Stop in the editor
    void OnDestroy()
    {
        if (worker != null)
        {
            worker.Dispose();
            Debug.Log("🧹 PINN Worker memory successfully disposed.");
        }
    }
}