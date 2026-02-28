using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using grefurBackend.Models;
using grefurBackend.Models.AlarmConfiguration;
using grefurBackend.Services;
using Microsoft.Extensions.Logging;
using grefurBackend.Types;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.EntityFrameworkCore;
using grefurBackend.Context;

namespace grefurBackend.Services
{
    public class MlTrainingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /* Summary of class: Service responsible for orchestrating the ML lifecycle, 
       utilizing TimescaleDB for telemetry and MySQL for alarm configurations. */
    public class MlTrainingService
    {
        private readonly AlarmService _alarmService;
        private readonly LoggerService _loggerService;
        private readonly ILogger<MlTrainingService> _logger;
        private readonly IDbContextFactory<MySqlContext> _mySqlContext;

        public MlTrainingService(
            AlarmService alarmService,
            LoggerService loggerService,
            ILogger<MlTrainingService> logger,
            IDbContextFactory<MySqlContext> mySqlContext
            )
        {
            _alarmService = alarmService;
            _loggerService = loggerService;
            _logger = logger;
            _mySqlContext = mySqlContext;
        }

        /* Summary of function: Aligns multiple sensor data streams into a single time-synchronized matrix. */
        private List<MlDataRow> AlignData(
            DateTime start,
            DateTime end,
            IReadOnlyList<LogPoint> targetData,
            Dictionary<string, IReadOnlyList<LogPoint>> featureData,
            TimeSpan interval)
        {
            var alignedRows = new List<MlDataRow>();

            for (DateTime current = start; current <= end; current = current.Add(interval))
            {
                var features = new Dictionary<string, float>();
                bool allFeaturesFound = true;

                foreach (var entry in featureData)
                {
                    var featureId = entry.Key;
                    var dataPoints = entry.Value;

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
                        allFeaturesFound = false;
                        break;
                    }
                }

                if (!allFeaturesFound) continue;

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

        /* Summary of function: Converts local data objects into a ML.NET IDataView with fixed-size feature vectors. */
        private IDataView CreateDynamicFeatureMatrixAndLabels(MLContext mlContext, List<MlDataRow> alignedRows)
        {
            var firstRow = alignedRows.FirstOrDefault();
            int featureCount = firstRow?.Features.Count ?? 0;

            var observations = alignedRows.Select(row => new MlTrainingData
            {
                Label = row.Label,
                Features = row.Features
                    .OrderBy(f => f.Key)
                    .Select(f => f.Value)
                    .ToArray()
            }).ToList();

            var schemaDefinition = SchemaDefinition.Create(typeof(MlTrainingData));
            schemaDefinition[nameof(MlTrainingData.Features)].ColumnType = new VectorDataViewType(NumberDataViewType.Single, featureCount);

            _logger.LogInformation("[MlTrainingService]: Matrix built with {RowBase} rows and {FeatureCount} features.",
                observations.Count, featureCount);

            return mlContext.Data.LoadFromEnumerable(observations, schemaDefinition);
        }

        /* Summary of function: Performs data gathering, model training via SDCA regression, and saves the binary model file. */
        public async Task<MlTrainingResult> TrainAndPublish(MlAlarmConfiguration config, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("[MlTrainingService]: Starting training for Customer {CustomerId}, Target {TargetId}",
                    config.CustomerId, config.TargetMeasurementId);

                var end = DateTime.UtcNow;
                DateTime start = config.TrainingFrequency switch
                {
                    TrainingFrequency.Weekly => end.AddDays(-7),
                    TrainingFrequency.Monthly => end.AddMonths(-1),
                    _ => end.AddDays(-7)
                };

                var featureData = new Dictionary<string, IReadOnlyList<LogPoint>>();
                foreach (var featureId in config.FeatureMeasurementIds)
                {
                    var log = await _loggerService.GetLogAsync(featureId, start, end, cancellationToken);
                    featureData[featureId] = log;
                }

                var targetData = await _loggerService.GetLogAsync(config.TargetMeasurementId, start, end, cancellationToken);
                var sampleInterval = TimeSpan.FromMinutes(config.SampleIntervalMinutes);
                var alignedRows = AlignData(start, end, targetData, featureData, sampleInterval);

                if (alignedRows.Count < 10)
                {
                    return new MlTrainingResult { Success = false, Message = "Insufficient aligned data for model training." };
                }

                var mlContext = new MLContext(seed: 42);
                var trainingDataView = CreateDynamicFeatureMatrixAndLabels(mlContext, alignedRows);

                var pipeline = mlContext.Transforms.NormalizeMinMax("Features")
                    .Append(mlContext.Regression.Trainers.Sdca(
                        labelColumnName: "Label",
                        featureColumnName: "Features"
                    ));

                _logger.LogInformation("[MlTrainingService]: Training SDCA Regression model...");
                var model = pipeline.Fit(trainingDataView);

                string safeTargetName = config.TargetMeasurementId.Replace("/", "_").Replace("\\", "_");
                string modelFileName = $"{config.CustomerId}_{safeTargetName}_v{config.ModelVersion}.zip";

                using (var modelStream = new MemoryStream())
                {
                    mlContext.Model.Save(model, trainingDataView.Schema, modelStream);
                    byte[] modelBytes = modelStream.ToArray();

                    bool saveSuccess = await SaveBinaryFileAsync(modelFileName, modelBytes);

                    if (!saveSuccess)
                    {
                        throw new Exception($"Failed to save model file {modelFileName} to storage.");
                    }
                }

                _logger.LogInformation("[MlTrainingService]: Model saved successfully: {FileName}", modelFileName);

                return new MlTrainingResult
                {
                    Success = true,
                    Message = $"Trained model for {config.TargetMeasurementId} using {alignedRows.Count} aligned samples."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MlTrainingService]: Training failed for {TargetId}", config.TargetMeasurementId);
                return new MlTrainingResult { Success = false, Message = ex.Message };
            }
        }

        /* Summary of function: Persists the model bytes to the local file system in a dedicated directory. */
        private async Task<bool> SaveBinaryFileAsync(string fileName, byte[] data)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string rootPath = baseDir.Contains(Path.Combine("bin", "Debug")) || baseDir.Contains(Path.Combine("bin", "Release"))
                    ? Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."))
                    : baseDir;

                string storagePath = Path.Combine(rootPath, "MachineLearningModels");

                if (!Directory.Exists(storagePath))
                {
                    Directory.CreateDirectory(storagePath);
                }

                string filePath = Path.Combine(storagePath, fileName);
                await File.WriteAllBytesAsync(filePath, data).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MlTrainingService: Model file save failed for {FileName}", fileName);
                return false;
            }
        }

        /* Summary of function: Retrieves ML alarm configurations from the MySQL database context factory. */
        public async Task<List<MlAlarmConfiguration>> GetAllMlAlarmConfigurationsAsync(CancellationToken ct = default)
        {
            using var context = await _mySqlContext.CreateDbContextAsync(ct);
            return await context.Set<MlAlarmConfiguration>()
                .AsNoTracking()
                .ToListAsync(ct);
        }
    }
}