using System;
using System.Collections.Generic;

namespace grefurBackend.Models.AlarmConfiguration;

public enum TrainingFrequency
{
    None = 0,
    Weekly = 1,
    Monthly = 2,
    Yearly = 3,
}

public class MlAlarmConfiguration
{
    public string CustomerId { get; set; } = string.Empty;

    public string TargetMeasurementId { get; set; } = string.Empty;

    public IReadOnlyList<string> FeatureMeasurementIds { get; init; } = new List<string>();

    public double DeviationProbabilityThreshold { get; set; } = 0.8;

    public TrainingFrequency TrainingFrequency { get; set; }

    /// <summary>
    /// Definerer tidsintervallet i minutter for time-alignment (resampling).
    /// Standardverdien er 5 minutter.
    /// </summary>
    public int SampleIntervalMinutes { get; set; } = 5;

    public int ModelVersion { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;
}