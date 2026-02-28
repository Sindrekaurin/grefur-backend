using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace grefurBackend.Models.AlarmConfiguration;

public enum TrainingFrequency
{
    None = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3,
}


public class MlModelMetadata
{
    public string CustomerId { get; set; } = string.Empty;
    public string TargetMeasurementId { get; set; } = string.Empty;
    public int ModelVersion { get; set; }
    public IReadOnlyList<string> FeatureOrder { get; set; } = new List<string>();
    public DateTime TrainedAtUtc { get; set; }
    public string ModelUri { get; set; } = string.Empty;
    public TrainingFrequency TrainingFrequency { get; set; }
}

public class MlAlarmConfiguration
{
    [Key]
    public string MlAlarmId { get; set; } = string.Empty;

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [ForeignKey(nameof(CustomerId))]
    public GrefurCustomer Customer { get; set; } = null!;

    [Required]
    public string TargetMeasurementId { get; set; } = string.Empty;

    public IReadOnlyList<string> FeatureMeasurementIds { get; init; } = new List<string>();

    public IReadOnlyList<string> FeatureOrder { get; set; } = new List<string>();

    public double DeviationProbabilityThreshold { get; set; } = 0.8;

    public TrainingFrequency TrainingFrequency { get; set; }

    public int SampleIntervalMinutes { get; set; } = 5;

    public int ModelVersion { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastTrainedAtUtc { get; set; }

    public string? ModelUri { get; set; }

    public void PrefillAlarmId()
    {
        string safeTarget = TargetMeasurementId.Replace("/", "_").Replace(" ", "_");
        MlAlarmId = $"{CustomerId}_{safeTarget}".ToLower();
    }

    public MlModelMetadata GetMetadata()
    {
        return new MlModelMetadata
        {
            CustomerId = CustomerId,
            TargetMeasurementId = TargetMeasurementId,
            ModelVersion = ModelVersion,
            FeatureOrder = FeatureOrder,
            TrainedAtUtc = LastTrainedAtUtc ?? DateTime.MinValue,
            ModelUri = ModelUri ?? string.Empty,
            TrainingFrequency = TrainingFrequency
        };
    }
}