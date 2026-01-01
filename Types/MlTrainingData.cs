using Microsoft.ML.Data;

namespace grefurBackend.Types;

public class MlTrainingData
{
    [ColumnName("Label")]
    public float Label { get; set; }

    [VectorType]
    public float[] Features { get; set; } = Array.Empty<float>();
}