using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using grefurBackend.Models;
using grefurBackend.Models.AlarmConfiguration;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;
using grefurBackend.Types;
using Microsoft.ML;
using Microsoft.ML.Data;


namespace grefurBackend.Services
{
    public class MlTrainingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MlTrainingService
    {
        private readonly AlarmService _alarmService;
        private readonly LoggerService _loggerService;
        private readonly ILogger<MlTrainingService> _logger;

        public MlTrainingService(
            AlarmService AlarmService,
            LoggerService LoggerService,
            ILogger<MlTrainingService> Logger)
        {
            _alarmService = AlarmService;
            _loggerService = LoggerService;
            _logger = Logger;
        }

        private MlAlarmConfiguration testConfig = new MlAlarmConfiguration
        {
            CustomerId = "CUST-001",
            TargetMeasurementId = "Grefur_3461/900/320/001/RT401/value",
            FeatureMeasurementIds = new List<string>(),
            TrainingFrequency = TrainingFrequency.Weekly
        };

        private List<MlDataRow> AlignData(
            DateTime start,
            DateTime end,
            IReadOnlyList<LogPoint> targetData,
            Dictionary<string, IReadOnlyList<LogPoint>> featureData,
            TimeSpan interval)
        {
            var alignedRows = new List<MlDataRow>();

            // Gå gjennom tidsperioden steg for steg
            for (DateTime current = start; current <= end; current = current.Add(interval))
            {
                var features = new Dictionary<string, float>();
                bool allFeaturesFound = true;

                foreach (var entry in featureData)
                {
                    var featureId = entry.Key;
                    var dataPoints = entry.Value;

                    // Finn den nyeste verdien som er eldre eller lik 'current' (Forward Fill)
                    var closestPoint = dataPoints
                        .Where(lp => lp.timestamp <= current)
                        .OrderByDescending(lp => lp.timestamp)
                        .FirstOrDefault();

                    if (closestPoint != null)
                    {
                        features[featureId] = (float)closestPoint.value;
                    }
                    else
                    {
                        // Hvis vi mangler data for en feature i starten av perioden, hopper vi over denne raden
                        allFeaturesFound = false;
                        break;
                    }
                }

                if (!allFeaturesFound) continue;

                // Finn målverdien (Label) for samme tidspunkt
                var targetPoint = targetData
                    .Where(lp => lp.timestamp <= current)
                    .OrderByDescending(lp => lp.timestamp)
                    .FirstOrDefault();

                if (targetPoint != null)
                {
                    alignedRows.Add(new MlDataRow
                    {
                        Timestamp = current,
                        Features = features,
                        Label = (float)targetPoint.value
                    });
                }
            }

            return alignedRows;
        }

        private IDataView CreateDynamicFeatureMatrixAndLabels(MLContext mlContext, List<MlDataRow> alignedRows)
        {
            // 1. Konverter alignedRows til MlTrainingData objekter
            var observations = alignedRows.Select(row => new MlTrainingData
            {
                Label = row.Label,
                // Sortering er kritisk for at Feature[0] alltid er samme sensor i hele datasettet
                Features = row.Features
                    .OrderBy(f => f.Key)
                    .Select(f => f.Value)
                    .ToArray()
            }).ToList();

            _logger.LogInformation("[MlTrainingService]: Matrix built with {RowBase} rows and {FeatureCount} features per row.",
                observations.Count,
                observations.FirstOrDefault()?.Features.Length ?? 0);

            // 2. Last inn i IDataView
            return mlContext.Data.LoadFromEnumerable(observations);
        }

        public async Task<MlTrainingResult> TrainAndPublish(MlAlarmConfiguration Config, CancellationToken CancellationToken = default)
        {
            try
            {
                _logger.LogInformation("[MlTrainingService]: Starting training for Customer {CustomerId}, Target {TargetMeasurementId}",
                    Config.CustomerId, Config.TargetMeasurementId);

                // 1. Sett tidsperiode
                var End = DateTime.UtcNow;
                DateTime Start = Config.TrainingFrequency switch
                {
                    TrainingFrequency.Weekly => End.AddDays(-7),
                    TrainingFrequency.Monthly => End.AddMonths(-1),
                    _ => End.AddDays(-7)
                };

                _logger.LogInformation("[MlTrainingService]: Training period from {Start} to {End}", Start, End);

                // 2. Hent treningsdata for hver feature
                var FeatureData = new Dictionary<string, IReadOnlyList<LogPoint>>();
                foreach (var FeatureMeasurementId in Config.FeatureMeasurementIds)
                {
                    var Log = await _loggerService.getLogAsync(
                        FeatureMeasurementId,
                        Start,
                        End,
                        CancellationToken
                    );

                    FeatureData[FeatureMeasurementId] = Log;
                    _logger.LogInformation("[MlTrainingService]: Fetched {Count} points for feature {Feature}", Log.Count, FeatureMeasurementId);
                }

                // 3. Hent målverdi
                var TargetData = await _loggerService.getLogAsync(
                    Config.TargetMeasurementId,
                    Start,
                    End,
                    CancellationToken
                );

                _logger.LogInformation("[MlTrainingService]: Fetched {Count} points for target {Target}", TargetData.Count, Config.TargetMeasurementId);

                foreach (var lp in TargetData.Take(3))
                {
                    _logger.LogInformation("[MlTrainingService]: Target {Target}: {Time} -> {Value}", Config.TargetMeasurementId, lp.timestamp, lp.value);
                }


                // 4. Time-align features og target
                var sampleInterval = TimeSpan.FromMinutes(Config.SampleIntervalMinutes); // Defined in customers alarm setup
                var alignedRows = AlignData(Start, End, TargetData, FeatureData, sampleInterval);

                _logger.LogInformation("[MlTrainingService]: Created {Count} aligned data rows for training", alignedRows.Count);

                // Logg hver eneste rad
                foreach (var row in alignedRows)
                {
                    // Bygg en streng for alle features (f.eks. RT901: 22.5, RT902: 40.1)
                    string featureString = string.Join(", ", row.Features.Select(f => $"{f.Key}: {f.Value:F2}"));

                    _logger.LogInformation("[DATA-ROW] {Timestamp} | Features: [{Features}] | Label (Target): {Label:F2}",
                        row.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        featureString,
                        row.Label);
                }

                if (alignedRows.Count < 10)
                {
                    _logger.LogWarning("[MlTrainingService]: Not enough aligned data to train. Rows: {Count}", alignedRows.Count);
                    return new MlTrainingResult { Success = false, Message = "Too little data for alignment." };
                }

                // --- TODO: 5. Bygg feature matrise og labels ---
                var mlContext = new MLContext(seed: 42);
                var trainingDataView = CreateDynamicFeatureMatrixAndLabels(mlContext, alignedRows);

                // --- TODO: 6. Tren ML-modell ---
                // Vi bruker en enkel rørledning: Konkatenert data -> Trener
                var pipeline = mlContext.Regression.Trainers.Sdca(
                    labelColumnName: "Label",
                    featureColumnName: "Features"
                );

                _logger.LogInformation("[MlTrainingService]: Starting SDCA Regression training...");
                var model = pipeline.Fit(trainingDataView);
                _logger.LogInformation("[MlTrainingService]: Model training completed.");

                // --- TODO: 7. Save model and metadata ---

                // Create a safe filename based on the target (e.g., CUST-001_Grefur_3461_RT401_value_v1.zip)
                string safeTargetName = Config.TargetMeasurementId.Replace("/", "_").Replace("\\", "_");
                string modelFileName = $"{Config.CustomerId}_{safeTargetName}_v{Config.ModelVersion}.zip";

                using (var modelStream = new MemoryStream())
                {
                    // ML.NET saves the trained model and the input schema to a stream
                    mlContext.Model.Save(model, trainingDataView.Schema, modelStream);

                    // Convert stream to byte array to hand over to LoggerService
                    byte[] modelBytes = modelStream.ToArray();
                    bool saveSuccess = await _loggerService.saveBinaryFileAsync(modelFileName, modelBytes);

                    if (!saveSuccess)
                    {
                        throw new Exception($"Failed to save model file {modelFileName} via LoggerService");
                    }
                }

                _logger.LogInformation("[MlTrainingService]: Model saved successfully as {FileName}", modelFileName);

                // TODO: 8. Publiser ModelUpdatedEvent

                return new MlTrainingResult
                {
                    Success = true,
                    Message = $"Successfully processed {TargetData.Count} points for {Config.TargetMeasurementId}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlTrainingService]: Error during TrainAndPublish for {TargetId}", Config.TargetMeasurementId);
                return new MlTrainingResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }


    }
}