namespace grefurBackend.Models.AlarmConfiguration;

public class MlModelMetadata
{
    public string CustomerId { get; init; } = string.Empty;

    public string TargetMeasurementId { get; init; } = string.Empty;

    public int ModelVersion { get; init; }

    public IReadOnlyList<string> FeatureOrder { get; init; } = new List<string>();

    public DateTime TrainedAtUtc { get; init; }

    public string ModelUri { get; init; } = string.Empty;

    public TrainingFrequency TrainingFrequency { get; init; }
}
