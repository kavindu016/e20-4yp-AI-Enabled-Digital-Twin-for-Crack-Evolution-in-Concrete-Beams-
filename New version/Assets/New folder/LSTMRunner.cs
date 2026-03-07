using Unity.Barracuda;
using UnityEngine;

public class LSTMRunner : MonoBehaviour
{
    public NNModel modelAsset;
    IWorker worker;

    void Start()
    {
        var model = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
    }

    public float Predict(float damage)
    {
        Tensor input = new Tensor(1, 1);
        input[0] = damage;

        worker.Execute(input);
        Tensor output = worker.PeekOutput();

        float result = output[0];
        input.Dispose();
        output.Dispose();
        return result;
    }
}