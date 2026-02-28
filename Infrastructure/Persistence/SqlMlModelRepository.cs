using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using grefurBackend.Models.AlarmConfiguration;
using grefurBackend.Context;

namespace grefurBackend.Infrastructure.Persistence;

public class SqlMlModelRepository : MlModelRepository
{
    private readonly TimescaleContext _context;
    private readonly string _storagePath;

    public SqlMlModelRepository(TimescaleContext context)
    {
        _context = context;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string rootPath = baseDir.Contains(Path.Combine("bin", "Debug")) || baseDir.Contains(Path.Combine("bin", "Release"))
            ? Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."))
            : baseDir;

        _storagePath = Path.Combine(rootPath, "MachineLearningModels");
    }

    public MlModelMetadata? GetActiveModel(string customerId, string targetMeasurementId)
    {
        return _context.Set<MlModelMetadata>()
            .Where(m => m.CustomerId == customerId && m.TargetMeasurementId == targetMeasurementId)
            .OrderByDescending(m => m.ModelVersion)
            .FirstOrDefault();
    }

    public void SaveModel(MlModelMetadata metadata, Stream modelStream)
    {
        _context.Set<MlModelMetadata>().Add(metadata);
        _context.SaveChanges();

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }

        string safeName = metadata.TargetMeasurementId.Replace("/", "_").Replace("\\", "_");
        string fileName = $"{metadata.CustomerId}_{safeName}_v{metadata.ModelVersion}.zip";
        string filePath = Path.Combine(_storagePath, fileName);

        using var fileStream = File.Create(filePath);
        modelStream.Seek(0, SeekOrigin.Begin);
        modelStream.CopyTo(fileStream);
    }
}